using System;
using System.Collections.Generic;
using System.IO;
using BluffAuction.Art;
using UnityEditor;
using UnityEngine;

namespace BluffAuction.EditorTools
{
    /// <summary>
    /// 모든 소리를 코드로 합성해 WAV(44.1kHz 모노 16비트)로 저장하고 ArtLibrary 에 이름으로 넣는다 (외부 음원 없음).
    /// VRChat판 AuctionSoundSynth(효과음)·AuctionBgm(재즈 라운지)의 합성법을 옮기고, PC판용으로
    /// UI 소리 · 경매장 소리 · 군중(감탄·헉·박수·웃음) · 캐릭터 웅얼거림 목소리 8종과 경매사 · 경매/결과 음악 · 실내 소리를 더했다.
    /// 한 번 만든 WAV 는 다시 만들지 않는다(지우면 다시 생성). 소리마다 이름에서 얻은 고정 시드를 써서 언제 만들어도 같은 소리가 난다.
    /// 음악·실내 소리는 끝을 넘친 잔향 꼬리를 앞쪽에 겹쳐 끊김 없이 반복된다.
    /// </summary>
    public static class SoundFactory
    {
        public const string Dir = "Assets/BluffAuction/Generated/Audio";
        const int Rate = 44100;
        const float TwoPi = 2f * Mathf.PI;
        static System.Random rng = new System.Random(7);

        enum Kind { Sfx, Music, Ambience }

        struct Entry
        {
            public readonly string Name;
            public readonly Kind Kind;
            public readonly Func<float[]> Make;
            public Entry(string name, Kind kind, Func<float[]> make) { Name = name; Kind = kind; Make = make; }
        }

        static readonly Entry[] Entries =
        {
            // ── UI (자주 나므로 작고 부드럽게) ──
            new Entry("ui_hover", Kind.Sfx, Hover),
            new Entry("ui_click", Kind.Sfx, Click),
            new Entry("ui_confirm", Kind.Sfx, Confirm),
            new Entry("ui_back", Kind.Sfx, Back),
            new Entry("ui_error", Kind.Sfx, Error),
            new Entry("ui_open", Kind.Sfx, () => Sweep(0.17f, 430f, 990f, true)),
            new Entry("ui_close", Kind.Sfx, () => Sweep(0.15f, 900f, 420f, false)),
            new Entry("ui_toggle", Kind.Sfx, Toggle),
            new Entry("ui_slider", Kind.Sfx, SliderTick),
            new Entry("ui_coin", Kind.Sfx, Coin),
            new Entry("ui_reveal", Kind.Sfx, Reveal),
            new Entry("ui_notify", Kind.Sfx, Notify),
            new Entry("ui_type", Kind.Sfx, TypeTick),
            // ── 경매장 ──
            new Entry("sfx_bell", Kind.Sfx, DeskBell),
            new Entry("sfx_bid", Kind.Sfx, WoodTok),
            new Entry("sfx_bid_big", Kind.Sfx, BigBid),
            new Entry("sfx_gavel", Kind.Sfx, GavelKnocks),
            new Entry("sfx_gavel_single", Kind.Sfx, GavelSingle),
            new Entry("sfx_unsold", Kind.Sfx, Unsold),
            new Entry("sfx_tick", Kind.Sfx, ClockTick),
            new Entry("sfx_tick_final", Kind.Sfx, ClockTickHigh),
            new Entry("sfx_reveal_ting", Kind.Sfx, CrystalTing),
            new Entry("sfx_reveal_good", Kind.Sfx, RevealGood),
            new Entry("sfx_reveal_bad", Kind.Sfx, RevealBad),
            new Entry("sfx_fanfare", Kind.Sfx, Fanfare),
            new Entry("sfx_ability", Kind.Sfx, AbilityShimmer),
            new Entry("sfx_say", Kind.Sfx, SayChime),
            new Entry("sfx_whoosh", Kind.Sfx, Whoosh),
            new Entry("sfx_cards", Kind.Sfx, Cards),
            new Entry("sfx_coins", Kind.Sfx, Coins),
            new Entry("sfx_count", Kind.Sfx, CountTick),
            new Entry("sfx_drumroll", Kind.Sfx, Drumroll),
            new Entry("sfx_cymbal", Kind.Sfx, Cymbal),
            new Entry("sfx_loan", Kind.Sfx, Register),
            new Entry("sfx_win", Kind.Sfx, Win),
            new Entry("sfx_lose", Kind.Sfx, Lose),
            new Entry("sfx_throw", Kind.Sfx, ThrowSwish),
            new Entry("sfx_thud", Kind.Sfx, Thud),
            new Entry("sfx_splat", Kind.Sfx, Splat),
            new Entry("sfx_pop", Kind.Sfx, Pop),
            new Entry("sfx_slap", Kind.Sfx, Slap),
            new Entry("ui_chat", Kind.Sfx, ChatBlip),
            new Entry("ui_mic_on", Kind.Sfx, () => Sweep(0.12f, 620f, 1180f, true)),
            new Entry("ui_mic_off", Kind.Sfx, () => Sweep(0.12f, 1100f, 560f, false)),
            // ── 군중 ──
            new Entry("crowd_ooh", Kind.Sfx, CrowdOoh),
            new Entry("crowd_gasp", Kind.Sfx, Gasp),
            new Entry("crowd_applause", Kind.Sfx, Applause),
            new Entry("crowd_laugh", Kind.Sfx, Laugh),
            // ── 말풍선 목소리 (웅얼거림) ──
            new Entry("voice_0", Kind.Sfx, () => Speech(0)),
            new Entry("voice_1", Kind.Sfx, () => Speech(1)),
            new Entry("voice_2", Kind.Sfx, () => Speech(2)),
            new Entry("voice_3", Kind.Sfx, () => Speech(3)),
            new Entry("voice_4", Kind.Sfx, () => Speech(4)),
            new Entry("voice_5", Kind.Sfx, () => Speech(5)),
            new Entry("voice_6", Kind.Sfx, () => Speech(6)),
            new Entry("voice_7", Kind.Sfx, () => Speech(7)),
            new Entry("voice_auctioneer", Kind.Sfx, Auctioneer),
            // ── 음악 · 실내 소리 (반복) ──
            new Entry("bgm_menu", Kind.Music, BgmMenu),
            new Entry("bgm_auction", Kind.Music, BgmAuction),
            new Entry("bgm_results", Kind.Music, BgmResults),
            new Entry("amb_room", Kind.Ambience, AmbRoom),
        };

        /// <summary>없는 WAV 만 새로 만들고, 가져오기 설정을 맞춘 뒤 lib.Clips 를 채운다. ContentBuilder 가 부른다.</summary>
        public static void BuildAll(ArtLibrary lib)
        {
            Directory.CreateDirectory(Dir);
            lib.Clips.Clear();
            int made = 0;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            foreach (var e in Entries)
            {
                string path = Dir + "/" + e.Name + ".wav";
                if (!File.Exists(path))
                {
                    Save(path, Synthesize(e.Name));
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                    made++;
                }
                Configure(path, e.Kind != Kind.Sfx);
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip != null) lib.Clips.Add(new ArtLibrary.NamedClip { Name = e.Name, Clip = clip });
                else Debug.LogWarning("[Sound] missing clip " + path);
            }
            Debug.Log("[Sound] " + lib.Clips.Count + " clips (" + made + " new, " + watch.ElapsedMilliseconds + " ms): " + Dir);
        }

        /// <summary>이름의 소리를 합성해 표본 배열(-1~1)로 돌려준다. 파일은 쓰지 않는다.</summary>
        public static float[] Synthesize(string name)
        {
            foreach (var e in Entries)
            {
                if (e.Name != name) continue;
                rng = new System.Random(Seed(name));
                return e.Make();
            }
            throw new ArgumentException("[Sound] unknown clip " + name);
        }

        /// <summary>만드는 모든 소리 이름.</summary>
        public static string[] Names()
        {
            var n = new string[Entries.Length];
            for (int i = 0; i < n.Length; i++) n[i] = Entries[i].Name;
            return n;
        }

        /// <summary>끊김 없이 반복되게 만든 소리(음악·실내 소리)인지.</summary>
        public static bool IsLoop(string name)
        {
            foreach (var e in Entries) if (e.Name == name) return e.Kind != Kind.Sfx;
            return false;
        }

        /// <summary>이름에서 얻는 고정 시드 (FNV-1a). string.GetHashCode 는 실행마다 달라질 수 있어 쓰지 않는다.</summary>
        static int Seed(string s)
        {
            unchecked
            {
                uint h = 2166136261;
                foreach (char c in s) { h ^= c; h *= 16777619; }
                return (int)(h & 0x7fffffff);
            }
        }

        /// <summary>효과음: 풀어서 메모리에 (PCM, 바로 재생). 음악·실내 소리: 압축(Vorbis) 그대로 메모리에, 백그라운드로 불러오기.</summary>
        static void Configure(string path, bool loop)
        {
            var imp = AssetImporter.GetAtPath(path) as AudioImporter;
            if (imp == null) return;
            bool dirty = false;
            // 이미 모노라 '정규화'는 필요 없다 — 켜져 있으면 만든 최댓값(크기 균형)이 바뀌므로 끈다
            var so = new SerializedObject(imp);
            var norm = so.FindProperty("m_Normalize");
            if (norm != null && norm.boolValue)
            {
                norm.boolValue = false;
                so.ApplyModifiedPropertiesWithoutUndo();
                dirty = true;
            }
            if (!imp.forceToMono) { imp.forceToMono = true; dirty = true; }
            if (imp.loadInBackground != loop) { imp.loadInBackground = loop; dirty = true; }
            var s = imp.defaultSampleSettings;
            var loadType = loop ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
            var format = loop ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.PCM;
            if (s.loadType != loadType || s.compressionFormat != format || !s.preloadAudioData || (loop && Mathf.Abs(s.quality - 0.6f) > 0.01f))
            {
                s.loadType = loadType;
                s.compressionFormat = format;
                s.preloadAudioData = true;
                if (loop) s.quality = 0.6f;
                imp.defaultSampleSettings = s;
                dirty = true;
            }
            if (dirty) imp.SaveAndReimport();
        }

        // ═════════════════ UI ═════════════════

        /// <summary>아주 작은 '틱' — 버튼 위에 마우스를 올렸을 때 (가장 자주 나므로 가장 작고 부드럽게).</summary>
        static float[] Hover()
        {
            var b = Buf(0.045f);
            float lp = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                lp += 0.25f * (Noise() - lp);
                b[i] = (0.6f * Mathf.Sin(TwoPi * 2600f * t) * Mathf.Exp(-t / 0.0035f)
                      + 0.45f * Mathf.Sin(TwoPi * 1250f * t) * Mathf.Exp(-t / 0.008f)
                      + 0.25f * lp * Mathf.Exp(-t / 0.0015f)) * Mathf.Min(1f, t / 0.0006f);
            }
            return Finish(b, 0.3f);
        }

        /// <summary>짧고 단단한 '톡' — 숫자 조정처럼 자주 누르는 버튼용.</summary>
        static float[] Click()
        {
            var b = Buf(0.075f);
            float ph1 = 0, ph2 = 0, lp = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float f1 = 2300f * (1f - 0.25f * t / 0.075f);
                ph1 += TwoPi * f1 / Rate;
                ph2 += TwoPi * 820f / Rate;
                float n = Noise();
                lp += 0.35f * (n - lp);
                float noise = (n - lp) * Mathf.Exp(-t / 0.0025f);         // 고역 잡음 순간음
                float tick = Mathf.Sin(ph1) * Mathf.Exp(-t / 0.010f);
                float body = Mathf.Sin(ph2) * Mathf.Exp(-t / 0.022f);
                b[i] = 0.45f * noise + 0.55f * tick + 0.45f * body;
            }
            return Finish(b, 0.42f);
        }

        /// <summary>위로 올라가는 두 음 '띠링' — 참가·입찰·확정 같은 주요 버튼용.</summary>
        static float[] Confirm()
        {
            var b = Buf(0.42f);
            Tone(b, 0.000f, 880.0f, 0.075f, 0.55f);
            Tone(b, 0.070f, 1318.5f, 0.130f, 0.65f);
            return Finish(b, 0.5f);
        }

        /// <summary>부드럽게 내려가는 두 음 — 뒤로·취소.</summary>
        static float[] Back()
        {
            var b = Buf(0.36f);
            Tone(b, 0.000f, 1174.7f, 0.055f, 0.45f); // D6
            Tone(b, 0.065f, 880.0f, 0.110f, 0.55f);  // A5
            return Finish(b, 0.42f);
        }

        /// <summary>낮은 '둥둥' — 지금 할 수 없는 조작.</summary>
        static float[] Error()
        {
            var b = Buf(0.30f);
            for (int k = 0; k < 2; k++)
            {
                float start = k * 0.12f;
                float f0 = k == 0 ? 210f : 175f;
                float ph = 0;
                for (int i = (int)(start * Rate); i < b.Length; i++)
                {
                    float t = i / (float)Rate - start;
                    if (t > 0.11f) break;
                    float f = f0 * (1f - 0.12f * t / 0.11f);
                    ph += TwoPi * f / Rate;
                    // 부드러운 사각파 (홀수 배음 몇 개)
                    float s = Mathf.Sin(ph) + 0.30f * Mathf.Sin(3 * ph) + 0.12f * Mathf.Sin(5 * ph);
                    float env = Mathf.Min(1f, t / 0.004f) * Mathf.Exp(-t / 0.045f);
                    b[i] += s * env;
                }
            }
            return Finish(b, 0.45f);
        }

        /// <summary>창 열기/닫기 '휙'.</summary>
        static float[] Sweep(float len, float from, float to, bool up)
        {
            var b = Buf(len);
            float ph = 0, lp = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float x = t / len;
                float f = Mathf.Lerp(from, to, up ? Mathf.Sqrt(x) : x * x);
                ph += TwoPi * f / Rate;
                float env = Mathf.Sin(Mathf.PI * Mathf.Min(1f, x * (up ? 1.2f : 1f)));
                float n = Noise();
                lp += 0.08f * (n - lp);
                b[i] = env * (0.8f * Mathf.Sin(ph) + 0.25f * Mathf.Sin(2 * ph) + 1.2f * lp);
            }
            return Finish(b, 0.38f);
        }

        /// <summary>스위치 '딸깍' — 켜기/끄기.</summary>
        static float[] Toggle()
        {
            var b = Buf(0.1f);
            Snap(b, 0.000f, 2500f, 0.75f);
            Snap(b, 0.034f, 1650f, 1.0f);
            return Finish(b, 0.42f);
        }

        /// <summary>슬라이더 눈금 '톡' — 값이 바뀔 때마다.</summary>
        static float[] SliderTick()
        {
            var b = Buf(0.03f);
            Snap(b, 0f, 3100f, 1f);
            return Finish(b, 0.32f);
        }

        /// <summary>동전 '띠링!' — 입찰이 받아들여졌을 때.</summary>
        static float[] Coin()
        {
            var b = Buf(0.55f);
            Square(b, 0.000f, 987.8f, 0.070f, 0.010f, 0.5f);   // B5
            Square(b, 0.065f, 1318.5f, 0.450f, 0.160f, 0.55f); // E6
            // 금속성 반짝임
            Tone(b, 0.065f, 3951f, 0.060f, 0.10f);
            return Finish(b, 0.45f);
        }

        /// <summary>종소리 아르페지오 — 능력 결과 도착.</summary>
        static float[] Reveal()
        {
            var b = Buf(0.9f);
            float[] notes = { 1046.5f, 1318.5f, 1568.0f, 2093.0f };
            for (int k = 0; k < notes.Length; k++) Bell(b, k * 0.065f, notes[k], 0.30f, k == notes.Length - 1 ? 0.65f : 0.45f);
            return Finish(b, 0.45f);
        }

        /// <summary>부드러운 두 음 '딩동' — 알림 (누가 더 높게 불렀을 때 등).</summary>
        static float[] Notify()
        {
            var b = Buf(0.95f);
            Mallet(b, 0.00f, 1318.5f, 0.30f, 0.5f);  // E6
            Mallet(b, 0.16f, 1046.5f, 0.45f, 0.55f); // C6
            return Finish(Reverb(b, 0.16f), 0.42f);
        }

        /// <summary>타자기 글쇠 '탁' — 글자가 한 자씩 나올 때.</summary>
        static float[] TypeTick()
        {
            var b = Buf(0.05f);
            float lp = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float n = Noise();
                lp += 0.6f * (n - lp);
                b[i] = (0.7f * (n - lp) * Mathf.Exp(-t / 0.0012f)
                      + 0.4f * Mathf.Sin(TwoPi * 3400f * t) * Mathf.Exp(-t / 0.004f)
                      + 0.5f * Mathf.Sin(TwoPi * 420f * t) * Mathf.Exp(-t / 0.009f)) * Mathf.Min(1f, t / 0.0005f);
            }
            return Finish(b, 0.34f);
        }

        /// <summary>짧은 딸깍: 아주 짧은 잡음 + 높은 울림 + 낮은 몸통.</summary>
        static void Snap(float[] b, float start, float f, float amp)
        {
            int s0 = (int)(start * Rate);
            float lp = 0f;
            for (int i = s0; i < b.Length; i++)
            {
                float t = (i - s0) / (float)Rate;
                if (t > 0.05f) break;
                float n = Noise();
                lp += 0.4f * (n - lp);
                b[i] += amp * ((n - lp) * 0.5f * Mathf.Exp(-t / 0.0012f)
                    + 0.7f * Mathf.Sin(TwoPi * f * t) * Mathf.Exp(-t / 0.004f)
                    + 0.4f * Mathf.Sin(TwoPi * f * 0.37f * t) * Mathf.Exp(-t / 0.009f)) * Mathf.Min(1f, t / 0.0004f);
            }
        }

        // ═════════════════ 경매장 ═════════════════

        /// <summary>호텔 데스크 벨 '딩~' — 물건 경매 시작.</summary>
        static float[] DeskBell()
        {
            var b = Buf(1.8f);
            float f = 2349f; // D7 부근의 맑은 종
            float[] ratio = { 1f, 2.32f, 3.86f, 5.41f };
            float[] amp = { 1f, 0.45f, 0.22f, 0.10f };
            float[] dec = { 0.95f, 0.45f, 0.22f, 0.12f };
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float s = 0f;
                for (int k = 0; k < ratio.Length; k++) s += amp[k] * Mathf.Sin(TwoPi * f * ratio[k] * t) * Mathf.Exp(-t / dec[k]);
                s *= 1f + 0.06f * Mathf.Sin(TwoPi * 6.5f * t); // 종의 맥놀이
                float strike = Noise() * Mathf.Exp(-t / 0.002f) * 0.6f;
                b[i] = s * Mathf.Min(1f, t / 0.0015f) + strike;
            }
            return Finish(Reverb(b, 0.18f), 0.5f);
        }

        /// <summary>나무 '톡' — 누군가 입찰 (자주 나므로 작고 짧게).</summary>
        static float[] WoodTok()
        {
            var b = Buf(0.22f);
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float n = Noise();
                b[i] = 0.9f * Mathf.Sin(TwoPi * 1180f * t) * Mathf.Exp(-t / 0.028f)
                     + 0.45f * Mathf.Sin(TwoPi * 1830f * t) * Mathf.Exp(-t / 0.014f)
                     + 0.35f * Mathf.Sin(TwoPi * 520f * t) * Mathf.Exp(-t / 0.035f)
                     + 0.4f * n * Mathf.Exp(-t / 0.0025f);
            }
            return Finish(Reverb(b, 0.12f), 0.4f);
        }

        /// <summary>크게 뛴 입찰: 넓은 패들이 '탁!' (낮은 나무 울림 + 찰싹) 하고, 사람들이 짧게 '오~'.</summary>
        static float[] BigBid()
        {
            var b = Buf(1.3f);
            float ph = 0f, lp = 0f, lp2 = 0f;
            int n = (int)(0.35f * Rate);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float x = Noise();
                lp += 0.3f * (x - lp);
                lp2 += 0.04f * (x - lp2);
                float slap = (lp - lp2) * Mathf.Exp(-t / 0.011f);
                float wood = 0.9f * Mathf.Sin(TwoPi * 640f * t) * Mathf.Exp(-t / 0.035f)
                           + 0.5f * Mathf.Sin(TwoPi * 1010f * t) * Mathf.Exp(-t / 0.02f)
                           + 0.6f * Mathf.Sin(TwoPi * 290f * t) * Mathf.Exp(-t / 0.05f);
                ph += TwoPi * (70f + 60f * Mathf.Exp(-t / 0.02f)) / Rate;
                float thump = Mathf.Sin(ph) * Mathf.Exp(-t / 0.07f);
                b[i] += (1.3f * slap + 0.8f * wood + 0.9f * thump) * Mathf.Min(1f, t / 0.0006f);
            }
            b = Reverb(b, 0.18f);
            var ooh = new float[b.Length];
            OohInto(ooh, 0.07f, 1.2f, 5, 1f);
            ooh = Reverb(ooh, 0.3f);
            float k = 0.42f * Peak(b) / Peak(ooh);
            for (int i = 0; i < b.Length; i++) b[i] += k * ooh[i];
            return Finish(b, 0.55f);
        }

        /// <summary>의사봉 세 번 '땅, 땅, 땅!' — 낙찰. 마지막이 가장 세다.</summary>
        static float[] GavelKnocks()
        {
            var b = Buf(1.9f);
            float[] at = { 0f, 0.36f, 0.72f };
            float[] amp = { 0.75f, 0.8f, 1.1f };
            for (int k = 0; k < at.Length; k++) Knock(b, at[k], amp[k]);
            return Finish(Reverb(b, 0.22f), 0.6f);
        }

        /// <summary>의사봉 한 번 '땅!'.</summary>
        static float[] GavelSingle()
        {
            var b = Buf(1.0f);
            Knock(b, 0f, 1.1f);
            return Finish(Reverb(b, 0.22f), 0.6f);
        }

        /// <summary>의사봉 한 번: 아래로 떨어지는 둔탁한 울림 + 나무 몸통 + 딱 하는 순간음.</summary>
        static void Knock(float[] b, float at, float amp)
        {
            int s0 = (int)(at * Rate);
            float ph = 0f, lp = 0f;
            for (int i = s0; i < b.Length; i++)
            {
                float t = (i - s0) / (float)Rate;
                if (t > 0.5f) break;
                float f = 95f + 70f * Mathf.Exp(-t / 0.02f);
                ph += TwoPi * f / Rate;
                float n = Noise();
                lp += 0.25f * (n - lp);
                float thump = Mathf.Sin(ph) * Mathf.Exp(-t / 0.075f);
                float body = Mathf.Sin(TwoPi * 410f * t) * Mathf.Exp(-t / 0.05f) + 0.5f * Mathf.Sin(TwoPi * 690f * t) * Mathf.Exp(-t / 0.03f);
                float crack = (n - lp) * Mathf.Exp(-t / 0.006f) + 0.6f * Mathf.Sin(TwoPi * 1500f * t) * Mathf.Exp(-t / 0.008f);
                b[i] += amp * (1.0f * thump + 0.55f * body + 0.6f * crack) * Mathf.Min(1f, t / 0.0008f);
            }
        }

        /// <summary>부드럽게 내려가는 두 음 — 유찰.</summary>
        static float[] Unsold()
        {
            var b = Buf(0.9f);
            Tone(b, 0.00f, 659.3f, 0.22f, 0.5f);  // E5
            Tone(b, 0.18f, 523.3f, 0.35f, 0.55f); // C5
            Tone(b, 0.18f, 392.0f, 0.35f, 0.25f); // G4
            return Finish(Reverb(b, 0.15f), 0.4f);
        }

        /// <summary>시계 초침 '똑' — 마감 5초 전부터.</summary>
        static float[] ClockTick()
        {
            var b = Buf(0.09f);
            float lp = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float n = Noise();
                lp += 0.5f * (n - lp);
                b[i] = 0.8f * (n - lp) * Mathf.Exp(-t / 0.0018f)
                     + 0.5f * Mathf.Sin(TwoPi * 2900f * t) * Mathf.Exp(-t / 0.006f)
                     + 0.45f * Mathf.Sin(TwoPi * 950f * t) * Mathf.Exp(-t / 0.014f);
            }
            return Finish(b, 0.38f);
        }

        /// <summary>더 높고 또렷한 '딱' — 마지막 2초.</summary>
        static float[] ClockTickHigh()
        {
            var b = Buf(0.1f);
            float lp = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float n = Noise();
                lp += 0.5f * (n - lp);
                b[i] = 0.8f * (n - lp) * Mathf.Exp(-t / 0.0016f)
                     + 0.55f * Mathf.Sin(TwoPi * 4200f * t) * Mathf.Exp(-t / 0.007f)
                     + 0.5f * Mathf.Sin(TwoPi * 1580f * t) * Mathf.Exp(-t / 0.018f);
            }
            return Finish(b, 0.45f);
        }

        /// <summary>수정 '팅~' — 가치 하나 공개.</summary>
        static float[] CrystalTing()
        {
            var b = Buf(1.2f);
            Bell(b, 0.00f, 1568.0f, 0.55f, 0.55f); // G6
            Bell(b, 0.05f, 2349.3f, 0.45f, 0.40f); // D7
            return Finish(Reverb(b, 0.2f), 0.4f);
        }

        /// <summary>반짝이며 빠르게 올라가는 종 + 높은 화음 + 반짝이 — 공개된 가치가 값보다 훨씬 높다 (횡재).</summary>
        static float[] RevealGood()
        {
            var b = Buf(1.8f);
            float[] notes = { 1046.5f, 1318.5f, 1568.0f, 2093.0f, 2637.0f, 3136.0f }; // C6 E6 G6 C7 E7 G7
            for (int k = 0; k < notes.Length; k++) Bell(b, k * 0.05f, notes[k], 0.42f, 0.3f + 0.04f * k);
            Tone(b, 0.30f, 2093.0f, 0.55f, 0.22f);
            Tone(b, 0.30f, 2637.0f, 0.50f, 0.18f);
            Tone(b, 0.30f, 3136.0f, 0.45f, 0.16f);
            // 반짝이: 작고 높은 핑 여러 개
            for (int k = 0; k < 14; k++) Bell(b, 0.22f + 0.9f * Rand(), 3500f + 4000f * Rand(), 0.05f + 0.06f * Rand(), 0.06f + 0.05f * Rand());
            // 올라가는 바람
            var bp = new Svf();
            int n = (int)(0.45f * Rate);
            for (int i = 0; i < n; i++)
            {
                float x = i / (float)n;
                if ((i & 15) == 0) bp.Set(1500f + 5000f * x, 1.2f);
                b[i] += 0.12f * bp.Run(Noise()) * Mathf.Sin(Mathf.PI * x);
            }
            return Finish(Reverb(b, 0.28f), 0.5f);
        }

        /// <summary>우스꽝스러운 약음기 트롬본 '와-와-와-와아~' — 손해 본 거래.</summary>
        static float[] RevealBad()
        {
            var b = Buf(2.3f);
            float[] at = { 0f, 0.3f, 0.6f, 0.9f };
            int[] midi = { 58, 57, 56, 55 }; // Bb3 A3 Ab3 G3
            float[] len = { 0.25f, 0.25f, 0.25f, 1.05f };
            for (int k = 0; k < at.Length; k++) Trombone(b, at[k], Hz(midi[k]), len[k], k == at.Length - 1);
            return Finish(Reverb(b, 0.18f), 0.5f);
        }

        /// <summary>약음기(플런저) 트롬본 한 음: 톱니파 배음을 대역 필터로 '와' 하고 연다. 마지막 음은 떨며 처진다.</summary>
        static void Trombone(float[] b, float start, float f, float len, bool last)
        {
            var wah = new Svf();
            int s0 = (int)(start * Rate);
            const float rel = 0.08f;
            float ph = 0f;
            for (int i = s0; i < b.Length; i++)
            {
                float t = (i - s0) / (float)Rate;
                if (t > len + rel) break;
                float env = Mathf.Min(1f, t / 0.025f) * (t < len ? 1f : 1f - (t - len) / rel);
                float semi = -0.4f * Mathf.Exp(-t / 0.03f); // 살짝 아래에서 올라 붙기
                float open = Mathf.Min(1f, t / 0.12f);
                if (last)
                {
                    float depth = Mathf.Min(1f, t / 0.35f);
                    semi += 0.35f * depth * Mathf.Sin(TwoPi * 5f * t) - 1.2f * Mathf.Max(0f, (t - len + 0.35f) / 0.35f);
                    open = 0.5f + 0.5f * open * (0.6f + 0.4f * Mathf.Sin(TwoPi * 5f * t - 1.2f));
                }
                float fm = f * Mathf.Exp(semi * 0.05776227f);
                ph += TwoPi * fm / Rate;
                if (ph > TwoPi) ph -= TwoPi;
                float s = 0f;
                for (int n = 1; n <= 12; n++)
                {
                    if (fm * n > 9000f) break;
                    s += Mathf.Sin(n * ph) / n;
                }
                if (((i - s0) & 15) == 0) wah.Set(300f + 1000f * open, 2.5f);
                b[i] += env * (0.8f * wah.Run(s) + 0.12f * s * open);
            }
        }

        /// <summary>금관 팡파르 — 최종 순위 발표. 솔-도-미-솔, 이어서 도 장화음.</summary>
        static float[] Fanfare()
        {
            var b = Buf(3.0f);
            Brass(b, 0.00f, 0.16f, 392.0f, 0.7f);
            Brass(b, 0.15f, 0.16f, 523.3f, 0.75f);
            Brass(b, 0.30f, 0.16f, 659.3f, 0.8f);
            Brass(b, 0.45f, 0.26f, 784.0f, 0.9f);
            foreach (float f in new[] { 523.3f, 659.3f, 784.0f, 1046.5f }) Brass(b, 0.78f, 1.5f, f, 0.55f);
            Brass(b, 0.78f, 1.5f, 261.6f, 0.5f);
            // 심벌 같은 은은한 쉬익
            float lp = 0f;
            for (int i = (int)(0.78f * Rate); i < b.Length; i++)
            {
                float t = i / (float)Rate - 0.78f;
                float n = Noise();
                lp += 0.6f * (n - lp);
                b[i] += 0.12f * (n - lp) * Mathf.Exp(-t / 0.9f) * Mathf.Min(1f, t / 0.01f);
            }
            return Finish(Reverb(b, 0.25f), 0.58f);
        }

        /// <summary>반짝이며 올라가는 종소리 — 누군가 능력을 썼다.</summary>
        static float[] AbilityShimmer()
        {
            var b = Buf(1.3f);
            float[] notes = { 1318.5f, 1568.0f, 1975.5f, 2637.0f, 3136.0f };
            for (int k = 0; k < notes.Length; k++) Bell(b, k * 0.045f, notes[k], 0.35f, 0.32f + 0.05f * k);
            // 은은한 쉬익 (마법 느낌)
            float lp = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float n = Noise();
                lp += 0.5f * (n - lp);
                b[i] += 0.08f * (n - lp) * Mathf.Sin(Mathf.PI * Mathf.Min(1f, t / 0.5f)) * Mathf.Exp(-t / 0.4f);
            }
            return Finish(Reverb(b, 0.25f), 0.42f);
        }

        /// <summary>말풍선 '띵동' — 판매자 한마디.</summary>
        static float[] SayChime()
        {
            var b = Buf(0.9f);
            Tone(b, 0.00f, 880.0f, 0.18f, 0.55f);  // A5
            Tone(b, 0.11f, 1174.7f, 0.30f, 0.6f);  // D6
            return Finish(Reverb(b, 0.18f), 0.4f);
        }

        /// <summary>부드러운 바람 '휘익' — 카메라 이동·카드 미끄러짐.</summary>
        /// <summary>던질 때 짧은 '휙'.</summary>
        static float[] ThrowSwish()
        {
            var b = Buf(0.28f);
            var bp = new Svf();
            for (int i = 0; i < b.Length; i++)
            {
                float x = i / (float)b.Length;
                float shape = Mathf.Sin(Mathf.PI * Mathf.Pow(x, 0.5f)) * (1f - x);
                if ((i & 15) == 0) bp.Set(900f + 2600f * (1f - x), 1.2f);
                b[i] = shape * bp.Run(Noise());
            }
            return Finish(b, 0.45f);
        }

        /// <summary>둔하게 '퍽' — 맞거나 탁자에 떨어질 때.</summary>
        static float[] Thud()
        {
            var b = Buf(0.3f);
            float lp = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float n = Noise();
                lp += 0.08f * (n - lp);
                float f = 95f + 70f * Mathf.Exp(-t / 0.02f);
                b[i] = 0.9f * Mathf.Sin(TwoPi * f * t) * Mathf.Exp(-t / 0.06f) + 0.8f * lp * Mathf.Exp(-t / 0.03f);
            }
            return Finish(b, 0.5f);
        }

        /// <summary>물렁한 것이 '철퍽' (토마토·빵).</summary>
        static float[] Splat()
        {
            var b = Buf(0.42f);
            var bp = new Svf();
            float lp = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                if ((i & 15) == 0) bp.Set(1800f * Mathf.Exp(-t / 0.08f) + 250f, 2.2f);
                float n = Noise();
                lp += 0.1f * (n - lp);
                float env = Mathf.Min(1f, t / 0.002f) * Mathf.Exp(-t / 0.09f);
                b[i] = env * (bp.Run(n) * 0.9f + lp * 0.7f) + 0.5f * Mathf.Sin(TwoPi * (140f - 60f * t) * t) * Mathf.Exp(-t / 0.05f);
            }
            return Finish(b, 0.5f);
        }

        /// <summary>감정 표현 '뽁'.</summary>
        static float[] Pop()
        {
            var b = Buf(0.16f);
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float f = 520f + 900f * Mathf.Exp(-t / 0.02f);
                b[i] = Mathf.Sin(TwoPi * f * t) * Mathf.Exp(-t / 0.035f) * Mathf.Min(1f, t / 0.001f);
            }
            return Finish(b, 0.38f);
        }

        /// <summary>신문지로 '찰싹'.</summary>
        static float[] Slap()
        {
            var b = Buf(0.25f);
            var bp = new Svf();
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                if ((i & 15) == 0) bp.Set(2600f - 1500f * Mathf.Clamp01(t / 0.08f), 1.4f);
                b[i] = bp.Run(Noise()) * Mathf.Min(1f, t / 0.0008f) * Mathf.Exp(-t / 0.035f) + 0.4f * Mathf.Sin(TwoPi * 180f * t) * Mathf.Exp(-t / 0.02f);
            }
            return Finish(Reverb(b, 0.08f), 0.5f);
        }

        /// <summary>채팅이 왔을 때 작은 '톡'.</summary>
        static float[] ChatBlip()
        {
            var b = Buf(0.18f);
            Mallet(b, 0.0f, 1568f, 0.12f, 0.5f);
            Mallet(b, 0.05f, 2093f, 0.1f, 0.4f);
            return Finish(b, 0.3f);
        }

        static float[] Whoosh()
        {
            const float len = 0.6f;
            var b = Buf(len);
            var bp = new Svf();
            float lp = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                float x = i / (float)b.Length;
                float shape = Mathf.Sin(Mathf.PI * Mathf.Pow(x, 0.8f));
                if ((i & 15) == 0) bp.Set(280f + 1400f * shape * shape, 0.8f);
                float n = Noise();
                lp += 0.04f * (n - lp);
                b[i] = shape * shape * (bp.Run(n) + 0.9f * lp);
            }
            return Finish(b, 0.4f);
        }

        /// <summary>카드를 촤르륵 넘기다 '탁' 내려놓는 소리 — 등록 화면.</summary>
        static float[] Cards()
        {
            var b = Buf(0.7f);
            float t = 0.01f;
            for (int k = 0; k < 15; k++)
            {
                Flick(b, t, (0.35f + 0.25f * Rand()) * (0.6f + 0.4f * k / 15f), 2600f + 3200f * Rand(), 0.0035f);
                t += 0.03f - 0.0009f * k + 0.004f * Rand();
            }
            // 마지막에 카드 묶음을 '탁' 내려놓음: 넓은 대역 + 낮은 몸통
            t += 0.05f;
            Flick(b, t, 1.0f, 1500f, 0.008f);
            int s0 = (int)(t * Rate);
            for (int i = s0; i < b.Length; i++)
            {
                float tt = (i - s0) / (float)Rate;
                if (tt > 0.1f) break;
                b[i] += 0.5f * Mathf.Sin(TwoPi * 190f * tt) * Mathf.Exp(-tt / 0.02f) * Mathf.Min(1f, tt / 0.0005f);
            }
            return Finish(Reverb(b, 0.08f), 0.45f);
        }

        /// <summary>종이 한 장이 스치는 짧은 '팍'.</summary>
        static void Flick(float[] b, float start, float amp, float fc, float decay)
        {
            var f = new Svf().Set(fc, 0.9f);
            int s0 = (int)(start * Rate);
            for (int i = s0; i < b.Length; i++)
            {
                float t = (i - s0) / (float)Rate;
                if (t > decay * 8f) break;
                float env = Mathf.Min(1f, t / 0.0006f) * Mathf.Exp(-t / decay);
                float y = f.Run(Noise());
                b[i] += amp * env * (y + 0.25f * f.High);
            }
        }

        /// <summary>동전 더미가 '짤랑' — 돈이 오갈 때.</summary>
        static float[] Coins()
        {
            var b = Buf(1.0f);
            float t = 0.005f;
            for (int k = 0; k < 16; k++)
            {
                float x = k / 16f;
                Clink(b, t, 2600f + 3600f * Rand(), (1f - 0.6f * x) * (0.5f + 0.5f * Rand()));
                t += 0.01f + 0.05f * x * Rand() + 0.012f * Rand();
            }
            return Finish(Reverb(b, 0.12f), 0.45f);
        }

        static readonly float[] ClinkRatio = { 1f, 1.58f, 2.37f, 3.11f, 4.02f };
        static readonly float[] ClinkAmp = { 1f, 0.6f, 0.45f, 0.3f, 0.18f };

        /// <summary>동전 하나가 부딪는 '찡': 비조화 배음 몇 개 + 딸깍.</summary>
        static void Clink(float[] b, float start, float f, float amp)
        {
            int s0 = (int)(start * Rate);
            float dec = 0.05f + 0.07f * Rand();
            float ph = Rand() * TwoPi;
            for (int i = s0; i < b.Length; i++)
            {
                float t = (i - s0) / (float)Rate;
                if (t > dec * 7f) break;
                float s = 0f;
                for (int k = 0; k < ClinkRatio.Length; k++)
                {
                    float fk = f * ClinkRatio[k];
                    if (fk > 16000f) break;
                    s += ClinkAmp[k] * Mathf.Sin(TwoPi * fk * t + ph * k) * Mathf.Exp(-t * (1f + 0.5f * k) / dec);
                }
                s += 0.6f * Noise() * Mathf.Exp(-t / 0.0008f);
                b[i] += amp * s * Mathf.Min(1f, t / 0.0003f);
            }
        }

        /// <summary>숫자가 올라가는 동안 빠르게 되풀이하는 아주 짧은 '틱'.</summary>
        static float[] CountTick()
        {
            var b = Buf(0.03f);
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                b[i] = (0.7f * Mathf.Sin(TwoPi * 1900f * t) * Mathf.Exp(-t / 0.003f)
                      + 0.4f * Mathf.Sin(TwoPi * 950f * t) * Mathf.Exp(-t / 0.006f)) * Mathf.Min(1f, t / 0.0005f);
            }
            return Finish(b, 0.32f);
        }

        /// <summary>작은북 롤 2.5초: 작게 시작해 점점 커지고 조금씩 빨라진다. 마지막 타격 없이 끝난다 (이어서 sfx_cymbal).</summary>
        static float[] Drumroll()
        {
            const float len = 2.5f;
            var b = Buf(len + 0.2f);
            float t = 0f;
            int k = 0;
            while (t < len)
            {
                float x = t / len;
                float vel = (0.1f + 0.9f * Mathf.Pow(x, 1.7f)) * (k % 2 == 0 ? 1f : 0.84f) * (0.88f + 0.24f * Rand());
                SnareStroke(b, t + 0.0015f * Rand(), vel, 0.04f + 0.02f * x);
                t += 1f / (21f + 7f * x);
                k++;
            }
            return Finish(Reverb(b, 0.15f), 0.55f);
        }

        /// <summary>작은북 한 번: 울림판 + 스네어 줄 잡음 + 스틱 딸깍.</summary>
        static void SnareStroke(float[] b, float start, float vel, float wire)
        {
            int s0 = (int)(start * Rate);
            var f = new Svf().Set(3200f, 0.6f); // 스네어 줄 소리는 3kHz 부근에 모인다
            for (int i = s0; i < b.Length; i++)
            {
                float t = (i - s0) / (float)Rate;
                if (t > wire * 5f) break;
                float band = f.Run(Noise());
                float wires = 1.3f * band * Mathf.Exp(-t / wire);
                float head = (0.7f * Mathf.Sin(TwoPi * 185f * t) + 0.35f * Mathf.Sin(TwoPi * 332f * t)) * Mathf.Exp(-t / 0.016f);
                float stick = (band + f.Low) * Mathf.Exp(-t / 0.0012f);
                b[i] += vel * (0.8f * wires + 0.6f * head + 0.5f * stick) * Mathf.Min(1f, t / 0.0003f);
            }
        }

        /// <summary>심벌 '쾅~' (큰북·작은북과 함께) — 드럼롤의 끝.</summary>
        static float[] Cymbal()
        {
            var b = Buf(3.0f);
            int len = b.Length;
            // 금속판의 비조화 배음 수십 개 (회전 페이저로 빠르게)
            for (int k = 0; k < 44; k++)
            {
                double f = 420.0 * Math.Pow(26.0, rng.NextDouble());
                double w = 2.0 * Math.PI * f / Rate;
                double c = Math.Cos(w), s = Math.Sin(w), ph = rng.NextDouble() * 2.0 * Math.PI;
                double re = Math.Cos(ph), im = Math.Sin(ph);
                float amp = 0.11f * (0.5f + 0.5f * Rand()) * Mathf.Pow((float)(f / 420.0), -0.2f);
                float dec = (0.6f + 1.4f * Rand()) * Mathf.Pow((float)(f / 3000.0), -0.3f);
                float e = 1f, ed = Mathf.Exp(-1f / (dec * Rate));
                for (int i = 0; i < len && e > 1e-4f; i++)
                {
                    b[i] += amp * e * (float)im;
                    double nr = re * c - im * s;
                    im = re * s + im * c;
                    re = nr;
                    e *= ed;
                }
            }
            // 쉬익: 처음엔 밝게 터지고 길게 사라지는 고역 잡음
            float lp = 0f;
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)Rate;
                float x = Noise();
                lp += 0.45f * (x - lp);
                b[i] += (x - lp) * (0.55f * Mathf.Exp(-t / 1.0f) + 1.2f * Mathf.Exp(-t / 0.04f));
                b[i] *= Mathf.Min(1f, t / 0.0006f);
            }
            Kick(b, 0f, 0.9f, 0.22f);
            SnareStroke(b, 0f, 0.9f, 0.09f);
            return Finish(Reverb(b, 0.22f), 0.55f);
        }

        /// <summary>금전등록기 '카-칭!' — 대출. 레버 철컥 · 서랍 드르륵 · 종 · 동전.</summary>
        static float[] Register()
        {
            var b = Buf(1.4f);
            Clank(b, 0.000f, 1.0f, 2100f);  // 레버 '철'
            Clank(b, 0.045f, 0.8f, 1400f);  // '컥'
            // 서랍이 굴러 나오는 '드르륵'
            float lp = 0f;
            int s0 = (int)(0.07f * Rate), s1 = (int)(0.24f * Rate);
            for (int i = s0; i < s1; i++)
            {
                float x = (i - s0) / (float)(s1 - s0);
                lp += 0.06f * (Noise() - lp);
                float rattle = 0.5f + 0.5f * Mathf.Sin(TwoPi * 38f * i / Rate);
                b[i] += 0.9f * lp * rattle * Mathf.Sin(Mathf.PI * x);
            }
            Clank(b, 0.245f, 0.7f, 900f);     // 서랍이 멈추는 '턱'
            RegisterBell(b, 0.11f, 2637f, 1f); // '칭!' (E7)
            Clink(b, 0.27f, 4300f, 0.22f);
            Clink(b, 0.33f, 3700f, 0.18f);
            Clink(b, 0.40f, 5000f, 0.12f);
            return Finish(Reverb(b, 0.14f), 0.55f);
        }

        /// <summary>금속 기계 '철컥': 좁은 대역 잡음 + 금속 울림 + 낮은 쿵.</summary>
        static void Clank(float[] b, float start, float amp, float f)
        {
            var bp = new Svf().Set(f, 3f);
            int s0 = (int)(start * Rate);
            for (int i = s0; i < b.Length; i++)
            {
                float t = (i - s0) / (float)Rate;
                if (t > 0.08f) break;
                b[i] += amp * (1.2f * bp.Run(Noise()) * Mathf.Exp(-t / 0.009f)
                    + 0.5f * Mathf.Sin(TwoPi * f * 0.43f * t) * Mathf.Exp(-t / 0.015f)
                    + 0.6f * Mathf.Sin(TwoPi * 120f * t) * Mathf.Exp(-t / 0.025f)) * Mathf.Min(1f, t / 0.0004f);
            }
        }

        /// <summary>금전등록기 종 '칭'.</summary>
        static void RegisterBell(float[] b, float start, float f, float amp)
        {
            float[] ratio = { 1f, 2.02f, 2.93f, 4.17f };
            float[] a = { 1f, 0.5f, 0.35f, 0.15f };
            float[] dec = { 0.75f, 0.42f, 0.25f, 0.12f };
            int s0 = (int)(start * Rate);
            for (int i = s0; i < b.Length; i++)
            {
                float t = (i - s0) / (float)Rate;
                float s = 0f;
                for (int k = 0; k < ratio.Length; k++) s += a[k] * Mathf.Sin(TwoPi * f * ratio[k] * t) * Mathf.Exp(-t / dec[k]);
                s *= 1f + 0.08f * Mathf.Sin(TwoPi * 7f * t);
                s += 0.5f * Noise() * Mathf.Exp(-t / 0.0015f);
                b[i] += amp * s * Mathf.Min(1f, t / 0.0008f);
            }
        }

        /// <summary>짧은 승리 팡파르 (약 2초) — 1등. 도-미-솔 하고 도 장화음 + 팀파니 + 종.</summary>
        static float[] Win()
        {
            var b = Buf(2.2f);
            Brass(b, 0.00f, 0.09f, 523.3f, 0.65f); // C5
            Brass(b, 0.11f, 0.09f, 659.3f, 0.7f);  // E5
            Brass(b, 0.22f, 0.09f, 784.0f, 0.75f); // G5
            foreach (float f in new[] { 523.3f, 659.3f, 784.0f, 1046.5f }) Brass(b, 0.34f, 1.05f, f, 0.5f);
            Brass(b, 0.34f, 1.05f, 261.6f, 0.45f);
            Brass(b, 0.34f, 1.05f, 130.8f, 0.3f);
            Timpani(b, 0.34f, 130.8f, 0.9f);
            Bell(b, 0.34f, 2093.0f, 0.5f, 0.22f);
            Bell(b, 0.40f, 2637.0f, 0.45f, 0.2f);
            Bell(b, 0.46f, 3136.0f, 0.4f, 0.18f);
            float lp = 0f;
            int s0 = (int)(0.34f * Rate);
            for (int i = s0; i < b.Length; i++)
            {
                float t = (i - s0) / (float)Rate;
                float n = Noise();
                lp += 0.6f * (n - lp);
                b[i] += 0.1f * (n - lp) * Mathf.Exp(-t / 0.7f) * Mathf.Min(1f, t / 0.01f);
            }
            return Finish(Reverb(b, 0.25f), 0.58f);
        }

        /// <summary>팀파니 한 번: 둥근 기음 + 약한 위 배음 + 말렛 잡음.</summary>
        static void Timpani(float[] b, float start, float f, float amp)
        {
            int s0 = (int)(start * Rate);
            float lp = 0f;
            for (int i = s0; i < b.Length; i++)
            {
                float t = (i - s0) / (float)Rate;
                if (t > 1.5f) break;
                float w = TwoPi * f * t;
                lp += 0.2f * (Noise() - lp);
                float s = Mathf.Sin(w) * Mathf.Exp(-t / 0.55f) + 0.5f * Mathf.Sin(1.5f * w) * Mathf.Exp(-t / 0.3f)
                        + 0.3f * Mathf.Sin(1.99f * w) * Mathf.Exp(-t / 0.2f) + 1.2f * lp * Mathf.Exp(-t / 0.01f);
                b[i] += amp * s * Mathf.Min(1f, t / 0.001f);
            }
        }

        /// <summary>짧고 부드러운 아쉬움 (약 2초) — 순위가 낮을 때. 비브라폰이 천천히 내려와 A단조 화음에 머문다.</summary>
        static float[] Lose()
        {
            var b = Buf(2.3f);
            Vibe(b, 0.00f, 76, 0.24f, 0.7f);  // E5
            Vibe(b, 0.26f, 74, 0.24f, 0.62f); // D5
            Vibe(b, 0.52f, 71, 0.24f, 0.56f); // B4
            EpChord(b, 0.78f, new[] { 55, 60, 64, 69 }, 1.0f, 0.55f); // Am7 (G C E A)
            Bass(b, 0.78f, 45, 1.0f, 0.55f);  // A2
            Vibe(b, 0.80f, 69, 0.9f, 0.5f);   // A4
            return Finish(Reverb(b, 0.32f), 0.5f);
        }

        // ═════════════════ 군중 ═════════════════

        /// <summary>작은 군중(8명) '오~': 'o'에서 'u'로 입을 오므리며 음높이가 올랐다 내려간다. 사람마다 시작·높이·떨림이 다르다.</summary>
        static float[] CrowdOoh()
        {
            var b = Buf(1.7f);
            OohInto(b, 0.01f, 1.65f, 8, 1f);
            return Finish(Reverb(b, 0.3f), 0.5f);
        }

        static void OohInto(float[] b, float start, float len, int people, float gain)
        {
            for (int p = 0; p < people; p++)
            {
                bool female = (p % 2 == 1) ^ (Rand() < 0.2f);
                var v = new Voice
                {
                    Pitch = female ? 195f + 70f * Rand() : 98f + 50f * Rand(),
                    Formant = female ? 1.13f + 0.08f * Rand() : 0.92f + 0.08f * Rand(),
                    Breath = 0.25f + 0.2f * Rand(),
                    Open = 0.7f,
                };
                var sp = new Speaker(v);
                float delay = 0.2f * Rand() * Rand() + 0.01f * p; // 대부분 함께, 몇 명은 늦게
                float dur = len - delay - 0.15f - 0.25f * Rand();
                float rise = 2f + 3f * Rand(), fall = 4f + 4f * Rand(), peakAt = 0.25f + 0.15f * Rand();
                float vibRate = 4.5f + 1.5f * Rand(), vibDepth = 0.1f + 0.25f * Rand();
                float amp0 = 0.6f + 0.4f * Rand();
                int s0 = (int)((start + delay) * Rate);
                int n = (int)(dur * Rate);
                for (int i = 0; i < n && s0 + i < b.Length; i++)
                {
                    float t = i / (float)Rate, x = t / dur;
                    float semi = x < peakAt ? rise * Smooth(x / peakAt) : rise - (rise + fall) * Smooth((x - peakAt) / (1f - peakAt));
                    semi += vibDepth * Mathf.Sin(TwoPi * vibRate * t) * Mathf.Min(1f, t / 0.3f);
                    float amp = Smooth(Mathf.Min(1f, t / 0.14f)) * (x < 0.35f ? 1f : Mathf.Pow(1f - (x - 0.35f) / 0.65f, 1.3f));
                    float o2u = Mathf.Clamp01(x / 0.25f);
                    float f1 = Mathf.Lerp(540f, 330f, o2u), f2 = Mathf.Lerp(880f, 760f, o2u), f3 = Mathf.Lerp(2450f, 2250f, o2u);
                    b[s0 + i] += gain * amp0 * sp.Next(v.Pitch * Semi(semi), amp, f1, f2, f3, 0.15f * amp, 0f, 3000f);
                }
            }
        }

        /// <summary>여럿이 짧게 숨을 들이켜는 '헉!' (약 0.6초).</summary>
        static float[] Gasp()
        {
            var b = Buf(0.62f);
            for (int p = 0; p < 6; p++)
            {
                bool female = p % 2 == 0;
                var v = new Voice { Pitch = female ? 260f + 80f * Rand() : 140f + 50f * Rand(), Formant = female ? 1.15f : 0.97f, Open = 0.7f };
                var sp = new Speaker(v);
                float delay = 0.06f * Rand();
                float peakT = 0.07f + 0.05f * Rand(), decay = 0.08f + 0.06f * Rand();
                float voicedAmt = p < 2 ? 0.25f : 0f; // 두 명은 '읍!' 하고 목소리가 살짝 섞인다
                float amp0 = 0.6f + 0.4f * Rand();
                int s0 = (int)(delay * Rate);
                int n = (int)(0.5f * Rate);
                for (int i = 0; i < n && s0 + i < b.Length; i++)
                {
                    float t = i / (float)Rate;
                    float env = t < peakT ? Smooth(t / peakT) : Mathf.Exp(-(t - peakT) / decay);
                    // 들이쉬는 숨: 'a'처럼 벌린 입, 포먼트가 조금씩 올라간다
                    float x = Mathf.Min(1f, t / 0.3f);
                    b[s0 + i] += amp0 * sp.Next(v.Pitch * (1f + 0.15f * x), env * voicedAmt, 650f + 150f * x, 1150f + 500f * x, 2500f + 300f * x, env, env * 0.15f, 3500f);
                }
            }
            return Finish(Reverb(b, 0.2f), 0.5f);
        }

        /// <summary>예의 바른 박수 (약 3초): 사람마다 손바닥 소리 높이·빠르기가 다르고, 금방 커졌다 하나둘 멈춘다.</summary>
        static float[] Applause()
        {
            var b = Buf(3.2f);
            var person = new float[b.Length];
            for (int p = 0; p < 22; p++)
            {
                Array.Clear(person, 0, person.Length);
                float fc = 900f + 1800f * Rand(), q = 1.0f + 1.2f * Rand();
                float rate = 3.3f + 2.2f * Rand();          // 초당 박수
                float t = 0.02f + 0.35f * Rand() * Rand();  // 대부분 곧바로 시작
                float stop = 1.7f + 1.2f * Rand();          // 그만두는 시각
                float loud = 0.5f + 0.5f * Rand();
                while (t < stop)
                {
                    float x = t / stop;
                    float a = loud * Mathf.Min(1f, (t + 0.1f) / 0.4f) * (1f - 0.65f * x * x) * (0.8f + 0.4f * Rand());
                    Clap(person, t, a);
                    t += (1f / rate) * (0.88f + 0.24f * Rand());
                }
                var f = new Svf().Set(fc, q);
                for (int i = 0; i < b.Length; i++) b[i] += f.Run(person[i]) + 0.12f * person[i];
            }
            // 거친 고역을 덜어 부드럽게 (방 안에서 듣는 박수)
            var soft = new Svf().Set(6000f, 0.7f);
            for (int i = 0; i < b.Length; i++) { soft.Run(b[i]); b[i] = soft.Low; }
            return Finish(Reverb(b, 0.3f), 0.5f);
        }

        /// <summary>손바닥이 맞부딪는 순간: 아주 짧은 잡음 두 겹.</summary>
        static void Clap(float[] b, float start, float amp)
        {
            int s0 = (int)(start * Rate);
            int n = (int)(0.03f * Rate);
            for (int i = 0; i < n && s0 + i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float e = Mathf.Exp(-t / 0.0045f) + (t > 0.002f ? 0.4f * Mathf.Exp(-(t - 0.002f) / 0.003f) : 0f);
                b[s0 + i] += amp * Noise() * e * Mathf.Min(1f, t / 0.0003f);
            }
        }

        /// <summary>가벼운 웃음 몇 사람 '흐흐·하하' (약 1.5초).</summary>
        static float[] Laugh()
        {
            var b = Buf(1.6f);
            for (int p = 0; p < 4; p++)
            {
                bool female = p % 2 == 1;
                var v = new Voice { Pitch = female ? 230f + 60f * Rand() : 115f + 35f * Rand(), Formant = female ? 1.16f : 0.95f, Breath = 0.3f, Open = 0.65f };
                float start = p == 0 ? 0.02f : 0.05f + 0.4f * Rand();
                int pulses = 4 + rng.Next(4);
                float rate = 5.2f + 2f * Rand();
                char vowel = female ? (Rand() < 0.5f ? 'e' : 'i') : (Rand() < 0.5f ? 'a' : 'e');
                LaughInto(b, start, v, pulses, rate, vowel, 0.6f + 0.4f * Rand());
            }
            return Finish(Reverb(b, 0.25f), 0.5f);
        }

        /// <summary>'하' 하나 = 짧은 숨(h) + 빨리 사라지는 모음. 웃을수록 음높이가 내려가고 작아진다.</summary>
        static void LaughInto(float[] b, float start, Voice v, int pulses, float rate, char vowel, float gain)
        {
            var sp = new Speaker(v);
            int s0 = (int)(start * Rate);
            float pl = 1f / rate;
            int n = (int)((pulses * pl + 0.1f) * Rate);
            float f1, f2, f3;
            Formants(vowel, out f1, out f2, out f3);
            float va = 0f, aa = 0f;
            for (int i = 0; i < n && s0 + i < b.Length; i++)
            {
                float t = i / (float)Rate;
                int k = (int)(t / pl);
                float p = (t - k * pl) / pl;
                float voiced = 0f, asp = 0f;
                if (k < pulses)
                {
                    float fade = 1f - 0.55f * k / pulses;
                    if (p < 0.3f) asp = 0.8f * fade * Mathf.Sin(Mathf.PI * p / 0.3f);
                    if (p >= 0.22f) voiced = fade * Mathf.Exp(-(p - 0.22f) * 3.2f) * Mathf.Clamp01((0.92f - p) / 0.12f);
                }
                va += (voiced - va) * 0.01f;
                aa += (asp - aa) * 0.03f;
                float semi = 3.5f - 6f * Mathf.Clamp01(t / (pulses * pl)) + 1.5f * (1f - p);
                b[s0 + i] += gain * sp.Next(v.Pitch * Semi(semi), va, f1, f2, f3, aa, 0f, 3000f);
            }
        }

        // ═════════════════ 목소리 (말풍선 웅얼거림) ═════════════════

        /// <summary>
        /// 캐릭터 말풍선 목소리 8종 (동물의 숲·역전재판 식 웅얼거림, 약 1초):
        /// 0 굵은 바리톤 · 1 품위 있는 알토 · 2 빠른 콧소리 테너 · 3 떨리는 노교수 · 4 거만한 소프라노 공작부인 · 5 쉰 목소리 노인 · 6 속삭임 · 7 걸걸한 뱃사람.
        /// </summary>
        static float[] Speech(int idx)
        {
            Voice v;
            int syl;
            switch (idx)
            {
                case 0:
                    v = new Voice { Pitch = 92f, Range = 4f, Formant = 0.9f, Rate = 6f, Breath = 0.05f, Open = 0.62f, Legato = 0.5f, Vowels = "aaooue", Consonants = "bdgmnlrh" };
                    syl = 6; break;
                case 1:
                    v = new Voice { Pitch = 205f, Range = 5f, Formant = 1.16f, Rate = 7.2f, Vib = 0.15f, Breath = 0.14f, Open = 0.7f, Swoop = 1f, Vowels = "aeeiou", Consonants = "mnlsthdy" };
                    syl = 7; break;
                case 2:
                    v = new Voice { Pitch = 168f, Range = 5f, Formant = 1.06f, Rate = 9.5f, Nasal = 0.9f, Open = 0.45f, Legato = 0.3f, Consonant = 0.7f, Vowels = "aeiie", Consonants = "tkpsdnn" };
                    syl = 8; break;
                case 3:
                    v = new Voice { Pitch = 128f, Range = 4f, Formant = 0.97f, Rate = 5.2f, Vib = 0.6f, VibRate = 6.5f, Wobble = 0.5f, Breath = 0.12f, Creak = 0.15f, Vowels = "aeoou", Consonants = "hmbwlrd" };
                    syl = 5; break;
                case 4:
                    v = new Voice { Pitch = 320f, Range = 7f, Formant = 1.22f, Rate = 6.4f, Vib = 0.35f, VibRate = 5.8f, Breath = 0.08f, Open = 0.65f, Swoop = 2.5f, Legato = 0.55f, Vowels = "aaooe", Consonants = "hmlndr" };
                    syl = 6; break;
                case 5:
                    v = new Voice { Pitch = 98f, Range = 3f, Formant = 0.95f, Rate = 5.2f, Creak = 0.55f, Wobble = 0.3f, Breath = 0.2f, Open = 0.55f, Vowels = "aeou", Consonants = "hkgrbm" };
                    syl = 5; break;
                case 6:
                    v = new Voice { Pitch = 170f, Range = 3f, Formant = 1.08f, Rate = 7.5f, Whisper = 0.88f, Breath = 0.3f, Consonant = 0.7f, Vowels = "aeiou", Consonants = "shtkpfh" };
                    syl = 7; break;
                default:
                    v = new Voice { Pitch = 104f, Range = 5f, Formant = 0.93f, Rate = 6.7f, Growl = 0.45f, Creak = 0.25f, Breath = 0.15f, Open = 0.5f, Vowels = "aaoor", Consonants = "rgbkdhy" };
                    syl = 6; break;
            }
            var b = Buf(2.5f);
            float end = Speak(b, 0.02f, v, syl, 1f);
            return Finish(Reverb(Trim(b, end + 0.1f), 0.08f), 0.5f);
        }

        /// <summary>경매사의 빠른 가락 (약 1.5초): 규칙적인 강세로 '비디-비디-빗' 하고 굴리다 끝을 올린다.</summary>
        static float[] Auctioneer()
        {
            var v = new Voice { Pitch = 122f, Range = 3f, Formant = 0.98f, Rate = 12f, Open = 0.48f, Breath = 0.04f, Consonant = 0.8f, Legato = 0.6f, Chant = true, Vowels = "iiaaeo", Consonants = "bdgtkln" };
            var b = Buf(2.5f);
            float end = Speak(b, 0.02f, v, 15, 1f);
            return Finish(Reverb(Trim(b, end + 0.1f), 0.1f), 0.55f);
        }

        /// <summary>목소리 성격 (말풍선 캐릭터·군중).</summary>
        sealed class Voice
        {
            public float Pitch = 120f;      // 기본 음높이 (Hz)
            public float Range = 4f;        // 억양 폭 (반음)
            public float Formant = 1f;      // 포먼트 배율: 성도가 짧을수록(여성) 크다
            public float Rate = 6f;         // 초당 음절 수
            public float Vib;               // 떨림 폭 (반음)
            public float VibRate = 5.5f;    // 떨림 빠르기 (Hz)
            public float Wobble;            // 느리고 불규칙한 흔들림 (반음, 노인)
            public float Breath = 0.06f;    // 숨소리
            public float Whisper;           // 1에 가까울수록 성대 울림 없이 숨소리만 (속삭임)
            public float Nasal;             // 콧소리
            public float Creak;             // 쉰 목: 성문 주기마다 세기·길이가 들쭉날쭉
            public float Growl;             // 으르렁: 낮은 진폭 떨림
            public float Open = 0.6f;       // 성문 열림 비율 (작을수록 밝고 날카롭다)
            public float Swoop;             // 음절 안에서 미끄러지는 음높이 (반음)
            public float Consonant = 0.55f; // 음절 앞에 자음이 붙을 확률
            public float Legato = 0.4f;     // 음절을 끊지 않고 이을 확률
            public bool Chant;              // 경매사 가락: 강세가 규칙적으로 돌고 끝을 올린다
            public string Vowels = "aeiou";
            public string Consonants = "tkpbdgmnshl";
        }

        struct Syl { public float T, Dur, Accent, Swoop; public char V, C; public bool Gap; }
        static readonly float[] ChantAccent = { 1f, -0.35f, 0.25f, -0.5f };

        /// <summary>
        /// 알아들을 수 없는 말 한 마디. 음절마다 [자음] + 모음. 평서문 억양(처음이 높고 끝으로 갈수록 내려감)에
        /// 음절마다 강세가 오르내리고, 마지막 음절은 길게 끈다. 끝난 시각(초)을 돌려준다.
        /// </summary>
        static float Speak(float[] b, float start, Voice v, int syllables, float gain)
        {
            var plan = new Syl[syllables];
            float total = 0f;
            char prev = ' ';
            for (int k = 0; k < syllables; k++)
            {
                var s = new Syl();
                s.T = total;
                s.Dur = (0.78f + 0.44f * Rand()) / v.Rate;
                if (k == syllables - 1) s.Dur *= 1.6f;
                s.V = Pick(v.Vowels);
                if (s.V == prev) s.V = Pick(v.Vowels);
                s.C = (k == 0 ? Rand() < 0.75f : Rand() < v.Consonant) ? Pick(v.Consonants) : ' ';
                s.Gap = k == syllables - 1 || Rand() > v.Legato;
                s.Accent = v.Chant ? ChantAccent[k % ChantAccent.Length] : Rand() * 2f - 1f;
                s.Swoop = v.Swoop * (Rand() < 0.5f ? -1f : 1f);
                plan[k] = s;
                total += s.Dur;
                prev = s.V;
            }
            var sp = new Speaker(v);
            int s0 = (int)(start * Rate);
            int n = (int)((total + 0.06f) * Rate);
            float semi = v.Chant ? 0f : v.Range * 0.5f;
            float va = 0f, aa = 0f, fa = 0f; // 울림·숨·마찰 세기 (딸깍 소리가 나지 않게 부드럽게 따라감)
            float wob1 = Rand() * TwoPi, wob2 = Rand() * TwoPi;
            int cur = 0;
            for (int i = 0; i < n && s0 + i < b.Length; i++)
            {
                float tt = i / (float)Rate;
                while (cur < syllables - 1 && tt >= plan[cur + 1].T) cur++;
                var s = plan[cur];
                float u = tt - s.T;
                float cl = Mathf.Min(ConsonantLength(s.C), s.Dur * 0.45f);
                float f1, f2, f3;
                Formants(s.V, out f1, out f2, out f3);
                float voiced = 0f, asp = 0f, fr = 0f, frFc = 4000f;
                bool voicedCons = s.C != ' ' && "mnlrwybdg".IndexOf(s.C) >= 0;
                if (u < cl)
                {
                    float p = u / cl;
                    switch (s.C)
                    {
                        case 'p': case 't': case 'k': // 막힘 → 터짐 → 거센 숨
                            if (p > 0.4f && p < 0.62f) { fr = 0.9f; frFc = s.C == 'p' ? 1100f : s.C == 't' ? 4300f : 2300f; }
                            else if (p >= 0.62f) asp = 0.55f * (1f - (p - 0.62f) / 0.38f) + 0.1f;
                            break;
                        case 'b': case 'd': case 'g': // 막힌 채 낮게 울리다 약하게 터짐
                            if (p < 0.6f) { voiced = 0.18f; f1 = 250f; f2 = 900f; f3 = 2300f; }
                            else if (p < 0.78f) { fr = 0.45f; frFc = s.C == 'b' ? 1000f : s.C == 'd' ? 3600f : 2000f; voiced = 0.2f; }
                            else voiced = 0.5f;
                            break;
                        case 's': fr = 0.6f * Mathf.Sin(Mathf.PI * p); frFc = 6300f; break;
                        case 'f': fr = 0.4f * Mathf.Sin(Mathf.PI * p); frFc = 4800f; break;
                        case 'h': asp = 0.7f * Mathf.Min(1f, p * 3f); break;
                        case 'm': voiced = 0.5f; f1 = 280f; f2 = 1000f; f3 = 2200f; break;
                        case 'n': voiced = 0.5f; f1 = 280f; f2 = 1700f; f3 = 2600f; break;
                        case 'l': voiced = 0.7f; f1 = 360f; f2 = 1300f; f3 = 2800f; break;
                        case 'r': voiced = 0.7f; f1 = 420f; f2 = 1250f; f3 = 1650f; break;
                        case 'w': voiced = 0.6f; f1 = 300f; f2 = 650f; f3 = 2200f; break;
                        case 'y': voiced = 0.6f; f1 = 280f; f2 = 2250f; f3 = 3000f; break;
                    }
                }
                else if (u < s.Dur)
                {
                    float on = Mathf.Min(1f, (u - cl) / Mathf.Min(0.018f, 0.25f * s.Dur));
                    bool glide = s.C == ' ' && cur > 0 && !plan[cur - 1].Gap; // 앞 음절에서 끊지 않고 이어짐
                    voiced = voicedCons ? 0.55f + 0.45f * on : glide ? 0.8f + 0.2f * on : on;
                    if (s.Gap) voiced *= Mathf.Clamp01((s.Dur - u) / Mathf.Min(0.04f, 0.35f * s.Dur));
                    voiced *= 0.78f + 0.22f * (s.Accent * 0.5f + 0.5f); // 강세
                }
                // 음높이: 억양 + 강세 + 음절 안 미끄러짐 + 떨림
                float x = Mathf.Clamp01(tt / total);
                float target;
                if (v.Chant) target = v.Range * 0.5f * s.Accent + (cur == syllables - 1 ? v.Range * 1.2f * Mathf.Clamp01(u / s.Dur) : 0f);
                else target = v.Range * (0.55f - 0.9f * x) + v.Range * 0.55f * s.Accent;
                target += s.Swoop * (Mathf.Clamp01(u / s.Dur) - 0.4f);
                semi += (target - semi) * 0.0022f; // 약 10ms 에 걸쳐 따라간다
                float mod = v.Vib * Mathf.Sin(TwoPi * v.VibRate * tt)
                          + v.Wobble * (0.6f * Mathf.Sin(TwoPi * 2.1f * tt + wob1) + 0.4f * Mathf.Sin(TwoPi * 3.7f * tt + wob2));
                va += (voiced - va) * 0.012f;
                aa += (asp - aa) * 0.03f;
                fa += (fr - fa) * 0.05f;
                b[s0 + i] += gain * sp.Next(v.Pitch * Semi(semi + mod), va, f1, f2, f3, aa, fa, frFc);
            }
            return start + total + 0.06f;
        }

        static float ConsonantLength(char c)
        {
            switch (c)
            {
                case 'p': case 't': case 'k': return 0.055f;
                case 'b': case 'd': case 'g': return 0.04f;
                case 's': return 0.075f;
                case 'f': return 0.06f;
                case 'h': return 0.05f;
                case 'm': case 'n': return 0.05f;
                case 'l': case 'r': case 'w': case 'y': return 0.045f;
                default: return 0f;
            }
        }

        /// <summary>모음 포먼트 (성인 남성 기준 F1, F2, F3 Hz). 'r' 은 '어r', 그 밖은 중성 모음 '어'.</summary>
        static void Formants(char v, out float f1, out float f2, out float f3)
        {
            switch (v)
            {
                case 'a': f1 = 730f; f2 = 1100f; f3 = 2450f; return;
                case 'e': f1 = 530f; f2 = 1850f; f3 = 2500f; return;
                case 'i': f1 = 290f; f2 = 2250f; f3 = 3000f; return;
                case 'o': f1 = 520f; f2 = 860f; f3 = 2400f; return;
                case 'u': f1 = 320f; f2 = 800f; f3 = 2250f; return;
                case 'r': f1 = 490f; f2 = 1350f; f3 = 1700f; return;
                default: f1 = 600f; f2 = 1300f; f3 = 2500f; return;
            }
        }

        // 숨·마찰 잡음 크기: asp=1 이면 모음 울림의 0.4~1배, fr=1 이면 약 0.75배 실효값이 되게 맞췄다
        const float AspGain = 0.8f;
        const float FricGain = 0.24f;

        /// <summary>
        /// 포먼트 합성 목소리 한 사람: 성문 펄스(로젠버그 파형의 미분) + 숨 잡음 → 병렬 대역 필터 셋(F1~F3)과 고정 F4·콧소리 공명.
        /// 표본 하나씩 낸다. 포먼트는 목표값을 십여 ms 에 걸쳐 따라가 모음이 자연스럽게 이어진다.
        /// </summary>
        sealed class Speaker
        {
            readonly Voice v;
            readonly Svf b1 = new Svf(), b2 = new Svf(), b3 = new Svf(), b4 = new Svf(), nas1 = new Svf(), nas2 = new Svf(), fric = new Svf();
            double phase;
            bool odd;
            float cycAmp = 1f, stretch = 1f, tilt, growl;
            float f1, f2, f3, g1 = 1f, g2 = 0.6f;
            int count;

            public Speaker(Voice v)
            {
                this.v = v;
                f1 = 600f * v.Formant;
                f2 = 1300f * v.Formant;
                f3 = 2500f * v.Formant;
                b4.Set(3400f * v.Formant, 14f);
                nas1.Set(260f * v.Formant, 2.5f);
                nas2.Set(2100f * v.Formant, 5f);
                phase = Rand();
            }

            /// <param name="f0">음높이 (Hz)</param>
            /// <param name="voiced">성대 울림 세기</param>
            /// <param name="tf1">목표 포먼트 (배율 적용 전)</param>
            /// <param name="asp">숨(h) 세기: 포먼트 필터를 지난다</param>
            /// <param name="fr">마찰·파열 잡음 세기: 포먼트와 따로 frFc 대역</param>
            public float Next(float f0, float voiced, float tf1, float tf2, float tf3, float asp, float fr, float frFc)
            {
                if ((count++ & 15) == 0)
                {
                    f1 += 0.03f * (tf1 * v.Formant - f1);
                    f2 += 0.03f * (tf2 * v.Formant - f2);
                    f3 += 0.03f * (tf3 * v.Formant - f3);
                    b1.Set(f1, f1 / (55f + 0.06f * f1));
                    b2.Set(f2, f2 / (75f + 0.04f * f2));
                    b3.Set(f3, f3 / 140f);
                    fric.Set(frFc, 1.6f);
                    // 성문 원천은 위로 갈수록 약해지므로 높은 포먼트일수록 키운다 ('아'가 '이'보다 약해지지 않게)
                    g1 = Mathf.Pow(f1 / 450f, 1.2f) * (1f - 0.4f * v.Nasal);
                    g2 = 0.6f * Mathf.Pow(f2 / 1500f, 0.6f);
                }
                phase += f0 * stretch / Rate;
                if (phase >= 1.0)
                {
                    phase -= Math.Floor(phase);
                    odd = !odd;
                    cycAmp = 1f - v.Creak * ((odd ? 0.45f : 0f) + 0.45f * Rand());
                    stretch = 1f + (0.012f + 0.3f * v.Creak) * (Rand() - 0.5f); // 주기 흔들림 (jitter)
                }
                float p = (float)phase, tp = v.Open * 0.62f, tn = v.Open * 0.38f, g;
                if (p < tp) g = tn / tp * Mathf.Sin(Mathf.PI * p / tp);
                else if (p < tp + tn) g = -Mathf.Sin(0.5f * Mathf.PI * (p - tp) / tn);
                else g = 0f;
                tilt += 0.45f * (g - tilt); // 고역을 살짝 깎는다
                float src = tilt * cycAmp;
                if (v.Growl > 0f)
                {
                    growl += TwoPi * 27f / Rate;
                    if (growl > TwoPi) growl -= TwoPi;
                    src *= 1f - v.Growl * (0.5f + 0.5f * Mathf.Sin(growl));
                }
                float x = src * voiced * (1f - v.Whisper) + AspGain * Noise() * (asp + voiced * (v.Breath + v.Whisper));
                float y = g1 * b1.Run(x) - g2 * b2.Run(x) + 0.35f * b3.Run(x) - 0.12f * b4.Run(x);
                if (v.Nasal > 0f) y += v.Nasal * (0.7f * nas1.Run(x) + 0.3f * nas2.Run(x));
                y += FricGain * fr * fric.Run(Noise());
                return y;
            }
        }

        // ═════════════════ 음악 ═════════════════

        // 메뉴 음악 (VRChat판 그대로): 마디마다 코드 (루트 없는 보이싱 MIDI 음, 베이스 루트). 15마디는 앞 두 박 Gm7 · 뒤 두 박 C7.
        static readonly int[][] MenuVoicing =
        {
            new[] { 57, 60, 64, 67 }, // Fmaj9
            new[] { 53, 57, 60, 64 }, // Dm9
            new[] { 58, 62, 65, 69 }, // Gm9
            new[] { 58, 62, 64, 69 }, // C13
            new[] { 60, 64, 67, 71 }, // Am9
            new[] { 60, 64, 66, 69 }, // D9
            new[] { 58, 62, 65, 69 }, // Gm9
            new[] { 58, 62, 64, 69 }, // C13
            new[] { 57, 60, 64, 67 }, // Fmaj9
            new[] { 57, 60, 63, 67 }, // F9
            new[] { 62, 65, 69, 72 }, // Bbmaj9
            new[] { 61, 65, 67, 70 }, // Bbm6
            new[] { 60, 64, 67, 71 }, // Am9
            new[] { 60, 63, 66, 69 }, // D7b9
            new[] { 58, 62, 65, 69 }, // Gm9 (→ C13)
            new[] { 57, 60, 64, 67 }, // Fmaj9
        };
        static readonly int[] MenuBassRoot = { 41, 38, 43, 36, 45, 38, 43, 36, 41, 41, 46, 46, 45, 38, 43, 41 };
        static readonly int[] MenuC13 = { 58, 62, 64, 69 };

        // 비브라폰 리듬 (박, 길이). 4마디마다 한 마디는 쉰다.
        static readonly float[][][] MenuRhythms =
        {
            new[] { new[] { 0f, 2f }, new[] { 2.667f, 1f } },
            new[] { new[] { 1f, 0.667f }, new[] { 1.667f, 0.667f }, new[] { 2.333f, 1.5f } },
            new[] { new[] { 0.667f, 1f }, new[] { 2f, 1.5f } },
            new float[0][],
        };
        static readonly int[] MenuRhythmOfBar = { 0, 1, 2, 3, 0, 2, 1, 3, 0, 1, 2, 3, 2, 1, 0, 3 };

        /// <summary>
        /// 메뉴 음악: VRChat판 경매장 배경 음악 그대로 — 느린 재즈 라운지 (일렉트릭 피아노 · 콘트라베이스 · 비브라폰 · 브러시).
        /// 72BPM, F장조 16마디(약 53초).
        /// </summary>
        static float[] BgmMenu()
        {
            rng = new System.Random(11); // VRChat판과 같은 연주가 나오게 같은 시드
            const float bpm = 72f;
            const int bars = 16;
            float beat = 60f / bpm;
            int loopLen = Mathf.RoundToInt(bars * 4 * beat * Rate);
            int len = loopLen + Rate * 4;
            var ep = new float[len];
            var bass = new float[len];
            var vib = new float[len];
            var kit = new float[len];

            int prevMel = 76;
            for (int bar = 0; bar < bars; bar++)
            {
                float t0 = bar * 4 * beat;
                int[] chordA = MenuVoicing[bar];
                int[] chordB = bar == 14 ? MenuC13 : chordA;

                // 일렉트릭 피아노 컴핑: 첫 박 길게 + 3박 뒤 엇박(스윙) 짧게. 8·16마디는 한 번만.
                float v1 = 0.78f + Rand() * 0.12f;
                EpChord(ep, t0, chordA, bar == 15 || bar == 7 ? 3.6f * beat : 2.5f * beat, v1);
                if (bar != 7 && bar != 15)
                {
                    float v2 = 0.5f + Rand() * 0.12f;
                    EpChord(ep, t0 + 2.667f * beat, chordB, 1.25f * beat, v2);
                }

                // 베이스: 2비트 (루트 → 5도 또는 다음 코드로 이어지는 음)
                int root = MenuBassRoot[bar];
                int nextRoot = MenuBassRoot[(bar + 1) % bars];
                int second = bar == 14 ? 36 : (Math.Abs(nextRoot - root) <= 2 ? root + 7 : (nextRoot > root ? nextRoot - 1 : nextRoot + 1));
                if (second > 50) second -= 12;
                Bass(bass, t0, root, 1.9f * beat, 0.95f);
                Bass(bass, t0 + 2f * beat, second, 1.9f * beat, 0.8f);

                // 비브라폰 멜로디: 코드 음 중 앞 음과 가까운 음을 골라 프레이즈 앞쪽은 오르고 뒤쪽은 내린다.
                var rhythm = MenuRhythms[MenuRhythmOfBar[bar]];
                for (int n = 0; n < rhythm.Length; n++)
                {
                    float at = rhythm[n][0];
                    int[] chord = (bar == 14 && at >= 2f) ? MenuC13 : chordA;
                    bool rising = (bar % 4) < 2;
                    int target = prevMel + (rising ? 2 : -2) + (n == rhythm.Length - 1 && !rising ? -1 : 0);
                    int mel = MenuNearest(chord, MenuBassRoot[bar], target, 70, 86);
                    if (mel == prevMel) mel = MenuNearest(chord, MenuBassRoot[bar], target + (rising ? 3 : -3), 70, 86);
                    prevMel = mel;
                    Vibe(vib, t0 + at * beat, mel, rhythm[n][1] * beat, 0.8f + Rand() * 0.15f);
                }

                // 브러시: 2·4박 쓸기 + 라이드 '칭 치키'
                for (int bt = 0; bt < 4; bt++)
                {
                    if (bt == 1 || bt == 3) Swish(kit, t0 + bt * beat, 0.9f);
                    Ride(kit, t0 + bt * beat, bt % 2 == 0 ? 0.55f : 0.75f);
                    if (bt == 1 || bt == 3) Ride(kit, t0 + (bt + 0.667f) * beat, 0.45f);
                }
            }

            var mix = new float[len];
            for (int i = 0; i < len; i++) mix[i] = 0.34f * ep[i] + 0.22f * vib[i] + 0.035f * kit[i];
            mix = Reverb(mix, 0.3f);
            for (int i = 0; i < len; i++) mix[i] += 0.5f * bass[i];
            return Limit(Fold(mix, loopLen), 0.6f, 1.25f);
        }

        static int MenuNearest(int[] chord, int root, int target, int lo, int hi)
        {
            var pcs = new HashSet<int>();
            foreach (int n in chord) pcs.Add(n % 12);
            pcs.Add(root % 12);
            int best = target, bestD = int.MaxValue;
            for (int m = lo; m <= hi; m++)
            {
                if (!pcs.Contains(m % 12)) continue;
                int d = Math.Abs(m - target);
                if (d < bestD) { bestD = d; best = m; }
            }
            return best;
        }

        // ───────── 경매 음악: D단조 108BPM 24마디(약 53초). 걷는 베이스 · 엇박 컴핑 · 약음기 트럼펫 · 브러시와 라이드 ─────────

        static readonly string[] AuctionBars =
        {
            "Dm9", "Dm9", "Bbmaj9", "A7alt", "Dm9", "Gm9", "Em7b5", "A7b9",
            "Dm9", "Dm9", "Bbmaj9", "A7alt", "Dm9", "Gm9", "Em7b5|A7b9", "Dm9",
            "Gm9", "C9", "Fmaj9", "Bbmaj9", "Em7b5", "A7b9", "Dm9", "A7alt",
        };

        static Dictionary<string, ChordDef> AuctionChords()
        {
            return new Dictionary<string, ChordDef>
            {
                { "Dm9", new ChordDef(38, 'm', 53, 57, 60, 64) },    // F A C E
                { "Bbmaj9", new ChordDef(46, 'M', 53, 57, 60, 62) }, // F A C D
                { "A7alt", new ChordDef(45, '7', 55, 58, 61, 65) },  // G Bb C# F
                { "Gm9", new ChordDef(43, 'm', 53, 57, 58, 62) },    // F A Bb D
                { "Em7b5", new ChordDef(40, 'h', 55, 58, 62, 64) },  // G Bb D E
                { "A7b9", new ChordDef(45, '7', 55, 58, 61, 64) },   // G Bb C# E
                { "C9", new ChordDef(36, '7', 52, 55, 58, 62) },     // E G Bb D
                { "Fmaj9", new ChordDef(41, 'M', 55, 57, 60, 64) },  // G A C E
            };
        }

        // 엇박 위주의 드문 컴핑 (박, 길이)
        static readonly float[][][] AuctionComp =
        {
            new[] { new[] { 1.667f, 0.55f }, new[] { 3.667f, 0.4f } },
            new[] { new[] { 0.667f, 0.6f }, new[] { 2.667f, 0.5f } },
            new[] { new[] { 1.667f, 1.2f } },
            new[] { new[] { 0.667f, 0.4f }, new[] { 1.667f, 0.4f }, new[] { 3.667f, 0.35f } },
        };

        // 약음기 트럼펫 동기 (마디, 박, MIDI, 길이 박) — A(1~8) · A'(9~16) · B(17~24, 한 음씩 내려가는 반복)
        static readonly float[] AuctionLead =
        {
            0, 1.667f, 69, 0.33f,  0, 2f, 72, 0.5f,  0, 2.667f, 74, 1.1f,
            1, 0.667f, 76, 0.33f,  1, 1f, 74, 0.5f,  1, 1.667f, 72, 0.33f,  1, 2f, 69, 1.4f,
            2, 2.667f, 65, 0.33f,  2, 3f, 69, 0.6f,
            3, 0f, 70, 0.6f,  3, 0.667f, 69, 0.33f,  3, 1f, 67, 0.5f,  3, 1.667f, 65, 0.33f,  3, 2f, 73, 1.2f,
            4, 0f, 74, 1.4f,
            5, 1.667f, 70, 0.33f,  5, 2f, 74, 0.5f,  5, 2.667f, 77, 1.1f,
            6, 0.667f, 76, 0.33f,  6, 1f, 74, 0.33f,  6, 1.667f, 70, 0.33f,  6, 2f, 67, 0.6f,
            7, 0f, 70, 0.5f,  7, 0.667f, 69, 0.33f,  7, 1f, 67, 0.33f,  7, 1.667f, 64, 0.33f,  7, 2f, 67, 0.5f,  7, 2.667f, 70, 0.33f,  7, 3f, 73, 0.9f,
            8, 1.667f, 69, 0.33f,  8, 2f, 72, 0.5f,  8, 2.667f, 74, 0.33f,  8, 3f, 77, 0.9f,
            9, 0.667f, 76, 0.33f,  9, 1f, 74, 0.5f,  9, 1.667f, 69, 0.33f,  9, 2f, 72, 1.4f,
            10, 2.667f, 74, 0.33f,  10, 3f, 72, 0.6f,
            11, 0f, 70, 0.6f,  11, 0.667f, 69, 0.33f,  11, 1f, 65, 0.5f,  11, 1.667f, 67, 0.33f,  11, 2f, 73, 1.2f,
            12, 0f, 74, 1.6f,
            13, 2.667f, 70, 0.33f,  13, 3f, 69, 0.6f,
            14, 0f, 67, 0.6f,  14, 0.667f, 70, 0.33f,  14, 1f, 74, 0.5f,  14, 2f, 73, 0.5f,  14, 2.667f, 70, 0.33f,  14, 3f, 67, 0.5f,
            15, 0.667f, 69, 2.2f,
            16, 0.667f, 74, 0.33f,  16, 1f, 77, 0.5f,  16, 1.667f, 76, 0.33f,  16, 2f, 74, 1.1f,
            17, 0.667f, 72, 0.33f,  17, 1f, 76, 0.5f,  17, 1.667f, 74, 0.33f,  17, 2f, 72, 1.1f,
            18, 0.667f, 69, 0.33f,  18, 1f, 72, 0.5f,  18, 1.667f, 70, 0.33f,  18, 2f, 69, 1.1f,
            19, 2.667f, 65, 0.33f,  19, 3f, 69, 0.6f,
            20, 0f, 70, 0.6f,  20, 0.667f, 67, 0.33f,  20, 1f, 64, 0.5f,  20, 2f, 62, 0.6f,
            21, 0f, 69, 0.5f,  21, 0.667f, 73, 0.33f,  21, 1f, 76, 0.5f,  21, 1.667f, 79, 0.33f,  21, 2f, 77, 1.1f,
            22, 0f, 76, 0.5f,  22, 0.667f, 74, 1.6f,
            23, 2.667f, 73, 0.33f,  23, 3f, 70, 0.4f,
        };

        /// <summary>
        /// 경매 음악: 긴장감 있게 앞으로 나아가는 D단조 스윙 (108BPM, 24마디 약 53초).
        /// 4분음표로 걷는 베이스 · 엇박에 드문 일렉트릭 피아노 · 약음기 트럼펫의 짧은 동기 · 브러시와 라이드 · 매 박 아주 가벼운 큰북.
        /// UI 소리 밑에 깔리도록 차분하게 섞는다.
        /// </summary>
        static float[] BgmAuction()
        {
            const float bpm = 108f;
            float beat = 60f / bpm;
            var chords = Progression(AuctionBars, AuctionChords());
            int bars = AuctionBars.Length;
            float totalBeats = bars * 4f;
            int loopLen = Mathf.RoundToInt(totalBeats * beat * Rate);
            int len = loopLen + Rate * 4;
            var ep = new float[len];
            var lead = new float[len];
            var kit = new float[len];
            var bass = new float[len];
            var low = new float[len];

            WalkingBass(bass, chords, beat, 0.85f);

            int last = -1;
            for (int bar = 0; bar < bars; bar++)
            {
                int pi = rng.Next(AuctionComp.Length);
                if (pi == last) pi = (pi + 1) % AuctionComp.Length;
                last = pi;
                foreach (var hit in AuctionComp[pi])
                {
                    float at = bar * 4 + hit[0];
                    var ch = ChordAt(chords, hit[0] >= 3.5f ? at + 0.5f : at, totalBeats); // 4박 엇박은 다음 화음을 앞당겨 친다
                    EpChord(ep, at * beat, ch.V, hit[1] * beat, 0.5f + 0.12f * Rand());
                }
            }

            for (int k = 0; k + 3 < AuctionLead.Length; k += 4)
                Muted(lead, (AuctionLead[k] * 4f + AuctionLead[k + 1]) * beat, (int)AuctionLead[k + 2], AuctionLead[k + 3] * beat, 0.72f + 0.14f * Rand());

            for (int bar = 0; bar < bars; bar++)
            {
                float t0 = bar * 4 * beat;
                for (int bt = 0; bt < 4; bt++)
                {
                    float t = t0 + bt * beat;
                    Ride(kit, t, bt % 2 == 0 ? 0.5f : 0.68f);
                    if (bt % 2 == 1)
                    {
                        Ride(kit, t + 0.667f * beat, 0.4f);
                        Swish(kit, t, 0.75f);
                        Hat(kit, t, 0.55f);
                    }
                    Kick(low, t, bt % 2 == 0 ? 0.5f : 0.35f, 0.09f); // 깃털처럼 가볍게 매 박
                }
                if (bar % 2 == 1) Rim(kit, t0 + 3f * beat, 0.35f);
            }

            var mix = new float[len];
            for (int i = 0; i < len; i++) mix[i] = 0.3f * ep[i] + 0.17f * lead[i] + 0.035f * kit[i];
            mix = Reverb(mix, 0.28f);
            for (int i = 0; i < len; i++) mix[i] += 0.5f * bass[i] + 0.18f * low[i];
            return Limit(Fold(mix, loopLen), 0.6f, 1.25f);
        }

        // ───────── 결과 음악: C장조 120BPM 16마디(32초). 밝은 보이싱 · 걷는 베이스 · 흥얼거리기 쉬운 비브라폰 선율 ─────────

        static readonly string[] ResultsBars =
        {
            "Cmaj9", "A13", "Dm9", "G13", "Cmaj9", "C13", "Fmaj9", "F#dim7",
            "Cmaj9", "A13", "Dm9", "G13", "Em7|A13", "Dm9|G13", "C69", "G13",
        };

        static Dictionary<string, ChordDef> ResultsChords()
        {
            return new Dictionary<string, ChordDef>
            {
                { "Cmaj9", new ChordDef(36, 'M', 64, 67, 71, 74) },  // E G B D
                { "A13", new ChordDef(45, '7', 61, 66, 67, 71) },    // C# F# G B
                { "Dm9", new ChordDef(38, 'm', 60, 64, 65, 69) },    // C E F A
                { "G13", new ChordDef(43, '7', 59, 64, 65, 69) },    // B E F A
                { "C13", new ChordDef(36, '7', 64, 69, 70, 74) },    // E A Bb D
                { "Fmaj9", new ChordDef(41, 'M', 64, 67, 69, 72) },  // E G A C
                { "F#dim7", new ChordDef(42, 'o', 63, 66, 69, 72) }, // Eb F# A C
                { "Em7", new ChordDef(40, 'm', 62, 64, 67, 71) },    // D E G B
                { "C69", new ChordDef(36, '6', 64, 67, 69, 74) },    // E G A D
            };
        }

        // 찰스턴 리듬 위주의 밝은 컴핑 (박, 길이)
        static readonly float[][][] ResultsComp =
        {
            new[] { new[] { 0f, 0.6f }, new[] { 1.667f, 0.5f } },
            new[] { new[] { 0.667f, 0.5f }, new[] { 2f, 0.6f }, new[] { 3.667f, 0.4f } },
            new[] { new[] { 0f, 0.5f }, new[] { 1.667f, 0.4f }, new[] { 2.667f, 0.6f } },
            new[] { new[] { 1f, 0.5f }, new[] { 2.667f, 0.5f }, new[] { 3.667f, 0.4f } },
        };

        // 비브라폰 선율 (마디, 박, MIDI, 길이 박). 1~4마디는 9~12마디에서 한 번 더.
        static readonly float[] ResultsMelody =
        {
            0, 0f, 76, 1.5f,  0, 1.667f, 74, 0.33f,  0, 2f, 72, 0.67f,  0, 2.667f, 69, 1.2f,
            1, 0.667f, 73, 0.33f,  1, 1f, 76, 1f,  1, 2f, 79, 1.5f,
            2, 0f, 77, 1.5f,  2, 1.667f, 76, 0.33f,  2, 2f, 74, 0.67f,  2, 2.667f, 72, 1.2f,
            3, 0.667f, 71, 0.33f,  3, 1f, 74, 0.67f,  3, 1.667f, 77, 0.33f,  3, 2f, 76, 1f,  3, 3f, 74, 0.67f,
            4, 0f, 72, 1.6f,  4, 2.667f, 67, 0.33f,  4, 3f, 69, 0.67f,
            5, 0f, 70, 1f,  5, 1f, 72, 0.67f,  5, 1.667f, 76, 0.33f,  5, 2f, 79, 1.5f,
            6, 0f, 81, 1.5f,  6, 1.667f, 79, 0.33f,  6, 2f, 77, 0.67f,  6, 2.667f, 72, 1.2f,
            7, 0.667f, 72, 0.33f,  7, 1f, 75, 0.67f,  7, 1.667f, 78, 0.33f,  7, 2f, 81, 1f,  7, 3f, 79, 0.9f,
            12, 0f, 79, 0.67f,  12, 0.667f, 76, 0.33f,  12, 1f, 71, 0.67f,  12, 2f, 73, 0.67f,  12, 2.667f, 76, 0.33f,  12, 3f, 79, 0.67f,
            13, 0f, 77, 0.67f,  13, 0.667f, 76, 0.33f,  13, 1f, 74, 0.67f,  13, 2f, 71, 0.67f,  13, 2.667f, 67, 0.33f,  13, 3f, 71, 0.67f,
            14, 0f, 72, 2.6f,
            15, 2f, 67, 0.67f,  15, 2.667f, 71, 0.33f,  15, 3f, 74, 0.67f,
        };

        /// <summary>
        /// 결과 음악: 따뜻하고 신나는 장조 스윙 (120BPM, 16마디 32초). 밝은 일렉트릭 피아노 찰스턴 컴핑 · 걷는 베이스 ·
        /// 비브라폰 선율 · 라이드와 2·4박 브러시 · 1·3박 큰북. 15마디 끝에 글로켄슈필 반짝임.
        /// </summary>
        static float[] BgmResults()
        {
            const float bpm = 120f;
            float beat = 60f / bpm;
            var chords = Progression(ResultsBars, ResultsChords());
            int bars = ResultsBars.Length;
            float totalBeats = bars * 4f;
            int loopLen = Mathf.RoundToInt(totalBeats * beat * Rate);
            int len = loopLen + Rate * 4;
            var ep = new float[len];
            var vib = new float[len];
            var kit = new float[len];
            var bass = new float[len];
            var low = new float[len];

            WalkingBass(bass, chords, beat, 0.85f);

            int last = -1;
            for (int bar = 0; bar < bars; bar++)
            {
                int pi = rng.Next(ResultsComp.Length);
                if (pi == last) pi = (pi + 1) % ResultsComp.Length;
                last = pi;
                foreach (var hit in ResultsComp[pi])
                {
                    float at = bar * 4 + hit[0];
                    var ch = ChordAt(chords, hit[0] >= 3.5f ? at + 0.5f : at, totalBeats);
                    EpChord(ep, at * beat, ch.V, hit[1] * beat, 0.6f + 0.12f * Rand());
                }
            }

            for (int k = 0; k + 3 < ResultsMelody.Length; k += 4)
            {
                float bar = ResultsMelody[k], at = ResultsMelody[k + 1], l = ResultsMelody[k + 3];
                int midi = (int)ResultsMelody[k + 2];
                Vibe(vib, (bar * 4f + at) * beat, midi, l * beat, 0.78f + 0.14f * Rand());
                if (bar < 4f) Vibe(vib, ((bar + 8f) * 4f + at) * beat, midi, l * beat, 0.78f + 0.14f * Rand());
            }
            // 15마디: 해결 화음 위 글로켄슈필 반짝임 (C6 E6 G6 C7)
            float[] glock = { 1046.5f, 1318.5f, 1568.0f, 2093.0f };
            for (int k = 0; k < glock.Length; k++) Mallet(vib, (14 * 4 + 1f + k * 0.333f) * beat, glock[k], 0.5f, 0.22f);

            for (int bar = 0; bar < bars; bar++)
            {
                float t0 = bar * 4 * beat;
                for (int bt = 0; bt < 4; bt++)
                {
                    float t = t0 + bt * beat;
                    Ride(kit, t, bt % 2 == 0 ? 0.55f : 0.72f);
                    if (bt % 2 == 1)
                    {
                        Ride(kit, t + 0.667f * beat, 0.45f);
                        Swish(kit, t, 0.7f);
                        Hat(kit, t, 0.6f);
                    }
                    else Kick(low, t, 0.55f, 0.1f);
                }
            }

            var mix = new float[len];
            for (int i = 0; i < len; i++) mix[i] = 0.28f * ep[i] + 0.26f * vib[i] + 0.04f * kit[i];
            mix = Reverb(mix, 0.25f);
            for (int i = 0; i < len; i++) mix[i] += 0.5f * bass[i] + 0.2f * low[i];
            return Limit(Fold(mix, loopLen), 0.6f, 1.25f);
        }

        // ───────── 화성 도구 ─────────

        /// <summary>코드 이름의 뜻: 베이스 근음(MIDI), 성질(m 단7 · 7 속7 · M 장7 · 6 장6 · h 반감7 · o 감7), 루트 없는 보이싱.</summary>
        sealed class ChordDef
        {
            public readonly int Root;
            public readonly char Q;
            public readonly int[] V;
            public ChordDef(int root, char q, params int[] v) { Root = root; Q = q; V = v; }
        }

        /// <summary>진행 속 코드 하나 (시작 박, 길이 박).</summary>
        sealed class Chord
        {
            public float At, Beats;
            public int Root;
            public char Q;
            public int[] V;
        }

        /// <summary>"Dm9", "Em7b5|A7b9"(반 마디씩) 같은 마디 목록을 코드 진행으로 바꾼다.</summary>
        static List<Chord> Progression(string[] bars, Dictionary<string, ChordDef> defs)
        {
            var list = new List<Chord>();
            for (int bar = 0; bar < bars.Length; bar++)
            {
                string[] parts = bars[bar].Split('|');
                float beats = 4f / parts.Length;
                for (int k = 0; k < parts.Length; k++)
                {
                    var d = defs[parts[k]];
                    list.Add(new Chord { At = bar * 4f + k * beats, Beats = beats, Root = d.Root, Q = d.Q, V = d.V });
                }
            }
            return list;
        }

        static Chord ChordAt(List<Chord> chords, float atBeat, float totalBeats)
        {
            atBeat %= totalBeats;
            var c = chords[0];
            foreach (var ch in chords) if (ch.At <= atBeat + 0.001f) c = ch;
            return c;
        }

        /// <summary>
        /// 걷는 베이스: 코드마다 근음에서 시작해 화음 음·경과음을 4분음표로 지나, 마지막 박은 다음 근음의 반음 위나 아래로 다가간다.
        /// 마지막 코드는 첫 코드로 이어져 반복이 자연스럽다.
        /// </summary>
        static void WalkingBass(float[] b, List<Chord> chords, float beat, float vel)
        {
            const int lo = 33, hi = 50;
            int prev = chords[0].Root;
            for (int c = 0; c < chords.Count; c++)
            {
                var ch = chords[c];
                var next = chords[(c + 1) % chords.Count];
                int n = Mathf.Max(1, Mathf.RoundToInt(ch.Beats));
                int third = ch.Q == '7' || ch.Q == 'M' || ch.Q == '6' ? 4 : 3;
                int fifth = ch.Q == 'h' || ch.Q == 'o' ? 6 : 7;
                int seventh = ch.Q == 'M' ? 11 : (ch.Q == '6' || ch.Q == 'o') ? 9 : 10;
                int[][] pats = { new[] { third, fifth }, new[] { fifth, seventh }, new[] { 2, third }, new[] { 12, fifth } };
                int pat = rng.Next(pats.Length);
                var line = new int[n];
                line[0] = Near(ch.Root, prev, lo, hi);
                for (int k = 1; k < n - 1; k++)
                {
                    int iv = n == 4 ? pats[pat][k - 1] : fifth;
                    if (iv == 12) line[k] = line[0] + 12 <= hi ? line[0] + 12 : line[0] - 12; // 옥타브 도약
                    else line[k] = Near(ch.Root + iv, line[k - 1], lo, hi);
                }
                if (n >= 2)
                {
                    int target = Near(next.Root, line[n - 2], lo, hi);
                    int a = target + (rng.NextDouble() < 0.5 ? 1 : -1);
                    if (a > hi) a = target - 1;
                    if (a < lo) a = target + 1;
                    if (a == line[n - 2]) a = 2 * target - a;
                    line[n - 1] = a;
                }
                for (int k = 0; k < n; k++)
                    Bass(b, (ch.At + k) * beat, line[k], 0.92f * beat, vel * (k == 0 ? 1f : 0.82f) * (0.92f + 0.12f * Rand()));
                prev = line[n - 1];
            }
        }

        /// <summary>같은 음이름 중 prev 에 가장 가까운 음 (lo~hi 안).</summary>
        static int Near(int note, int prev, int lo, int hi)
        {
            int pc = ((note % 12) + 12) % 12;
            int best = note, bd = int.MaxValue;
            for (int m = lo; m <= hi; m++)
            {
                if (m % 12 != pc) continue;
                int d = Math.Abs(m - prev);
                if (d < bd) { bd = d; best = m; }
            }
            return best;
        }

        // ───────── 악기 ─────────

        static float Hz(int midi) { return 440f * Mathf.Pow(2f, (midi - 69) / 12f); }

        /// <summary>FM 일렉트릭 피아노 화음. 아래 음부터 살짝 흩어 친다.</summary>
        static void EpChord(float[] b, float start, int[] notes, float len, float vel)
        {
            for (int k = 0; k < notes.Length; k++) Ep(b, start + k * 0.011f, Hz(notes[k]), len, vel * (k == notes.Length - 1 ? 1f : 0.85f));
        }

        static void Ep(float[] b, float start, float f, float len, float vel)
        {
            int s0 = (int)(start * Rate);
            const float rel = 0.12f;
            for (int i = s0; i < b.Length; i++)
            {
                float t = (i - s0) / (float)Rate;
                if (t > len + rel) break;
                float env = Mathf.Min(1f, t / 0.004f) * Mathf.Exp(-t / 1.9f) * (t < len ? 1f : 1f - (t - len) / rel);
                float index = 1.5f * vel * Mathf.Exp(-t / 0.32f) + 0.22f;
                float w = TwoPi * f * t;
                float s = Mathf.Sin(w + index * Mathf.Sin(w));
                s += 0.12f * vel * Mathf.Sin(w * 14f) * Mathf.Exp(-t / 0.025f); // 건반 '팅'
                s *= 1f + 0.1f * Mathf.Sin(TwoPi * 4.6f * t);                   // 트레몰로
                b[i] += vel * env * s * 0.3f;
            }
        }

        /// <summary>콘트라베이스 퉁김.</summary>
        static void Bass(float[] b, float start, int midi, float len, float vel)
        {
            int s0 = (int)(start * Rate);
            float f = Hz(midi);
            const float rel = 0.07f;
            for (int i = s0; i < b.Length; i++)
            {
                float t = (i - s0) / (float)Rate;
                if (t > len + rel) break;
                float env = Mathf.Min(1f, t / 0.01f) * Mathf.Exp(-t / 1.1f) * (t < len ? 1f : 1f - (t - len) / rel);
                float w = TwoPi * f * t;
                float pluck = 1f + 0.6f * Mathf.Exp(-t / 0.05f);
                float s = Mathf.Sin(w) + 0.35f * pluck * Mathf.Sin(2f * w) + 0.12f * pluck * Mathf.Sin(3f * w);
                b[i] += vel * env * s * 0.6f;
            }
        }

        static void Vibe(float[] b, float start, int midi, float len, float vel)
        {
            int s0 = (int)(start * Rate);
            float f = Hz(midi);
            float hold = len + 0.6f;
            for (int i = s0; i < b.Length; i++)
            {
                float t = (i - s0) / (float)Rate;
                if (t > hold) break;
                float damp = t < len ? 1f : Mathf.Exp(-(t - len) / 0.15f);
                float w = TwoPi * f * t;
                float s = Mathf.Sin(w) * Mathf.Exp(-t / 1.4f) + 0.22f * Mathf.Sin(4f * w) * Mathf.Exp(-t / 0.18f);
                s *= 1f + 0.22f * Mathf.Sin(TwoPi * 5.2f * t); // 모터 떨림
                b[i] += vel * damp * Mathf.Min(1f, t / 0.002f) * s * 0.5f;
            }
        }

        /// <summary>
        /// 약음기(하몬) 트럼펫: 배음을 더하되 1.7kHz 부근을 강조하고 저음을 줄인 코맹맹이 금관.
        /// 처음엔 어둡다 금방 열리고, 살짝 아래에서 올라 붙으며, 길면 떤다.
        /// </summary>
        static void Muted(float[] b, float start, int midi, float len, float vel)
        {
            int s0 = (int)(start * Rate);
            float f = Hz(midi);
            const float rel = 0.06f;
            int nh = Math.Min(16, (int)(9000f / f));
            var amp = new float[nh + 1];
            for (int n = 1; n <= nh; n++)
            {
                float fn = f * n;
                float d = (fn - 1700f) / 650f;
                float res = 1f / (1f + d * d);
                float lowCut = fn / (fn + 400f);
                amp[n] = (0.25f + res) * lowCut / Mathf.Pow(n, 0.7f);
            }
            float ph = 0f;
            float sustain = 1f - 0.25f * Mathf.Min(1f, len / 0.4f);
            for (int i = s0; i < b.Length; i++)
            {
                float t = (i - s0) / (float)Rate;
                if (t > len + rel) break;
                float env = Mathf.Min(1f, t / 0.02f) * (t < len ? 1f - 0.25f * Mathf.Min(1f, t / 0.4f) : sustain * (1f - (t - len) / rel));
                float scoop = 1f - 0.03f * Mathf.Exp(-t / 0.035f);
                float vib = t > 0.22f ? 1f + 0.005f * Mathf.Sin(TwoPi * 5.5f * t) * Mathf.Min(1f, (t - 0.22f) / 0.2f) : 1f;
                ph += TwoPi * f * scoop * vib / Rate;
                if (ph > TwoPi) ph -= TwoPi;
                float bright = 0.55f + 0.45f * Mathf.Min(1f, t / 0.05f);
                float s = 0f, a = 1f;
                for (int n = 1; n <= nh; n++) { s += amp[n] * a * Mathf.Sin(n * ph); a *= bright; }
                s += 0.05f * Noise() * Mathf.Exp(-t / 0.02f); // 숨이 닿는 소리
                b[i] += vel * env * s;
            }
        }

        static void Swish(float[] b, float start, float vel)
        {
            int s0 = (int)(start * Rate);
            float lp1 = 0f, lp2 = 0f;
            for (int i = s0; i < b.Length; i++)
            {
                float t = (i - s0) / (float)Rate;
                if (t > 0.35f) break;
                float n = Noise();
                lp1 += 0.35f * (n - lp1);
                lp2 += 0.08f * (lp1 - lp2);
                float env = Mathf.Min(1f, t / 0.035f) * Mathf.Exp(-t / 0.11f);
                b[i] += vel * env * (lp1 - lp2) * 1.6f;
            }
        }

        static void Ride(float[] b, float start, float vel)
        {
            int s0 = (int)(start * Rate);
            float lp = 0f;
            for (int i = s0; i < b.Length; i++)
            {
                float t = (i - s0) / (float)Rate;
                if (t > 0.5f) break;
                float n = Noise();
                lp += 0.55f * (n - lp);
                float metal = Mathf.Sin(TwoPi * 5400f * t) * 0.3f + Mathf.Sin(TwoPi * 7900f * t) * 0.2f;
                b[i] += vel * Mathf.Exp(-t / 0.16f) * ((n - lp) * 0.7f + metal) * Mathf.Min(1f, t / 0.001f);
            }
        }

        /// <summary>하이햇을 발로 닫는 '칙'.</summary>
        static void Hat(float[] b, float start, float vel)
        {
            int s0 = (int)(start * Rate);
            float lp = 0f;
            for (int i = s0; i < b.Length; i++)
            {
                float t = (i - s0) / (float)Rate;
                if (t > 0.12f) break;
                float n = Noise();
                lp += 0.6f * (n - lp);
                float metal = 0.25f * Mathf.Sin(TwoPi * 6100f * t) + 0.2f * Mathf.Sin(TwoPi * 8300f * t) + 0.15f * Mathf.Sin(TwoPi * 10400f * t);
                b[i] += vel * Mathf.Exp(-t / 0.028f) * ((n - lp) * 0.8f + metal) * Mathf.Min(1f, t / 0.004f);
            }
        }

        /// <summary>림(크로스 스틱) '똑'.</summary>
        static void Rim(float[] b, float start, float vel)
        {
            int s0 = (int)(start * Rate);
            for (int i = s0; i < b.Length; i++)
            {
                float t = (i - s0) / (float)Rate;
                if (t > 0.06f) break;
                b[i] += vel * (0.8f * Mathf.Sin(TwoPi * 1750f * t) * Mathf.Exp(-t / 0.006f)
                    + 0.6f * Mathf.Sin(TwoPi * 520f * t) * Mathf.Exp(-t / 0.012f)
                    + 0.4f * Noise() * Mathf.Exp(-t / 0.0015f)) * Mathf.Min(1f, t / 0.0004f);
            }
        }

        /// <summary>큰북: 아래로 떨어지는 낮은 사인.</summary>
        static void Kick(float[] b, float start, float vel, float decay)
        {
            int s0 = (int)(start * Rate);
            float ph = 0f;
            for (int i = s0; i < b.Length; i++)
            {
                float t = (i - s0) / (float)Rate;
                if (t > decay * 6f) break;
                ph += TwoPi * (52f + 70f * Mathf.Exp(-t / 0.025f)) / Rate;
                b[i] += vel * Mathf.Sin(ph) * Mathf.Exp(-t / decay) * Mathf.Min(1f, t / 0.002f);
            }
        }

        // ═════════════════ 실내 소리 ═════════════════

        /// <summary>
        /// 경매장 실내 소리 (20초 반복): 낮은 방 울림 + 멀리서 웅성거리는 사람들(목소리 16개를 낮은 통과·큰 잔향으로 흐리게)
        /// + 가끔 촛불 심지가 '탁'. 조용히 깔리도록 작게.
        /// </summary>
        static float[] AmbRoom()
        {
            const float loopSec = 20f;
            int loopLen = (int)(loopSec * Rate);
            int len = loopLen + 4 * Rate;

            var crowd = new float[len];
            for (int p = 0; p < 16; p++)
            {
                var v = RandomVoice();
                float level = 0.35f + 0.65f * Rand();
                float t = 3f * Rand();
                while (t < loopSec)
                {
                    float end = Speak(crowd, t, v, 3 + rng.Next(10), level * (0.7f + 0.3f * Rand()));
                    t = end + 0.3f + 2.5f * Rand() * Rand();
                }
            }
            // 멀리서 들리게: 4차 낮은 통과 + 낮은 소리 조금 덜기 + 잔향을 많이
            var lp1 = new Svf().Set(650f, 0.7f);
            var lp2 = new Svf().Set(650f, 0.7f);
            var hp = new Svf().Set(110f, 0.7f);
            for (int i = 0; i < len; i++)
            {
                lp1.Run(crowd[i]);
                lp2.Run(lp1.Low);
                hp.Run(lp2.Low);
                crowd[i] = hp.High;
            }
            crowd = Reverb(crowd, 0.9f, 0.45f);
            Scale(crowd, 1f / Peak(crowd));

            // 촛불 '탁' (20초에 다섯 번쯤)
            var crack = new float[len];
            const int pops = 5;
            for (int k = 0; k < pops; k++) Crackle(crack, (k + 0.15f + 0.7f * Rand()) * (loopSec / pops), 1f);
            crack = Reverb(crack, 0.25f);
            Scale(crack, 0.3f / Peak(crack));
            for (int i = 0; i < len; i++) crowd[i] += crack[i];

            var loop = Fold(crowd, loopLen);
            var room = RoomTone(loopLen, 2 * Rate);
            float level0 = 0.35f * Rms(loop);
            for (int i = 0; i < loopLen; i++) loop[i] += level0 * room[i];
            return Limit(loop, 0.35f, 1f);
        }

        static Voice RandomVoice()
        {
            bool female = Rand() < 0.45f;
            return new Voice
            {
                Pitch = female ? 185f + 60f * Rand() : 95f + 45f * Rand(),
                Formant = female ? 1.12f + 0.08f * Rand() : 0.9f + 0.1f * Rand(),
                Range = 3f + 2f * Rand(),
                Rate = 4.6f + 2f * Rand(),
                Breath = 0.08f + 0.1f * Rand(),
                Open = 0.55f + 0.15f * Rand(),
                Vib = 0.05f,
            };
        }

        /// <summary>촛불 심지가 '탁' 튀는 소리: 아주 짧은 딸깍 몇 번.</summary>
        static void Crackle(float[] b, float start, float amp)
        {
            int clicks = 2 + rng.Next(4);
            float t = start;
            var f = new Svf().Set(1800f + 1800f * Rand(), 0.9f);
            for (int c = 0; c < clicks; c++)
            {
                int s0 = (int)(t * Rate);
                float a = amp * (0.4f + 0.6f * Rand());
                int n = (int)(0.004f * Rate);
                for (int i = 0; i < n && s0 + i < b.Length; i++)
                    b[s0 + i] += a * f.Run(Noise()) * Mathf.Exp(-i / (float)Rate / 0.0008f);
                t += 0.008f + 0.05f * Rand();
            }
        }

        /// <summary>낮은 방 울림 (갈색 잡음 + 아주 작은 공기 소리). 실효값 1, 끝과 처음을 겹쳐 이어 붙였다.</summary>
        static float[] RoomTone(int loopLen, int fade)
        {
            var src = new float[loopLen + fade];
            float br = 0f;
            var lp = new Svf().Set(220f, 0.6f);
            var hp = new Svf().Set(35f, 0.6f);
            var air = new Svf().Set(1400f, 0.5f);
            for (int i = 0; i < src.Length; i++)
            {
                br = 0.997f * br + 0.03f * Noise(); // 갈색 잡음
                lp.Run(br);
                hp.Run(lp.Low);
                air.Run(Noise());
                src[i] = hp.High + 0.02f * air.Low;
            }
            Scale(src, 1f / Rms(src));
            return Crossfold(src, loopLen);
        }

        // ═════════════════ 합성 도구 ═════════════════

        /// <summary>상태 변수 필터 (TPT/사이먼 방식). 주파수를 매 순간 바꿔도 안정적이라 모음 이동·와와 필터에 쓴다. Band 는 중심에서 크기 1.</summary>
        sealed class Svf
        {
            float a1, a2, a3, k = 1f, ic1, ic2;
            public float Low, Band, High;

            public Svf Set(float fc, float q)
            {
                fc = Mathf.Clamp(fc, 20f, Rate * 0.45f);
                float g = (float)Math.Tan(Math.PI * fc / Rate);
                k = 1f / Mathf.Max(0.05f, q);
                a1 = 1f / (1f + g * (g + k));
                a2 = g * a1;
                a3 = g * a2;
                return this;
            }

            public float Run(float x)
            {
                float v3 = x - ic2;
                float v1 = a1 * ic1 + a2 * v3;
                float v2 = ic2 + a2 * ic1 + a3 * v3;
                ic1 = 2f * v1 - ic1;
                ic2 = 2f * v2 - ic2;
                Low = v2;
                Band = k * v1;
                High = x - k * v1 - v2;
                return Band;
            }
        }

        /// <summary>슈뢰더 잔향 (빗살 4 + 전역 통과 2). wet 비율만큼 섞는다.</summary>
        static float[] Reverb(float[] dry, float wet) { return Reverb(dry, wet, 1f); }

        static float[] Reverb(float[] dry, float wet, float dryGain)
        {
            int[] combD = { 1557, 1617, 1491, 1422 };
            float[] out1 = new float[dry.Length];
            foreach (int d in combD)
            {
                var buf = new float[d];
                int idx = 0;
                float lp = 0f;
                for (int i = 0; i < dry.Length; i++)
                {
                    float y = buf[idx];
                    lp = y * 0.75f + lp * 0.25f; // 고역이 먼저 줄어드는 방 울림
                    buf[idx] = dry[i] + lp * 0.78f;
                    idx = (idx + 1) % d;
                    out1[i] += y * 0.25f;
                }
            }
            foreach (int d in new[] { 225, 556 })
            {
                var buf = new float[d];
                int idx = 0;
                for (int i = 0; i < out1.Length; i++)
                {
                    float bufOut = buf[idx];
                    float x = out1[i];
                    float y = -x + bufOut;
                    buf[idx] = x + bufOut * 0.5f;
                    idx = (idx + 1) % d;
                    out1[i] = y;
                }
            }
            var res = new float[dry.Length];
            for (int i = 0; i < dry.Length; i++) res[i] = dryGain * dry[i] + wet * out1[i];
            return res;
        }

        /// <summary>끝을 넘친 꼬리(음 끝·잔향)를 앞에 겹쳐 끊김 없이 반복되게 한다. 소리가 모두 0초 이후에 시작해야 정확하다.</summary>
        static float[] Fold(float[] mix, int loopLen)
        {
            var loop = new float[loopLen];
            for (int i = 0; i < mix.Length; i++) loop[i % loopLen] += mix[i];
            return loop;
        }

        /// <summary>계속 이어지는 잡음을 반복되게: 넘친 부분을 앞쪽과 같은 세기(사인·코사인)로 엇갈려 섞는다.</summary>
        static float[] Crossfold(float[] src, int loopLen)
        {
            int x = src.Length - loopLen;
            var loop = new float[loopLen];
            Array.Copy(src, loop, loopLen);
            for (int i = 0; i < x; i++)
            {
                float th = 0.5f * Mathf.PI * i / x;
                loop[i] = src[i] * Mathf.Sin(th) + src[loopLen + i] * Mathf.Cos(th);
            }
            return loop;
        }

        /// <summary>크기 맞추기: 최댓값 peakOut, 아주 부드러운 압축(tanh)으로 튀는 음 줄이기.</summary>
        static float[] Limit(float[] loop, float peakOut, float drive)
        {
            float peak = Peak(loop);
            float norm = (float)Math.Tanh(drive);
            for (int i = 0; i < loop.Length; i++)
                loop[i] = peakOut * (float)Math.Tanh(loop[i] / peak * drive) / norm;
            return loop;
        }

        static float[] Buf(float seconds) { return new float[(int)(seconds * Rate)]; }

        static float[] Trim(float[] b, float seconds)
        {
            int n = Mathf.Min(b.Length, (int)(seconds * Rate));
            var r = new float[n];
            Array.Copy(b, r, n);
            return r;
        }

        static float Rand() { return (float)rng.NextDouble(); }
        static float Noise() { return (float)(rng.NextDouble() * 2 - 1); }
        static char Pick(string s) { return s[rng.Next(s.Length)]; }
        static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }
        static float Semi(float semitones) { return Mathf.Exp(semitones * 0.05776227f); } // 2^(반음/12)

        static float Peak(float[] b)
        {
            float max = 1e-6f;
            foreach (float v in b) max = Mathf.Max(max, Mathf.Abs(v));
            return max;
        }

        static float Rms(float[] b)
        {
            double s = 0;
            foreach (float v in b) s += v * v;
            return Mathf.Max(1e-6f, (float)Math.Sqrt(s / Math.Max(1, b.Length)));
        }

        static void Scale(float[] b, float k) { for (int i = 0; i < b.Length; i++) b[i] *= k; }

        static void Tone(float[] b, float start, float freq, float decay, float amp)
        {
            int s0 = (int)(start * Rate);
            for (int i = s0; i < b.Length; i++)
            {
                float t = (i - s0) / (float)Rate;
                float env = Mathf.Min(1f, t / 0.003f) * Mathf.Exp(-t / decay);
                if (env < 0.0005f && t > 0.01f) break;
                float w = TwoPi * freq * t;
                b[i] += amp * env * (Mathf.Sin(w) + 0.28f * Mathf.Sin(2 * w) * Mathf.Exp(-t / (decay * 0.5f)) + 0.08f * Mathf.Sin(3 * w));
            }
        }

        static void Square(float[] b, float start, float freq, float len, float decay, float amp)
        {
            int s0 = (int)(start * Rate);
            for (int i = s0; i < b.Length; i++)
            {
                float t = (i - s0) / (float)Rate;
                if (t > len) break;
                float env = Mathf.Min(1f, t / 0.002f) * (t < len - 0.01f ? 1f : (len - t) / 0.01f) * Mathf.Exp(-t / Mathf.Max(decay, 0.05f));
                float w = TwoPi * freq * t;
                float s = Mathf.Sin(w) + Mathf.Sin(3 * w) / 3f * 0.7f + Mathf.Sin(5 * w) / 5f * 0.45f + Mathf.Sin(7 * w) / 7f * 0.25f;
                b[i] += amp * env * s;
            }
        }

        static void Bell(float[] b, float start, float freq, float decay, float amp)
        {
            int s0 = (int)(start * Rate);
            for (int i = s0; i < b.Length; i++)
            {
                float t = (i - s0) / (float)Rate;
                if (t > decay * 12f) break;
                float env = Mathf.Min(1f, t / 0.002f);
                float w = TwoPi * freq * t;
                float s = Mathf.Sin(w) * Mathf.Exp(-t / decay)
                    + 0.35f * Mathf.Sin(2.76f * w) * Mathf.Exp(-t / (decay * 0.45f))
                    + 0.15f * Mathf.Sin(5.40f * w) * Mathf.Exp(-t / (decay * 0.25f));
                b[i] += amp * env * s;
            }
        }

        /// <summary>말렛 악기(마림바·비브라폰 사이) 한 음: 맑은 기음 + 빨리 사라지는 위 배음 + 두드림.</summary>
        static void Mallet(float[] b, float start, float freq, float decay, float amp)
        {
            int s0 = (int)(start * Rate);
            for (int i = s0; i < b.Length; i++)
            {
                float t = (i - s0) / (float)Rate;
                float env = Mathf.Exp(-t / decay);
                if (env < 0.0005f) break;
                float w = TwoPi * freq * t;
                b[i] += amp * Mathf.Min(1f, t / 0.0015f) * (Mathf.Sin(w) * env
                    + 0.3f * Mathf.Sin(3.9f * w) * Mathf.Exp(-t / (decay * 0.15f))
                    + 0.1f * Mathf.Sin(9.2f * w) * Mathf.Exp(-t / 0.006f));
            }
        }

        /// <summary>배음을 더해 만든 금관 소리. 처음엔 어둡다가 금방 밝아지고, 길면 살짝 떤다.</summary>
        static void Brass(float[] b, float start, float len, float freq, float amp)
        {
            int s0 = (int)(start * Rate);
            float ph = 0f;
            for (int i = s0; i < b.Length; i++)
            {
                float t = (i - s0) / (float)Rate;
                const float rel = 0.14f;
                if (t > len + rel) break;
                float env = Mathf.Min(1f, t / 0.03f) * (t < len ? 1f : 1f - (t - len) / rel);
                float vib = t > 0.25f ? 1f + 0.004f * Mathf.Sin(TwoPi * 5.2f * t) : 1f;
                ph += TwoPi * freq * vib / Rate;
                if (ph > TwoPi) ph -= TwoPi;
                float bright = 0.35f + 0.4f * Mathf.Min(1f, t / 0.05f);
                float s = 0f, a = 1f;
                for (int n = 1; n <= 10; n++)
                {
                    if (freq * n > 12000f) break;
                    s += a * Mathf.Sin(n * ph) / Mathf.Pow(n, 0.6f);
                    a *= bright;
                }
                b[i] += amp * env * s;
            }
        }

        /// <summary>끝을 부드럽게 줄이고(시작도 0.2ms 만 열어 '툭' 하는 단차를 없앤다) 최대 진폭을 맞춘다.</summary>
        static float[] Finish(float[] b, float peak)
        {
            int fade = Mathf.Min(b.Length / 4, (int)(0.012f * Rate));
            for (int i = 0; i < fade; i++) b[b.Length - 1 - i] *= i / (float)fade;
            for (int i = 0; i < 8 && i < b.Length; i++) b[i] *= i / 8f;
            float max = Peak(b);
            for (int i = 0; i < b.Length; i++) b[i] = b[i] / max * peak;
            return b;
        }

        /// <summary>44.1kHz 모노 16비트 PCM WAV 로 쓴다.</summary>
        static void Save(string path, float[] samples)
        {
            using (var w = new BinaryWriter(File.Create(path)))
            {
                int bytes = samples.Length * 2;
                w.Write(new[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F' });
                w.Write(36 + bytes);
                w.Write(new[] { (byte)'W', (byte)'A', (byte)'V', (byte)'E', (byte)'f', (byte)'m', (byte)'t', (byte)' ' });
                w.Write(16);
                w.Write((short)1);
                w.Write((short)1);
                w.Write(Rate);
                w.Write(Rate * 2);
                w.Write((short)2);
                w.Write((short)16);
                w.Write(new[] { (byte)'d', (byte)'a', (byte)'t', (byte)'a' });
                w.Write(bytes);
                foreach (float v in samples) w.Write((short)Mathf.Clamp(Mathf.RoundToInt(v * 32767f), -32768, 32767));
            }
        }
    }
}
