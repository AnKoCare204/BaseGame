using LitMotion;
using TW.Utility.CustomComponent;
using UnityEngine;

/// <summary>
/// Gốc chung cho các hiệu ứng ParticleSystem dùng pool (TapEffect, BubbleBreakEffect): Play lấy một instance từ pool
/// ALinkedPoolMonoBehaviour (tạo mới khi hết), đặt tại vị trí world + DepthOffset, scale theo tham số rồi Play; ParticleSystem gốc
/// của prefab phải có Stop Action = Callback để OnParticleSystemStopped trả instance về pool, không Instantiate / Destroy mỗi lần.
/// ALinkedPoolMonoBehaviour giữ Root tĩnh theo T nên mỗi prefab effect cần một lớp con riêng, không thì instance của các prefab lẫn nhau.
/// </summary>
[RequireComponent(typeof(ParticleSystem))]
public abstract class PooledParticleEffect<T> : ALinkedPoolMonoBehaviour<T> where T : PooledParticleEffect<T>
{
    [field: SerializeField, Tooltip("ParticleSystem gốc của prefab; các emitter con chạy theo nó")]
    private ParticleSystem Particle { get; set; }

    [field: SerializeField, Tooltip("Độ lệch Z so với điểm phát (âm = về phía camera) để effect vẽ trước bóng và cá")]
    private float DepthOffset { get; set; } = -3f;
    private MotionHandle ClearHandle { get; set; }
    private void Reset()
    {
        Particle = GetComponent<ParticleSystem>();
    }

    /// <summary>
    /// Phát effect tại một điểm world. scale nhân vào localScale của instance (prefab dựng ở scale 1, ParticleSystem scaling mode
    /// Hierarchy nên cỡ, tốc độ và shape đều theo). prefab null thì bỏ qua.
    /// </summary>
    public static void Play(T prefab, Vector3 position, Transform parent, float scale = 1f)
    {
        if (prefab == null) return;
        position.z += prefab.DepthOffset;
        T effect = prefab.Get(position, Quaternion.identity, parent);
        effect.transform.localScale = Vector3.one * scale;
        ParticleSystem particle = effect.Particle != null ? effect.Particle : effect.GetComponent<ParticleSystem>();
        particle.Clear(true);
        particle.Play(true);
        effect.ClearHandle = LMotion.Create(0,1,3f).WithOnComplete(effect.OnParticleSystemStopped).RunWithoutBinding();
    }

    /// <summary>Unity gọi khi ParticleSystem gốc dừng (Stop Action = Callback): trả instance về pool.</summary>
    private void OnParticleSystemStopped()
    {
        Release();
    }
}
