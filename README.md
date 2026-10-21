# BaseGame

Template dựng game mobile casual theo khung chung của AntFood / FishSort: UI TW.UGUI (Screen / Modal / Activity + MVP Context),
async UniTask, reactive R3, tween LitMotion, save `IDataModule` + `[DataGenerate]`, GlobalConfig (Odin), Remote Config Firebase,
SDK `com.antq.*` (Ads MAX/AdMob, IAP, AppsFlyer, Firebase). UI và art lấy từ FishSort; gameplay là `SampleLevel` tối giản để thay.

Unity 6000.3.20f1, URP 2D, một scene: `Assets/BaseGame/Scenes/GamePlayScene.unity`.

## Có sẵn

- Boot: màn loading đầu → người mới (level ≤ `DefaultLevelPlaying`) vào thẳng bàn, còn lại về Home. Remote Config đọc nền.
- Home dạng tab vuốt: Shop / Home / Setting; Profile (avatar, khung, huy hiệu, hiệu ứng tên), Daily Reward, No Ads, Rate.
- Vòng chơi: loading → `ScreenInGame` (HUD, pause, tiến độ, 3 booster) → thắng (`ActivityCompleted` → `ModalWin`, +coin, +1 mạng)
  / kẹt (`ModalLose` Play On bằng coin hoặc ads, Fail Offer) → thua (`ModalConfirmLose`) → Retry / Home.
- Mạng hồi theo giờ, mạng vô hạn, `ModalFillHeart`; Shop IAP + `ModalReward`; toast / loading block (`ActivityNotificationOverlay`).
- Booster chung `Hint` / `Undo` / `Shuffle` (ResourceType 51–53): mở khoá theo level (`ItemGlobalConfig`), popup mở khoá,
  ép dùng thử lần đầu, mua bằng coin / ads (`ModalBooster`).
- Cheat (`CheatObject` trong HUD / Pause, chỉ bản Cheat): nhảy level, thắng, thua, đặt số lần chơi lại.

## Cấu trúc

```
Assets/BaseGame/
  Addressable/UI/        prefab Screen / Modal / Activity — address Addressables = tên prefab = tên class
  Prefabs/Manager/       prefab các manager (đặt trong scene)
  Prefabs/Gameplay/      SampleLevel.prefab (PrefabGlobalConfig.levelPrefab)
  Prefabs/UI/            prefab con dùng lại (PopupBG, TitleWrap, UIMoneyResource, CheatObject...)
  Scripts/
    UI/                  Screens, Modals, Activities, Button, Anim, Other (tab Home, resource bar...)
    Manager/             UIManager, GameManager, LoadingManager, AudioManager, InGameDataManager, PlayerResourceManager...
    Core/Manager/        LevelManager, BoosterManager, InputManager, CameraManager, DailyRewardManager
    CoreGamePlay/Level/  Level (lớp gốc của màn chơi), SampleLevel, LevelDifficulty, GameGlobalEvent
    Data/                module save (IDataModule)
    GlobalConfig/        GlobalConfig<T> (asset ở Assets/Resources/GlobalConfig/)
    RemoteConfig/        *RemoteConfigData + RemoteConfigManager + KeyRemoteConfig
    Editor/              Perf tools, Tests (EditMode, chạy bằng Tools/Game/Run EditMode Tests)
Assets/Samples/ADS FB AF IAP SDK/1.1.7/   SDK sample (AdsController, IAPController, FirebaseManager...)
```

## Làm game mới từ template

1. Copy repo, đổi `productName` / `applicationIdentifier` / icon trong **từng** Build Profile (`Assets/Settings/Build Profiles`).
   Hiện chỉ có profile `Android - Cheat` (define `CHEAT_ONLY;UNITY_AD_MAX;UNITY_APPSFLYER;UNITY_AD_ADMOB`): tạo profile Release
   bằng cách nhân bản và thay `CHEAT_ONLY` bằng `RELEASE_ONLY`. `UNITY_AD_MAX` cần plugin AppLovin MAX (`Assets/MaxSdk`, chưa có
   trong repo) — import MAX hoặc bỏ define này nếu chỉ dùng AdMob.
2. Firebase: thay `google-services.json` / `GoogleService-Info.plist`, import lại Firebase SDK (thư mục `Assets/Firebase` không nằm
   trong git vì có file > 100 MB). Tạo key Remote Config theo `KeyRemoteConfig`.
3. Ads / IAP / AppsFlyer: điền ID trong `Samples/ADS FB AF IAP SDK/1.1.7/Base Setup/AdsSetup`, gói IAP trong
   `Resources/GlobalConfig/ShopGlobalConfig.asset`.
4. Gameplay: viết lớp kế thừa `Level` (xem `SampleLevel.cs`):
   - dựng bàn trong `OnStartGame(LevelConfig)`, dọn trong `OnClear()`;
   - input qua `OnPointerDown / OnPointerUp / OnClick` (InputManager chỉ gửi khi không pause, không đè UI);
   - báo tiến độ `SetProgress(cur, total)`, thắng `Win()`, kẹt `Lose(LoseReason)`; Play On gọi `Revive()`;
   - booster trong `UseBooster(type)` / `CanUseBooster(type)`.
   Gán prefab vào `PrefabGlobalConfig.levelPrefab`; độ khó theo level trong `LevelGlobalConfig`.
5. Booster: đổi tên / thêm trong dải `ResourceType` 51–59 (không đổi số đã có), cấu hình trong `ItemGlobalConfig`.
6. Âm thanh: key trong `AudioKey` (số tường minh, dải 50–69 cho gameplay riêng), gán clip trong `Prefabs/Manager/AudioManager.prefab`.
7. Reskin: art hiện là của FishSort (`Assets/BaseGame/Graphics`), thay sprite giữ nguyên tên / GUID để prefab không mất tham chiếu.

Quy ước code, cách thêm popup / data / config: skill Claude `abi-unity-game` (`~/.claude/skills/abi-unity-game`).

## Kiểm tra

- Compile không cần mở Unity: `~/.claude/skills/abi-unity-game/scripts/compile_check.sh <thư mục project>`.
- Test EditMode: menu **Tools/Game/Run EditMode Tests** (kết quả trong Console, dòng cuối `done pass=.. fail=..`).
