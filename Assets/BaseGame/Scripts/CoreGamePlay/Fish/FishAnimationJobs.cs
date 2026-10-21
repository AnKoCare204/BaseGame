using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.Jobs;

/// Loại bone trong job: Wave = thân / đầu / vây giữa / chân / tay (xoay quanh một trục theo sóng thân), SideFin = vây ngực / bụng / chân chèo (quét + xoắn theo pha vây).
/// Trục xoay chọn bằng Axis: 0 = up, 1 = dọc thân, 2 = FinAxis (trục tùy ý root space, cho chân / tay của loài không phải cá).
public enum FishBoneKind
{
    Wave = 0,
    SideFin = 1
}

/// Dữ liệu một bone đã bake từ tham số Inspector (class con bake trong BakeBone, lớp cha điền Parent / StaticChain / BindLocal).
/// Mọi hệ số không phụ thuộc Speed đều được nhân sẵn vào Amp / TwistAmp / PhaseLag để job chỉ còn sin, cos và nhân quaternion.
public struct FishBoneData
{
    public int Kind;                // FishBoneKind
    public int Parent;              // index toàn cục của bone đã đăng ký gần nhất phía trên, -1 = root của animator
    public quaternion StaticChain;  // tích localRotation của các transform không đăng ký nằm giữa bone cha và cha trực tiếp (identity nếu là con trực tiếp)
    public quaternion BindLocal;    // localRotation lúc bind
    public float Along;             // 0 đầu .. 1 đuôi
    public float Amp;               // độ. Wave: biên độ đã nhân profile / hệ số đốt / tipSign; SideFin: biên độ quét
    public float TwistAmp;          // SideFin: biên độ xoắn quanh FinAxis (đã nhân hệ số đốt)
    public float PhaseLag;          // rad, trừ vào pha (trễ theo đốt, lệch pha trái / phải, lệch pha vây bụng)
    public float BendScale;         // hệ số uốn yaw theo đường bơi, 0 = không uốn
    public int PitchBend;           // 1 = uốn thêm theo pitch (thân)
    public float Side;              // SideFin: -1 trái / +1 phải
    public float FoldScale;         // SideFin: hệ số gập ra sau khi bơi nhanh (1 vây ngực, 0.5 vây bụng)
    public int Axis;                // 0 = trục up, 1 = trục dọc thân, 2 = FinAxis (trục tùy ý, root space)
    public float3 FinAxis;          // SideFin: trục gốc -> chóp vây; Axis = 2: trục xoay tùy ý (root space, đơn vị)
    public float Bias;              // độ, góc nghỉ lệch khỏi bind pose (dao động quanh Bias, không quanh 0): tay bạch tuộc cụp về tâm
    public float PhaseSkew;         // Wave + SideFin: méo pha ph + k·sin(ph) (0 = sin đều; 0.5 = nửa chu kỳ lên nhanh, xuống chậm — co bóp / phụt)
    public float PulseSharpen;      // Wave: 0 = sin; càng gần 1 sóng càng vuông (giữ lâu ở hai đầu, chuyển nhanh): sóng = sign(s)·|s|^(1 − sharpen)
    public float3 ScaleAmp;         // biên độ scale theo pha (tỉ lệ, 0 = không scale): localScale = BindScale * (1 + ScaleAmp * sin)
    public float3 BindScale;        // localScale lúc bind (lớp cha điền)
    public int IdleVariant;         // 1 = có bộ tham số idle riêng (IdleAmp / IdlePhaseLag / IdleBias / IdleScaleAmp), 0 = lớp cha copy giá trị bơi vào
    public float IdleAmp;           // giá trị dùng khi Speed ≤ Params.IdleSpeed; lerp sang Amp khi Speed → Params.SwimSpeed
    public float IdlePhaseLag;
    public float IdleBias;
    public float3 IdleScaleAmp;
    public float RollBend;          // hệ số trễ quanh trục dọc thân khi root xoay quanh trục đó (bạch tuộc đổi hướng trong mặt phẳng màn hình), 0 = không
    public float IdlePhaseSkew;     // dạng sóng lúc idle (thường mềm hơn lúc bơi)
    public float IdlePulseSharpen;
    public float PitchBias;         // Wave: độ ngả tĩnh quanh trục ngang (root space), cộng thêm ngoài sóng (hải cẩu nằm ngang hơn khi bơi)
    public float IdlePitchBias;     // PitchBias lúc idle (IdleVariant)
    public int Profile;             // Wave: 0 = sin (+ PhaseSkew / PulseSharpen), 1 = jet khi bơi (FishWaveProfiles.JetWave: mở → giật → giữ chụm); idle vẫn sin, trộn theo blend
    public float JetOpen;           // Profile 1: phần chu kỳ mở tay (chụm → xòe), 0..1
    public float JetSnap;           // Profile 1: phần chu kỳ giật chụm, sau đó giữ chụm tới hết chu kỳ
    public int YawBendUp;           // Wave: 1 = uốn khi rẽ (BendScale) luôn quanh trục up, sóng vẫn quanh trục Axis (cá heo: sóng quanh trục ngang = quẫy lên xuống);
                                    // 0 = uốn rẽ cộng vào góc sóng quanh cùng trục Axis (mọi loài cũ)
}

/// Tham số chung của một con cá, bake từ Inspector (đổi tham số lúc Play thì OnValidate bake lại).
public struct FishParams
{
    public float SwayFrequency;     // Hz sóng thân khi Speed = 1
    public float FinFrequency;      // Hz vây khi Speed = 1
    public float WaveK;             // 2π / waveLength
    public float FinFold;           // độ gập vây ra sau tại Speed = 2
    public float TailSign;          // +1 nếu đuôi nằm phía +ForwardAxis
    public float3 UpAxis;           // root space
    public float3 ForwardAxis;      // root space, trục dài thân
    public float3 LateralAxis;      // root space, trục ngang
    public float TurnBend;          // độ mỗi đốt khi rẽ 90°/s
    public float PitchBend;         // độ mỗi đốt khi ngóc / chúi 90°/s
    public float BendDelay;         // giây trễ tại chóp đuôi
    public float BendSmoothing;     // lọc tốc độ góc
    public float MaxBendPerBone;
    public float IdleSpeed;         // Speed ≤ IdleSpeed dùng bộ idle của bone (IdleVariant), ≥ SwimSpeed dùng bộ bơi, giữa thì lerp
    public float SwimSpeed;
    public float BlendSmoothing;    // 1/s: blend idle→bơi đuổi theo Speed với hằng số thời gian 1/BlendSmoothing (0 = tức thì)
    public int SteadyRhythm;        // 1 = nhịp ổn định khi bơi (rùa): pha thân / pha vây chạy đúng SwimSwayFrequency / SwimFinFrequency và biên độ không theo Speed (không nhân sqrt(Speed), vây không bị luật 1.4 − 0.5·Speed bóp); idle như cá thường, trộn theo blend
    public float SwimSwayFrequency; // Hz pha thân khi bơi với SteadyRhythm (≤ 0 = dùng SwayFrequency)
    public float SwimFinFrequency;  // Hz pha vây khi bơi với SteadyRhythm (≤ 0 = dùng FinFrequency)
}

/// Đầu vào mỗi frame cho một con cá (main thread ghi trước khi schedule).
public struct FishFrameInput
{
    public quaternion RootRotation; // transform.rotation của animator
    public float Speed;
    public float Dt;                // 0 = đứng yên (đóng băng)
    public int Active;              // 0 = bỏ qua (đã bị hủy)
    public int PoseOnly;            // 1 = chỉ đặt pose theo pha hiện có, không tích luỹ (Evaluate / preview)
}

/// Trạng thái mô phỏng của một con cá, sống trong FishAnimationSystem, giữ nguyên khi layout dựng lại.
public struct FishSimState
{
    public float Clock;
    public quaternion PrevRot;
    public int HasPrev;
    public float YawRate;           // độ/giây, local space, đã lọc
    public float PitchRate;
    public float RollRate;          // độ/giây quanh trục dọc thân (local Z), đã lọc
    public int HistHead;
    public float BodyPhase;         // rad
    public float FinPhase;          // rad
    public float IdleBlend;         // 0 = idle .. 1 = bơi, đã làm mượt theo thời gian (Evaluate / PoseOnly dùng giá trị tức thời)
}

/// Tính pose mọi bone của từng con cá (một cá mỗi index): đo tốc độ góc của root, tích luỹ pha, rồi duyệt bone cha trước con,
/// tự nhân dồn world rotation nên không cần đọc Transform. Kết quả là localRotation trong LocalRot, FishApplyJob ghi vào Transform.
[BurstCompile]
public struct FishPoseJob : IJobParallelFor
{
    public const int Hist = 128;    // số mẫu lịch sử tốc độ góc mỗi cá (lấy mẫu trễ theo vị trí trên thân)

    [ReadOnly] public NativeArray<FishFrameInput> Inputs;
    [ReadOnly] public NativeArray<FishParams> Params;
    [ReadOnly] public NativeArray<int2> Ranges;                                          // x = bone đầu, y = số bone
    [ReadOnly, NativeDisableParallelForRestriction] public NativeArray<FishBoneData> Bones;
    public NativeArray<FishSimState> States;
    [NativeDisableParallelForRestriction] public NativeArray<float4> History;            // Hist mẫu mỗi cá: (t, yaw, pitch, roll)
    [NativeDisableParallelForRestriction] public NativeArray<quaternion> LocalRot;
    [NativeDisableParallelForRestriction] public NativeArray<quaternion> WorldRot;
    [NativeDisableParallelForRestriction] public NativeArray<float3> LocalScale;     // BindScale * (1 + ScaleAmp * sin), mọi bone

    public void Execute(int f)
    {
        FishFrameInput input = Inputs[f];
        if (input.Active == 0) return;
        FishParams p = Params[f];
        FishSimState st = States[f];
        int2 range = Ranges[f];
        int histBase = f * Hist;
        float speed = input.Speed;
        quaternion root = input.RootRotation;
        // blend idle (Speed ≤ IdleSpeed) -> bơi (Speed ≥ SwimSpeed) cho bone có IdleVariant; bone khác luôn dùng bộ bơi.
        // Lúc chạy thật blend được làm mượt theo thời gian (BlendSmoothing) để Speed nhấp nhô không làm tay giật; Evaluate dùng tức thời.
        float w = math.saturate((speed - p.IdleSpeed) / math.max(p.SwimSpeed - p.IdleSpeed, 1e-3f));

        if (input.PoseOnly == 0)
        {
            float dt = input.Dt;
            if (dt <= 0f) return;
            st.Clock += dt;
            MeasureTurn(ref st, root, dt, p.BendSmoothing, histBase);
            if (p.BlendSmoothing > 0f) st.IdleBlend += (w - st.IdleBlend) * (1f - math.exp(-p.BlendSmoothing * dt));
            else st.IdleBlend = w;
            w = st.IdleBlend;
            // nhịp ổn định (rùa): đang bơi pha thân / pha vây chạy đúng SwimSwayFrequency / SwimFinFrequency, đứng yên thì theo Speed như cá thường;
            // trộn theo w (đã làm mượt) nên đổi chế độ không giật
            float steady = p.SteadyRhythm != 0 ? w : 0f;
            float swimSway = p.SwimSwayFrequency > 0f ? p.SwimSwayFrequency : p.SwayFrequency;
            float swimFin = p.SwimFinFrequency > 0f ? p.SwimFinFrequency : p.FinFrequency;
            st.BodyPhase += dt * math.lerp(p.SwayFrequency * speed, swimSway, steady) * (2f * math.PI);
            st.FinPhase += dt * math.lerp(p.FinFrequency * (0.7f + 0.3f * speed), swimFin, steady) * (2f * math.PI);
            States[f] = st;
        }

        float3 up = math.mul(root, p.UpAxis);
        float3 fwd = math.mul(root, p.ForwardAxis);
        float3 lat = math.mul(root, p.LateralAxis);
        float s = math.max(speed, 0.05f);
        float sq = math.sqrt(s);                                  // bơi nhanh vẫy mạnh hơn
        float steadyAmp = p.SteadyRhythm != 0 ? w : 0f;
        float ampScale = math.lerp(sq, 1f, steadyAmp);            // nhịp ổn định: bơi biên độ = tham số; cá thường / idle × sqrt(Speed)
        float finAmp = math.lerp(math.clamp(1.4f - 0.5f * s, 0.4f, 1.4f), 1f, steadyAmp);   // cá: bơi chậm vây vẫy nhiều, bơi nhanh vây gần như giữ yên; nhịp ổn định: không bóp
        float fold = p.FinFold * math.saturate(s - 1f);

        int end = range.x + range.y;
        for (int i = range.x; i < end; i++)
        {
            FishBoneData b = Bones[i];
            quaternion parentWorld = b.Parent >= 0 ? WorldRot[b.Parent] : root;
            parentWorld = math.mul(parentWorld, b.StaticChain);
            quaternion invParent = math.inverse(parentWorld);
            float wb = b.IdleVariant != 0 ? w : 1f;
            float amp = math.lerp(b.IdleAmp, b.Amp, wb);
            float lag = math.lerp(b.IdlePhaseLag, b.PhaseLag, wb);
            float bias = math.lerp(b.IdleBias, b.Bias, wb);
            float pitchBias = math.lerp(b.IdlePitchBias, b.PitchBias, wb);
            float3 scaleAmp = math.lerp(b.IdleScaleAmp, b.ScaleAmp, wb);
            quaternion rot;
            float pulse;   // sin của pha bone, dùng cho kênh scale
            if (b.Kind == (int)FishBoneKind.SideFin)
            {
                float ph = st.FinPhase - lag;
                float skew = math.lerp(b.IdlePhaseSkew, b.PhaseSkew, wb);
                ph += skew * math.sin(ph);                          // méo pha: skew âm = nửa đi xuống nhanh / đi lên chậm (chân chèo rùa quạt xuống có lực)
                pulse = math.sin(ph);
                float sweep = amp * finAmp * pulse;
                float twist = b.TwistAmp * finAmp * math.cos(ph);   // lệch pha 90° so với quét (đẩy nước)
                float sweepSigned = b.Side * (sweep + bias - p.TailSign * fold * b.FoldScale); // gập về phía đuôi
                float3 sweepAxis = b.Axis == 0 ? up : b.Axis == 1 ? fwd : math.mul(root, b.FinAxis);
                rot = math.mul(RotateAbout(invParent, sweepAxis, sweepSigned),
                    RotateAbout(invParent, math.mul(root, b.FinAxis), twist));
            }
            else
            {
                float ph = st.BodyPhase - p.WaveK * b.Along - lag;
                float skew = math.lerp(b.IdlePhaseSkew, b.PhaseSkew, wb);
                float sharpen = math.lerp(b.IdlePulseSharpen, b.PulseSharpen, wb);
                float phS = ph + skew * math.sin(ph);              // méo pha: nửa chu kỳ lên nhanh, xuống chậm (co bóp / phụt)
                pulse = math.sin(phS);
                if (sharpen > 0f)                                  // ép về dạng vuông mượt (tanh): giữ ở hai đầu, chuyển nhanh nhưng không gãy vận tốc
                {
                    float k = sharpen * 6f;
                    pulse = math.tanh(k * pulse) / math.tanh(k);
                }
                if (b.Profile == 1)                                // jet (mở → giật → giữ chụm) chỉ khi bơi; idle giữ sin mềm, giữa hai mức trộn tuyến tính
                    pulse = math.lerp(pulse, FishWaveProfiles.JetWave(math.frac(ph / (2f * math.PI)), b.JetOpen, b.JetSnap), wb);
                float a = bias + amp * ampScale * pulse;
                float by = 0f, bp = 0f, br = 0f;
                if (b.BendScale > 0f || b.RollBend > 0f) GetTurnBend(in p, in st, histBase, b.Along, b.BendScale, b.RollBend, out by, out bp, out br);
                float3 axis = b.Axis == 0 ? up : b.Axis == 1 ? fwd : math.mul(root, b.FinAxis);
                rot = b.YawBendUp != 0
                    ? math.mul(RotateAbout(invParent, up, by), RotateAbout(invParent, axis, a))
                    : RotateAbout(invParent, axis, a + by);
                if (b.PitchBend != 0 || pitchBias != 0f) rot = math.mul(rot, RotateAbout(invParent, lat, bp + pitchBias));
                if (b.RollBend > 0f) rot = math.mul(rot, RotateAbout(invParent, fwd, br));   // trễ khi root xoay quanh trục dọc thân (bạch tuộc đổi hướng)
            }

            quaternion local = math.mul(rot, b.BindLocal);
            LocalRot[i] = local;
            WorldRot[i] = math.mul(parentWorld, local);
            LocalScale[i] = b.BindScale * (1f + scaleAmp * pulse);
        }
    }

    /// Quaternion xoay `degrees` quanh trục worldAxis, biểu diễn trong parent space (tương đương Quaternion.AngleAxis(deg, parent.InverseTransformDirection(axis))).
    private static quaternion RotateAbout(quaternion invParent, float3 worldAxis, float degrees)
    {
        float3 axis = math.mul(invParent, worldAxis);
        float lenSq = math.lengthsq(axis);
        if (lenSq < 1e-12f) return quaternion.identity;
        return quaternion.AxisAngle(axis * math.rsqrt(lenSq), math.radians(degrees));
    }

    /// Đo tốc độ góc của root trong local space frame trước (yaw quanh Y, pitch quanh X), lọc rồi ghi lịch sử.
    private void MeasureTurn(ref FishSimState st, quaternion rot, float dt, float smoothing, int histBase)
    {
        if (st.HasPrev != 0)
        {
            quaternion d = math.mul(math.inverse(st.PrevRot), rot);
            ToAngleAxis(d, out float ang, out float3 axis);
            if (ang > 180f) ang -= 360f;
            float y = ang * axis.y / dt;
            float pch = ang * axis.x / dt;
            float rl = ang * axis.z / dt;
            float k = 1f - math.exp(-smoothing * dt);
            st.YawRate += (y - st.YawRate) * k;
            st.PitchRate += (pch - st.PitchRate) * k;
            st.RollRate += (rl - st.RollRate) * k;
        }

        st.PrevRot = rot;
        st.HasPrev = 1;
        st.HistHead = (st.HistHead + 1) % Hist;
        History[histBase + st.HistHead] = new float4(st.Clock, st.YawRate, st.PitchRate, st.RollRate);
    }

    /// Như Quaternion.ToAngleAxis: góc (độ, 0..360) và trục đơn vị; quaternion gần identity trả trục 0.
    private static void ToAngleAxis(quaternion q, out float degrees, out float3 axis)
    {
        float4 v = q.value;
        float w = math.clamp(v.w, -1f, 1f);
        degrees = math.degrees(2f * math.acos(w));
        float s = math.sqrt(math.max(0f, 1f - w * w));
        axis = s > 1e-6f ? v.xyz / s : float3.zero;
    }

    /// Góc uốn (độ) tại vị trí along do rẽ / ngóc-chúi: lấy mẫu trễ BendDelay * along, nhân scale, kẹp MaxBendPerBone.
    private void GetTurnBend(in FishParams p, in FishSimState st, int histBase, float along, float scale, float rollScale, out float yawDeg, out float pitchDeg, out float rollDeg)
    {
        SampleTurnRates(in st, histBase, p.BendDelay * along, out float yr, out float pr, out float rr);
        yawDeg = math.clamp(-p.TurnBend * yr / 90f * scale, -p.MaxBendPerBone, p.MaxBendPerBone);
        pitchDeg = math.clamp(-p.PitchBend * pr / 90f * scale, -p.MaxBendPerBone, p.MaxBendPerBone);
        rollDeg = math.clamp(-p.TurnBend * rr / 90f * rollScale, -p.MaxBendPerBone, p.MaxBendPerBone);
    }

    /// Tốc độ góc (độ/giây, đã lọc) cách đây `delay` giây, nội suy từ lịch sử.
    private void SampleTurnRates(in FishSimState st, int histBase, float delay, out float yaw, out float pitch, out float roll)
    {
        float target = st.Clock - delay;
        int i = st.HistHead;
        for (int n = 0; n < Hist - 1; n++)
        {
            int j = (i - 1 + Hist) % Hist;
            float4 hj = History[histBase + j];
            if (hj.x <= target)
            {
                float4 hi = History[histBase + i];
                float span = hi.x - hj.x;
                float f = span > 1e-5f ? math.saturate((target - hj.x) / span) : 1f;
                yaw = math.lerp(hj.y, hi.y, f);
                pitch = math.lerp(hj.z, hi.z, f);
                roll = math.lerp(hj.w, hi.w, f);
                return;
            }

            i = j;
        }

        float4 last = History[histBase + i];
        yaw = last.y;
        pitch = last.z;
        roll = last.w;
    }
}

/// Ghi localRotation đã tính vào Transform của từng bone (chạy song song theo hierarchy root).
[BurstCompile]
public struct FishApplyJob : IJobParallelForTransform
{
    [ReadOnly] public NativeArray<quaternion> LocalRot;
    [ReadOnly] public NativeArray<float3> LocalScale;
    [ReadOnly] public NativeArray<FishBoneData> Bones;    // chỉ để biết bone nào có ScaleAmp (mới ghi localScale)
    [ReadOnly] public NativeArray<int> BoneFish;
    [ReadOnly] public NativeArray<FishFrameInput> Inputs;

    public void Execute(int i, TransformAccess t)
    {
        FishFrameInput input = Inputs[BoneFish[i]];
        if (input.Active == 0 || (input.PoseOnly == 0 && input.Dt <= 0f)) return;
        t.localRotation = LocalRot[i];
        FishBoneData b = Bones[i];
        if (math.any(b.ScaleAmp != 0f) || math.any(b.IdleScaleAmp != 0f)) t.localScale = LocalScale[i];   // chỉ bone có kênh scale (mantle bạch tuộc)
    }
}
