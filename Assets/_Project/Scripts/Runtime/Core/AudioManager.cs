using System.Collections.Generic;
using UnityEngine;

namespace IdleArcade
{
    /// <summary>Pooled 2D SFX with pitch jitter, rate limiting and a rising-pitch combo for rapid pickups.</summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager I { get; private set; }

        readonly Dictionary<Sfx, SfxEntry> map = new Dictionary<Sfx, SfxEntry>();
        readonly Dictionary<Sfx, float> lastPlayed = new Dictionary<Sfx, float>();
        readonly List<AudioSource> voices = new List<AudioSource>();
        AudioSource music;
        int nextVoice;
        float comboPitch = 1f, comboTime;
        float musicTarget = 0.42f;

        void Awake()
        {
            if (I != null && I != this) { Destroy(gameObject); return; }
            I = this;
            var cfg = GameConfig.I;
            if (cfg != null && cfg.sfx != null)
                foreach (var e in cfg.sfx) map[e.id] = e;
            for (int i = 0; i < 16; i++)
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 0f;
                voices.Add(src);
            }
            music = gameObject.AddComponent<AudioSource>();
            music.loop = true;
            music.playOnAwake = false;
            music.volume = 0f;
            if (cfg != null && cfg.music != null) { music.clip = cfg.music; music.Play(); }
            GameSettings.Changed += ApplySettings;
            ApplySettings();
        }

        void OnDestroy()
        {
            GameSettings.Changed -= ApplySettings;
            if (I == this) I = null;
        }

        void ApplySettings()
        {
            AudioListener.volume = 1f;
        }

        void Update()
        {
            float target = GameSettings.Music ? musicTarget : 0f;
            music.volume = Mathf.MoveTowards(music.volume, target, Time.unscaledDeltaTime * 0.5f);
            if (Time.unscaledTime - comboTime > 0.45f) comboPitch = 1f;
        }

        public void Duck(float amount, float seconds)
        {
            musicTarget = 0.42f * (1f - amount);
            Tween.Delay(seconds, () => musicTarget = 0.42f, this, true);
        }

        public static void Play(Sfx id, float volume = 1f, float pitch = 1f)
        {
            if (I == null || !GameSettings.Sound) return;
            I.PlayInternal(id, volume, pitch);
        }

        /// <summary>Plays with a pitch that climbs while called in quick succession (stack pickups).</summary>
        public static void PlayCombo(Sfx id, float volume = 1f)
        {
            if (I == null || !GameSettings.Sound) return;
            I.comboTime = Time.unscaledTime;
            I.PlayInternal(id, volume, I.comboPitch, true);
            I.comboPitch = Mathf.Min(I.comboPitch + 0.045f, 1.6f);
        }

        void PlayInternal(Sfx id, float volume, float pitch, bool noJitter = false)
        {
            if (!map.TryGetValue(id, out var e) || e.clips == null || e.clips.Length == 0) return;
            float now = Time.unscaledTime;
            if (lastPlayed.TryGetValue(id, out var last) && now - last < e.minInterval) return;
            lastPlayed[id] = now;
            var clip = e.clips[Random.Range(0, e.clips.Length)];
            if (clip == null) return;
            var src = voices[nextVoice];
            nextVoice = (nextVoice + 1) % voices.Count;
            src.clip = clip;
            src.volume = e.volume * volume;
            src.pitch = pitch * (noJitter ? 1f : 1f + Random.Range(-e.pitchJitter, e.pitchJitter));
            src.Play();
        }
    }
}
