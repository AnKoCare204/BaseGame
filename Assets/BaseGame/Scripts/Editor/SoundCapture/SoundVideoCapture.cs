using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Core;
using Core.Manager;
using Core.UI.Activities;
using Core.UI.Modals;
using CoreData;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEditor.Recorder.Input;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Quay video gameplay WIN và LOSE để đưa cho AI tạo sound (video-to-SFX). Menu Tools/FishSort/Sound Capture/Record Win + Lose:
/// sao lưu PlayerPrefs (registry của editor), vào Play, tự chơi một ván thắng rồi một ván thua, ghi bằng Unity Recorder 30 fps
/// (một file có tiếng hiện tại, một file không tiếng) kèm log thời điểm mỗi AudioKey được gọi (AudioManager.OnKeyPlayed),
/// thoát Play rồi trả PlayerPrefs về như cũ. Kết quả ở SoundRework/Capture/&lt;ngày_giờ&gt;/.
/// </summary>
[InitializeOnLoad]
public static class SoundVideoCapture
{
    private const string MenuRoot = "Tools/FishSort/Sound Capture/";
    private const string PendingKey = "FishSort.SoundCapture.OutputDir";
    private const string LogTag = "[SoundCapture]";

    // Level > DefaultBackToMenuLevel (10) để thắng xong về Home có hiệu ứng nhận xu; 11 không có popup mở khoá booster.
    private const int WinLevel = 11;
    private const int LoseLevel = 11;
    private const int FrameRate = 30;
    private const int VideoWidth = 720;
    private const int VideoHeight = 1280;
    /// <summary>Khoảng cách giữa hai lần chạm cá (giây game), gần nhịp người chơi thật.</summary>
    private const float TapInterval = 0.4f;
    /// <summary>Ván thua: số lần chạm cá khớp bể trước khi cố tình đưa cá lệch lên ô chờ.</summary>
    private const int LoseWarmUpTaps = 5;
    /// <summary>Giới hạn thời gian thực cho mỗi bước chờ, quá thì huỷ cả lượt quay.</summary>
    private const float StepTimeoutRealtime = 600f;

    private static readonly Stack<IEnumerator> routine = new();
    private static int lastFrame = -1;
    private static string outputDir;
    private static RecorderController recorder;
    private static string take;
    private static float takeStartTime;
    private static readonly List<(float time, AudioKey key, float delay)> events = new();
    private static readonly List<Fish> fishBuffer = new();

    static SoundVideoCapture()
    {
        outputDir = SessionState.GetString(PendingKey, "");
        if (string.IsNullOrEmpty(outputDir)) return;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    [MenuItem(MenuRoot + "Record Win + Lose")]
    private static void RecordWinLose()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling)
        {
            Debug.LogWarning($"{LogTag} Dừng Play mode (và chờ compile xong) rồi chạy lại menu.");
            return;
        }

        string dir = Path.Combine(ProjectRoot, "SoundRework", "Capture", DateTime.Now.ToString("yyyy-MM-dd_HHmm"));
        Directory.CreateDirectory(dir);
        if (!BackupPrefs())
        {
            Debug.LogError($"{LogTag} Không sao lưu được PlayerPrefs, huỷ để không làm hỏng save.");
            return;
        }

        outputDir = dir;
        SessionState.SetString(PendingKey, dir);
        File.WriteAllText(LogPath, "");
        Log($"Bắt đầu, output {dir}");
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorApplication.isPlaying = true;
    }

    [MenuItem(MenuRoot + "Restore PlayerPrefs Backup")]
    private static void RestorePrefsMenu()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning($"{LogTag} Dừng Play mode trước khi khôi phục PlayerPrefs.");
            return;
        }

        RestorePrefs();
    }

    private static string ProjectRoot => Path.GetDirectoryName(Application.dataPath);
    private static string LogPath => Path.Combine(outputDir, "capture_log.txt");
    private static string PrefsBackupPath => Path.Combine(ProjectRoot, "Library", "SoundCapture", "prefs_backup.reg");
    private static string PrefsRegistryKey => $@"HKCU\Software\Unity\UnityEditor\{Application.companyName}\{Application.productName}";

    // ===================== Vòng chạy =====================

    private static void Tick()
    {
        if (!EditorApplication.isPlaying || string.IsNullOrEmpty(outputDir)) return;
        if (routine.Count == 0 && lastFrame < 0)
        {
            Application.runInBackground = true;
            routine.Push(Run());
        }

        if (Time.frameCount == lastFrame) return;
        lastFrame = Time.frameCount;
        try
        {
            while (routine.Count > 0)
            {
                IEnumerator top = routine.Peek();
                if (!top.MoveNext())
                {
                    routine.Pop();
                    continue;
                }

                if (top.Current is IEnumerator nested)
                {
                    routine.Push(nested);
                    continue;
                }

                return;
            }

            Finish("xong");
        }
        catch (Exception e)
        {
            Log($"LỖI: {e}");
            Finish("lỗi");
        }
    }

    private static void Finish(string reason)
    {
        routine.Clear();
        StopTake();
        Log($"Kết thúc ({reason}), thoát Play để trả PlayerPrefs.");
        EditorApplication.update -= Tick;
        EditorApplication.isPlaying = false;
    }

    private static void OnPlayModeChanged(PlayModeStateChange change)
    {
        if (change != PlayModeStateChange.EnteredEditMode) return;
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.update -= Tick;
        StopTake();
        RestorePrefs();
        SessionState.EraseString(PendingKey);
        if (!string.IsNullOrEmpty(outputDir)) Log("Đã trả PlayerPrefs. Hết.");
        outputDir = null;
        lastFrame = -1;
        routine.Clear();
    }

    private static IEnumerator Run()
    {
        yield return WaitUntil("game khởi động xong", () =>
            Time.frameCount > 150 && GameManager.Instance != null && GameManager.Instance.IsState(GameState.MainMenu) && !IsLoading());
        PrepareSave();

        yield return PlayTake("win", WinLevel, true);
        yield return WaitUntil("về Home sau ván thắng", () => GameManager.Instance.IsState(GameState.MainMenu) && !IsLoading());
        yield return WaitGameSeconds(1f);
        yield return PlayTake("lose", LoseLevel, false);
    }

    /// <summary>Đủ mạng để vào level, bật tiếng và nhạc (save sẽ được trả lại khi thoát Play).</summary>
    private static void PrepareSave()
    {
        PlayerResourceManager.Instance.AddResourceValue(ResourceType.Life, 5, "sound_capture", "cheat");
        AudioManager.Instance.SetSoundFx(true);
        AudioManager.Instance.SetMusic(true);
    }

    private static IEnumerator PlayTake(string takeName, int level, bool win)
    {
        Log($"--- {takeName}: vào level {level}");
        GameManager.Instance.Level.Value = level;
        GameManager.Instance.TryLoadCurrentLevel().Forget();
        yield return WaitUntil("bắt đầu nạp level", IsLoading);
        yield return WaitUntil("nạp level xong", () =>
            !IsLoading() && GameManager.Instance.IsState(GameState.InGame) && LevelManager.Instance.CurrentLevel != null);

        StartTake(takeName);
        yield return WaitUntil("intro đầu level xong", () => !LevelManager.Instance.IsPause);
        yield return WaitGameSeconds(1f);

        if (win) yield return PlayToWin();
        else yield return PlayToLose();
    }

    // ===================== Ván thắng =====================

    private static IEnumerator PlayToWin()
    {
        float nextTap = 0f;
        float stepStart = Time.realtimeSinceStartup;
        while (!GameManager.Instance.IsState(GameState.Win))
        {
            if (GameManager.Instance.IsState(GameState.Lose) || Object.FindFirstObjectByType<ModalLose>() != null)
                throw new Exception("Ván thắng bị kẹt (thua), thử level khác.");
            if (Time.realtimeSinceStartup - stepStart > StepTimeoutRealtime) throw new Exception("Ván thắng quá lâu.");
            if (Time.time >= nextTap && CanPlay(out BoardManager board))
            {
                if (TapMatching(board) || TapToHolding(board, 3, true)) nextTap = Time.time + TapInterval;
            }

            yield return null;
        }

        Log("Bàn đã thắng, chờ ModalWin");
        yield return WaitUntil("ModalWin mở", () => Object.FindFirstObjectByType<ModalWin>() != null);
        yield return WaitGameSeconds(2.5f);
        ModalWin modalWin = Object.FindFirstObjectByType<ModalWin>();
        Log("Bấm Continue");
        modalWin.UIPresenter.View.ButtonContinue.onClick.Invoke();
        yield return WaitUntil("về Home", () => GameManager.Instance.IsState(GameState.MainMenu) && !IsLoading(), 30f);
        yield return WaitGameSeconds(6f);
        StopTake();
    }

    // ===================== Ván thua =====================

    private static IEnumerator PlayToLose()
    {
        float nextTap = 0f;
        int taps = 0;
        float stepStart = Time.realtimeSinceStartup;
        while (Object.FindFirstObjectByType<ModalLose>() == null)
        {
            if (GameManager.Instance.IsState(GameState.Win)) throw new Exception("Ván thua lại thắng, thử level khác.");
            if (Time.realtimeSinceStartup - stepStart > StepTimeoutRealtime) throw new Exception("Ván thua quá lâu.");
            if (Time.time >= nextTap && CanPlay(out BoardManager board))
            {
                bool tapped = taps < LoseWarmUpTaps
                    ? TapMatching(board)
                    : TapToHolding(board, 1, false) || TapMatching(board);
                if (tapped)
                {
                    taps++;
                    nextTap = Time.time + TapInterval;
                }
            }

            yield return null;
        }

        Log("ModalLose mở");
        yield return WaitGameSeconds(3f);
        ModalLose modalLose = Object.FindFirstObjectByType<ModalLose>();
        if (modalLose != null)
        {
            Log("Bấm Close");
            modalLose.UIPresenter.View.ButtonClose.onClick.Invoke();
        }

        yield return WaitGameSeconds(1.5f);
        modalLose = Object.FindFirstObjectByType<ModalLose>();
        if (modalLose != null)
        {
            // Lần bấm đầu có thể chỉ hiện cảnh báo sự kiện; bấm lại để chốt thua.
            Log("Bấm Close lần 2");
            modalLose.UIPresenter.View.ButtonClose.onClick.Invoke();
        }

        yield return WaitUntil("ModalConfirmLose mở", () => Object.FindFirstObjectByType<ModalConfirmLose>() != null, 20f);
        yield return WaitGameSeconds(3f);
        StopTake();
    }

    // ===================== Chạm cá =====================

    private static bool CanPlay(out BoardManager board)
    {
        board = null;
        if (!GameManager.Instance.IsState(GameState.InGame) || LevelManager.Instance.IsPause) return false;
        Level level = LevelManager.Instance.CurrentLevel;
        if (level == null || level.Board == null || level.Board.Model == null) return false;
        board = level.Board;
        return true;
    }

    /// <summary>Chạm một cá mà bể đang có đơn nhận.</summary>
    private static bool TapMatching(BoardManager board)
    {
        foreach (TankEntry tank in board.Model.Tanks)
        {
            if (!tank.Accepts(tank.FishId)) continue;
            if (!board.TryCollectTappableFish(tank.FishId, 1, fishBuffer)) continue;
            if (board.TryTapFish(fishBuffer[0])) return true;
        }

        return false;
    }

    /// <summary>
    /// Đưa một cá lên ô chờ khi còn ít nhất <paramref name="minFree"/> ô trống. preferUpcoming: chọn cá của đơn sắp tới (ván thắng, bể mới
    /// tới sẽ tự thu); ngược lại chọn cá không bể nào nhận (ván thua, lấp ô chờ).
    /// </summary>
    private static bool TapToHolding(BoardManager board, int minFree, bool preferUpcoming)
    {
        BoardModel model = board.Model;
        if (model.HoldingFreeCount < minFree) return false;
        Fish fallback = null;
        foreach (BubbleView view in board.Views)
        {
            if (view == null || view.FishContainer == null || !view.IsFront || view.Entry == null || !view.Entry.IsTappable) continue;
            IReadOnlyList<Fish> fishes = view.FishContainer.Fishes;
            for (int i = 0; i < fishes.Count; i++)
            {
                Fish fish = fishes[i];
                if (fish == null || fish.IsHidden || !board.CanTapFish(fish) || AnyTankAccepts(model, fish.FishId)) continue;
                if (preferUpcoming && !model.OrderQueue.Contains(fish.FishId))
                {
                    fallback ??= fish;
                    continue;
                }

                if (board.TryTapFish(fish)) return true;
            }
        }

        return preferUpcoming && fallback != null && board.TryTapFish(fallback);
    }

    private static bool AnyTankAccepts(BoardModel model, int fishId)
    {
        foreach (TankEntry tank in model.Tanks)
        {
            if (tank.Accepts(fishId)) return true;
        }

        return false;
    }

    // ===================== Chờ =====================

    private static bool IsLoading() => Object.FindFirstObjectByType<ActivityLoading>() != null;

    private static IEnumerator WaitUntil(string what, Func<bool> condition, float timeoutRealtime = StepTimeoutRealtime)
    {
        float start = Time.realtimeSinceStartup;
        while (!condition())
        {
            if (Time.realtimeSinceStartup - start > timeoutRealtime) throw new Exception($"Hết thời gian chờ: {what}");
            yield return null;
        }

        Log($"OK {what}");
    }

    private static IEnumerator WaitGameSeconds(float seconds)
    {
        float end = Time.time + seconds;
        while (Time.time < end) yield return null;
    }

    // ===================== Ghi hình + log sound =====================

    private static void StartTake(string takeName)
    {
        take = takeName;
        events.Clear();
        RecorderControllerSettings controllerSettings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
        controllerSettings.AddRecorderSettings(CreateMovieSettings(Path.Combine(outputDir, $"{takeName}_sound"), true));
        controllerSettings.AddRecorderSettings(CreateMovieSettings(Path.Combine(outputDir, $"{takeName}_silent"), false));
        controllerSettings.SetRecordModeToManual();
        controllerSettings.FrameRatePlayback = FrameRatePlayback.Constant;
        controllerSettings.FrameRate = FrameRate;
        controllerSettings.CapFrameRate = true;

        recorder = new RecorderController(controllerSettings);
        recorder.PrepareRecording();
        if (!recorder.StartRecording()) throw new Exception("Unity Recorder không bắt đầu ghi được (xem Console).");
        takeStartTime = Time.time;
        AudioManager.OnKeyPlayed -= OnKeyPlayed;
        AudioManager.OnKeyPlayed += OnKeyPlayed;
        Log($"Bắt đầu ghi {takeName} (Time.time {takeStartTime:0.000})");
    }

    private static MovieRecorderSettings CreateMovieSettings(string outputFile, bool withAudio)
    {
        MovieRecorderSettings settings = ScriptableObject.CreateInstance<MovieRecorderSettings>();
        settings.name = Path.GetFileName(outputFile);
        settings.Enabled = true;
        settings.EncoderSettings = new CoreEncoderSettings
        {
            Codec = CoreEncoderSettings.OutputCodec.MP4,
            EncodingQuality = CoreEncoderSettings.VideoEncodingQuality.High,
        };
        settings.CaptureAudio = withAudio;
        settings.ImageInputSettings = new GameViewInputSettings { OutputWidth = VideoWidth, OutputHeight = VideoHeight };
        settings.OutputFile = outputFile;
        return settings;
    }

    private static void OnKeyPlayed(AudioKey key, float delay)
    {
        events.Add((Time.time - takeStartTime + delay, key, delay));
    }

    private static void StopTake()
    {
        AudioManager.OnKeyPlayed -= OnKeyPlayed;
        if (recorder == null) return;
        float duration = Time.time - takeStartTime;
        recorder.StopRecording();
        recorder = null;
        WriteEvents(take, duration);
        Log($"Dừng ghi {take}: {duration:0.0} s, {events.Count} lần gọi sound");
    }

    /// <summary>{take}_events.tsv (giây, frame, key, trễ) và {take}_sound.srt để trình xem video hiện tên key đúng lúc.</summary>
    private static void WriteEvents(string takeName, float duration)
    {
        events.Sort((a, b) => a.time.CompareTo(b.time));
        StringBuilder tsv = new StringBuilder("time_s\tframe\tkey\tdelay_s\r\n");
        StringBuilder srt = new StringBuilder();
        for (int i = 0; i < events.Count; i++)
        {
            (float time, AudioKey key, float delay) = events[i];
            tsv.Append(string.Format(CultureInfo.InvariantCulture, "{0:0.000}\t{1}\t{2}\t{3:0.###}\r\n",
                time, Mathf.RoundToInt(time * FrameRate), key, delay));
            srt.Append(i + 1).Append("\r\n")
                .Append(SrtTime(time)).Append(" --> ").Append(SrtTime(time + 0.6f)).Append("\r\n")
                .Append(key).Append("\r\n\r\n");
        }

        tsv.Append(string.Format(CultureInfo.InvariantCulture, "# duration_s\t{0:0.000}\r\n", duration));
        File.WriteAllText(Path.Combine(outputDir, $"{takeName}_events.tsv"), tsv.ToString(), new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(outputDir, $"{takeName}_sound.srt"), srt.ToString(), new UTF8Encoding(false));
    }

    private static string SrtTime(float seconds)
    {
        TimeSpan t = TimeSpan.FromSeconds(Mathf.Max(0f, seconds));
        return $"{(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00},{t.Milliseconds:000}";
    }

    private static void Log(string message)
    {
        Debug.Log($"{LogTag} {message}");
        if (string.IsNullOrEmpty(outputDir)) return;
        File.AppendAllText(LogPath, $"{DateTime.Now:HH:mm:ss} {message}\r\n");
    }

    // ===================== Sao lưu PlayerPrefs =====================

    private static bool BackupPrefs()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PrefsBackupPath));
        return RunReg($"export \"{PrefsRegistryKey}\" \"{PrefsBackupPath}\" /y") && new FileInfo(PrefsBackupPath).Length > 0;
    }

    private static void RestorePrefs()
    {
        if (!File.Exists(PrefsBackupPath))
        {
            Debug.LogWarning($"{LogTag} Không có bản sao lưu PlayerPrefs ở {PrefsBackupPath}.");
            return;
        }

        // Xoá key rồi import lại để bỏ cả các giá trị mới sinh ra trong lúc quay.
        RunReg($"delete \"{PrefsRegistryKey}\" /f");
        bool ok = RunReg($"import \"{PrefsBackupPath}\"");
        Debug.Log($"{LogTag} Khôi phục PlayerPrefs từ {PrefsBackupPath}: {(ok ? "OK" : "LỖI")}");
    }

    private static bool RunReg(string arguments)
    {
        using System.Diagnostics.Process process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("reg.exe", arguments)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        });
        process.WaitForExit(30000);
        return process.ExitCode == 0;
    }
}
