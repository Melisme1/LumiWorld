using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Quản lý âm thanh toàn diện cho LumiWorld:
/// - Nhạc nền (BGM):
///   + Màn chơi chính (MapBuilding / Ingame): phát luân phiên 2 bài ingame1 và ingame2.
///   + Tất cả các màn hình khác (Login, MainMenu, WorldMap, VisitWorld, Loading...): phát bài waitgame.
/// - Hiệu ứng âm thanh (SFX):
///   + Tiếng "tít" (PlayClick): tự động gắn cho TẤT CẢ nút bấm trong toàn bộ game.
///   + Tiếng "xoạc" (PlayPlace): khi đặt môi trường / terrain lên bản đồ.
///   + Tiếng "tắc" (PlayCard): khi thả / dùng thẻ bài.
/// - Lưu trữ âm lượng vào PlayerPrefs và đồng bộ tức thì với GameSettingsUI.
/// </summary>
[DefaultExecutionOrder(-300)]
public class GameAudioManager : MonoBehaviour
{
    private static GameAudioManager instance;
    public static GameAudioManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindAnyObjectByType<GameAudioManager>();
                if (instance == null)
                {
                    GameObject go = new GameObject("GameAudioManager");
                    instance = go.AddComponent<GameAudioManager>();
                    DontDestroyOnLoad(go);
                }
            }
            return instance;
        }
    }

    private const string PREF_MASTER_VOL = "Lumi_MasterVolume";
    private const string PREF_BGM_VOL = "Lumi_BGMVolume";
    private const string PREF_SFX_VOL = "Lumi_SFXVolume";
    private const string PREF_MUTED = "Lumi_IsMuted";

    private AudioSource bgmSource;
    private AudioSource sfxSource;

    private AudioClip clickClip;
    private AudioClip placeClip;
    private AudioClip cardClip;

    // Nhạc nền game theo màn hình
    private AudioClip ingame1Clip;
    private AudioClip ingame2Clip;
    private AudioClip waitgameClip;

    private int currentIngameIndex = 0;
    private bool isInGameScene = false;

    // Quản lý tự động gắn âm thanh cho tất cả Button trong game
    private readonly HashSet<Button> hookedButtons = new HashSet<Button>();
    private float buttonScanTimer = 0f;

    public float MasterVolume { get; private set; } = 1.0f;
    public float BGMVolume { get; private set; } = 0.7f;
    public float SFXVolume { get; private set; } = 0.85f;
    public bool IsMuted { get; private set; } = false;

    public event Action OnVolumeChanged;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);

        LoadSettings();
        InitializeAudioSources();
        GenerateProceduralSfx();
        LoadBgmClips();

        SceneManager.sceneLoaded += OnSceneLoaded;
        CheckCurrentScene(SceneManager.GetActiveScene().name);
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Update()
    {
        // 1. Tự động kiểm tra bài nhạc BGM kế tiếp khi bài hiện tại chạy hết
        HandleBgmLoop();

        // 2. Tự động quét và gắn tiếng "tít" cho tất cả các Button mới xuất hiện
        buttonScanTimer -= Time.unscaledDeltaTime;
        if (buttonScanTimer <= 0f)
        {
            buttonScanTimer = 0.4f;
            HookAllSceneButtons();
        }
    }

    private void LoadSettings()
    {
        MasterVolume = PlayerPrefs.GetFloat(PREF_MASTER_VOL, 1.0f);
        BGMVolume = PlayerPrefs.GetFloat(PREF_BGM_VOL, 0.7f);
        SFXVolume = PlayerPrefs.GetFloat(PREF_SFX_VOL, 0.85f);
        IsMuted = PlayerPrefs.GetInt(PREF_MUTED, 0) == 1;
    }

    private void SaveSettings()
    {
        PlayerPrefs.SetFloat(PREF_MASTER_VOL, MasterVolume);
        PlayerPrefs.SetFloat(PREF_BGM_VOL, BGMVolume);
        PlayerPrefs.SetFloat(PREF_SFX_VOL, SFXVolume);
        PlayerPrefs.SetInt(PREF_MUTED, IsMuted ? 1 : 0);
        PlayerPrefs.Save();
    }

    private void InitializeAudioSources()
    {
        bgmSource = gameObject.AddComponent<AudioSource>();
        bgmSource.loop = false; // Tự chuyển bài ingame1 <-> ingame2
        bgmSource.playOnAwake = false;

        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.loop = false;
        sfxSource.playOnAwake = false;

        ApplyVolumes();
    }

    public void ApplyVolumes()
    {
        float master = IsMuted ? 0f : MasterVolume;
        if (bgmSource != null) bgmSource.volume = master * BGMVolume;
        if (sfxSource != null) sfxSource.volume = master * SFXVolume;
        OnVolumeChanged?.Invoke();
    }

    public void SetMasterVolume(float vol)
    {
        MasterVolume = Mathf.Clamp01(vol);
        SaveSettings();
        ApplyVolumes();
    }

    public void SetBGMVolume(float vol)
    {
        BGMVolume = Mathf.Clamp01(vol);
        SaveSettings();
        ApplyVolumes();
    }

    public void SetSFXVolume(float vol)
    {
        SFXVolume = Mathf.Clamp01(vol);
        SaveSettings();
        ApplyVolumes();
    }

    public void SetMuted(bool muted)
    {
        IsMuted = muted;
        SaveSettings();
        ApplyVolumes();
    }

    public void ToggleMute()
    {
        SetMuted(!IsMuted);
    }

    // =========================================================
    // QUẢN LÝ NHẠC NỀN THEO SCENE
    // =========================================================

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        CheckCurrentScene(scene.name);
    }

    private void CheckCurrentScene(string sceneName)
    {
        // Màn chơi chính: MapBuilding (hoặc khi đang chơi trên đảo của mình)
        bool newInGame = sceneName.Equals("MapBuilding", StringComparison.OrdinalIgnoreCase);

        if (newInGame != isInGameScene || !bgmSource.isPlaying)
        {
            isInGameScene = newInGame;
            PlayAppropriateBgm();
        }
    }

    private void PlayAppropriateBgm()
    {
        if (isInGameScene)
        {
            // Trong màn chơi: phát ingame1 hoặc ingame2
            AudioClip clip = currentIngameIndex == 0 ? ingame1Clip : ingame2Clip;
            if (clip == null) clip = ingame1Clip ?? ingame2Clip ?? waitgameClip;
            PlayBgm(clip);
        }
        else
        {
            // Tất cả màn hình khác (WorldMap, Login, MainMenu, Visit...): phát waitgame
            PlayBgm(waitgameClip ?? ingame1Clip);
        }
    }

    private void HandleBgmLoop()
    {
        if (bgmSource == null || !bgmSource.isPlaying)
        {
            if (isInGameScene)
            {
                // Chuyển luân phiên giữa ingame1 và ingame2
                currentIngameIndex = (currentIngameIndex + 1) % 2;
                AudioClip nextClip = currentIngameIndex == 0 ? ingame1Clip : ingame2Clip;
                if (nextClip == null) nextClip = ingame1Clip ?? ingame2Clip ?? waitgameClip;
                PlayBgm(nextClip);
            }
            else
            {
                // Lặp lại waitgame
                if (waitgameClip != null) PlayBgm(waitgameClip);
            }
        }
    }

    private void PlayBgm(AudioClip clip)
    {
        if (clip == null || bgmSource == null) return;
        bgmSource.Stop();
        bgmSource.clip = clip;
        bgmSource.Play();
    }

    private void LoadBgmClips()
    {
        // 1. Thử tải từ Resources/Audio trước
        ingame1Clip = Resources.Load<AudioClip>("Audio/ingame1");
        ingame2Clip = Resources.Load<AudioClip>("Audio/ingame2");
        waitgameClip = Resources.Load<AudioClip>("Audio/waitgame");

        // 2. Nếu Unity chưa import kịp vào Resources, load trực tiếp từ file mp3 trong Assets/nhacgame
        if (ingame1Clip == null) StartCoroutine(LoadStreamingAudio("ingame1.mp3", (c) => { ingame1Clip = c; if (isInGameScene && !bgmSource.isPlaying) PlayAppropriateBgm(); }));
        if (ingame2Clip == null) StartCoroutine(LoadStreamingAudio("ingame2.mp3", (c) => { ingame2Clip = c; }));
        if (waitgameClip == null) StartCoroutine(LoadStreamingAudio("waitgame.mp3", (c) => { waitgameClip = c; if (!isInGameScene && !bgmSource.isPlaying) PlayAppropriateBgm(); }));

        // 3. Fallback: Nếu chưa load xong clip mp3 nào, dùng procedural loop tạm thời
        if (ingame1Clip == null && waitgameClip == null)
        {
            AudioClip fallbackBgm = CreateBgmClip("BGM_Fallback");
            PlayBgm(fallbackBgm);
        }
    }

    private IEnumerator LoadStreamingAudio(string fileName, Action<AudioClip> onLoaded)
    {
        string fullPath = System.IO.Path.Combine(Application.dataPath, "nhacgame", fileName);
        if (!System.IO.File.Exists(fullPath)) yield break;

        string uri = "file://" + fullPath.Replace('\\', '/');
        using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(uri, AudioType.MPEG))
        {
            yield return www.SendWebRequest();
            if (www.result == UnityWebRequest.Result.Success)
            {
                AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
                if (clip != null)
                {
                    clip.name = fileName;
                    onLoaded?.Invoke(clip);
                }
            }
        }
    }

    // =========================================================
    // HIỆU ỨNG ÂM THANH CHO TẤT CẢ CÁC NÚT (GLOBAL BUTTON SOUND)
    // =========================================================

    private void HookAllSceneButtons()
    {
        Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Include);
        foreach (Button btn in buttons)
        {
            if (btn == null) continue;
            if (hookedButtons.Add(btn))
            {
                btn.onClick.AddListener(PlayClick);
            }
        }
    }

    // =========================================================
    // PHÁT SFX
    // =========================================================

    /// <summary>
    /// Phát âm thanh "tít" trong trẻo khi bấm bất kỳ nút nào trong game
    /// </summary>
    public void PlayClick()
    {
        if (IsMuted || clickClip == null) return;
        sfxSource.PlayOneShot(clickClip, MasterVolume * SFXVolume);
    }

    /// <summary>
    /// Phát âm thanh "xoạc" sột soạt khi đặt môi trường / terrain
    /// </summary>
    public void PlayPlace()
    {
        if (IsMuted || placeClip == null) return;
        sfxSource.PlayOneShot(placeClip, MasterVolume * SFXVolume);
    }

    /// <summary>
    /// Phát âm thanh "tắc" lách cách dứt khoát khi dùng / thả thẻ bài
    /// </summary>
    public void PlayCard()
    {
        if (IsMuted || cardClip == null) return;
        sfxSource.PlayOneShot(cardClip, MasterVolume * SFXVolume);
    }

    // =========================================================
    // TẠO SFX BẰNG CODE (PROCEDURAL AUDIO)
    // =========================================================

    private void GenerateProceduralSfx()
    {
        clickClip = CreateClickClip("SFX_Click_Tach");
        placeClip = CreatePlaceClip("SFX_Place_Xoac");
        cardClip = CreateCardClip("SFX_Card_Tac");
    }

    private AudioClip CreateClickClip(string name)
    {
        const int sampleRate = 44100;
        const float duration = 0.045f;
        int sampleCount = Mathf.RoundToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            float progress = (float)i / sampleCount;

            // Tiếng "tách" lách cách dứt khoát, thanh thoát, đầm tay
            float freq = Mathf.Lerp(950f, 520f, progress);
            float body = Mathf.Sin(2f * Mathf.PI * freq * t);

            // Xung click đanh ở đầu âm (5ms đầu)
            float snap = progress < 0.12f
                ? Mathf.Sin(2f * Mathf.PI * 2600f * t) * (1f - progress / 0.12f)
                : 0f;

            float env = Mathf.Exp(-progress * 11f);
            samples[i] = (body * 0.7f + snap * 0.45f) * env * 0.85f;
        }

        AudioClip clip = AudioClip.Create(name, sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip CreatePlaceClip(string name)
    {
        const int sampleRate = 44100;
        const float duration = 0.22f;
        int sampleCount = Mathf.RoundToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];
        var rng = new System.Random(42);

        float lastNoise = 0f;
        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            float progress = (float)i / sampleCount;

            float white = (float)(rng.NextDouble() * 2.0 - 1.0);
            float filteredNoise = Mathf.Lerp(lastNoise, white, 0.45f);
            lastNoise = filteredNoise;

            float bodyFreq = Mathf.Lerp(380f, 160f, progress);
            float bodyTone = Mathf.Sin(2f * Mathf.PI * bodyFreq * t) * 0.35f;

            float env = progress < 0.15f
                ? (progress / 0.15f)
                : Mathf.Pow(1f - (progress - 0.15f) / 0.85f, 2.2f);

            samples[i] = (filteredNoise * 0.65f + bodyTone) * env * 0.8f;
        }

        AudioClip clip = AudioClip.Create(name, sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip CreateCardClip(string name)
    {
        const int sampleRate = 44100;
        const float duration = 0.07f;
        int sampleCount = Mathf.RoundToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            float progress = (float)i / sampleCount;

            float snapFreq = Mathf.Lerp(520f, 240f, progress);
            float wave = Mathf.Sin(2f * Mathf.PI * snapFreq * t);
            float click = progress < 0.1f ? Mathf.Sin(2f * Mathf.PI * 2200f * t) * (1f - progress / 0.1f) : 0f;
            float env = Mathf.Exp(-progress * 9f);
            samples[i] = (wave * 0.7f + click * 0.4f) * env * 0.85f;
        }

        AudioClip clip = AudioClip.Create(name, sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip CreateBgmClip(string name)
    {
        const int sampleRate = 22050;
        const float duration = 12.0f;
        int sampleCount = Mathf.RoundToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];

        float[][] chords = new float[][]
        {
            new float[] { 261.63f, 329.63f, 392.00f, 493.88f },
            new float[] { 220.00f, 261.63f, 329.63f, 392.00f },
            new float[] { 174.61f, 261.63f, 329.63f, 349.23f },
            new float[] { 196.00f, 246.94f, 293.66f, 392.00f }
        };

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            int chordIndex = Mathf.Clamp(Mathf.FloorToInt(t / 3.0f), 0, chords.Length - 1);
            float[] chord = chords[chordIndex];

            float localT = t % 3.0f;
            float chordEnv = Mathf.Sin((localT / 3.0f) * Mathf.PI);

            float sum = 0f;
            for (int n = 0; n < chord.Length; n++)
            {
                float freq = chord[n];
                float note = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.5f
                           + Mathf.Sin(4f * Mathf.PI * freq * t) * 0.15f;
                sum += note;
            }

            float loopFade = 1f;
            if (t < 0.2f) loopFade = t / 0.2f;
            else if (t > duration - 0.2f) loopFade = (duration - t) / 0.2f;

            samples[i] = sum * 0.12f * chordEnv * loopFade;
        }

        AudioClip clip = AudioClip.Create(name, sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
