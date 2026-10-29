using System;
using UnityEngine;

/// <summary>
/// Quản lý tổng hợp âm thanh thủ tục (Procedural Audio Synthesis) cho Minigame Vũ Điệu Hòa Nhịp:
/// - Tiếng gõ kalimba nốt nhạc ngũ âm theo hướng mũi tên (C4, E4, G4, B4).
/// - Tiếng chuông gió tinh linh ngân vang (Chime) khi bấm Space trúng nhịp (Perfect/Great/Good).
/// - Tiếng thud gõ trầm khi bấm sai phím.
/// - Khúc nhạc khải hoàn chiến thắng (Victory Fanfare arpeggio Đô Trưởng ngân vang ấm áp).
/// </summary>
public static class RhythmProceduralAudio
{
    private enum ToneType { Pluck, Chime, Thud }

    private static AudioClip noteLeftClip;
    private static AudioClip noteUpClip;
    private static AudioClip noteDownClip;
    private static AudioClip noteRightClip;
    private static AudioClip beatPerfectClip;
    private static AudioClip beatGreatClip;
    private static AudioClip beatMissClip;
    private static AudioClip wrongKeyClip;
    private static AudioClip victoryFanfareClip;

    public static AudioClip VictoryFanfare => victoryFanfareClip ??= CreateVictoryFanfareClip();

    public static AudioSource EnsureAudioSource(GameObject host, AudioSource current)
    {
        if (current == null && host != null)
        {
            current = host.GetComponent<AudioSource>();
            if (current == null) current = host.AddComponent<AudioSource>();
            current.playOnAwake = false;
            current.spatialBlend = 0f; // 2D Stereo UI Sound
        }

        EnsureClips();
        return current;
    }

    public static void EnsureClips()
    {
        if (noteLeftClip == null)
        {
            noteLeftClip = CreateToneClip("Note_C4", 261.63f, 0.14f, 0.35f, ToneType.Pluck);
            noteUpClip = CreateToneClip("Note_E4", 329.63f, 0.14f, 0.35f, ToneType.Pluck);
            noteDownClip = CreateToneClip("Note_G4", 392.00f, 0.14f, 0.35f, ToneType.Pluck);
            noteRightClip = CreateToneClip("Note_B4", 493.88f, 0.14f, 0.35f, ToneType.Pluck);
            beatPerfectClip = CreateToneClip("Beat_Perfect", 523.25f, 0.28f, 0.50f, ToneType.Chime);
            beatGreatClip = CreateToneClip("Beat_Great", 440.00f, 0.22f, 0.45f, ToneType.Chime);
            beatMissClip = CreateToneClip("Beat_Miss", 180.00f, 0.25f, 0.30f, ToneType.Thud);
            wrongKeyClip = CreateToneClip("Wrong_Key", 140.00f, 0.18f, 0.35f, ToneType.Thud);
            victoryFanfareClip = CreateVictoryFanfareClip();
        }
    }

    public static void PlayDirectionSound(AudioSource audioSource, RhythmDirection dir)
    {
        if (audioSource == null) return;
        EnsureClips();

        AudioClip clip = dir switch
        {
            RhythmDirection.Left => noteLeftClip,
            RhythmDirection.Up => noteUpClip,
            RhythmDirection.Down => noteDownClip,
            RhythmDirection.Right => noteRightClip,
            _ => noteLeftClip
        };
        if (clip != null) audioSource.PlayOneShot(clip);
    }

    public static void PlayBeatSound(AudioSource audioSource, RhythmHitGrade grade)
    {
        if (audioSource == null) return;
        EnsureClips();

        AudioClip clip = grade switch
        {
            RhythmHitGrade.Perfect => beatPerfectClip,
            RhythmHitGrade.Great => beatGreatClip,
            RhythmHitGrade.Good => beatGreatClip,
            _ => beatMissClip
        };
        if (clip != null) audioSource.PlayOneShot(clip);
    }

    public static void PlayWrongSound(AudioSource audioSource)
    {
        if (audioSource == null) return;
        EnsureClips();

        if (wrongKeyClip != null) audioSource.PlayOneShot(wrongKeyClip);
    }

    public static void PlayVictoryFanfare(AudioSource audioSource)
    {
        if (audioSource == null) return;
        AudioClip clip = VictoryFanfare;
        if (clip != null) audioSource.PlayOneShot(clip);
    }

    private static AudioClip CreateVictoryFanfareClip()
    {
        int sampleRate = 44100;
        float duration = 2.4f;
        int sampleCount = Mathf.RoundToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];

        // Chuỗi arpeggio Đô Trưởng: C5, E5, G5, B5, C6 (chuông gió tinh linh)
        float[] notes = new float[] { 523.25f, 659.25f, 783.99f, 987.77f, 1046.50f };
        float[] startTimes = new float[] { 0.0f, 0.12f, 0.24f, 0.36f, 0.50f };

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            float totalSample = 0f;

            // 1. Các nốt chuông arpeggio
            for (int n = 0; n < notes.Length; n++)
            {
                if (t >= startTimes[n])
                {
                    float noteTime = t - startTimes[n];
                    float noteDur = n == notes.Length - 1 ? 1.8f : 0.65f;
                    if (noteTime < noteDur)
                    {
                        float env = Mathf.Exp(-4.0f * (noteTime / noteDur));
                        float freq = notes[n];
                        // Âm chuông sin + hài âm lấp lánh
                        float s = Mathf.Sin(2f * Mathf.PI * freq * noteTime) * 0.45f;
                        s += Mathf.Sin(2f * Mathf.PI * freq * 2f * noteTime) * 0.22f;
                        s += Mathf.Sin(2f * Mathf.PI * freq * 3f * noteTime) * 0.12f;
                        totalSample += s * env;
                    }
                }
            }

            // 2. Hợp âm đệm ngân vang (C Major warm chord) từ t = 0.5s đến 2.4s
            if (t >= 0.50f)
            {
                float chordTime = t - 0.50f;
                float chordDur = 1.9f;
                float chordEnv = Mathf.Exp(-2.2f * (chordTime / chordDur));
                float chord = (Mathf.Sin(2f * Mathf.PI * 261.63f * chordTime) * 0.20f
                             + Mathf.Sin(2f * Mathf.PI * 329.63f * chordTime) * 0.18f
                             + Mathf.Sin(2f * Mathf.PI * 392.00f * chordTime) * 0.18f
                             + Mathf.Sin(2f * Mathf.PI * 523.25f * chordTime) * 0.15f);
                totalSample += chord * chordEnv;
            }

            samples[i] = Mathf.Clamp(totalSample * 0.55f, -1f, 1f);
        }

        AudioClip clip = AudioClip.Create("Victory_Fanfare", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private static AudioClip CreateToneClip(string name, float freq, float duration, float volume, ToneType type)
    {
        int sampleRate = 44100;
        int sampleCount = Mathf.RoundToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            float progress = (float)i / sampleCount;

            // Envelope (ADSR)
            float env = 1f;
            if (type == ToneType.Pluck)
            {
                // Tiếng phím gõ đàn kalimba ngân vang êm tai
                env = Mathf.Exp(-6.5f * progress);
            }
            else if (type == ToneType.Chime)
            {
                // Chuông gió tinh linh ngân vang lấp lánh khi trúng nhịp Space
                env = Mathf.Exp(-3.2f * progress);
            }
            else // Thud
            {
                // Tiếng gõ gỗ trầm báo sai nhẹ nhàng không chát chúa
                env = Mathf.Exp(-12.0f * progress);
            }

            // Waveform
            float sin = Mathf.Sin(2f * Mathf.PI * freq * t);
            float harmonic = Mathf.Sin(2f * Mathf.PI * freq * 2f * t) * 0.35f;
            if (type == ToneType.Chime)
            {
                harmonic += Mathf.Sin(2f * Mathf.PI * freq * 3f * t) * 0.20f;
            }

            samples[i] = (sin + harmonic) * env * volume;
        }

        AudioClip clip = AudioClip.Create(name, sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}

