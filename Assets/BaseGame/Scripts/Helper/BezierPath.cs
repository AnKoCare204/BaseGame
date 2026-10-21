using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Đường bơi ghép từ nhiều đoạn Bezier bậc 3 đi qua đúng các điểm cho trước. Điểm nào có hướng (Forward khác Vector3.zero)
/// thì tiếp tuyến tại đó bị ghim đúng hướng ấy; điểm nào không có hướng thì tiếp tuyến được giải từ hệ tam giác (spline bậc 3 kẹp,
/// tham số theo chiều dài dây cung) sao cho độ cong liên tục (C2) tại mọi điểm, tức là đường trơn nhất đi qua các điểm với
/// những hướng đã ghim. Đầu hoặc cuối không có hướng thì là đầu tự do (độ cong 0). Thường chỉ cần ghim hướng đầu và cuối.
/// Tham số t của GetPoint/GetTangent/GetDirection là tỉ lệ quãng đường 0..1 trên cả đường (qua bảng chiều dài) nên đi với tốc độ đều.
/// Đường mở, đi từ điểm đầu tới điểm cuối. Gọi Set để dựng lại trên cùng instance, không cấp phát lại khi số điểm không tăng.
/// </summary>
public class BezierPath
{
    /// <summary>Độ căng mặc định tại điểm có hướng ghim: điểm điều khiển cách điểm đó 1/3 độ dài đoạn (bằng tốc độ tự nhiên của spline).</summary>
    public const float DefaultTension = 1f / 3f;

    /// <summary>Số mẫu mỗi đoạn trong bảng chiều dài. Kết hợp nội suy bậc hai theo tốc độ, 32 mẫu đủ đều kể cả đoạn vòng chữ U.</summary>
    private const int SamplesPerSegment = 32;

    /// <summary>Số dây cung con để đo chiều dài mỗi khoảng mẫu, giảm sai số dây cung ngắn hơn cung ở chỗ rẽ gắt.</summary>
    private const int SubChordsPerSample = 4;

    /// <summary>Độ dài dây cung tối thiểu để hai điểm trùng nhau không làm hệ suy biến.</summary>
    private const float MinChord = 1e-5f;

    private Vector3[] points = Array.Empty<Vector3>();

    /// <summary>Hướng đầu vào tại từng điểm, Vector3.zero = tự giải. Giữ riêng để đổi Tension giải lại đúng.</summary>
    private Vector3[] inputForwards = Array.Empty<Vector3>();

    /// <summary>Hướng đơn vị thực dùng tại từng điểm (ghim hoặc đã giải).</summary>
    private Vector3[] forwards = Array.Empty<Vector3>();

    /// <summary>Tiếp tuyến dP/ds (theo tham số chiều dài dây cung) tại từng điểm, có độ lớn.</summary>
    private Vector3[] tangents = Array.Empty<Vector3>();

    /// <summary>Hai điểm điều khiển mỗi đoạn: [2i] sau điểm i, [2i+1] trước điểm i+1.</summary>
    private Vector3[] controls = Array.Empty<Vector3>();

    /// <summary>Độ dài dây cung từng đoạn (không nhỏ hơn MinChord).</summary>
    private float[] chords = Array.Empty<float>();

    /// <summary>Hướng đơn vị từng đoạn (P[i+1] - P[i]) / chord.</summary>
    private Vector3[] deltas = Array.Empty<Vector3>();

    /// <summary>Scratch cho thuật toán Thomas.</summary>
    private float[] cPrime = Array.Empty<float>();

    private Vector3[] dPrime = Array.Empty<Vector3>();

    /// <summary>Chiều dài tích lũy tại từng mẫu, [0] = 0, kích thước SegmentCount * SamplesPerSegment + 1.</summary>
    private float[] lengths = Array.Empty<float>();

    /// <summary>
    /// Tốc độ tham số |B'(u)| tại SamplesPerSegment + 1 mẫu của từng đoạn (mỗi đoạn có riêng hai đầu vì tốc độ hai bên mối nối
    /// khác nhau khi hai đoạn dài khác nhau), dùng nội suy bậc hai trong mỗi khoảng mẫu. Kích thước SegmentCount * (SamplesPerSegment + 1).
    /// </summary>
    private float[] speeds = Array.Empty<float>();

    private int count;
    private float tension = DefaultTension;

    public BezierPath()
    {
    }

    /// <summary>forwards có thể null hoặc ngắn hơn points, phần thiếu coi như Vector3.zero (tự giải cho trơn).</summary>
    public BezierPath(IReadOnlyList<Vector3> points, IReadOnlyList<Vector3> forwards, float tension = DefaultTension)
    {
        this.tension = Mathf.Max(0f, tension);
        Set(points, forwards);
    }

    /// <summary>Chỉ ghim hướng đầu và cuối, các điểm giữa tự giải. endForward bỏ trống (zero) = cuối tự do.</summary>
    public BezierPath(IReadOnlyList<Vector3> points, Vector3 startForward, Vector3 endForward = default, float tension = DefaultTension)
    {
        this.tension = Mathf.Max(0f, tension);
        Set(points, startForward, endForward);
    }

    /// <summary>Số điểm đi qua.</summary>
    public int Count => count;

    /// <summary>Số đoạn Bezier, bằng Count - 1.</summary>
    public int SegmentCount => Mathf.Max(0, count - 1);

    /// <summary>Tổng chiều dài đường (xấp xỉ qua bảng mẫu).</summary>
    public float Length { get; private set; }

    /// <summary>Các điểm đi qua.</summary>
    public IReadOnlyList<Vector3> Points => new ArraySegment<Vector3>(points, 0, count);

    /// <summary>Hướng đơn vị thực dùng tại từng điểm, gồm cả hướng đã giải cho phần tử Vector3.zero.</summary>
    public IReadOnlyList<Vector3> Forwards => new ArraySegment<Vector3>(forwards, 0, count);

    /// <summary>
    /// Độ căng tại các điểm có hướng ghim: điểm điều khiển cách điểm đó Tension * độ dài đoạn. Điểm tự giải không bị ảnh hưởng
    /// trực tiếp (chúng tự thích nghi để giữ trơn). Đổi là dựng lại ngay.
    /// </summary>
    public float Tension
    {
        get => tension;
        set
        {
            float clamped = Mathf.Max(0f, value);
            if (Mathf.Approximately(clamped, tension)) return;
            tension = clamped;
            Rebuild();
        }
    }

    /// <summary>Dựng lại đường từ danh sách điểm và hướng. forwards null hoặc ngắn hơn thì phần thiếu tự giải.</summary>
    public void Set(IReadOnlyList<Vector3> points, IReadOnlyList<Vector3> forwards)
    {
        int n = points?.Count ?? 0;
        EnsureCapacity(n);
        count = n;
        for (int i = 0; i < n; i++)
        {
            this.points[i] = points[i];
            inputForwards[i] = forwards != null && i < forwards.Count ? forwards[i] : Vector3.zero;
        }

        Rebuild();
    }

    /// <summary>Dựng lại đường chỉ ghim hướng đầu và cuối, các điểm giữa tự giải. Hướng bằng zero = đầu đó tự do.</summary>
    public void Set(IReadOnlyList<Vector3> points, Vector3 startForward, Vector3 endForward = default)
    {
        int n = points?.Count ?? 0;
        EnsureCapacity(n);
        count = n;
        for (int i = 0; i < n; i++)
        {
            this.points[i] = points[i];
            inputForwards[i] = Vector3.zero;
        }

        if (n > 0)
        {
            inputForwards[0] = startForward;
            inputForwards[n - 1] = n > 1 ? endForward : startForward;
        }

        Rebuild();
    }

    /// <summary>Vị trí tại tỉ lệ quãng đường t (0..1, clamp). Không có điểm thì trả về Vector3.zero.</summary>
    public Vector3 GetPoint(float t)
    {
        if (count == 0) return Vector3.zero;
        if (count == 1) return points[0];
        Locate(t, out int segment, out float u);
        return Evaluate(segment, u);
    }

    /// <summary>Vị trí tại quãng đường d tính từ điểm đầu (0..Length, clamp).</summary>
    public Vector3 GetPointAtDistance(float distance)
    {
        return GetPoint(Length > 1e-6f ? distance / Length : 0f);
    }

    /// <summary>Tiếp tuyến đã chuẩn hóa tại tỉ lệ quãng đường t. Tại điểm có hướng ghim trùng đúng hướng đó.</summary>
    public Vector3 GetTangent(float t)
    {
        if (count == 0) return Vector3.forward;
        if (count == 1) return forwards[0];
        Locate(t, out int segment, out float u);
        Vector3 d = Derivative(segment, u);
        if (d.sqrMagnitude > 1e-12f) return d.normalized;
        // Đoạn suy biến (hai điểm trùng nhau): lấy hướng của đầu gần hơn.
        return u < 0.5f ? forwards[segment] : forwards[segment + 1];
    }

    /// <summary>Quay nhìn theo tiếp tuyến, lưng hướng lên trời. Bơi gần thẳng đứng thì dùng trục Z thế giới làm up để không suy biến.</summary>
    public Quaternion GetDirection(float t)
    {
        Vector3 tangent = GetTangent(t);
        Vector3 up = Mathf.Abs(Vector3.Dot(tangent, Vector3.up)) > 0.999f ? Vector3.forward : Vector3.up;
        return Quaternion.LookRotation(tangent, up);
    }

    /// <summary>Quãng đường từ điểm đầu tới điểm thứ index.</summary>
    public float GetDistanceAt(int index)
    {
        if (count < 2) return 0f;
        index = Mathf.Clamp(index, 0, count - 1);
        return lengths[index * SamplesPerSegment];
    }

    /// <summary>Tỉ lệ quãng đường t (0..1) tại điểm thứ index, dùng để biết lúc nào đi qua điểm đó.</summary>
    public float GetTAt(int index)
    {
        return Length > 1e-6f ? GetDistanceAt(index) / Length : 0f;
    }

    /// <summary>Vị trí trên đoạn segment theo tham số Bezier u (0..1, clamp), không đều theo quãng đường. Dùng để debug/vẽ.</summary>
    public Vector3 GetSegmentPoint(int segment, float u)
    {
        if (count == 0) return Vector3.zero;
        if (count == 1) return points[0];
        segment = Mathf.Clamp(segment, 0, count - 2);
        return Evaluate(segment, Mathf.Clamp01(u));
    }

    private void EnsureCapacity(int n)
    {
        if (points.Length >= n) return;
        points = new Vector3[n];
        inputForwards = new Vector3[n];
        forwards = new Vector3[n];
        tangents = new Vector3[n];
        cPrime = new float[n];
        dPrime = new Vector3[n];
        int segments = Mathf.Max(0, n - 1);
        chords = new float[segments];
        deltas = new Vector3[segments];
        controls = new Vector3[segments * 2];
        lengths = new float[segments * SamplesPerSegment + 1];
        speeds = new float[segments * (SamplesPerSegment + 1)];
    }

    private void Rebuild()
    {
        Length = 0f;
        if (count == 0) return;

        int segments = count - 1;
        for (int i = 0; i < segments; i++)
        {
            Vector3 d = points[i + 1] - points[i];
            float h = Mathf.Max(d.magnitude, MinChord);
            chords[i] = h;
            deltas[i] = d / h;
        }

        ResolveTangents();
        if (count == 1) return;

        // Hermite theo tham số chiều dài dây cung sang Bezier: điểm điều khiển cách đầu đoạn tiếp tuyến * h / 3.
        for (int i = 0; i < segments; i++)
        {
            float third = chords[i] / 3f;
            controls[i * 2] = points[i] + tangents[i] * third;
            controls[i * 2 + 1] = points[i + 1] - tangents[i + 1] * third;
        }

        float total = 0f;
        Vector3 prev = points[0];
        lengths[0] = 0f;
        for (int i = 0; i < segments; i++)
        {
            int speedBase = i * (SamplesPerSegment + 1);
            speeds[speedBase] = Derivative(i, 0f).magnitude;
            for (int k = 1; k <= SamplesPerSegment; k++)
            {
                for (int j = 1; j <= SubChordsPerSample; j++)
                {
                    Vector3 cur = Evaluate(i, (k - 1 + (float)j / SubChordsPerSample) / SamplesPerSegment);
                    total += Vector3.Distance(prev, cur);
                    prev = cur;
                }

                lengths[i * SamplesPerSegment + k] = total;
                speeds[speedBase + k] = Derivative(i, (float)k / SamplesPerSegment).magnitude;
            }
        }

        Length = total;
    }

    /// <summary>
    /// Tiếp tuyến dP/ds tại từng điểm (s = chiều dài dây cung tích lũy). Điểm có hướng ghim: đúng hướng đó, độ lớn 3 * Tension
    /// (để điểm điều khiển cách Tension * độ dài đoạn). Điểm còn lại: giải hệ tam giác điều kiện độ cong liên tục
    /// h[i] m[i-1] + 2 (h[i-1] + h[i]) m[i] + h[i-1] m[i+1] = 3 (h[i] delta[i-1] + h[i-1] delta[i]) bằng thuật toán Thomas;
    /// đầu/cuối không ghim dùng điều kiện tự do 2 m[0] + m[1] = 3 delta[0] và m[n-2] + 2 m[n-1] = 3 delta[n-2].
    /// Hàng của điểm ghim thay bằng m[i] = giá trị ghim nên hệ vẫn tam giác và chéo trội.
    /// </summary>
    private void ResolveTangents()
    {
        int n = count;
        float pinnedMagnitude = 3f * tension;
        if (n == 1)
        {
            Vector3 f = inputForwards[0];
            forwards[0] = f.sqrMagnitude > 1e-12f ? f.normalized : Vector3.forward;
            tangents[0] = forwards[0] * pinnedMagnitude;
            return;
        }

        int last = n - 1;
        for (int i = 0; i < n; i++)
        {
            Vector3 f = inputForwards[i];
            float a, b, c;
            Vector3 d;
            if (f.sqrMagnitude > 1e-12f)
            {
                a = 0f;
                b = 1f;
                c = 0f;
                d = f.normalized * pinnedMagnitude;
            }
            else if (i == 0)
            {
                a = 0f;
                b = 2f;
                c = 1f;
                d = deltas[0] * 3f;
            }
            else if (i == last)
            {
                a = 1f;
                b = 2f;
                c = 0f;
                d = deltas[last - 1] * 3f;
            }
            else
            {
                a = chords[i];
                b = 2f * (chords[i - 1] + chords[i]);
                c = chords[i - 1];
                d = (deltas[i - 1] * chords[i] + deltas[i] * chords[i - 1]) * 3f;
            }

            if (i == 0)
            {
                cPrime[0] = c / b;
                dPrime[0] = d / b;
            }
            else
            {
                float denom = b - a * cPrime[i - 1];
                cPrime[i] = c / denom;
                dPrime[i] = (d - dPrime[i - 1] * a) / denom;
            }
        }

        tangents[last] = dPrime[last];
        for (int i = last - 1; i >= 0; i--)
        {
            tangents[i] = dPrime[i] - tangents[i + 1] * cPrime[i];
        }

        for (int i = 0; i < n; i++)
        {
            Vector3 t = tangents[i];
            if (t.sqrMagnitude > 1e-12f) forwards[i] = t.normalized;
            else forwards[i] = i > 0 ? forwards[i - 1] : Vector3.forward;
        }
    }

    /// <summary>Đổi tỉ lệ quãng đường t sang (đoạn, tham số u của Bezier) bằng bảng chiều dài. Cần count >= 2.</summary>
    private void Locate(float t, out int segment, out float u)
    {
        int segments = count - 1;
        t = Mathf.Clamp01(t);
        if (Length <= 1e-6f)
        {
            segment = Mathf.Min(Mathf.FloorToInt(t * segments), segments - 1);
            u = t * segments - segment;
            return;
        }

        float target = t * Length;
        int last = segments * SamplesPerSegment;
        int lo = 1;
        int hi = last;
        while (lo < hi)
        {
            int mid = (lo + hi) >> 1;
            if (lengths[mid] < target) lo = mid + 1;
            else hi = mid;
        }

        float span = lengths[lo] - lengths[lo - 1];
        float f = span > 1e-8f ? FractionInInterval(Mathf.Clamp01((target - lengths[lo - 1]) / span), lo) : 1f;
        float sample = lo - 1 + f;
        segment = Mathf.Min(Mathf.FloorToInt(sample / SamplesPerSegment), segments - 1);
        u = sample / SamplesPerSegment - segment;
    }

    /// <summary>
    /// Vị trí tham số f (0..1) trong khoảng mẫu [lo-1, lo] ứng với tỉ lệ quãng đường r, giả sử tốc độ tham số đổi tuyến tính
    /// từ s0 sang s1 trong khoảng: quãng đường tỉ lệ với s0 f + (s1 - s0) f^2 / 2, giải phương trình bậc hai.
    /// Nội suy tuyến tính (f = r) lệch tới 15% ở chỗ tốc độ tụt mạnh như đỉnh vòng chữ U.
    /// </summary>
    private float FractionInInterval(float r, int lo)
    {
        int segment = (lo - 1) / SamplesPerSegment;
        int k = lo - 1 - segment * SamplesPerSegment;
        int speedBase = segment * (SamplesPerSegment + 1) + k;
        float s0 = speeds[speedBase];
        float s1 = speeds[speedBase + 1];
        float ds = s1 - s0;
        float avg = (s0 + s1) * 0.5f;
        if (avg <= 1e-8f || Mathf.Abs(ds) <= 1e-4f * avg) return r;

        float disc = s0 * s0 + 2f * ds * r * avg;
        if (disc < 0f) return r;
        return Mathf.Clamp01((Mathf.Sqrt(disc) - s0) / ds);
    }

    private Vector3 Evaluate(int segment, float u)
    {
        Vector3 p0 = points[segment];
        Vector3 c1 = controls[segment * 2];
        Vector3 c2 = controls[segment * 2 + 1];
        Vector3 p1 = points[segment + 1];
        float v = 1f - u;
        float b0 = v * v * v;
        float b1 = 3f * v * v * u;
        float b2 = 3f * v * u * u;
        float b3 = u * u * u;
        return p0 * b0 + c1 * b1 + c2 * b2 + p1 * b3;
    }

    private Vector3 Derivative(int segment, float u)
    {
        Vector3 p0 = points[segment];
        Vector3 c1 = controls[segment * 2];
        Vector3 c2 = controls[segment * 2 + 1];
        Vector3 p1 = points[segment + 1];
        float v = 1f - u;
        return (c1 - p0) * (3f * v * v) + (c2 - c1) * (6f * v * u) + (p1 - c2) * (3f * u * u);
    }
}
