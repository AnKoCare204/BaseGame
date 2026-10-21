using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

/// <summary>Chụp vị trí particle ở đầu substep để bóng khác đọc, tránh đọc dữ liệu đang bị ghi song song.</summary>
[BurstCompile]
public struct SnapshotPositionsJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<float3> Pos;
    [WriteOnly] public NativeArray<float3> Snapshot;

    public void Execute(int i) => Snapshot[i] = Pos[i];
}

/// <summary>
/// Một substep XPBD cho mọi bóng. Chạy song song theo bóng: mỗi bóng chỉ ghi vào particle của mình,
/// bóng khác được đọc qua BubbleState và PosSnapshot của đầu substep.
/// Thứ tự: predict -> cạnh -> thể tích -> shape matching -> ghim -> chạm bóng khác (hạt với hạt) -> chạm shape -> khóa Z
/// -> cập nhật vận tốc (kèm contact damping) -> xét ngủ.
/// </summary>
[BurstCompile]
public struct SolveSubstepJob : IJobParallelFor
{
    public float Dt;
    public float3 Gravity;
    public int BubbleCount;
    public int ShapeCount;
    public int ApplyOneShot;

    /// <summary>1 = giữ tâm bóng cố định tại cmd.PlaneZ, bóng chỉ di chuyển trong mặt phẳng XY.</summary>
    public int LockZ;

    /// <summary>
    /// Giới hạn cứng theo cạnh (bit 1 trái, 2 phải, 4 dưới, 8 trên): mặt bóng (particle + ParticleRadius) vượt cạnh thì cả
    /// bóng bị dịch nguyên khối vào trong, không có contact từng particle nên bóng giữ nguyên hình cầu. Dùng cho cạnh trên
    /// của vùng chơi (bóng nổi lên chạm trần vẫn tròn). 0 = tắt.
    /// </summary>
    public int RigidMask;

    /// <summary>x = trái, y = phải, z = dưới, w = trên (world).</summary>
    public float4 RigidRect;

    /// <summary>1 = đánh thức mọi bóng đang ngủ trong substep này (shape di chuyển, đổi luật, teleport...).</summary>
    public int WakeAll;

    [ReadOnly] public NativeArray<BubbleDesc> Descs;
    [ReadOnly] public NativeArray<SoftBubbleParams> Params;
    [ReadOnly] public NativeArray<BubbleCommand> Commands;
    [ReadOnly] public NativeArray<BubbleState> StatesRead;
    [WriteOnly] public NativeArray<BubbleState> StatesWrite;
    [ReadOnly] public NativeArray<ContactRule> PairRules;
    [ReadOnly] public NativeArray<SoftShapeData> Shapes;
    [ReadOnly] public NativeArray<ParticlePin> Pins;
    [ReadOnly] public NativeArray<float3> PosSnapshot;

    [NativeDisableParallelForRestriction] public NativeArray<float3> Pos;
    [NativeDisableParallelForRestriction] public NativeArray<float3> PrevPos;
    [NativeDisableParallelForRestriction] public NativeArray<float3> Vel;
    [NativeDisableParallelForRestriction] public NativeArray<float3> Scratch;
    [ReadOnly, NativeDisableParallelForRestriction] public NativeArray<float3> RestPos;
    [ReadOnly, NativeDisableParallelForRestriction] public NativeArray<int2> Edges;
    [ReadOnly, NativeDisableParallelForRestriction] public NativeArray<float> RestLength;
    [ReadOnly, NativeDisableParallelForRestriction] public NativeArray<int3> Tris;

    /// <summary>
    /// Mỗi bóng một cờ: main thread đặt 1 trước step, job ghi 0 khi bóng thật sự được mô phỏng (không ngủ, không frozen).
    /// Bóng còn 1 sau cả step thì particle và trạng thái không đổi, world bỏ qua dựng lưới và upload mesh cho bóng đó.
    /// </summary>
    public NativeArray<int> Untouched;

    // Khối lượng nghịch đảo của particle, đồng nhất cho mọi particle.
    private const float W = 1f;

    // Hai bóng có tâm gần hơn (rA + rB) * CoreOverlapFactor thì coi là lồng sâu (chỉ xảy ra khi spawn/teleport chồng lên nhau).
    private const float CoreOverlapFactor = 0.5f;

    // Tỉ lệ phần lồng sâu được giải quyết mỗi substep.
    private const float CoreRepulsion = 0.25f;

    // Khe hở tối đa (world) giữa bóng và vật còn tính là đang đỡ nó (IsSupported), để bóng tựa hơi lệch / bẹt vẫn được ngủ.
    private const float SupportSlack = 0.05f;

    public void Execute(int b)
    {
        BubbleDesc d = Descs[b];
        BubbleState st = StatesRead[b];
        BubbleCommand cmd = Commands[b];
        if (st.Active == 0 || cmd.Frozen != 0)
        {
            StatesWrite[b] = st;
            return;
        }

        SoftBubbleParams p = Params[d.ParamsIndex];
        bool hasImpulse = ApplyOneShot != 0 && math.lengthsq(cmd.VelocityDelta) > 0f;
        bool targetFar = cmd.HasTarget != 0 && math.distancesq(cmd.Target, st.Center) > 1e-4f;
        bool disturbed = hasImpulse || targetFar || WakeAll != 0 || cmd.PinCount > 0 || cmd.Wake != 0;

        if (st.Sleeping != 0)
        {
            // Vật đỡ trôi đi (kể cả chậm hơn ngưỡng của HasMovingNeighbor) thì thức để nổi tiếp, không đứng im dưới khoảng trống.
            if (!disturbed && !HasMovingNeighbor(b, in d, in p, in st) && IsSupported(b, in d, in p, st.Center, st.MaxRadius))
            {
                StatesWrite[b] = st;
                return;
            }

            st.Sleeping = 0;
            st.SleepTimer = 0f;
        }

        Untouched[b] = 0;
        int start = d.ParticleStart;
        int end = start + d.ParticleCount;          // gồm cả particle tâm ở cuối
        int surfaceEnd = start + d.SurfaceCount;    // chỉ các đỉnh mesh
        float invCount = 1f / d.ParticleCount;
        float dt = Dt;
        float dt2 = dt * dt;

        Predict(in p, in cmd, in st, start, end, dt);
        float maxStrain = SolveDistances(d.EdgeStart, d.EdgeStart + d.EdgeCount, p.EdgeCompliance / dt2, p.EdgeDampingGamma);
        SolveDistances(d.RadialEdgeStart, d.RadialEdgeStart + d.RadialEdgeCount, p.RadialCompliance / dt2, p.RadialDampingGamma);

        float3 cm = Mean(start, end, invCount);
        SolveVolume(in d, in p, cm, start, end, dt2);

        cm = Mean(start, end, invCount);
        quaternion rot = SolveShapeMatching(in p, in st, cm, start, end);

        ApplyPins(in cmd, start, d.SurfaceCount);

        // Từ đây Scratch gom pháp tuyến tiếp xúc của từng particle, bodyNormals gom pháp tuyến ở mức thân bóng
        // (một entry cho mỗi vật đang chạm) để triệt vận tốc tiến vào bề mặt ở bước vận tốc.
        for (int i = start; i < end; i++) Scratch[i] = float3.zero;
        var bodyNormals = new FixedList512Bytes<float3>();

        cm = Mean(start, end, invCount);
        SolveBubbleContacts(b, in d, in st, cm, start, surfaceEnd, dt2, ref bodyNormals);
        SolveShapeContacts(in d, in p, in st, cm, start, surfaceEnd, dt2, ref bodyNormals);

        if (LockZ != 0)
        {
            // Khóa Z ở mức tâm: dịch cả bóng về mặt phẳng, particle vẫn được phình/bẹt theo Z.
            cm = Mean(start, end, invCount);
            float3 shift = new float3(0f, 0f, cmd.PlaneZ - cm.z);
            for (int i = start; i < end; i++) Pos[i] += shift;
        }

        if (RigidMask != 0) ApplyRigidBounds(in d, start, surfaceEnd, end, ref bodyNormals);

        float3 vcm = UpdateVelocities(in p, in bodyNormals, start, end, dt, invCount, out float meanSpeed);

        cm = Mean(start, end, invCount);
        float radius = 0f;
        float maxRadius = 0f;
        for (int i = start; i < surfaceEnd; i++)
        {
            float dist = math.length(Pos[i] - cm);
            radius += dist;
            maxRadius = math.max(maxRadius, dist);
        }

        radius /= d.SurfaceCount;

        float sleepTimer = 0f;
        int sleeping = 0;
        // Chỉ ngủ khi có vật đỡ ngược lực ròng: bóng đang nổi chậm (chen giữa bóng khác, cản lớn) chưa phải đứng yên thật.
        if (p.SleepEnabled != 0 && !disturbed && meanSpeed < p.SleepThreshold && IsSupported(b, in d, in p, cm, maxRadius))
        {
            sleepTimer = st.SleepTimer + dt;
            if (sleepTimer >= p.SleepDelay)
            {
                sleeping = 1;
                for (int i = start; i < end; i++) Vel[i] = float3.zero;
                vcm = float3.zero;
            }
        }

        StatesWrite[b] = new BubbleState
        {
            Center = cm,
            Velocity = vcm,
            Rotation = rot,
            Radius = radius,
            MaxRadius = maxRadius,
            Volume = Volume(in d, cm),
            MaxStrain = maxStrain,
            SleepTimer = sleepTimer,
            Sleeping = sleeping,
            Active = 1
        };
    }

    /// <summary>Bóng đang ngủ có bị một bóng thức, đang chuyển động, tới gần không.</summary>
    private bool HasMovingNeighbor(int b, in BubbleDesc d, in SoftBubbleParams p, in BubbleState st)
    {
        float wakeSpeedSq = p.SleepThreshold * p.SleepThreshold * 4f;
        for (int j = 0; j < BubbleCount; j++)
        {
            if (j == b) continue;
            BubbleState oj = StatesRead[j];
            if (oj.Active == 0 || oj.Sleeping != 0) continue;
            if (math.lengthsq(oj.Velocity) < wakeSpeedSq) continue;

            ContactRule rule = PairRules[b * BubbleCount + j];
            if (rule.Enabled == 0) continue;

            float reach = st.MaxRadius + oj.MaxRadius + d.ParticleRadius + Descs[j].ParticleRadius
                          + rule.Margin + rule.AdhesionDistance + 0.1f;
            if (math.distancesq(st.Center, oj.Center) <= reach * reach) return true;
        }

        return false;
    }

    /// <summary>
    /// Bóng có vật đỡ ngược lực ròng (trọng lực + lực nổi) không: chỉ khi đó đứng yên mới là cân bằng thật và được ngủ. Vật đỡ là
    /// bóng khác, SoftBodyShape và cạnh cứng trong tầm chạm; mỗi vật cho một pháp tuyến (từ vật sang tâm bóng) mở rộng theo góc ma
    /// sát, gom vào SupportCone. Tầm chạm dùng MaxRadius + SupportSlack nên bóng đang tựa vẫn tính là chạm dù bị bẹt.
    /// Xét trong mặt phẳng XY nên chỉ áp dụng khi LockZ (bàn chơi 2D); không khoá Z hoặc lực ròng ~0 thì coi như luôn được đỡ.
    /// </summary>
    private bool IsSupported(int b, in BubbleDesc d, in SoftBubbleParams p, float3 center, float maxRadius)
    {
        if (LockZ == 0) return true;
        float2 accel = (Gravity * p.GravityScale).xy + new float2(0f, p.Buoyancy);
        float accelLen = math.length(accel);
        if (accelLen < 1e-4f) return true;

        var cone = new SupportCone(-accel / accelLen);
        float reach = maxRadius + d.ParticleRadius + SupportSlack;
        float2 c = center.xy;
        for (int j = 0; j < BubbleCount; j++)
        {
            if (j == b) continue;
            BubbleState oj = StatesRead[j];
            if (oj.Active == 0) continue;

            ContactRule rule = PairRules[b * BubbleCount + j];
            if (rule.Enabled == 0) continue;

            float2 diff = c - oj.Center.xy;
            float dist = math.length(diff);
            if (dist < 1e-5f || dist > reach + oj.MaxRadius + Descs[j].ParticleRadius + rule.Margin) continue;
            cone.Add(diff / dist, rule.Friction);
            if (cone.IsSupported) return true;
        }

        for (int s = 0; s < ShapeCount; s++)
        {
            SoftShapeData sh = Shapes[s];
            float dist = SoftBodyMath.SignedDistance(in sh, center, out float3 n);
            if (dist > reach) continue;
            cone.Add(n.xy, (sh.Friction + p.SurfaceFriction) * 0.5f);
        }

        if (RigidMask != 0)
        {
            if ((RigidMask & 1) != 0 && c.x - reach <= RigidRect.x) cone.Add(new float2(1f, 0f), 0f);
            if ((RigidMask & 2) != 0 && c.x + reach >= RigidRect.y) cone.Add(new float2(-1f, 0f), 0f);
            if ((RigidMask & 4) != 0 && c.y - reach <= RigidRect.z) cone.Add(new float2(0f, 1f), 0f);
            if ((RigidMask & 8) != 0 && c.y + reach >= RigidRect.w) cone.Add(new float2(0f, -1f), 0f);
        }

        return cone.IsSupported;
    }

    private void Predict(in SoftBubbleParams p, in BubbleCommand cmd, in BubbleState st, int start, int end, float dt)
    {
        float3 accel = Gravity * p.GravityScale + new float3(0f, p.Buoyancy, 0f);
        if (cmd.HasTarget != 0)
        {
            accel += (cmd.Target - st.Center) * cmd.TargetStiffness - st.Velocity * cmd.TargetDamping;
        }

        float3 oneShot = ApplyOneShot != 0 ? cmd.VelocityDelta : float3.zero;
        float dragMul = math.max(0f, 1f - p.LinearDrag * dt);
        float maxSpeed = p.MaxSpeed;

        for (int i = start; i < end; i++)
        {
            float3 v = (Vel[i] + oneShot + accel * dt) * dragMul;
            float sp = math.length(v);
            if (sp > maxSpeed) v *= maxSpeed / sp;
            Vel[i] = v;
            float3 x = Pos[i];
            PrevPos[i] = x;
            Pos[i] = x + v * dt;
        }
    }

    /// <summary>
    /// Ràng buộc khoảng cách XPBD kèm giảm chấn cho một dải cạnh (cạnh bề mặt hoặc nan hoa):
    /// số hạng gamma triệt phần vận tốc co giãn dọc cạnh (Macklin 2016, mục 3.2). Trả về strain lớn nhất.
    /// </summary>
    private float SolveDistances(int first, int end, float alpha, float gamma)
    {
        float maxStrain = 0f;
        for (int e = first; e < end; e++)
        {
            int2 ed = Edges[e];
            float3 p1 = Pos[ed.x];
            float3 p2 = Pos[ed.y];
            float3 diff = p1 - p2;
            float len = math.length(diff);
            if (len < 1e-7f) continue;

            float rest = RestLength[e];
            float c = len - rest;
            maxStrain = math.max(maxStrain, math.abs(c) / rest);

            float3 n = diff / len;
            float rate = math.dot(n, (p1 - PrevPos[ed.x]) - (p2 - PrevPos[ed.y]));
            float dl = -(c + gamma * rate) / ((1f + gamma) * 2f * W + alpha);
            float3 corr = n * dl;
            Pos[ed.x] = p1 + corr * W;
            Pos[ed.y] = p2 - corr * W;
        }

        return maxStrain;
    }

    private float3 Mean(int start, int end, float invCount)
    {
        float3 sum = float3.zero;
        for (int i = start; i < end; i++) sum += Pos[i];
        return sum * invCount;
    }

    private float Volume(in BubbleDesc d, float3 cm)
    {
        float vol = 0f;
        int end = d.TriStart + d.TriCount;
        for (int t = d.TriStart; t < end; t++)
        {
            int3 tr = Tris[t];
            vol += math.dot(Pos[tr.x] - cm, math.cross(Pos[tr.y] - cm, Pos[tr.z] - cm));
        }

        return vol / 6f;
    }

    private void SolveVolume(in BubbleDesc d, in SoftBubbleParams p, float3 cm, int start, int end, float dt2)
    {
        for (int i = start; i < end; i++) Scratch[i] = float3.zero;

        float vol = 0f;
        int triEnd = d.TriStart + d.TriCount;
        for (int t = d.TriStart; t < triEnd; t++)
        {
            int3 tr = Tris[t];
            float3 a = Pos[tr.x] - cm;
            float3 b = Pos[tr.y] - cm;
            float3 c = Pos[tr.z] - cm;
            vol += math.dot(a, math.cross(b, c));
            Scratch[tr.x] += math.cross(b, c);
            Scratch[tr.y] += math.cross(c, a);
            Scratch[tr.z] += math.cross(a, b);
        }

        vol /= 6f;
        float cVol = vol - d.RestVolume * p.Pressure;

        // Giảm chấn XPBD: rate = gradC . (x - x_prev), phần vận tốc nén/nở của thể tích trong substep này.
        float gradSum = 0f;
        float rate = 0f;
        for (int i = start; i < end; i++)
        {
            float3 g = Scratch[i] / 6f;
            gradSum += W * math.dot(g, g);
            rate += math.dot(g, Pos[i] - PrevPos[i]);
        }

        if (gradSum < 1e-12f) return;
        float gamma = p.VolumeDampingGamma;
        float lambda = -(cVol + gamma * rate) / ((1f + gamma) * gradSum + p.VolumeCompliance / dt2);
        float k = lambda * W / 6f;
        for (int i = start; i < end; i++) Pos[i] += Scratch[i] * k;
    }

    private quaternion SolveShapeMatching(in SoftBubbleParams p, in BubbleState st, float3 cm, int start, int end)
    {
        quaternion rot = quaternion.identity;
        if (p.AllowRotation != 0)
        {
            float3x3 a = float3x3.zero;
            for (int i = start; i < end; i++)
            {
                float3 pr = Pos[i] - cm;
                float3 q = RestPos[i];
                a.c0 += pr * q.x;
                a.c1 += pr * q.y;
                a.c2 += pr * q.z;
            }

            rot = SoftBodyMath.ExtractRotation(in a, st.Rotation, 8);
        }

        float k = math.saturate(p.ShapeStiffness);
        if (k <= 0f) return rot;

        float3x3 r = new float3x3(rot);
        for (int i = start; i < end; i++)
        {
            float3 goal = cm + math.mul(r, RestPos[i]);
            Pos[i] += (goal - Pos[i]) * k;
        }

        return rot;
    }

    /// <summary>Ghim particle về vị trí mục tiêu (kéo bằng chuột, gắn vào transform). Đặt trước contact để tường vẫn chặn được.</summary>
    private void ApplyPins(in BubbleCommand cmd, int start, int count)
    {
        int end = cmd.PinStart + cmd.PinCount;
        for (int k = cmd.PinStart; k < end; k++)
        {
            ParticlePin pin = Pins[k];
            if (pin.Particle < 0 || pin.Particle >= count) continue;
            int i = start + pin.Particle;
            Pos[i] += (pin.Target - Pos[i]) * math.saturate(pin.Stiffness);
        }
    }

    /// <summary>
    /// Va chạm hạt với hạt: mỗi particle của bóng này là quả cầu bán kính ParticleRadius, chạm với các particle của bóng kia
    /// (đọc từ PosSnapshot). Broadphase bằng hình cầu bao, rồi chỉ xét particle trong vùng chồng lấn. Hai bóng cùng giải
    /// nên mỗi bên đẩy một nửa. Tam giác lưới được bịt kín vì bán kính hạt bằng 30% cạnh lưới.
    /// </summary>
    private void SolveBubbleContacts(int b, in BubbleDesc d, in BubbleState st, float3 cm, int start, int end, float dt2,
        ref FixedList512Bytes<float3> bodyNormals)
    {
        float ra = d.ParticleRadius;
        for (int j = 0; j < BubbleCount; j++)
        {
            if (j == b) continue;
            BubbleState oj = StatesRead[j];
            if (oj.Active == 0) continue;

            ContactRule rule = PairRules[b * BubbleCount + j];
            if (rule.Enabled == 0) continue;

            BubbleDesc dj = Descs[j];
            float contactDist = ra + dj.ParticleRadius + rule.Margin;
            // Vùng dò hạt rộng hơn khoảng cách chạm để particle đè lên giữa tam giác lưới của bóng kia
            // (cách ba đỉnh tới 0.58 cạnh) vẫn thấy được hạt, không bật tắt contact mỗi substep.
            float detectDist = math.max(contactDist, 0f) + math.max(d.EdgeLength, dj.EdgeLength) * 0.6f;
            float reachExtra = detectDist + rule.AdhesionDistance + 0.05f;

            float3 centerDiff = cm - oj.Center;
            float centerDist = math.length(centerDiff);
            if (centerDist > st.MaxRadius + oj.MaxRadius + reachExtra) continue;

            float3 bodyNormal = centerDist > 1e-5f ? centerDiff / centerDist : new float3(0f, 1f, 0f);

            // Lồng sâu (tâm gần nhau hơn nửa tổng bán kính): hạt với hạt gần như trùng nhau nên không tạo được hướng đẩy,
            // đẩy cả bóng theo hướng tâm-tâm. Trùng tâm hoàn toàn thì dùng hướng giả ngẫu nhiên đối xứng.
            float coreDist = (st.Radius + oj.Radius) * CoreOverlapFactor;
            if (centerDist < coreDist)
            {
                float3 dir;
                if (centerDist > 1e-5f)
                {
                    dir = bodyNormal;
                }
                else
                {
                    int lo = math.min(b, j);
                    int hi = math.max(b, j);
                    dir = math.normalize(new float3(
                        math.sin(lo * 12.9898f + hi * 78.233f),
                        0.35f,
                        math.cos(lo * 37.719f + hi * 11.131f)));
                    if (b > j) dir = -dir;
                }

                float3 push = dir * ((coreDist - centerDist) * 0.5f * CoreRepulsion);
                for (int i = start; i < end; i++) Pos[i] += push;
                cm += push;
            }

            // Gom particle của bóng j nằm trong vùng có thể chạm bóng này.
            float zoneJ = st.MaxRadius + reachExtra;
            float zoneJSq = zoneJ * zoneJ;
            var candidates = new FixedList4096Bytes<int>();
            int jEnd = dj.ParticleStart + dj.SurfaceCount;
            for (int k = dj.ParticleStart; k < jEnd; k++)
            {
                if (math.distancesq(PosSnapshot[k], cm) > zoneJSq) continue;
                if (candidates.Length >= candidates.Capacity) break;
                candidates.Add(k);
            }

            if (candidates.Length == 0) continue;

            float zoneI = oj.MaxRadius + reachExtra;
            float zoneISq = zoneI * zoneI;
            float alpha = rule.Compliance / dt2;
            bool adhesion = rule.AdhesionStrength > 0f && rule.AdhesionDistance > 0f;
            // Particle nằm sâu hơn mức này bên trong bóng kia mà không gần hạt nào thì coi là đã lọt vào trong vỏ.
            float insideRadius = oj.Radius - contactDist * 0.5f;
            bool touched = false;

            for (int i = start; i < end; i++)
            {
                float3 pi = Pos[i];
                float3 toCenter = pi - oj.Center;
                float distCenter = math.length(toCenter);
                if (distCenter * distCenter > zoneISq) continue;

                bool inContact = false;
                float nearest = float.MaxValue;
                float3 nearestN = float3.zero;
                for (int c = 0; c < candidates.Length; c++)
                {
                    float3 q = PosSnapshot[candidates[c]];
                    float3 diff = pi - q;
                    float dist = math.length(diff);
                    if (dist < 1e-6f || dist >= detectDist + rule.AdhesionDistance) continue;

                    // Vỏ bóng kia được coi như tấm có độ dày quanh mặt phẳng tiếp tuyến tại hạt q, pháp tuyến hướng ra
                    // khỏi tâm bóng kia. Nhờ vậy particle đã lọt vào trong vỏ vẫn bị đẩy ra ngoài chứ không bị đẩy sâu thêm.
                    float3 outward = q - oj.Center;
                    float outLen = math.length(outward);
                    float3 n = outLen > 1e-6f ? outward / outLen : diff / dist;
                    float s = math.dot(diff, n);
                    float pen = contactDist - s;

                    if (dist < detectDist && pen > 0f)
                    {
                        float dl = pen / (2f * W + alpha);
                        float3 moved = pi + n * (dl * W);
                        pi = ApplyFriction(moved, PrevPos[i], n, dl, rule.Friction);
                        Scratch[i] += n;
                        inContact = true;
                    }
                    else if (adhesion && pen <= 0f && dist < nearest)
                    {
                        nearest = dist;
                        nearestN = n;
                    }
                }

                if (!inContact && distCenter > 1e-6f && distCenter < insideRadius)
                {
                    // Lọt hẳn vào trong vỏ, xa mọi hạt của bóng kia: đẩy ra khỏi mặt cầu trung bình của nó.
                    float3 n = toCenter / distCenter;
                    float pen = oj.Radius + contactDist - distCenter;
                    float dl = pen / (2f * W + alpha);
                    float3 moved = pi + n * (dl * W);
                    pi = ApplyFriction(moved, PrevPos[i], n, dl, rule.Friction);
                    Scratch[i] += n;
                    inContact = true;
                }

                if (!inContact && adhesion && nearest - contactDist < rule.AdhesionDistance)
                {
                    // Kéo về bề mặt bóng kia (dính), mỗi bên một nửa.
                    float gap = nearest - contactDist;
                    pi -= nearestN * (gap * rule.AdhesionStrength * 0.5f);
                    Scratch[i] += nearestN;
                    inContact = true;
                }

                if (inContact) touched = true;
                Pos[i] = pi;
            }

            if (touched && bodyNormals.Length < bodyNormals.Capacity) bodyNormals.Add(bodyNormal);
        }
    }

    private void SolveShapeContacts(in BubbleDesc d, in SoftBubbleParams p, in BubbleState st, float3 cm, int start, int end, float dt2,
        ref FixedList512Bytes<float3> bodyNormals)
    {
        float alpha = p.SurfaceCompliance / dt2;
        float radius = d.ParticleRadius;
        for (int s = 0; s < ShapeCount; s++)
        {
            SoftShapeData sh = Shapes[s];

            float dc = SoftBodyMath.SignedDistance(in sh, cm, out float3 bodyNormal);
            if (dc > st.MaxRadius + radius + 0.1f) continue;

            float friction = (sh.Friction + p.SurfaceFriction) * 0.5f;
            bool touched = false;
            for (int i = start; i < end; i++)
            {
                float dist = SoftBodyMath.SignedDistance(in sh, Pos[i], out float3 n);
                float pen = radius - dist;
                if (pen <= 0f) continue;

                float dl = pen / (W + alpha);
                float3 pi = Pos[i] + n * (dl * W);
                Pos[i] = ApplyFriction(pi, PrevPos[i], n, dl, friction);
                Scratch[i] += n;
                touched = true;
            }

            if (touched && bodyNormals.Length < bodyNormals.Capacity) bodyNormals.Add(bodyNormal);
        }
    }

    /// <summary>
    /// Giới hạn cứng theo cạnh: lấy bao XY của mặt bóng (particle bề mặt + ParticleRadius), phần vượt cạnh bật nào thì dịch cả
    /// bóng (kể cả particle tâm) vào trong đúng bấy nhiêu. Dịch nguyên khối nên hình không đổi; vận tốc tiến vào cạnh tự mất
    /// vì XPBD lấy vận tốc từ hiệu vị trí, thêm pháp tuyến thân để ContactDamping xử lý như chạm tường.
    /// </summary>
    private void ApplyRigidBounds(in BubbleDesc d, int start, int surfaceEnd, int end, ref FixedList512Bytes<float3> bodyNormals)
    {
        float pr = d.ParticleRadius;
        float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
        for (int i = start; i < surfaceEnd; i++)
        {
            float3 q = Pos[i];
            minX = math.min(minX, q.x);
            maxX = math.max(maxX, q.x);
            minY = math.min(minY, q.y);
            maxY = math.max(maxY, q.y);
        }

        float3 shift = float3.zero;
        if ((RigidMask & 1) != 0 && minX - pr < RigidRect.x) shift.x += RigidRect.x - (minX - pr);
        if ((RigidMask & 2) != 0 && maxX + pr > RigidRect.y) shift.x += RigidRect.y - (maxX + pr);
        if ((RigidMask & 4) != 0 && minY - pr < RigidRect.z) shift.y += RigidRect.z - (minY - pr);
        if ((RigidMask & 8) != 0 && maxY + pr > RigidRect.w) shift.y += RigidRect.w - (maxY + pr);
        if (math.lengthsq(shift) <= 0f) return;

        for (int i = start; i < end; i++) Pos[i] += shift;
        if (bodyNormals.Length < bodyNormals.Capacity) bodyNormals.Add(math.normalize(shift));
    }

    private static float3 ApplyFriction(float3 pi, float3 prev, float3 n, float dl, float friction)
    {
        if (friction <= 0f) return pi;
        float3 move = pi - prev;
        float3 tangent = move - math.dot(move, n) * n;
        float tl = math.length(tangent);
        if (tl < 1e-7f) return pi;
        return pi - tangent * math.min(1f, friction * dl / tl);
    }

    /// <summary>
    /// v = (pos - prev) / dt, rồi contact damping (tương đương restitution 0):
    /// - particle đang chạm: triệt vận tốc dọc pháp tuyến tiếp xúc của particle đó;
    /// - toàn bộ bóng: với mỗi vật đang chạm, triệt phần vận tốc tiến vào vật đó của mọi particle.
    ///   Không có bước này thì phần bóng chưa chạm vẫn mang động lượng ép vào ràng buộc thể tích rồi bật lại,
    ///   gây nảy/rung kéo dài. Vận tốc rời xa và tiếp tuyến được giữ nguyên nên vẫn đá lên và trượt được.
    /// Triệt về 0 chứ không về vận tốc bóng kia, vì vận tốc bóng kia trễ một substep tạo vòng lặp không suy giảm.
    /// </summary>
    private float3 UpdateVelocities(in SoftBubbleParams p, in FixedList512Bytes<float3> bodyNormals, int start, int end, float dt,
        float invCount, out float meanSpeed)
    {
        float invDt = 1f / dt;
        float damping = math.saturate(p.ContactDamping);
        int bodyCount = damping > 0f ? bodyNormals.Length : 0;
        float3 vcm = float3.zero;
        for (int i = start; i < end; i++)
        {
            float3 v = (Pos[i] - PrevPos[i]) * invDt;
            float3 c = Scratch[i];
            float clsq = math.lengthsq(c);
            if (damping > 0f && clsq > 1e-8f)
            {
                float3 n = c * math.rsqrt(clsq);
                float vn = math.dot(v, n);
                v -= n * (vn * damping);
            }

            for (int bi = 0; bi < bodyCount; bi++)
            {
                float3 n = bodyNormals[bi];
                float vn = math.dot(v, n);
                if (vn < 0f) v -= n * (vn * damping);
            }

            Vel[i] = v;
            vcm += v;
        }

        vcm *= invCount;
        float k = math.saturate(p.InternalDamping * dt);
        float speedSum = 0f;
        for (int i = start; i < end; i++)
        {
            float3 v = k > 0f ? math.lerp(Vel[i], vcm, k) : Vel[i];
            Vel[i] = v;
            speedSum += math.length(v);
        }

        // Tốc độ trung bình (không phải lớn nhất) để một particle nhiễu đơn lẻ không giữ cả bóng thức mãi.
        meanSpeed = speedSum * invCount;
        return vcm;
    }
}

/// <summary>
/// Dựng lưới hiển thị từ particle: vị trí so với tâm + pháp tuyến, ghi thẳng vào Visual (khớp stream 0 của Mesh để upload một lần).
/// Đỉnh mô phỏng lấy thẳng từ particle; đỉnh chia nhỏ thêm là trung điểm cạnh cha được đẩy ra theo cung tròn
/// (sag = L/2 * tan(theta/4) với theta là góc giữa hai pháp tuyến cha, đúng tuyệt đối cho mặt cầu) nên rìa tròn mượt dù lưới
/// vật lý chỉ 162 particle. Cuối cùng tính pháp tuyến từ tam giác lưới hiển thị và đẩy mesh ra ngoài đúng ParticleRadius
/// để bề mặt nhìn thấy trùng với bề mặt va chạm. Bóng không được mô phỏng trong step (Untouched) thì bỏ qua, lưới cũ còn nguyên.
/// </summary>
[BurstCompile]
public struct ComputeMeshJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<BubbleDesc> Descs;
    [ReadOnly] public NativeArray<BubbleState> States;
    [ReadOnly] public NativeArray<int> Untouched;
    [ReadOnly, NativeDisableParallelForRestriction] public NativeArray<float3> Pos;
    [ReadOnly, NativeDisableParallelForRestriction] public NativeArray<int3> Tris;
    [ReadOnly, NativeDisableParallelForRestriction] public NativeArray<int2> VisualParents;
    [ReadOnly, NativeDisableParallelForRestriction] public NativeArray<int3> VisualTris;
    [NativeDisableParallelForRestriction] public NativeArray<SoftVisualVertex> Visual;
    [NativeDisableParallelForRestriction] public NativeArray<float3> Normals; // scratch gom pháp tuyến

    public void Execute(int b)
    {
        if (Untouched[b] != 0) return;
        BubbleDesc d = Descs[b];
        float3 center = States[b].Center;
        int pStart = d.ParticleStart;
        int vStart = d.VisualStart;
        int surfaceEnd = vStart + d.SurfaceCount;
        int vEnd = vStart + d.VisualCount;

        // A. Đỉnh mô phỏng: vị trí so với tâm và pháp tuyến từ tam giác mô phỏng (cần cho bước nội suy).
        for (int k = 0; k < d.SurfaceCount; k++)
        {
            Visual[vStart + k] = new SoftVisualVertex { Position = Pos[pStart + k] - center };
            Normals[vStart + k] = float3.zero;
        }

        int triEnd = d.TriStart + d.TriCount;
        for (int t = d.TriStart; t < triEnd; t++)
        {
            int3 tr = Tris[t] - pStart + vStart;
            float3 a = Visual[tr.x].Position;
            float3 n = math.cross(Visual[tr.y].Position - a, Visual[tr.z].Position - a);
            Normals[tr.x] += n;
            Normals[tr.y] += n;
            Normals[tr.z] += n;
        }

        for (int i = vStart; i < surfaceEnd; i++) Normals[i] = SafeNormal(Normals[i], Visual[i].Position);

        // B. Đỉnh chia nhỏ: cha luôn có index nhỏ hơn nên duyệt theo thứ tự là đủ, kể cả chia nhiều mức.
        for (int i = surfaceEnd; i < vEnd; i++)
        {
            int2 p = VisualParents[i];
            float3 pa = Visual[p.x].Position;
            float3 pb = Visual[p.y].Position;
            float3 na = Normals[p.x];
            float3 nb = Normals[p.y];
            float3 mid = (pa + pb) * 0.5f;
            float3 nm = SafeNormal(na + nb, mid);
            float theta = math.acos(math.clamp(math.dot(na, nb), -1f, 1f));
            float sag = 0.5f * math.distance(pa, pb) * math.tan(theta * 0.25f);
            Visual[i] = new SoftVisualVertex { Position = mid + nm * sag };
            Normals[i] = nm;
        }

        // C. Pháp tuyến cuối từ tam giác lưới hiển thị, rồi đẩy mesh ra ngoài ParticleRadius.
        for (int i = vStart; i < vEnd; i++) Normals[i] = float3.zero;

        int vTriEnd = d.VisualTriStart + d.VisualTriCount;
        for (int t = d.VisualTriStart; t < vTriEnd; t++)
        {
            int3 tr = VisualTris[t];
            float3 a = Visual[tr.x].Position;
            float3 n = math.cross(Visual[tr.y].Position - a, Visual[tr.z].Position - a);
            Normals[tr.x] += n;
            Normals[tr.y] += n;
            Normals[tr.z] += n;
        }

        float inflate = d.ParticleRadius;
        for (int i = vStart; i < vEnd; i++)
        {
            SoftVisualVertex v = Visual[i];
            float3 n = SafeNormal(Normals[i], v.Position);
            v.Normal = n;
            v.Position += n * inflate;
            Visual[i] = v;
        }
    }

    private static float3 SafeNormal(float3 n, float3 fallbackDir)
    {
        float len = math.length(n);
        if (len > 1e-8f) return n / len;
        return math.normalizesafe(fallbackDir, new float3(0f, 1f, 0f));
    }
}
