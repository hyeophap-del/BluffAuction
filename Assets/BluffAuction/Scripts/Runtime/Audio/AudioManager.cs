using System.Collections.Generic;
using BluffAuction.Art;
using BluffAuction.Game;
using UnityEngine;

namespace BluffAuction.Audio
{
    public enum Bus { Sfx, Voice, Ui }

    /// <summary>
    /// 소리: 배경 음악 두 채널을 엇갈려 부드럽게 바꾸고(크로스페이드), 경매장 소음을 깔고, 효과음은 채널 묶음에서 돌려 쓴다.
    /// 음량은 설정(SettingsData)을 따른다. 같은 효과음이 한 프레임에 여러 번 나면 한 번만 낸다.
    /// </summary>
    public sealed class AudioManager : MonoBehaviour
    {
        public static AudioManager I { get; private set; }

        AudioSource musicA, musicB, ambience;
        bool aActive;
        float musicFade = 1f;
        string currentMusic;
        float duck = 1f, duckTarget = 1f;
        readonly List<AudioSource> pool = new List<AudioSource>();
        readonly Dictionary<string, float> lastPlayed = new Dictionary<string, float>();
        int next;

        public static AudioManager Create(Transform parent)
        {
            var go = new GameObject("Audio");
            go.transform.SetParent(parent, false);
            var a = go.AddComponent<AudioManager>();
            I = a;
            a.musicA = a.NewSource("MusicA", true);
            a.musicB = a.NewSource("MusicB", true);
            a.ambience = a.NewSource("Ambience", true);
            for (int i = 0; i < 24; i++) a.pool.Add(a.NewSource("Sfx" + i, false));
            return a;
        }

        AudioSource NewSource(string name, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = loop;
            s.spatialBlend = 0f;
            return s;
        }

        static SettingsData S { get { return SaveData.I.Settings; } }

        public void PlayMusic(string name, float fade = 1.5f)
        {
            if (currentMusic == name) return;
            currentMusic = name;
            var clip = ArtLibrary.I.Clip(name);
            aActive = !aActive;
            var to = aActive ? musicA : musicB;
            to.clip = clip;
            to.volume = 0f;
            if (clip != null) to.Play();
            musicFade = 0f;
            fadeSeconds = Mathf.Max(0.05f, fade);
        }

        float fadeSeconds = 1.5f;

        public void StopMusic(float fade = 1f)
        {
            currentMusic = null;
            aActive = !aActive;
            var to = aActive ? musicA : musicB;
            to.Stop();
            to.clip = null;
            musicFade = 0f;
            fadeSeconds = Mathf.Max(0.05f, fade);
        }

        public void PlayAmbience(string name)
        {
            var clip = ArtLibrary.I.Clip(name);
            if (ambience.clip == clip && ambience.isPlaying) return;
            ambience.clip = clip;
            if (clip != null) ambience.Play();
        }

        /// <summary>음악을 잠깐 낮춘다 (공개 드럼 소리 등).</summary>
        public void Duck(float level) { duckTarget = Mathf.Clamp01(level); }

        public AudioSource Play(string name, float volume = 1f, float pitch = 1f, Bus bus = Bus.Sfx)
        {
            if (string.IsNullOrEmpty(name)) return null;
            float last;
            if (lastPlayed.TryGetValue(name, out last) && Time.unscaledTime - last < 0.035f) return null;
            lastPlayed[name] = Time.unscaledTime;
            var clip = ArtLibrary.I != null ? ArtLibrary.I.Clip(name) : null;
            if (clip == null) return null;
            var s = pool[next];
            next = (next + 1) % pool.Count;
            s.Stop();
            s.clip = clip;
            s.pitch = pitch;
            float busVol = bus == Bus.Voice ? S.Voice : S.Sfx;
            s.volume = volume * busVol * S.Master;
            s.Play();
            return s;
        }

        /// <summary>캐릭터 목소리 (재잘거림). 높낮이를 조금씩 바꿔 매번 다르게.</summary>
        public void Voice(int voice, float pitchJitter = 0.06f)
        {
            string name = voice >= 8 ? "voice_auctioneer" : "voice_" + Mathf.Clamp(voice, 0, 7);
            Play(name, 0.9f, 1f + Random.Range(-pitchJitter, pitchJitter), Bus.Voice);
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            musicFade = Mathf.Min(1f, musicFade + dt / fadeSeconds);
            duck = Mathf.MoveTowards(duck, duckTarget, dt * 1.5f);
            float mv = S.Music * S.Master * duck;
            var on = aActive ? musicA : musicB;
            var off = aActive ? musicB : musicA;
            on.volume = mv * musicFade;
            off.volume = mv * (1f - musicFade);
            if (musicFade >= 1f && off.isPlaying) off.Stop();
            ambience.volume = S.Ambience * S.Master * 0.8f;
        }
    }
}
