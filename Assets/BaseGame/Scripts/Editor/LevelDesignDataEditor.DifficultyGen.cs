using System.Collections.Generic;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Cột "Sinh theo độ khó" trong cửa sổ <see cref="LevelRandomWindow"/>.
///
/// Vì sao không chép công thức của bộ data nguồn (Assets/BaseGame/TextAssets/LevelData): chạy solver trên 1000 level
/// của bộ đó cho thấy chúng không khó về cấu trúc - kể cả mức Siêu khó cũng thắng 93-100% và gần như không đụng tới
/// ô chờ (0.0-0.3 trên 5). Game nguồn có ĐỒNG HỒ và bể tự mở theo loài, nên độ khó của nó là 180s/240s/300s cộng với
/// level dài gấp rưỡi, tức là sức bền và tốc độ tay.
///
/// FishSort không có đồng hồ. Điều kiện thua là BoardModel.IsStuck - hết nước đi hợp lệ, tức ô chờ đầy. Và bể ở đây
/// nhận theo HÀNG ĐƠN cố định chứ không tự mở. Nên chỗ tạo khó của FishSort là ĐỘ LỆCH giữa hàng đơn và thứ tự cá
/// xuất hiện:
/// - Hàng đơn suy thẳng từ chuỗi cá (lệch 0): loài nào gom đủ trước thì đơn của loài đó đứng trước, nổ bóng là ăn
///   ngay, gần như không cần ô chờ.
/// - Đẩy một phần đơn lên sớm hơn vị trí tự nhiên: bể bị một đơn chưa có cá chiếm chỗ, cá của các đơn sau phải nằm
///   ô chờ đợi tới lượt. Đẩy càng xa thì càng sát trần ô chờ.
///
/// Nút vặn là "% thắng mục tiêu". Mặc định đo bằng bot như người (LevelHumanBot, lỡ tay PlayerMistakeRate) - đúng số
/// "≈ Người chơi thắng" của nút Đo; bỏ tick "Thước: người chơi" thì về bot cũ LevelSolver.EstimateWinRate.
/// Tool tự dò số bậc đẩy: sinh xong chạy LevelSolver, bot còn thắng nhiều thì đẩy mạnh thêm, thắng ít quá hoặc hỏng thì
/// lùi lại. "Ô chờ tối thiểu" là SÀN chứ không phải trần: giữ cho level khó vì phải tính nước, không phải vì hên xui.
/// Trước đây ô chờ là trần và là thước duy nhất - seed Siêu khó cần 4-5 ô chờ bị vứt, và tool không biết bot thắng
/// bao nhiêu nên level Thường ra ~60% còn Siêu khó không xuống nổi 40%.
/// Xem <see cref="LevelDesignDataEditor"/> cho danh sách các file partial khác.
/// </summary>
public partial class LevelDesignDataEditor
{
    private enum DifficultyPreset
    {
        Thuong = 0,
        Kho = 1,
        SieuKho = 2,

        [InspectorName("Dễ (level đầu)")]
        De = 3
    }

    /// <summary>Tỉ lệ đơn bị đẩy lên sớm. Giữ cố định, chỉ dò số bậc đẩy - hai tham số cùng dò thì nhiễu.</summary>
    private const float AdvanceShare = 0.15f;

    private const int MinAdvanceDistance = 2;
    private const int MaxAdvanceDistance = 40;

    /// <summary>Tỉ lệ thắng của bot lệch mục tiêu trong khoảng này (điểm %) thì nhận seed luôn, khỏi dò tiếp.</summary>
    private const int WinRateAcceptPoints = 5;

    private DifficultyPreset difficultyPreset = DifficultyPreset.Thuong;

    private int diffOrders = 41;
    private int diffSpecies = 14;
    private int diffMinFish = 2;
    private int diffMaxFish = 5;

    /// <summary>
    /// SÀN ô chờ: đường thắng của solver phải dùng ít nhất bấy nhiêu ô. Seed dùng ít hơn là dễ về cấu trúc (bot thắng
    /// thấp chỉ vì hên xui) nên bị đẩy mạnh thêm. Trước đây đây là TRẦN - seed cần nhiều ô chờ hơn bị vứt, tức tool tự
    /// ném đi đúng loại seed Siêu khó cần.
    /// </summary>
    private int diffTargetHolding = 1;

    /// <summary>
    /// Tỉ lệ thắng mục tiêu (%) theo thước đang chọn (diffPlayerRuler). Preset: người chơi 90 / 50 / 20, bot cũ 25 / 3 / 1
    /// (OldRuler*Target). Cả hai đều quy từ 85 / 60 / 40% người thật (LevelBands.TargetWinRate).
    /// </summary>
    private int diffTargetWinRate = Mathf.RoundToInt(LevelHumanBot.NormalTargetWinRate * 100f);

    /// <summary>
    /// Bật (mặc định): "% thắng mục tiêu" đo bằng bot như người lỡ tay PlayerMistakeRate - đúng con số "≈ Người chơi
    /// thắng" của nút Đo, là số người thiết kế đọc. Tắt: về bot cũ (bấm bừa 15%) như trước 2026-09-30, mục tiêu 25/3/1.
    /// Hai thang không quy đổi được cho nhau nên đổi thước thì mục tiêu nhảy về preset của thước đó.
    /// </summary>
    private bool diffPlayerRuler = true;

    /// <summary>
    /// Mục tiêu thước bot cũ (LevelSolver.EstimateWinRate, cũng chỉ bấm bóng thấy trên màn): quy từ 85 / 60 / 40% người thật
    /// theo report winrate 12-14/10 (bot cũ 1-5% thì người thật ~63%, dưới 1% thì ~40%). Dưới vài % bot cũ không còn tách
    /// được Khó với Siêu khó, nên thước này chỉ để tham khảo.
    /// </summary>
    private const float OldRulerNormalTarget = 0.25f;

    private const float OldRulerHardTarget = 0.03f;
    private const float OldRulerSuperHardTarget = 0.01f;

    /// <summary>Số ván đo nhanh mỗi lượt dò. Chỉ lượt nào lọt gần mục tiêu mới đo lại đủ ván (PlayoutsFor).</summary>
    private const int QuickPlayouts = 150;

    /// <summary>
    /// Bật: seed phải có BẪY - bot như người (LevelHumanBot) chơi KHÔNG lỡ tay mà vẫn thua được, tức nước hiển nhiên
    /// có lúc là nước sai. Không bật thì bot cũ có thể xuống thấp chỉ nhờ level dài: bấm bừa 15% cộng dồn qua cả trăm
    /// nước là thua, dù người chơi cứ "có bể thì cho vào" là thắng chắc. Đo trên 849 level DataTemp: Hard/SuperHard
    /// của game gốc bot cũ chỉ thắng 27-42% nhưng bot chơi đúng thắng 100% - dài chứ không khó.
    /// </summary>
    private bool diffRequireTrap;

    /// <summary>Bot như người không lỡ tay thắng không quá bấy nhiêu % thì coi là level có bẫy. Preset: 100 / 90 / 80.</summary>
    private int diffGreedyMax = 90;

    /// <summary>Số ván cho bot kiểm bẫy - chỉ cần biết nó có thua được hay không nên ít ván hơn lúc đo.</summary>
    private const int TrapPlayouts = 100;

    /// <summary>
    /// Tỉ lệ bộ 3 con được rải xa nhau thay vì nằm sát nhau. Sát nhau thì cá cùng loài toàn ở chung một bóng, người
    /// chơi nhặt theo phản xạ; rải ra thì bóng nào cũng nhiều loài, phải nhìn cả bàn mới biết nên phá bóng nào.
    /// </summary>
    private int diffSpreadPercent = 60;

    /// <summary>
    /// Tỉ lệ bóng được nới to hơn cỡ nhỏ nhất. Bóng không nới lấy đúng sàn theo số cá (xem ApplyGenRadius).
    /// </summary>
    private int diffSizeVariety = 40;

    private int diffPureOpeners = 1;

    /// <summary>Trộn cá giữa các bóng thuần đầu: giữ nguyên lượng cá mở màn nhưng không còn bóng nào nổ ra ăn ngay một bộ.</summary>
    private bool diffShuffleOpeners;
    private int diffAttempts = 14;

    /// <summary>Số bóng đá chèn thêm. Đá không chứa cá và không phá được nên nó giảm vĩnh viễn số bóng dùng được
    /// trên bàn - đòn bẩy mạnh nhất, cũng dễ bóp chết level nhất.</summary>
    private int diffRockCount;

    /// <summary>Số bóng bị đóng băng: cá bên trong chỉ bấm được sau khi bóng nổi lên đỉnh vùng chơi.</summary>
    private int diffIceCount = 3;

    /// <summary>Số bóng có vỏ HP. Vỏ chỉ mẻ khi HOÀN THÀNH một bể, nên nó ép người chơi giải phần còn lại của bàn trước.</summary>
    private int diffHpBubbles;

    /// <summary>
    /// Vỏ HP phải chặn bao nhiêu phần trăm đời bóng. HP không tính bằng số cố định nữa: vỏ chỉ mẻ khi HOÀN THÀNH một
    /// bể VÀ bóng đang nằm trên bàn, nên số lần trừ mà một bóng nhận được phụ thuộc vị trí của nó - đầu map ~20 lần,
    /// cuối map chỉ ~9. Đặt cứng HP = 2 thì vỏ vỡ ngay mấy nước đầu, đặt cứng HP = 12 thì bóng cuối map không bao giờ
    /// mở được và level thành bàn chết.
    /// </summary>
    private int diffHpLifePercent = 50;

    /// <summary>
    /// Số bóng người chơi thật sự thấy trên màn cùng lúc. Model có VisibleBubbles bóng trên bàn nhưng bóng to thì màn
    /// chỉ chứa được chừng này, mấy bóng còn lại nằm khuất - mà vỏ HP của bóng khuất vẫn bị trừ mỗi lần xong bể.
    /// </summary>
    private int diffScreenBubbles = 10;

    /// <summary>Số con cá bị ẩn. Mỗi bóng luôn chừa ít nhất một con lộ, vì bóng toàn cá ẩn thì runtime tự lộ hết.</summary>
    private int diffHiddenCount;

    /// <summary>
    /// Số bóng spawner. Spawner giữ một hàng cá chờ: mỗi con cá hiện rời bóng thì một con trong hàng thay vào đúng chỗ
    /// đó, nên bóng không nổ cho tới khi hàng cạn - nó giữ suất trên bàn lâu hơn và người chơi không thấy trước cá sắp ra.
    /// </summary>
    private int diffSpawnerCount;

    /// <summary>Số cá trong hàng chờ của mỗi spawner.</summary>
    private int diffSpawnerFish = 3;

    /// <summary>
    /// Số bóng nhiều lớp (isMultiLayer): lớp ngoài hết cá thì lớp kế mới lộ. Người chơi không thấy cá lớp trong và bóng
    /// giữ chỗ trên bàn lâu hơn - giống spawner nhưng lộ cả lớp một lần. Cá lớp trong trừ vào tổng cá, không cộng thêm.
    /// </summary>
    private int diffLayerBubbles;

    /// <summary>Số lớp mỗi bóng nhiều lớp, tính cả lớp ngoài.</summary>
    private int diffLayerCount = 2;

    private const int MaxLayerCount = 4;

    /// <summary>
    /// Số bóng cá link (isBundleBubble): bấm một con là cả đàn bay, thiếu chỗ cho cả đàn thì không bấm được. Đàn lẫn
    /// loài đổ nhiều con lên ô chờ một lúc - phạt nặng; đàn cùng loài khớp bể là một nước ăn đậm - thưởng.
    /// </summary>
    private int diffBundleCount;

    /// <summary>Cỡ đàn tối đa. Đàn 4-5 con lẫn loài trên 5 ô chờ gần như khoá bàn nên mặc định 3.</summary>
    private int diffBundleMaxFish = 3;

    /// <summary>Đổi cá cho mỗi đàn thuần một loài: biến cá link thành bóng thưởng thay vì bóng phạt.</summary>
    private bool diffBundleSameSpecies;

    /// <summary>
    /// Số cặp khoá - chìa. Ổ khoá (isLock) chặn bấm cá bên trong cho tới khi bóng chìa của nó NỔ HẾT CÁ
    /// (BoardModel.UnlockKeyLocks); chìa cầm keyId = vị trí ổ trong mảng + 1. Người chơi phải dọn bóng chìa trước,
    /// nên cá trong ổ đến muộn hơn chỗ nó đứng.
    /// </summary>
    private int diffLockPairs;

    /// <summary>Bóng chìa đứng trước ổ khoá của nó từ 1 tới bấy nhiêu bóng. Xa quá thì ổ nằm chờ lâu, dễ tắc bàn.</summary>
    private int diffLockGap = 4;

    private bool showObstacles = true;

    private string difficultyReport;

    private void DrawDifficultyPanel(LevelDesignData data)
    {
        EditorGUILayout.BeginVertical(SirenixGUIStyles.BoxContainer);
        GUILayout.Label("Sinh theo độ khó", SirenixGUIStyles.BoldLabelCentered, GUILayout.ExpandWidth(true));
        GUILayout.Label("độ khó = tỉ lệ thắng của bot", SirenixGUIStyles.CenteredGreyMiniLabel, GUILayout.ExpandWidth(true));

        EditorGUI.BeginChangeCheck();
        difficultyPreset = (DifficultyPreset)EditorGUILayout.EnumPopup("Mức", difficultyPreset);
        if (EditorGUI.EndChangeCheck()) ApplyDifficultyPreset();

        if (difficultyPreset == DifficultyPreset.De)
        {
            // Mức Dễ có bộ núm và cách dựng riêng (EasyGen.cs), không dùng vòng dò % thắng.
            DrawEasyKnobs();
            EditorGUILayout.Space(4f);
            GUI.enabled = fishIds.Length > 0;
            if (GUILayout.Button("Sinh level dễ", GUILayout.Height(28f))) pending = () => GenerateEasy(data);
            GUI.enabled = true;
            DrawCarefulToggle();
            if (GUILayout.Button("Đo level đang mở", GUILayout.Height(22f)))
            {
                difficultyReport = MeasureLevel(data.levelData) + MeasureLossSpots(data.levelData);
            }

            if (!string.IsNullOrEmpty(difficultyReport))
            {
                EditorGUILayout.Space(2f);
                GUILayout.Label(difficultyReport, SirenixGUIStyles.MultiLineCenteredLabel);
                DrawLossLevelPicker(data);
            }

            DrawTunePanel(data);

            EditorGUILayout.EndVertical();
            return;
        }

        EditorGUILayout.Space(4f);
        diffOrders = Mathf.Max(1, EditorGUILayout.IntField("Số đơn", diffOrders));
        diffSpecies = Mathf.Clamp(EditorGUILayout.IntField("Số loài", diffSpecies), 1, Mathf.Max(1, fishIds.Length));
        diffMinFish = Mathf.Max(1, EditorGUILayout.IntField("Cá/bóng ít nhất", diffMinFish));
        diffMaxFish = Mathf.Max(diffMinFish, EditorGUILayout.IntField("Cá/bóng nhiều nhất", diffMaxFish));

        int holdingSlots = LevelBands.DefaultHoldingSlots;
        TempContent.text = "Thước: người chơi";
        TempContent.tooltip = "Bật: % thắng đo bằng bot như người - đúng số \"≈ Người chơi thắng\" của nút Đo. " +
                              "Tắt: đo bằng bot cũ (bấm bừa 15%) - thước thô, chỉ để tham khảo.";
        EditorGUI.BeginChangeCheck();
        diffPlayerRuler = EditorGUILayout.Toggle(TempContent, diffPlayerRuler);
        if (EditorGUI.EndChangeCheck()) diffTargetWinRate = PresetTargetWinRate(difficultyPreset, diffPlayerRuler);

        TempContent.text = "% thắng mục tiêu";
        TempContent.tooltip = (diffPlayerRuler
                                  ? "≈ Người chơi thắng (bot như người, chỉ bấm bóng thấy trên màn 1080x1920). Thường 90 / Khó 50 / " +
                                    "Siêu khó 20% ứng với người thật ~85 / 60 / 40%. "
                                  : "Tỉ lệ thắng của bot cũ. ") +
                              $"Tool tự dò độ lệch hàng đơn tới khi bot thắng quanh mức này (±{AcceptPoints(diffTargetWinRate)}%). " +
                              "Càng thấp càng khó. Là thước tương đối, không phải tỉ lệ thắng thật của người chơi.";
        diffTargetWinRate = Mathf.Clamp(EditorGUILayout.IntField(TempContent, diffTargetWinRate), 1, 100);

        EditorGUILayout.BeginHorizontal();
        TempContent.text = "Bắt buộc có bẫy";
        TempContent.tooltip = "Bot như người chơi không lỡ tay (biết phá băng, chọn cá ô chờ có tính) vẫn phải thua " +
                              "được - level khó vì nước hiển nhiên có lúc sai, không phải chỉ vì dài. Tắt = dò như " +
                              "cũ, chỉ theo % thắng.";
        diffRequireTrap = EditorGUILayout.ToggleLeft(TempContent, diffRequireTrap, GUILayout.Width(120f));
        if (diffRequireTrap)
        {
            TempContent.text = "chơi đúng ≤";
            TempContent.tooltip = "Bot như người không lỡ tay thắng không quá bấy nhiêu % thì coi là có bẫy. Càng " +
                                  "thấp bẫy càng gắt.";
            float labelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 70f;
            diffGreedyMax = Mathf.Clamp(EditorGUILayout.IntField(TempContent, diffGreedyMax), 1, 100);
            EditorGUIUtility.labelWidth = labelWidth;
            GUILayout.Label("%", GUILayout.Width(14f));
        }

        EditorGUILayout.EndHorizontal();

        TempContent.text = "Ô chờ tối thiểu";
        TempContent.tooltip = $"Sàn: đường thắng phải dùng ít nhất bấy nhiêu trong {holdingSlots} ô chờ. Dùng nhiều " +
                              "hơn thì càng tốt. Giữ sàn để level khó vì phải tính nước, không phải khó vì hên xui.";
        diffTargetHolding = Mathf.Clamp(EditorGUILayout.IntField(TempContent, diffTargetHolding), 0, holdingSlots);

        TempContent.text = "% bộ rải";
        TempContent.tooltip = "Bao nhiêu phần trăm bộ 3 con bị tách ra xa nhau. 0 = ba con nằm sát nhau, thường rơi " +
                              "chung một bóng nên rất dễ. Càng cao thì bóng càng nhiều loài, càng phải tính nước.";
        diffSpreadPercent = Mathf.Clamp(EditorGUILayout.IntField(TempContent, diffSpreadPercent), 0, 100);

        TempContent.text = "% bóng nới cỡ";
        TempContent.tooltip = "Bóng thường lấy đúng cỡ nhỏ nhất theo số cá. Bấy nhiêu phần trăm bóng được nới to lên " +
                              "(sàn + BubbleGenMinPadding tới sàn bậc sau + BubbleGenMaxBonus) cho có vài quả to che bàn.";
        diffSizeVariety = Mathf.Clamp(EditorGUILayout.IntField(TempContent, diffSizeVariety), 0, 100);

        TempContent.text = "Bóng thuần đầu";
        TempContent.tooltip = "Số bóng chỉ chứa một loài đặt ở đoạn mở màn, nổ ra ăn ngay một bộ.";
        EditorGUILayout.BeginHorizontal();
        diffPureOpeners = Mathf.Clamp(EditorGUILayout.IntField(TempContent, diffPureOpeners), 0, 8);
        TempContent.text = "Đảo";
        TempContent.tooltip = "Trộn cá giữa các bóng thuần đầu với nhau: vẫn đủ bộ ở đoạn mở màn nhưng mỗi bóng lẫn " +
                              "nhiều loài, phải nổ vài bóng mới gom xong một bộ.";
        diffShuffleOpeners = EditorGUILayout.ToggleLeft(TempContent, diffShuffleOpeners, GUILayout.Width(46f));
        EditorGUILayout.EndHorizontal();

        diffAttempts = Mathf.Clamp(EditorGUILayout.IntField("Số lần thử", diffAttempts), 1, 60);

        EditorGUILayout.Space(4f);
        if (GUILayout.Button(showObstacles ? "▼ Vật cản" : "▶ Vật cản", SirenixGUIStyles.BoldLabelCentered,
                GUILayout.ExpandWidth(true)))
        {
            showObstacles = !showObstacles;
        }

        if (showObstacles) DrawObstacleKnobs();

        EditorGUILayout.Space(2f);
        if (GUILayout.Button(showTricks ? "▼ Trick" : "▶ Trick", SirenixGUIStyles.BoldLabelCentered,
                GUILayout.ExpandWidth(true)))
        {
            showTricks = !showTricks;
        }

        if (showTricks) DrawTrickKnobs();

        int totalFish = diffOrders * GamePlayGlobalConfig.TankGoal;
        GUILayout.Label($"→ {totalFish} cá, khoảng {Mathf.CeilToInt(totalFish / ((diffMinFish + diffMaxFish) * 0.5f))} bóng",
            SirenixGUIStyles.CenteredGreyMiniLabel, GUILayout.ExpandWidth(true));

        EditorGUILayout.Space(4f);
        GUI.enabled = fishIds.Length > 0;
        if (GUILayout.Button("Sinh level theo mức này", GUILayout.Height(28f)))
        {
            pending = () => GenerateByDifficulty(data);
        }

        GUI.enabled = true;
        DrawCarefulToggle();
        if (GUILayout.Button("Đo level đang mở", GUILayout.Height(22f)))
        {
            difficultyReport = MeasureLevel(data.levelData) + MeasureLossSpots(data.levelData);
        }

        if (!string.IsNullOrEmpty(difficultyReport))
        {
            EditorGUILayout.Space(2f);
            GUILayout.Label(difficultyReport, SirenixGUIStyles.MultiLineCenteredLabel);
            DrawLossLevelPicker(data);
        }

        DrawTunePanel(data);

        EditorGUILayout.Space(4f);
        GUILayout.Label((diffPlayerRuler
                            ? $"≈ Người chơi thắng: Thường {PresetTargetWinRate(DifficultyPreset.Thuong, true)}% · " +
                              $"Khó {PresetTargetWinRate(DifficultyPreset.Kho, true)}% · " +
                              $"Siêu khó {PresetTargetWinRate(DifficultyPreset.SieuKho, true)}%\n"
                            : $"Bot cũ thắng: Thường {PresetTargetWinRate(DifficultyPreset.Thuong, false)}% · " +
                              $"Khó {PresetTargetWinRate(DifficultyPreset.Kho, false)}% · " +
                              $"Siêu khó {PresetTargetWinRate(DifficultyPreset.SieuKho, false)}%\n") +
                        $"Ô chờ tối thiểu: 1 · 2 · 3 (trên {holdingSlots})",
            SirenixGUIStyles.MultiLineCenteredLabel);

        EditorGUILayout.EndVertical();
    }

    /// <summary>
    /// Các vật cản đang chạy trong game, xếp theo sức nặng.
    /// </summary>
    private void DrawObstacleKnobs()
    {
        GUILayout.Label("nhập số lượng, rải đều khắp level", SirenixGUIStyles.CenteredGreyMiniLabel,
            GUILayout.ExpandWidth(true));

        TempContent.text = "Số bóng đá";
        TempContent.tooltip = "Chèn thêm bấy nhiêu bóng đá (không có cá, không phá được). Đá nằm lì trên bàn nên giảm " +
                              "hẳn số bóng dùng được - mạnh nhất nhưng cũng dễ làm level không giải được.";
        diffRockCount = Mathf.Clamp(EditorGUILayout.IntField(TempContent, diffRockCount), 0, 40);

        TempContent.text = "Số bóng khói";
        TempContent.tooltip = "Bóng khói (isIce): cá bên trong bị khói che, chỉ bấm được sau khi bóng nổi lên đỉnh vùng chơi. Chặn tạm thời nên " +
                              "an toàn hơn đá, dùng để tạo nhịp chờ.";
        diffIceCount = Mathf.Clamp(EditorGUILayout.IntField(TempContent, diffIceCount), 0, 60);

        TempContent.text = "Số bóng HP";
        TempContent.tooltip = "Vỏ HP che cá bên trong, mỗi lần HOÀN THÀNH một bể thì mọi vỏ đang trên bàn mẻ 1. " +
                              "Ép người chơi giải phần còn lại của bàn trước mới mở được bóng này.";
        diffHpBubbles = Mathf.Clamp(EditorGUILayout.IntField(TempContent, diffHpBubbles), 0, 20);

        if (diffHpBubbles > 0)
        {
            TempContent.text = "Vỏ chặn % đời";
            TempContent.tooltip = "Vỏ HP giữ bóng đóng trong bao nhiêu phần trăm quãng đời nó nằm trên bàn. Tool tự " +
                                  "quy ra số HP theo vị trí bóng, và luôn chừa đủ lượt để vỏ kịp vỡ trước khi hết ván.";
            diffHpLifePercent = Mathf.Clamp(EditorGUILayout.IntField(TempContent, diffHpLifePercent), 10, 90);

            TempContent.text = "Bóng thấy trên màn";
            TempContent.tooltip = $"Màn hình chứa được bao nhiêu bóng cùng lúc (bàn có {LevelBands.VisibleBubbles}). " +
                                  "Máu vỏ bị trừ lúc bóng còn khuất được cộng bù, để lúc hiện lên vỏ vẫn còn đủ máu.";
            diffScreenBubbles = Mathf.Clamp(EditorGUILayout.IntField(TempContent, diffScreenBubbles), 1,
                LevelBands.VisibleBubbles);
        }

        TempContent.text = "Số cá ẩn";
        TempContent.tooltip = "Cá ẩn không cho biết loài cho tới khi bị lấy đi. Mỗi bóng chỉ nhận một con ẩn và luôn " +
                              "chừa ít nhất một con lộ, vì bóng toàn cá ẩn thì game tự lộ hết.";
        diffHiddenCount = Mathf.Clamp(EditorGUILayout.IntField(TempContent, diffHiddenCount), 0, 60);

        TempContent.text = "Số bóng spawner";
        TempContent.tooltip = "Bóng có hàng cá chờ: lấy một con ra thì một con trong hàng thay vào, hàng cạn mới nổ. " +
                              "Giữ chỗ trên bàn lâu hơn và giấu cá sắp ra. Cá spawner lấy từ tổng cá của level, không cộng thêm.";
        diffSpawnerCount = Mathf.Clamp(EditorGUILayout.IntField(TempContent, diffSpawnerCount), 0, 20);

        if (diffSpawnerCount > 0)
        {
            TempContent.text = "Cá mỗi spawner";
            TempContent.tooltip = "Số cá trong hàng chờ của mỗi bóng spawner.";
            diffSpawnerFish = Mathf.Clamp(EditorGUILayout.IntField(TempContent, diffSpawnerFish), 1, 12);
        }

        TempContent.text = "Số bóng nhiều lớp";
        TempContent.tooltip = "Bóng nhiều lớp: lớp ngoài hết cá thì lớp trong mới lộ. Giấu cá lớp trong và giữ chỗ trên " +
                              "bàn lâu hơn. Lớp sau không nhiều cá hơn lớp trước. Cá lớp trong lấy từ tổng cá, không cộng thêm.";
        diffLayerBubbles = Mathf.Clamp(EditorGUILayout.IntField(TempContent, diffLayerBubbles), 0, 20);

        if (diffLayerBubbles > 0)
        {
            TempContent.text = "Số lớp mỗi bóng";
            TempContent.tooltip = "Tính cả lớp ngoài. 2 = một lớp ẩn bên trong.";
            diffLayerCount = Mathf.Clamp(EditorGUILayout.IntField(TempContent, diffLayerCount), 2, MaxLayerCount);
        }

        TempContent.text = "Số bóng cá link";
        TempContent.tooltip = "Cá link (bundle): bấm một con là cả đàn bay cùng lúc, không đủ chỗ cho cả đàn thì không " +
                              "bấm được. Đàn lẫn loài đổ nhiều con lên ô chờ một lúc - phạt nặng.";
        diffBundleCount = Mathf.Clamp(EditorGUILayout.IntField(TempContent, diffBundleCount), 0, 20);

        if (diffBundleCount > 0)
        {
            TempContent.text = "Cỡ đàn tối đa";
            TempContent.tooltip = "Chỉ bóng có từ 2 tới bấy nhiêu cá mới thành cá link. Đàn 4-5 con lẫn loài trên 5 ô " +
                                  "chờ gần như khoá bàn.";
            diffBundleMaxFish = Mathf.Clamp(EditorGUILayout.IntField(TempContent, diffBundleMaxFish), 2, 8);

            TempContent.text = "Đàn cùng loài";
            TempContent.tooltip = "Đổi cá cho mỗi đàn thuần một loài: khớp bể là một nước ăn đậm - bóng thưởng thay vì bóng phạt.";
            diffBundleSameSpecies = EditorGUILayout.Toggle(TempContent, diffBundleSameSpecies);
        }

        TempContent.text = "Số cặp khoá - chìa";
        TempContent.tooltip = "Ổ khoá: cá bên trong chỉ bấm được sau khi bóng chìa của nó nổ hết cá. Chìa luôn đứng " +
                              "trước ổ trên bàn. Mỗi cặp dùng 2 bóng thường (không băng, HP, spawner, nhiều lớp, cá link).";
        diffLockPairs = Mathf.Clamp(EditorGUILayout.IntField(TempContent, diffLockPairs), 0, 10);

        if (diffLockPairs > 0)
        {
            TempContent.text = "Chìa trước ổ tối đa";
            TempContent.tooltip = "Bóng chìa đứng trước ổ khoá từ 1 tới bấy nhiêu bóng. Càng xa thì ổ càng phải chờ lâu.";
            diffLockGap = Mathf.Clamp(EditorGUILayout.IntField(TempContent, diffLockGap), 1, 12);
        }
    }

    private void ApplyDifficultyPreset()
    {
        if (difficultyPreset == DifficultyPreset.De)
        {
            ApplyEasyTemplate();
            return;
        }

        switch (difficultyPreset)
        {
            case DifficultyPreset.Thuong:
                diffOrders = 41;
                diffSpecies = 14;
                diffTargetHolding = 1;
                diffGreedyMax = 100;
                diffSpreadPercent = 40;
                diffSizeVariety = 30;
                diffPureOpeners = 1;
                diffRockCount = 0;
                diffIceCount = 3;
                diffHpBubbles = 0;
                diffHiddenCount = 0;
                diffSpawnerCount = 0;
                diffTrickHideThird = 0;
                diffTrickBlockUrgent = 0;
                diffTrickSpawnerJunk = false;
                diffTrickMidChoke = false;
                diffTrickBait = 0;
                diffTrickDoubleOrder = 0;
                diffTrickKeyJunk = false;
                diffTrickBundleLate = false;
                break;

            case DifficultyPreset.Kho:
                diffOrders = 54;
                diffSpecies = 12;
                diffTargetHolding = 2;
                diffGreedyMax = 90;
                diffSpreadPercent = 70;
                diffSizeVariety = 45;
                diffPureOpeners = 0;
                diffRockCount = 3;
                diffIceCount = 8;
                diffHpBubbles = 2;
                diffHpLifePercent = 50;
                diffHiddenCount = 6;
                diffSpawnerCount = 2;
                diffSpawnerFish = 3;
                diffTrickHideThird = 2;
                diffTrickBlockUrgent = 2;
                diffTrickSpawnerJunk = true;
                diffTrickMidChoke = false;
                diffTrickBait = 2;
                diffTrickDoubleOrder = 1;
                diffTrickKeyJunk = true;
                diffTrickBundleLate = true;
                break;

            default:
                diffOrders = 61;
                diffSpecies = 14;
                diffTargetHolding = 3;
                diffGreedyMax = 80;
                diffSpreadPercent = 85;
                diffSizeVariety = 60;
                diffPureOpeners = 0;
                diffRockCount = 6;
                diffIceCount = 12;
                diffHpBubbles = 4;
                diffHpLifePercent = 65;
                diffHiddenCount = 12;
                diffSpawnerCount = 3;
                diffSpawnerFish = 3;
                diffTrickHideThird = 4;
                diffTrickBlockUrgent = 3;
                diffTrickSpawnerJunk = true;
                diffTrickMidChoke = true;
                diffTrickBait = 3;
                diffTrickDoubleOrder = 2;
                diffTrickKeyJunk = true;
                diffTrickBundleLate = true;
                break;
        }

        diffTargetWinRate = PresetTargetWinRate(difficultyPreset, diffPlayerRuler);
        diffMinFish = 2;
        diffMaxFish = 5;
        diffTargetHolding = Mathf.Clamp(diffTargetHolding, 0, LevelBands.DefaultHoldingSlots);
        diffSpecies = Mathf.Clamp(diffSpecies, 1, Mathf.Max(1, fishIds.Length));
    }

    /// <summary>Mục tiêu (%) của mỗi mức theo thước đang chọn: người chơi 90/50/20, bot cũ 25/3/1.</summary>
    private static int PresetTargetWinRate(DifficultyPreset preset, bool playerRuler)
    {
        float rate = preset switch
        {
            DifficultyPreset.Kho => playerRuler ? LevelHumanBot.HardTargetWinRate : OldRulerHardTarget,
            DifficultyPreset.SieuKho => playerRuler ? LevelHumanBot.SuperHardTargetWinRate : OldRulerSuperHardTarget,
            _ => playerRuler ? LevelHumanBot.NormalTargetWinRate : OldRulerNormalTarget
        };
        return Mathf.RoundToInt(rate * 100f);
    }

    /// <summary>
    /// Độ lệch (điểm %) được nhận quanh mục tiêu: 30% của mục tiêu, trong 2..WinRateAcceptPoints. Cố định ±5 thì mục
    /// tiêu 5% nhận luôn seed 10% - đúng khoảng người thiết kế thấy khác hẳn nhau.
    /// </summary>
    private static int AcceptPoints(int targetPercent) =>
        Mathf.Clamp(Mathf.RoundToInt(targetPercent * 0.3f), 2, WinRateAcceptPoints);

    /// <summary>
    /// Tỉ lệ thắng của một seed theo thước đang chọn. Thước người chơi đo nhanh QuickPlayouts ván trước; chỉ seed lọt gần
    /// mục tiêu mới đo lại đủ ván (thấp thì 500) - seed lệch xa thì số nhanh là đủ để biết hướng dò.
    /// </summary>
    private float MeasureGenWinRate(LevelData probe, float targetWin, float accept)
    {
        if (!diffPlayerRuler) return LevelSolver.EstimateWinRate(probe);

        float quick = LevelHumanBot.EstimateWinRate(probe, PlayerMistakeRate, QuickPlayouts);
        if (Mathf.Abs(quick - targetWin) > accept * 2f + 0.03f) return quick;
        return LevelHumanBot.EstimateWinRate(probe, PlayerMistakeRate, LevelHumanBot.PlayoutsFor(targetWin));
    }

    /// <summary>
    /// Sinh và tự dò số bậc đẩy đơn theo TỈ LỆ THẮNG của thước đang chọn (MeasureGenWinRate):
    /// - Seed không giải được: đẩy quá tay, lùi lại.
    /// - Giải được nhưng dùng ít ô chờ hơn sàn: dễ về cấu trúc, đẩy mạnh thêm.
    /// - Bot thắng cao hơn mục tiêu: còn dễ, đẩy mạnh thêm. Thấp hơn: lùi lại.
    /// Bước đẩy tỉ lệ với độ lệch tỉ lệ thắng nên lệch xa thì nhảy xa, gần rồi thì nhích từng bậc.
    /// Giữ seed có tỉ lệ thắng gần mục tiêu nhất để hết lượt vẫn có cái dùng.
    /// </summary>
    private void GenerateByDifficulty(LevelDesignData data)
    {
        if (fishIds.Length == 0)
        {
            SetStatus(false, "FishModelGlobalConfig trống - không có loài cá nào để sinh");
            return;
        }

        LevelData level = data.levelData;
        int tankGoal = GamePlayGlobalConfig.TankGoal;
        int species = Mathf.Clamp(diffSpecies, 1, fishIds.Length);
        int orders = Mathf.Max(1, diffOrders);
        int attempts = Mathf.Clamp(diffAttempts, 1, 60);
        int floor = Mathf.Clamp(diffTargetHolding, 0, LevelBands.DefaultHoldingSlots);
        float targetWin = Mathf.Clamp(diffTargetWinRate, 1, 100) / 100f;
        int acceptPoints = AcceptPoints(diffTargetWinRate);
        float accept = acceptPoints / 100f;
        string ruler = diffPlayerRuler ? "≈ người chơi thắng" : "bot cũ thắng";
        float greedyMax = Mathf.Clamp(diffGreedyMax, 1, 100) / 100f;

        // Mức càng khó thì xuất phát càng mạnh, đỡ tốn mấy lượt đầu leo dốc.
        int distance = Mathf.Clamp(Mathf.RoundToInt((1f - targetWin) * 30f), MinAdvanceDistance, MaxAdvanceDistance);

        // Seed tốt nhất đạt sàn ô chờ, xếp theo độ lệch tỉ lệ thắng.
        float bestGap = float.MaxValue;
        float bestWin = 0f;
        float bestGreedy = -1f;
        int bestHolding = 0;
        int bestDistance = 0;
        int[] bestQueue = null;
        SoftBubbleData[] bestBubbles = null;

        // Bật "Bắt buộc có bẫy": seed đạt sàn ô chờ nhưng bot chơi đúng vẫn thắng quá ngưỡng. Chỉ dùng khi hết lượt
        // mà không seed nào có bẫy.
        float plainGap = float.MaxValue;
        float plainWin = 0f;
        float plainGreedy = -1f;
        int plainHolding = 0;
        int plainDistance = 0;
        int[] plainQueue = null;
        SoftBubbleData[] plainBubbles = null;

        // Seed giải được nhưng chưa đạt sàn ô chờ: chỉ dùng khi hết lượt mà không có seed nào đạt sàn.
        float looseGap = float.MaxValue;
        float looseWin = 0f;
        int looseHolding = 0;
        int looseDistance = 0;
        int[] looseQueue = null;
        SoftBubbleData[] looseBubbles = null;

        var trickLogs = new Dictionary<SoftBubbleData[], string>();
        bool cancelled = false;
        bool hitCeiling = false;
        int used = 0;

        try
        {
            for (used = 1; used <= attempts; used++)
            {
                if (EditorUtility.DisplayCancelableProgressBar("Sinh level theo độ khó",
                        $"Lượt {used}/{attempts} · đẩy đơn {distance} bậc", (float)(used - 1) / attempts))
                {
                    cancelled = true;
                    break;
                }

                SoftBubbleData[] bubbles = BuildBubbles(species, orders, tankGoal, out int[] natural);

                // Rắc vật cản TRƯỚC khi chạy solver: rắc sau thì con số solver báo không còn đúng với level đã lưu.
                bubbles = ApplyObstacles(bubbles);
                int[] queue = AdvanceOrders(natural, distance);

                // Trick cài sau khi đã có hàng đơn (chấm "gấp" / "rác" theo hàng đơn thật), trước solver.
                trickLogs[bubbles] = ApplyTricks(bubbles, queue, tankGoal);

                LevelData probe = LevelData.Create(level.Level, level.Difficulty, queue, bubbles);
                LevelSolver.Result result = LevelSolver.Solve(probe, CheckSolverMaxNodes);

                if (!result.Solved)
                {
                    distance = Mathf.Max(MinAdvanceDistance, distance - 3);
                    continue;
                }

                float win = MeasureGenWinRate(probe, targetWin, accept);
                float gap = Mathf.Abs(win - targetWin);

                if (result.MaxHoldingUsed < floor)
                {
                    if (gap < looseGap)
                    {
                        looseGap = gap;
                        looseWin = win;
                        looseHolding = result.MaxHoldingUsed;
                        looseDistance = distance;
                        looseQueue = queue;
                        looseBubbles = bubbles;
                    }

                    if (distance >= MaxAdvanceDistance) hitCeiling = true;
                    distance = Mathf.Min(MaxAdvanceDistance, distance + 3);
                    continue;
                }

                float greedy = -1f;
                if (diffRequireTrap)
                {
                    greedy = LevelHumanBot.EstimateWinRate(probe, 0f, TrapPlayouts);
                    if (greedy > greedyMax)
                    {
                        // Không có bẫy: bot thua chỉ vì bấm bừa. Bẫy sinh ra từ lệch hàng đơn nên đẩy mạnh thêm,
                        // kể cả khi % thắng đã lọt mục tiêu.
                        if (gap < plainGap)
                        {
                            plainGap = gap;
                            plainWin = win;
                            plainGreedy = greedy;
                            plainHolding = result.MaxHoldingUsed;
                            plainDistance = distance;
                            plainQueue = queue;
                            plainBubbles = bubbles;
                        }

                        if (distance >= MaxAdvanceDistance) hitCeiling = true;
                        distance = Mathf.Min(MaxAdvanceDistance, distance + 2);
                        continue;
                    }
                }

                if (gap < bestGap)
                {
                    bestGap = gap;
                    bestWin = win;
                    bestGreedy = greedy;
                    bestHolding = result.MaxHoldingUsed;
                    bestDistance = distance;
                    bestQueue = queue;
                    bestBubbles = bubbles;
                }

                if (gap <= accept) break;

                // Lệch 10 điểm % thì nhảy 2 bậc, lệch 30 điểm thì nhảy 6 bậc.
                int step = Mathf.Clamp(Mathf.RoundToInt(gap * 20f), 1, 6);
                if (win > targetWin)
                {
                    if (distance >= MaxAdvanceDistance) hitCeiling = true;
                    distance = Mathf.Min(MaxAdvanceDistance, distance + step);
                }
                else
                {
                    distance = Mathf.Max(MinAdvanceDistance, distance - step);
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        bool noTrap = false;
        if (bestBubbles == null && plainBubbles != null)
        {
            noTrap = true;
            bestGap = plainGap;
            bestWin = plainWin;
            bestGreedy = plainGreedy;
            bestHolding = plainHolding;
            bestDistance = plainDistance;
            bestQueue = plainQueue;
            bestBubbles = plainBubbles;
        }

        bool belowFloor = false;
        if (bestBubbles == null && looseBubbles != null)
        {
            belowFloor = true;
            bestGap = looseGap;
            bestWin = looseWin;
            bestGreedy = -1f;
            bestHolding = looseHolding;
            bestDistance = looseDistance;
            bestQueue = looseQueue;
            bestBubbles = looseBubbles;
        }

        if (bestBubbles == null)
        {
            SetStatus(false, cancelled
                ? "Đã huỷ, chưa sinh được seed nào - level cũ giữ nguyên"
                : $"Thử {Mathf.Max(0, used - 1)} lượt không ra seed giải được. Hạ số đơn, bớt vật cản hoặc nâng % thắng.");
            return;
        }

        int[] chosenQueue = bestQueue;
        SoftBubbleData[] chosenBubbles = bestBubbles;
        Mutate(data, "Sinh level theo độ khó", () =>
        {
            level.EditorSetOrderQueue(chosenQueue);
            level.EditorSetBubbles(chosenBubbles);
            data.NormalizeBubbleIds();
        });

        ResetSelection();
        difficultyReport = MeasureLevel(level);

        int tried = Mathf.Min(used, attempts);
        string head = $"Mức {difficultyPreset}: {level.BubbleCount} bóng, {orders} đơn, {species} loài";
        string line = $"{head}\nSau {tried} lượt: {ruler} {bestWin:P0} (mục tiêu {targetWin:P0} ±{acceptPoints}%), " +
                      $"ô chờ {bestHolding}/{LevelBands.DefaultHoldingSlots} (sàn {floor}), đẩy đơn {bestDistance} bậc";
        if (bestGreedy >= 0f) line += $"\nBot như người chơi đúng thắng {bestGreedy:P0} (tối đa {greedyMax:P0})";
        if (trickLogs.TryGetValue(bestBubbles, out string trickLine) && !string.IsNullOrEmpty(trickLine))
        {
            line += "\n" + trickLine;
        }

        if (belowFloor)
        {
            SetStatus(true, $"{line}\nKhông seed nào đạt sàn ô chờ - bàn khó vì hên xui hơn là vì tính nước. " +
                            "Tăng % bộ rải hoặc số đơn rồi sinh lại.", MessageType.Warning);
            return;
        }

        if (noTrap)
        {
            SetStatus(true, $"{line}\nKhông seed nào có bẫy - bot như người chơi đúng vẫn thắng quá {greedyMax:P0}. Tăng ô chờ " +
                            "tối thiểu, tăng % bộ rải, bỏ bóng thuần đầu hoặc tăng số lần thử.", MessageType.Warning);
            return;
        }

        if (bestGap <= accept)
        {
            SetStatus(true, line);
            return;
        }

        string advice = bestWin > targetWin
            ? hitCeiling
                ? "Đẩy đơn đã kịch trần mà bot vẫn thắng nhiều - phải khó lên bằng nút khác: tăng số đơn, thêm đá, " +
                  "tăng % bộ rải."
                : "Bot vẫn thắng nhiều hơn mục tiêu - tăng số lần thử, hoặc tăng số đơn / thêm đá."
            : "Bot thắng ít hơn mục tiêu - bớt vật cản hoặc giảm số đơn.";
        SetStatus(true, $"{line}\nChưa lọt ±{acceptPoints}% quanh mục tiêu. {advice}", MessageType.Warning);
    }

    /// <summary>Kế hoạch một bóng trước khi rót cá: cỡ lớp ngoài, số cá hàng chờ spawner, cỡ các lớp trong.</summary>
    private sealed class GenSlot
    {
        public int Size;
        public int Spawner;
        public int[] Layers;
    }

    /// <summary>
    /// Dựng bóng: mỗi đơn là một bộ TankGoal con cùng loài đặt sát nhau trong chuỗi cá, rồi cắt chuỗi theo cỡ bóng.
    /// Hàng đơn trả về ở natural là hàng "lệch 0" - suy thẳng từ thứ tự cá gom đủ, tức dễ nhất có thể.
    ///
    /// Cá cắt từ chuỗi theo đúng thứ tự người chơi gặp: lớp ngoài, rồi hàng chờ spawner, rồi các lớp trong của bóng
    /// nhiều lớp - nên hàng đơn tự nhiên vẫn khớp. Cá spawner và cá lớp trong đều trừ vào tổng cá, không cộng thêm.
    /// </summary>
    private SoftBubbleData[] BuildBubbles(int species, int orders, int tankGoal, out int[] natural)
    {
        int totalFish = orders * tankGoal;

        // Cá spawner cắt ra từ chính tổng cá nên phép cân với hàng đơn không đổi. Chừa ít nhất nửa số cá cho bóng
        // thường, kẻo level toàn spawner.
        int reserved = Mathf.Min(diffSpawnerCount * diffSpawnerFish, totalFish / 2);
        List<int[]> layerPlan = PlanLayers(totalFish - reserved);
        int layerFish = 0;
        foreach (int[] layers in layerPlan)
        {
            foreach (int n in layers) layerFish += n;
        }

        List<int> sizes = BuildSizes(totalFish - reserved - layerFish);
        int[] spawnerFish = PlanSpawners(sizes.Count, reserved);
        if (spawnerFish == null && reserved > 0) sizes = BuildSizes(totalFish - layerFish);

        var slots = new List<GenSlot>(sizes.Count + layerPlan.Count);
        for (int i = 0; i < sizes.Count; i++)
        {
            slots.Add(new GenSlot { Size = sizes[i], Spawner = spawnerFish != null ? spawnerFish[i] : 0 });
        }

        // Bóng nhiều lớp chèn vào các vị trí rải đều trên danh sách CUỐI (vị trí tăng dần nên chèn lần lượt là
        // đúng chỗ). Level quá ngắn không đủ chỗ thì các lớp thừa thành bóng thường ở cuối, tổng cá vẫn giữ nguyên.
        List<int> layerAt = SpreadPositions(slots.Count + layerPlan.Count, layerPlan.Count, OpeningSafeBubbles);
        for (int j = 0; j < layerPlan.Count; j++)
        {
            int[] plan = layerPlan[j];
            if (j < layerAt.Count)
            {
                var inner = new int[plan.Length - 1];
                System.Array.Copy(plan, 1, inner, 0, inner.Length);
                slots.Insert(Mathf.Min(layerAt[j], slots.Count), new GenSlot { Size = plan[0], Layers = inner });
                continue;
            }

            foreach (int n in plan) slots.Add(new GenSlot { Size = n });
        }

        var sequence = new int[totalFish];
        var used = new bool[totalFish];

        var pool = new List<int>(fishIds);
        Shuffle(pool);
        pool.RemoveRange(species, pool.Count - species);

        var sets = new List<int>(orders);
        for (int i = 0; i < orders; i++) sets.Add(pool[i % pool.Count]);
        Shuffle(sets);

        // Khoảng cách của bộ rải đo bằng số cá lọt trong cửa sổ đang hiện: nửa cửa sổ là đủ để hai con cùng loài
        // hiếm khi thấy cùng lúc, nhưng chưa xa tới mức phải ôm ô chờ quá lâu.
        int visibleBubbles = Mathf.Min(LevelBands.VisibleBubbles, slots.Count);
        int windowFish = Mathf.Max(1, Mathf.RoundToInt((float)totalFish * visibleBubbles / slots.Count));
        int gap = Mathf.Max(2, windowFish / 2);
        int spreadSets = Mathf.RoundToInt(sets.Count * diffSpreadPercent / 100f);

        for (int idx = 0; idx < sets.Count; idx++)
        {
            int fishId = sets[idx];
            int at = Random.Range(0, totalFish);
            for (int k = 0; k < tankGoal; k++)
            {
                int wanted = idx < spreadSets ? (at + k * gap) % totalFish : at + k;
                int slot = TakeFreeSlot(used, wanted);
                if (slot >= 0) sequence[slot] = fishId;
            }
        }

        var bubbles = new SoftBubbleData[slots.Count];
        int cursor = 0;

        FishData[] Take(int count)
        {
            var fish = new FishData[count];
            for (int k = 0; k < count; k++) fish[k] = new FishData { FishId = sequence[cursor + k] };
            cursor += count;
            return fish;
        }

        for (int i = 0; i < slots.Count; i++)
        {
            GenSlot plan = slots[i];
            var ids = new int[plan.Size];
            for (int k = 0; k < plan.Size; k++) ids[k] = sequence[cursor + k];
            cursor += plan.Size;
            bubbles[i] = SoftBubbleData.CreateWithRandomRadius(ids);

            // Hàng chờ spawner lấy luôn đoạn cá kế tiếp trong chuỗi: chúng ra sau cá hiện của bóng này, nên vẫn
            // giữ gần đúng thứ tự cá xuất hiện mà hàng đơn tự nhiên dựa vào.
            if (plan.Spawner > 0)
            {
                bubbles[i].isSpawner = true;
                bubbles[i].fishOnSpawner = Take(plan.Spawner);
            }

            // Lớp trong cũng vậy: lộ ra sau khi lớp ngoài hết cá, nên lấy đoạn kế tiếp theo đúng thứ tự lộ.
            if (plan.Layers != null && plan.Layers.Length > 0)
            {
                bubbles[i].isMultiLayer = true;
                bubbles[i].layers = new BubbleLayerData[plan.Layers.Length];
                for (int k = 0; k < plan.Layers.Length; k++)
                {
                    bubbles[i].layers[k] = new BubbleLayerData { index = k, FishDataArray = Take(plan.Layers[k]) };
                }
            }
        }

        ForcePureOpeners(bubbles);
        if (diffShuffleOpeners) ShuffleOpeners(bubbles);
        MarkBundles(bubbles);
        ApplyGenRadius(bubbles);
        natural = BuildQueueFromBubbles(bubbles, tankGoal);
        return bubbles;
    }

    /// <summary>
    /// Cỡ từng lớp của mỗi bóng nhiều lớp (phần tử 0 = lớp ngoài). Lớp sau không nhiều cá hơn lớp trước - luật của
    /// LevelData.Validate - và chỉ nhỏ hơn tối đa 1 con để bóng không teo quá nhanh. Tổng cá các bóng nhiều lớp không
    /// vượt nửa số cá còn lại, kẻo level toàn bóng nhiều lớp.
    /// </summary>
    private List<int[]> PlanLayers(int fishAvailable)
    {
        var plan = new List<int[]>();
        if (diffLayerBubbles <= 0) return plan;

        int layerCount = Mathf.Clamp(diffLayerCount, 2, MaxLayerCount);
        int budget = fishAvailable / 2;
        int spent = 0;
        for (int b = 0; b < diffLayerBubbles; b++)
        {
            var sizes = new int[layerCount];
            sizes[0] = Random.Range(Mathf.Max(2, diffMinFish), Mathf.Max(2, diffMaxFish) + 1);
            int sum = sizes[0];
            for (int k = 1; k < layerCount; k++)
            {
                sizes[k] = Random.Range(Mathf.Max(1, sizes[k - 1] - 1), sizes[k - 1] + 1);
                sum += sizes[k];
            }

            if (spent + sum > budget) break;
            plan.Add(sizes);
            spent += sum;
        }

        return plan;
    }

    /// <summary>
    /// Bật cá link (isBundleBubble - runtime FishSchoolBubble: bấm một con là cả đàn bay) cho diffBundleCount bóng rải
    /// đều. Chỉ chọn bóng thường 2..diffBundleMaxFish cá: không spawner / nhiều lớp (Validate cấm dùng chung), và
    /// không đàn to - đàn 4-5 con lẫn loài đổ một lúc lên 5 ô chờ gần như khoá bàn. Bỏ qua đoạn mở màn và bóng thuần
    /// đầu. diffBundleSameSpecies: đổi cá cho đàn thuần một loài - bóng thưởng thay vì bóng phạt. Chạy trước khi dựng
    /// hàng đơn nên hàng đơn tự khớp.
    /// </summary>
    private void MarkBundles(SoftBubbleData[] bubbles)
    {
        if (diffBundleCount <= 0) return;

        int skip = Mathf.Max(OpeningSafeBubbles, diffPureOpeners);
        int maxFish = Mathf.Max(2, diffBundleMaxFish);
        var candidates = new List<int>();
        for (int i = skip; i < bubbles.Length; i++)
        {
            SoftBubbleData b = bubbles[i];
            if (b == null || b.isRock || b.isSpawner || b.isMultiLayer) continue;
            if (b.FishCount >= 2 && b.FishCount <= maxFish) candidates.Add(i);
        }

        foreach (int k in SpreadPositions(candidates.Count, diffBundleCount, 0))
        {
            if (k < 0 || k >= candidates.Count) continue;

            int index = candidates[k];
            bubbles[index].isBundleBubble = true;
            if (!diffBundleSameSpecies) continue;

            // Duyệt bóng từ đầu tới cuối và chỉ đổi với bóng SAU: đàn đã thuần phía trước không bị đổi lại.
            FishData[] fish = bubbles[index].FishDataArray;
            int keep = fish[0].FishId;
            for (int f = 1; f < fish.Length; f++)
            {
                if (fish[f].FishId == keep) continue;
                if (!SwapFishWithLater(bubbles, index, f, keep)) break;
            }
        }
    }

    /// <summary>
    /// Rắc vật cản lên bộ bóng vừa dựng. Thứ tự: chèn đá (đổi số bóng nên làm trước), rồi băng / vỏ HP / cá ẩn trên
    /// các bóng có cá.
    ///
    /// Đá được CHÈN THÊM chứ không biến bóng có cá thành đá: làm vậy thì phải rải lại cá, và tổng cá sẽ lệch với hàng
    /// đơn. Chèn thêm giữ nguyên phép cân, đồng thời đúng cái ta cần - ít bóng dùng được hơn trên bàn.
    /// </summary>
    private SoftBubbleData[] ApplyObstacles(SoftBubbleData[] bubbles)
    {
        var list = new List<SoftBubbleData>(bubbles);

        // Đá chèn trước vì nó đổi số bóng. Vị trí chia đều theo đoạn, không bốc tự do - bốc tự do hay dồn cục.
        int rocks = Mathf.Min(diffRockCount, Mathf.Max(0, list.Count));
        foreach (int at in SpreadPositions(list.Count, rocks, OpeningSafeBubbles))
        {
            var rock = new SoftBubbleData { isRock = true, FishDataArray = System.Array.Empty<FishData>() };

            // Bỏ bước này thì Radius = 0 và lúc spawn bóng bị phóng về 0, nhìn như không có đá.
            rock.Radius = RockRadius();
            list.Insert(Mathf.Clamp(at, 0, list.Count), rock);
        }

        // Chỉ số của các bóng có cá, giữ nguyên thứ tự trên bàn để rải đều theo chiều dài level.
        var withFish = new List<int>();
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] != null && !list[i].isRock && list[i].FishCount > 0) withFish.Add(i);
        }

        var iced = new HashSet<int>();
        foreach (int k in SpreadPositions(withFish.Count, diffIceCount, OpeningSafeBubbles))
        {
            if (k < 0 || k >= withFish.Count) continue;

            list[withFish[k]].isIce = true;
            iced.Add(k);
        }

        // Vỏ HP tránh chồng lên bóng băng: chồng cả hai thì bóng đó bị khoá hai lớp, rất dễ thành bàn chết.
        foreach (int k in SpreadPositions(withFish.Count, diffHpBubbles, OpeningSafeBubbles))
        {
            int slot = NearestFree(withFish.Count, k, iced);
            if (slot < 0) break;

            int index = withFish[slot];
            SoftBubbleData bubble = list[index];
            bubble.isBubbleHp = true;
            bubble.bubbleHp = ShellHp(list, index);
            iced.Add(slot);
        }

        // Cá ẩn: mỗi bóng đúng một con, các bóng cũng rải đều - trước đây nhét dồn vào mấy bóng đầu danh sách.
        foreach (int k in SpreadPositions(withFish.Count, diffHiddenCount, OpeningSafeBubbles))
        {
            if (k < 0 || k >= withFish.Count) continue;

            FishData[] fish = list[withFish[k]].FishDataArray;
            if (fish == null || fish.Length < 2) continue;

            int at = Random.Range(0, fish.Length);
            if (fish[at] == null || fish[at].isHidden) continue;

            // Chừa lại ít nhất một con lộ.
            int visible = 0;
            foreach (FishData one in fish)
            {
                if (one != null && !one.isHidden) visible++;
            }

            if (visible <= 1) continue;

            fish[at].isHidden = true;
        }

        PlaceLockKeys(list);
        return list.ToArray();
    }

    /// <summary>
    /// Đặt diffLockPairs cặp khoá - chìa lên danh sách bóng CUỐI (sau khi chèn đá), vì runtime khớp chìa với ổ theo
    /// vị trí trong mảng: keyId = index của ổ + 1 (BoardModel.UnlockKeyLocks; NormalizeBubbleIds đặt softBubbleId =
    /// index + 1 nên Check Level cũng thấy khớp). Ổ rải đều theo chiều dài level, bỏ đoạn mở màn; chìa là một bóng
    /// thường đứng trước ổ 1..diffLockGap bóng. Cả hai chỉ lấy bóng "trơn": vật cản chồng lên ổ / chìa (băng, HP,
    /// spawner, nhiều lớp, cá link) dễ thành khoá chết. Solver kiểm lại cả level sau đó.
    /// </summary>
    private void PlaceLockKeys(List<SoftBubbleData> list)
    {
        if (diffLockPairs <= 0) return;

        bool Plain(SoftBubbleData b) =>
            b != null && !b.isRock && !b.isIce && !b.isBubbleHp && !b.isSpawner && !b.isMultiLayer &&
            !b.isBundleBubble && !b.isLock && !b.isKey && !b.isOrderBubble && b.FishCount > 0;

        int gap = Mathf.Max(1, diffLockGap);
        int skip = OpeningSafeBubbles + 1;
        foreach (int at in SpreadPositions(list.Count, diffLockPairs, skip))
        {
            // Ổ: bóng trơn gần vị trí rải nhất, chìa: bóng trơn gần nhất trong gap bóng phía trước ổ.
            int lockIndex = -1;
            for (int step = 0; step < list.Count && lockIndex < 0; step++)
            {
                foreach (int i in new[] { at + step, at - step })
                {
                    if (i < skip || i >= list.Count || !Plain(list[i])) continue;
                    bool hasKey = false;
                    for (int k = i - 1; k >= Mathf.Max(0, i - gap); k--)
                    {
                        if (Plain(list[k])) { hasKey = true; break; }
                    }

                    if (!hasKey) continue;
                    lockIndex = i;
                    break;
                }
            }

            if (lockIndex < 0) continue;

            int keyIndex = -1;
            for (int k = lockIndex - 1; k >= Mathf.Max(0, lockIndex - gap) && keyIndex < 0; k--)
            {
                if (Plain(list[k])) keyIndex = k;
            }

            if (keyIndex < 0) continue;
            list[lockIndex].isLock = true;
            list[keyIndex].isKey = true;
            list[keyIndex].keyId = lockIndex + 1;
        }
    }

    /// <summary>
    /// Số HP cho bóng đứng ở vị trí index, sao cho lúc HIỆN LÊN MÀN vỏ vẫn chặn đúng diffHpLifePercent phần đời bóng.
    ///
    /// Vỏ bị trừ 1 mỗi lần xong một bể, với mọi bóng đã lên bàn (BoardModel.DamageShells) - kể cả bóng còn khuất
    /// ngoài màn. Số lần xong bể thì bằng số cá bị lấy đi chia TankGoal, nên đếm theo CÁ THẬT của các bóng đứng trước
    /// chứ không theo trung bình: phía trước toàn bóng to thì vỏ bị trừ nhanh hơn hẳn.
    /// - diffScreenBubbles bóng ngay trước nó: phải dọn đám này thì nó mới lọt vào màn -> tính là đời "thấy được".
    /// - Các bóng xa hơn, tới hết VisibleBubbles: dọn trong lúc nó đã lên bàn nhưng còn khuất -> máu mất oan, cộng bù.
    /// Đá không bao giờ nổ nên không nhường chỗ, bỏ qua khi đếm.
    ///
    /// Luôn chừa ít nhất một lượt để vỏ chắc chắn vỡ kịp; solver sau đó kiểm lại toàn bộ.
    /// </summary>
    private int ShellHp(List<SoftBubbleData> list, int index)
    {
        int window = Mathf.Max(1, LevelBands.VisibleBubbles);
        int screen = Mathf.Clamp(diffScreenBubbles, 1, window);
        int tankGoal = Mathf.Max(1, GamePlayGlobalConfig.TankGoal);

        int shownFish = 0;
        int hiddenFish = 0;
        int seen = 0;
        for (int i = index - 1; i >= 0 && seen < window; i--)
        {
            SoftBubbleData bubble = list[i];
            if (bubble == null || bubble.isRock) continue;

            int fishInBubble = bubble.TotalFishCount + (bubble.isSpawner ? bubble.fishOnSpawner?.Length ?? 0 : 0);
            if (seen < screen) shownFish += fishInBubble;
            else hiddenFish += fishInBubble;
            seen++;
        }

        float lostOffScreen = (float)hiddenFish / tankGoal;
        float shownLife = (float)shownFish / tankGoal;

        int hp = Mathf.RoundToInt(lostOffScreen + shownLife * diffHpLifePercent / 100f);
        int cap = Mathf.Max(1, Mathf.FloorToInt(lostOffScreen + shownLife) - 1);
        return Mathf.Clamp(hp, 1, cap);
    }

    /// <summary>Số bóng đầu level không đặt vật cản, để bàn kịp chạy trước khi bị chặn.</summary>
    private const int OpeningSafeBubbles = 3;

    /// <summary>
    /// Chia đoạn [skip, count) thành đúng "wanted" khúc bằng nhau rồi lấy ngẫu nhiên một vị trí trong mỗi khúc.
    /// Bốc ngẫu nhiên tự do trên cả dải thì hay dồn cục một chỗ; chia khúc bảo đảm vật cản trải khắp level.
    /// </summary>
    private static List<int> SpreadPositions(int count, int wanted, int skip)
    {
        var result = new List<int>();
        if (wanted <= 0 || count <= 0) return result;

        int from = Mathf.Min(skip, Mathf.Max(0, count - 1));
        int span = count - from;
        if (span <= 0) return result;

        wanted = Mathf.Min(wanted, span);
        float step = (float)span / wanted;
        for (int i = 0; i < wanted; i++)
        {
            int lo = from + Mathf.FloorToInt(i * step);
            int hi = from + Mathf.FloorToInt((i + 1) * step);
            result.Add(Random.Range(lo, Mathf.Max(lo + 1, hi)));
        }

        return result;
    }

    /// <summary>Vị trí trống gần k nhất trong [0, count), bỏ qua những chỗ đã dùng.</summary>
    private static int NearestFree(int count, int k, HashSet<int> used)
    {
        for (int step = 0; step < count; step++)
        {
            for (int dir = 0; dir < 2; dir++)
            {
                int i = dir == 0 ? k - step : k + step;
                if (i < 0 || i >= count || used.Contains(i)) continue;

                return i;
            }
        }

        return -1;
    }

    private static void Shuffle(List<SoftBubbleData> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    /// <summary>
    /// Bán kính cho cột này: mặc định mọi bóng lấy ĐÚNG sàn của số cá bên trong (MinBubbleRadius), nhỏ gọn nhất có thể.
    /// Chỉ diffSizeVariety phần trăm bóng được nới, bốc trong [sàn n cá + BubbleGenMinPadding, sàn n+1 cá +
    /// BubbleGenMaxBonus] - vài quả to hơn hẳn để che bàn và để nhìn cỡ không đoán chắc được số cá.
    /// Gán thẳng chứ không lấy max với bán kính CreateWithRandomRadius đã cho, kẻo bóng thường bị đội lên khỏi sàn.
    /// </summary>
    private void ApplyGenRadius(SoftBubbleData[] bubbles)
    {
        if (GamePlayGlobalConfig.Instance == null) return;

        foreach (SoftBubbleData bubble in bubbles)
        {
            if (bubble == null) continue;

            bubble.Radius = GenRadius(bubble.FishCount);
        }
    }

    private const float RockMinRadius = 1.6f;
    private const float RockMaxRadius = 2.0f;

    /// <summary>
    /// Bán kính đá trong [RockMinRadius, RockMaxRadius], nghiêng về cỡ nhỏ: bình phương một số ngẫu nhiên 0..1 nên
    /// phần lớn rơi quanh 1.6-1.7, chạm 2.0 chỉ chừng 6%. Đá giữ suất trên bàn tới hết level, to quá thì che mất màn.
    /// </summary>
    private static float RockRadius()
    {
        float t = Random.value;
        float radius = Mathf.Lerp(RockMinRadius, RockMaxRadius, t * t);
        return Mathf.Clamp(Mathf.Round(radius * 10f) / 10f, RockMinRadius, RockMaxRadius);
    }

    /// <summary>Bán kính lúc sinh cho một bóng chứa fishCount cá: sàn, hoặc dải nới với xác suất diffSizeVariety.</summary>
    private float GenRadius(int fishCount)
    {
        GamePlayGlobalConfig config = GamePlayGlobalConfig.Instance;
        if (config == null) return 1f;

        float min = config.MinBubbleRadius(fishCount);
        if (Random.Range(0, 100) >= diffSizeVariety) return min;

        float floor = min + Mathf.Max(0f, config.BubbleGenMinPadding);
        float ceiling = Mathf.Max(floor, config.MinBubbleRadius(fishCount + 1) + Mathf.Max(0f, config.BubbleGenMaxBonus));
        return Mathf.Max(floor, Mathf.Round(Random.Range(floor, ceiling) * 10f) / 10f);
    }

    /// <summary>
    /// Đẩy AdvanceShare phần đơn lên sớm hơn distance bậc. Đây là toàn bộ độ khó: bể bị đơn chưa có cá chiếm chỗ nên
    /// cá của các đơn sau phải nằm ô chờ. distance = 0 thì hàng đơn giữ nguyên, level gần như không cần ô chờ.
    /// </summary>
    private static int[] AdvanceOrders(int[] natural, int distance)
    {
        var queue = new List<int>(natural);
        if (distance <= 0 || queue.Count < 2) return queue.ToArray();

        int moves = Mathf.Max(1, Mathf.RoundToInt(queue.Count * AdvanceShare));
        for (int m = 0; m < moves; m++)
        {
            int from = Random.Range(1, queue.Count);
            int to = Mathf.Max(0, from - distance);
            if (to == from) continue;

            int order = queue[from];
            queue.RemoveAt(from);
            queue.Insert(to, order);
        }

        return queue.ToArray();
    }

    /// <summary>
    /// Duyệt cá theo đúng thứ tự bóng sẽ hiện ra; loài nào vừa gom đủ TankGoal con thì nối một đơn của loài đó vào
    /// cuối hàng. Nhờ vậy hàng đơn gốc luôn khớp thứ tự cá xuất hiện.
    /// </summary>
    private static int[] BuildQueueFromBubbles(SoftBubbleData[] bubbles, int tankGoal)
    {
        var counter = new Dictionary<int, int>();
        var queue = new List<int>();

        foreach (SoftBubbleData bubble in bubbles)
        {
            if (bubble == null) continue;

            // Cá hàng chờ spawner ra sau cá hiện của chính bóng đó.
            CountForQueue(bubble.FishDataArray, counter, queue, tankGoal);
            if (bubble.isSpawner && !bubble.isRock) CountForQueue(bubble.fishOnSpawner, counter, queue, tankGoal);

            // Lớp trong lộ sau khi lớp ngoài hết, theo thứ tự lớp.
            if (bubble.isMultiLayer && bubble.layers != null)
            {
                foreach (BubbleLayerData layer in bubble.layers) CountForQueue(layer?.FishDataArray, counter, queue, tankGoal);
            }
        }

        return queue.ToArray();
    }

    private static void CountForQueue(FishData[] fish, Dictionary<int, int> counter, List<int> queue, int tankGoal)
    {
        if (fish == null) return;

        foreach (FishData one in fish)
        {
            if (one == null) continue;

            int id = one.FishId;
            int count = counter.TryGetValue(id, out int c) ? c + 1 : 1;
            counter[id] = count;
            if (count % tankGoal == 0) queue.Add(id);
        }
    }

    /// <summary>Cỡ từng bóng, bốc đều trong [min, max] và cộng lại đúng bằng tổng số cá.</summary>
    private List<int> BuildSizes(int totalFish)
    {
        var sizes = new List<int>();
        int remaining = totalFish;
        while (remaining > 0)
        {
            int size = Mathf.Min(Random.Range(diffMinFish, diffMaxFish + 1), remaining);
            int left = remaining - size;
            if (left > 0 && left < diffMinFish) size = size + left <= diffMaxFish ? size + left : remaining - diffMinFish;
            sizes.Add(size);
            remaining -= size;
        }

        return sizes;
    }

    /// <summary>Ô trống gần vị trí mong muốn nhất trong chuỗi; -1 khi chuỗi đã đầy.</summary>
    private static int TakeFreeSlot(bool[] used, int wanted)
    {
        for (int step = 0; step < used.Length; step++)
        {
            for (int dir = 0; dir < 2; dir++)
            {
                int i = dir == 0 ? wanted - step : wanted + step;
                if (i < 0 || i >= used.Length || used[i]) continue;

                used[i] = true;
                return i;
            }
        }

        return -1;
    }

    private static void Shuffle(List<int> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    /// <summary>Ép mấy bóng đầu thành bóng thuần một loài, chỉ đổi ngang cá nên tổng số cá từng loài không đổi.</summary>
    private void ForcePureOpeners(SoftBubbleData[] bubbles)
    {
        int openers = Mathf.Min(diffPureOpeners, bubbles.Length);
        for (int i = 0; i < openers; i++)
        {
            FishData[] fish = bubbles[i]?.FishDataArray;
            if (fish == null || fish.Length == 0) continue;

            int keep = fish[0].FishId;
            for (int k = 1; k < fish.Length; k++)
            {
                if (fish[k].FishId == keep) continue;
                if (!SwapFishWithLater(bubbles, i, k, keep)) break;
            }
        }
    }

    /// <summary>
    /// Chia reserved con cá spawner cho các bóng spawner rải đều theo chiều dài level (chừa OpeningSafeBubbles bóng
    /// đầu). Trả về số cá hàng chờ của từng bóng, hoặc null khi không có spawner / level quá ngắn để đặt.
    /// </summary>
    private int[] PlanSpawners(int bubbleCount, int reserved)
    {
        if (reserved <= 0 || diffSpawnerCount <= 0) return null;

        List<int> at = SpreadPositions(bubbleCount, diffSpawnerCount, OpeningSafeBubbles);
        if (at.Count == 0) return null;

        var plan = new int[bubbleCount];
        for (int i = 0; i < reserved; i++) plan[at[i % at.Count]]++;
        return plan;
    }

    /// <summary>
    /// Trộn cá giữa diffPureOpeners bóng đầu. Mỗi bóng giữ nguyên số cá nên bán kính không đổi, và tổng cá từng loài
    /// cũng không đổi. Thử vài lần để không bóng nào còn thuần một loài; chỉ có một loài thì đành bỏ.
    /// Chạy trước khi dựng hàng đơn, nên hàng đơn tự khớp thứ tự cá mới.
    /// </summary>
    private void ShuffleOpeners(SoftBubbleData[] bubbles)
    {
        int openers = Mathf.Min(diffPureOpeners, bubbles.Length);
        if (openers < 2) return;

        var pool = new List<int>();
        for (int i = 0; i < openers; i++)
        {
            FishData[] fish = bubbles[i]?.FishDataArray;
            if (fish == null) continue;
            foreach (FishData one in fish) pool.Add(one.FishId);
        }

        if (new HashSet<int>(pool).Count < 2) return;

        for (int attempt = 0; attempt < 20; attempt++)
        {
            Shuffle(pool);
            int cursor = 0;
            bool anyPure = false;
            for (int i = 0; i < openers; i++)
            {
                FishData[] fish = bubbles[i]?.FishDataArray;
                if (fish == null) continue;

                for (int k = 0; k < fish.Length; k++) fish[k].FishId = pool[cursor + k];
                if (fish.Length > 1 && new HashSet<int>(pool.GetRange(cursor, fish.Length)).Count == 1) anyPure = true;
                cursor += fish.Length;
            }

            if (!anyPure) return;
        }
    }

    private static bool SwapFishWithLater(SoftBubbleData[] bubbles, int bubble, int index, int wanted)
    {
        FishData give = bubbles[bubble].FishDataArray[index];
        for (int i = bubble + 1; i < bubbles.Length; i++)
        {
            FishData[] other = bubbles[i]?.FishDataArray;
            if (other == null) continue;

            for (int k = 0; k < other.Length; k++)
            {
                if (other[k].FishId != wanted) continue;

                (other[k].FishId, give.FishId) = (give.FishId, other[k].FishId);
                return true;
            }
        }

        return false;
    }

    /// <summary>Các mức lỡ tay của dải đo: 0 = người chơi giỏi, càng lên càng giống người chơi ẩu / bàn rối mắt.</summary>
    private static readonly float[] MistakeRange = { 0f, 0.05f, 0.1f, 0.15f };

    /// <summary>
    /// Mức lỡ tay khớp với người chơi thật, hiệu chỉnh bằng tay người thiết kế (2026-09-25) trên 3 level khác kiểu:
    /// level dễ 43 bóng ≈ 7-8%, level 144 (HP + cá ẩn + spawner) cảm giác ~40% ≈ 11%, level 140 (băng + spawner) cảm
    /// giác ~30% ≈ 11%. "Lỡ tay" gộp mọi thứ bot làm hơn người: thấy đủ 15 bóng dù màn chỉ hiện 11-12, không rối mắt,
    /// không phải dò cá. Chỉ là tay của MỘT người - người đã biết level - nên người chơi mới có thể còn sót nhiều hơn.
    /// </summary>
    private const float PlayerMistakeRate = LevelHumanBot.PlayerMistakeRate;

    /// <summary>
    /// Tỉ lệ thắng của bot như người ở từng mức lỡ tay. Một con số đứng riêng che mất độ "không tha lỗi": level 140
    /// lỡ tay 5% thắng 80% nhưng 10% chỉ còn 36%, còn level 45 lỡ tay 10% vẫn 84%. Bàn đông bóng, nhiều loài mỗi
    /// bóng thì mắt người sót nhiều hơn 5%, nên nhìn cả dải mới thấy đúng cảm giác chơi. Dải rơi càng dốc thì level
    /// càng không tha lỗi.
    /// </summary>
    private static string DescribeMistakeRange(LevelData level)
    {
        var rates = new string[MistakeRange.Length];
        var labels = new string[MistakeRange.Length];
        float player = -1f;

        // Số thấp thì đo lại cả dải bằng nhiều ván hơn: 200 ván ở mức 5-10% nhảy vài điểm mỗi lần sửa bóng.
        int playouts = LevelHumanBot.PlayoutsFor(LevelHumanBot.EstimateWinRate(level, PlayerMistakeRate));
        for (int i = 0; i < MistakeRange.Length; i++)
        {
            float rate = LevelHumanBot.EstimateWinRate(level, MistakeRange[i], playouts);
            rates[i] = rate.ToString("P0");
            labels[i] = Mathf.RoundToInt(MistakeRange[i] * 100f).ToString();
            if (Mathf.Approximately(MistakeRange[i], PlayerMistakeRate))
            {
                player = rate;
                rates[i] = $"[{rates[i]}]";
                labels[i] = $"[{labels[i]}]";
            }
        }

        if (player < 0f) player = LevelHumanBot.EstimateWinRate(level, PlayerMistakeRate, playouts);
        string target = level.Level > 0
            ? $" (mục tiêu {level.Difficulty}: {LevelHumanBot.PlayerTargetWinRate(level.Level, level.Difficulty):P0})"
            : string.Empty;
        string model = (LevelHumanBot.CarefulWhenFull ? "cẩn thận khi ô chờ gần đầy" : "lỡ tay đều mọi nước") +
                       (LevelHumanBot.LookAhead ? ", nhìn trước" : "");
        return $"≈ Người chơi thắng: {player:P0}{target} · {playouts} ván\n" +
               DescribeRetries(level, player, playouts) + "\n" +
               $"Bot như người: {string.Join(" · ", rates)}\n(lỡ tay {string.Join(" · ", labels)}%, {model})";
    }

    /// <summary>
    /// "Thắng theo lượt": người chơi ít khi thấy level khó theo tỉ lệ MỘT lượt - "1-2 lần là qua" là thắng trong vài
    /// lượt, và từ lượt 2 đã biết loài cá ẩn. Lượt 2 đo bằng bot knowsBoard; "trong 3 lượt" = 1 - (1-p1)(1-p2)².
    /// Cá ẩn là thứ duy nhất bot "nhớ" được: spawner / lớp trong bot vốn không tính trước, nên level không có cá ẩn
    /// thì lượt 2 ≈ lượt 1.
    /// </summary>
    private static string DescribeRetries(LevelData level, float first, int playouts)
    {
        float second = LevelHumanBot.EstimateWinRate(level, PlayerMistakeRate, playouts, knowsBoard: true);
        float withinThree = 1f - (1f - first) * (1f - second) * (1f - second);
        return $"Thắng lượt 1: {first:P0} · lượt 2 (đã biết cá ẩn): {second:P0} · trong 3 lượt: {withinThree:P0}";
    }

    /// <summary>Hai ô tick cách chơi của bot như người - dùng chung cho Đo, sinh level, Làm khó / dễ và báo cáo xlsx.</summary>
    private static void DrawCarefulToggle()
    {
        var content = new GUIContent("Bot cẩn thận khi ô chờ gần đầy",
            "Bật: lỡ tay giảm dần khi ô chờ đầy (trống 5 ô = đủ mức lỡ tay, còn 1 ô trống = không bấm bừa) - giống " +
            "người thật chậm lại lúc bàn căng. Tắt: lỡ tay đều mọi nước như cách đo cũ. Đổi xong bấm Đo lại để so.");
        EditorGUI.BeginChangeCheck();
        bool value = EditorGUILayout.ToggleLeft(content, LevelHumanBot.CarefulWhenFull);
        if (EditorGUI.EndChangeCheck()) LevelHumanBot.CarefulWhenFull = value;

        content = new GUIContent("Bot nhìn trước ở nước khó (chậm hơn)",
            "Bật: lúc không có cá vào bể và ô chờ đã căng, bot thử vài nước, chơi tiếp mỗi nước vài bước rồi chọn nước " +
            "dẫn tới bàn tốt nhất - như người chơi kỹ dừng lại tính. Tắt: chỉ theo luật cứng như cách đo cũ.");
        EditorGUI.BeginChangeCheck();
        value = EditorGUILayout.ToggleLeft(content, LevelHumanBot.LookAhead);
        if (EditorGUI.EndChangeCheck()) LevelHumanBot.LookAhead = value;
    }

    /// <summary>Số đo cấu trúc của seed đang mở, kèm kết quả solver, để đối chiếu với mức mong muốn.</summary>
    private static string MeasureLevel(LevelData level)
    {
        SoftBubbleData[] bubbles = level?.SoftBubbleDataArray;
        if (bubbles == null || bubbles.Length == 0) return "Chưa có bóng nào để đo";

        var ids = new List<int[]>(bubbles.Length);
        foreach (SoftBubbleData bubble in bubbles)
        {
            FishData[] fish = bubble?.FishDataArray;
            if (fish == null || fish.Length == 0) continue;

            var one = new int[fish.Length];
            for (int i = 0; i < fish.Length; i++) one[i] = fish[i]?.FishId ?? 0;
            ids.Add(one);
        }

        if (ids.Count == 0) return "Chưa có bóng nào để đo";

        int visible = Mathf.Min(LevelBands.VisibleBubbles, ids.Count);
        int pureOpeners = 0;
        for (int i = 0; i < visible; i++)
        {
            if (new HashSet<int>(ids[i]).Count == 1) pureOpeners++;
        }

        float speciesPerBubble = 0f;
        var species = new HashSet<int>();
        foreach (int[] b in ids)
        {
            speciesPerBubble += new HashSet<int>(b).Count;
            foreach (int id in b) species.Add(id);
        }

        speciesPerBubble /= ids.Count;

        LevelSolver.Result result = LevelSolver.Solve(level, CheckSolverMaxNodes);
        string solverLine = result.Solved
            ? $"Giải được: {result.Moves} nước, ô chờ tối đa {result.MaxHoldingUsed}/{LevelBands.DefaultHoldingSlots}"
            : result.NodeCapHit
                ? "Solver chạm trần nút, chưa kết luận được"
                : "Solver: KHÔNG giải được (luật băng của solver là xấp xỉ - tự chơi lại cho chắc)";

        // Bot đo độc lập với solver. Trước đây bot chỉ chạy khi solver giải xong, mà seed gen trên máy này đều đã qua
        // chính solver đó - nên level vẽ tay / người khác làm (dài, nhiều băng: 130, 134, 137) chạm trần nút là mất
        // luôn phần bot, dù bot chơi được bình thường.
        string solved = solverLine + "\n" +
                        $"Bot cũ (bấm bừa 15%): {LevelSolver.EstimateWinRate(level):P0}\n" +
                        DescribeMistakeRange(level);

        int rock = 0, ice = 0, hpBubbles = 0, hidden = 0, spawners = 0, layered = 0, bundles = 0, locks = 0;
        foreach (SoftBubbleData bubble in bubbles)
        {
            if (bubble == null) continue;
            if (bubble.isRock) rock++;
            if (bubble.isIce) ice++;
            if (bubble.isBubbleHp) hpBubbles++;
            if (bubble.isSpawner && !bubble.isRock) spawners++;
            if (bubble.isMultiLayer && !bubble.isRock) layered++;
            if (bubble.isBundleBubble && !bubble.isRock) bundles++;
            if (bubble.isLock && !bubble.isRock) locks++;

            FishData[] fish = bubble.FishDataArray;
            if (fish == null) continue;
            foreach (FishData one in fish)
            {
                if (one != null && one.isHidden) hidden++;
            }
        }

        string extra = layered + bundles + locks > 0 ? $" · Nhiều lớp {layered} · Cá link {bundles} · Khoá {locks}" : string.Empty;
        string obstacles = rock + ice + hpBubbles + hidden + spawners + layered + bundles + locks > 0
            ? $"Đá {rock} · Băng {ice} · HP {hpBubbles} · Cá ẩn {hidden} · Spawner {spawners}{extra}\n"
            : "Không có vật cản\n";

        return $"{ids.Count} bóng · {species.Count} loài\n" + obstacles +
               $"Bóng thuần trong {visible} bóng đầu: {pureOpeners}\n" +
               $"Loài mỗi bóng: {speciesPerBubble:0.00}\n" + solved;
    }
}
