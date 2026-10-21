using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

/// <summary>
/// Chạy TRƯỚC hook build của Addressables (AddressablesPlayerBuildProcessor có callbackOrder 1): đánh dấu đủ entry level,
/// ép layout App để APK / IPA luôn bake UI + level local và KHÔNG bật remote catalog, kể cả khi ai đó vừa Build Levels
/// xong mà layout chưa kịp restore, hoặc build từ một bản checkout chưa từng bấm Setup.
/// Thiếu file level trong dải 1..LevelMaxInConfig, hoặc có file level hỏng, thì dừng build.
/// </summary>
public class LevelAddressableBuildProcessor : BuildPlayerProcessor
{
    public override int callbackOrder => 0;

    public override void PrepareForBuild(BuildPlayerContext buildPlayerContext)
    {
        if (!LevelAddressableSetup.HasVariant)
        {
            Debug.LogWarning("[LevelAddressable] Build profile không có define CHEAT_ONLY / RELEASE_ONLY: " +
                             "runtime sẽ đọc catalog level của bản Release.");
        }

        if (!LevelAddressableSetup.SetupAll(out string message))
        {
            throw new BuildFailedException($"[LevelAddressable] Level thiếu hoặc hỏng, dừng build: {message}");
        }

        Debug.Log($"[LevelAddressable] Trước khi build player: {message}");
    }
}
