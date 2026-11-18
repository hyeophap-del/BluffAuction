using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BluffAuction.EditorTools
{
    /// <summary>
    /// UI 그림 (9칸 늘이기용): 금테 칠기 패널, 아이보리 종이 카드, 버건디·금빛 버튼, 장식 구분선, 화면 비네트, 조준점.
    /// USS 에서 url("Textures/....png") 와 -unity-slice-* 로 쓴다. 외부 그림 없이 코드로 그린다.
    /// </summary>
    public static class UiTextures
    {
        public const string Dir = "Assets/BluffAuction/UI/Textures";

        public static void BuildAll()
        {
            Directory.CreateDirectory(Dir);
            Panel();
            PanelSoft();
            Card();
            Button("btn", new Color(0.62f, 0.15f, 0.2f), new Color(0.34f, 0.06f, 0.1f), new Color(0.93f, 0.76f, 0.42f));
            Button("btn_hover", new Color(0.76f, 0.21f, 0.26f), new Color(0.44f, 0.09f, 0.13f), new Color(1f, 0.88f, 0.58f));
            Button("btn_gold", new Color(0.98f, 0.84f, 0.5f), new Color(0.72f, 0.52f, 0.2f), new Color(1f, 0.95f, 0.75f));
            Button("btn_ivory", new Color(0.98f, 0.95f, 0.88f), new Color(0.86f, 0.8f, 0.68f), new Color(0.6f, 0.47f, 0.25f));
            Button("btn_green", new Color(0.2f, 0.5f, 0.34f), new Color(0.08f, 0.26f, 0.17f), new Color(0.6f, 0.9f, 0.7f));
            Button("btn_purple", new Color(0.42f, 0.27f, 0.66f), new Color(0.2f, 0.12f, 0.36f), new Color(0.8f, 0.68f, 0.98f));
            Ghost();
            Divider();
            Vignette();
            Crosshair("crosshair", false);
            Crosshair("crosshair_ring", true);
            CrosshairTarget();
            Glow();
            Icons();
            TomatoSplat();
            AppleSplat();
        }

        /// <summary>화면 가장자리·모서리에만 덩어리 자리를 고른다 (가운데 넓은 타원 밖, 위 가운데는 비운다). x, y, 반지름, 모양 씨앗.</summary>
        static System.Collections.Generic.List<Vector4> EdgeBlobs(System.Random rnd, int W, int H, int count, float rMin, float rMax)
        {
            var blobs = new System.Collections.Generic.List<Vector4>();
            for (int i = 0; i < count; i++)
            {
                float x, y;
                do { x = (float)rnd.NextDouble() * W; y = (float)rnd.NextDouble() * H; }
                while (Mathf.Pow((x - W * 0.5f) / (W * 0.44f), 2) + Mathf.Pow((y - H * 0.5f) / (H * 0.44f), 2) < 1f || (y > H * 0.7f && Mathf.Abs(x - W * 0.5f) < W * 0.25f));
                blobs.Add(new Vector4(x, y, rMin + (float)rnd.NextDouble() * (rMax - rMin), (float)rnd.NextDouble() * 100f));
            }
            return blobs;
        }

        /// <summary>
        /// 토마토를 맞았을 때: 만화처럼 밝은 주황빛 과육 덩어리 + 노란 씨 + 반짝임 (흘러내림·검붉은 색은 피처럼 보여 쓰지 않는다).
        /// </summary>
        static void TomatoSplat()
        {
            const int W = 768, H = 432;
            const float K = 1.5f;   // 512×288 기준 크기에서 늘린 배율
            var rnd = new System.Random(23);
            var blobs = EdgeBlobs(rnd, W, H, 8, 30f * K, 58f * K);
            var seeds = new System.Collections.Generic.List<Vector3>(); // x, y, 각도
            foreach (var b in blobs)
                for (int i = 0; i < 6; i++)
                {
                    float a = (float)rnd.NextDouble() * Mathf.PI * 2, rr = b.z * (0.18f + 0.5f * (float)rnd.NextDouble());
                    seeds.Add(new Vector3(b.x + Mathf.Cos(a) * rr, b.y + Mathf.Sin(a) * rr, (float)rnd.NextDouble() * Mathf.PI));
                }
            var drops = new System.Collections.Generic.List<Vector3>();
            for (int i = 0; i < 14; i++)
            {
                var b = blobs[rnd.Next(blobs.Count)];
                float a = (float)rnd.NextDouble() * Mathf.PI * 2, d = b.z * (1.2f + (float)rnd.NextDouble() * 0.7f);
                drops.Add(new Vector3(b.x + Mathf.Cos(a) * d, b.y + Mathf.Sin(a) * d, (3f + (float)rnd.NextDouble() * 5f) * K));
            }
            var rim = new Color(0.86f, 0.24f, 0.06f);
            var body = new Color(1f, 0.4f, 0.13f);
            var pulp = new Color(1f, 0.6f, 0.28f);
            var seed = new Color(1f, 0.93f, 0.6f);
            var seedEdge = new Color(0.92f, 0.66f, 0.26f);
            Save("splat_tomato", W, H, (x, y) =>
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                // 가장 깊이 들어간 덩어리 (가장자리까지 남은 거리 sd)
                float sd = -999f;
                var hb = Vector4.zero;
                foreach (var b in blobs)
                {
                    var d = p - new Vector2(b.x, b.y);
                    float ang = Mathf.Atan2(d.y, d.x);
                    float r = b.z * (1f + 0.14f * Mathf.Sin(ang * 4f + b.w) + 0.07f * Mathf.Sin(ang * 7f + b.w * 2f));
                    float s = r - d.magnitude;
                    if (s > sd) { sd = s; hb = b; }
                }
                foreach (var dr in drops)
                {
                    float s = dr.z - Vector2.Distance(p, new Vector2(dr.x, dr.y));
                    if (s > sd) { sd = s; hb = new Vector4(dr.x, dr.y, dr.z, 0); }
                }
                if (sd < -1.5f) return new Color(0, 0, 0, 0);
                float cover = Mathf.Clamp01((sd + 1.5f) / 2.5f);
                // 진한 테두리 → 밝은 과육
                float depth = Mathf.Clamp01(sd / Mathf.Max(4f, hb.z * 0.55f));
                var c = sd < 3f * K ? Color.Lerp(rim, body, Mathf.Clamp01(sd / (3f * K))) : Color.Lerp(body, pulp, depth);
                // 씨
                foreach (var s in seeds)
                {
                    var q = p - new Vector2(s.x, s.y);
                    float cs = Mathf.Cos(s.z), sn = Mathf.Sin(s.z);
                    float u = (q.x * cs + q.y * sn) / (5.2f * K), v = (-q.x * sn + q.y * cs) / (2.8f * K);
                    float e = u * u + v * v;
                    if (e < 1f && sd > 2f * K) { c = Color.Lerp(seed, seedEdge, Mathf.Clamp01((e - 0.45f) / 0.4f)); break; }
                }
                // 반짝임: 덩어리 왼쪽 위의 흰 타원 (y 가 위로 갈수록 크다)
                if (hb.z > 8f * K)
                {
                    var hl = new Vector2(hb.x - hb.z * 0.32f, hb.y + hb.z * 0.36f);
                    var hq = (p - hl);
                    float hd = Mathf.Sqrt(hq.x * hq.x / (hb.z * hb.z * 0.05f) + hq.y * hq.y / (hb.z * hb.z * 0.022f));
                    if (hd < 1f) c = Color.Lerp(c, Color.white, (1f - hd) * 0.85f);
                }
                c.a = cover * 0.95f;
                return c;
            });
        }

        /// <summary>사과를 맞았을 때: 크림색 과육 조각(한쪽에 빨간 껍질 띠) 몇 개 + 작은 즙 방울.</summary>
        static void AppleSplat()
        {
            const int W = 768, H = 432;
            var rnd = new System.Random(31);
            var chunks = EdgeBlobs(rnd, W, H, 11, 16f, 30f);
            var drops = new System.Collections.Generic.List<Vector3>();
            for (int i = 0; i < 18; i++)
            {
                var b = chunks[rnd.Next(chunks.Count)];
                float a = (float)rnd.NextDouble() * Mathf.PI * 2, d = b.z * (1.4f + (float)rnd.NextDouble() * 1.2f);
                drops.Add(new Vector3(b.x + Mathf.Cos(a) * d, b.y + Mathf.Sin(a) * d, 3f + (float)rnd.NextDouble() * 5f));
            }
            var flesh = new Color(1f, 0.96f, 0.8f);
            var fleshEdge = new Color(0.95f, 0.84f, 0.56f);
            var peel = new Color(0.84f, 0.1f, 0.1f);
            var juice = new Color(1f, 0.95f, 0.72f);
            Save("splat_apple", W, H, (x, y) =>
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                foreach (var b in chunks)
                {
                    var d = p - new Vector2(b.x, b.y);
                    float ang = Mathf.Atan2(d.y, d.x);
                    // 조금 모난 조각
                    float r = b.z * (1f + 0.16f * Mathf.Sin(ang * 3f + b.w) + 0.07f * Mathf.Sin(ang * 5f + b.w * 3f));
                    float s = r - d.magnitude;
                    if (s < -1.5f) continue;
                    float cover = Mathf.Clamp01((s + 1.5f) / 2.5f);
                    var c = Color.Lerp(fleshEdge, flesh, Mathf.Clamp01(s / (r * 0.5f)));
                    // 조각 셋 중 둘은 한쪽 둘레에 껍질 띠 (부드럽게 이어지게)
                    if (((int)b.w) % 3 != 0)
                    {
                        float wedge = Mathf.Abs(Mathf.DeltaAngle(ang * Mathf.Rad2Deg, b.w * 37f));
                        float band = Mathf.Clamp01((r * 0.26f - s) / 2f) * Mathf.Clamp01((75f - wedge) / 15f);
                        c = Color.Lerp(c, peel, band);
                    }
                    // 반짝임
                    var hq = p - new Vector2(b.x - r * 0.3f, b.y + r * 0.3f);
                    float hd = hq.magnitude / (r * 0.28f);
                    if (hd < 1f) c = Color.Lerp(c, Color.white, (1f - hd) * 0.6f);
                    c.a = cover * 0.96f;
                    return c;
                }
                foreach (var dr in drops)
                {
                    float s = dr.z - Vector2.Distance(p, new Vector2(dr.x, dr.y));
                    if (s > -1.5f) { var c = juice; c.a = Mathf.Clamp01((s + 1.5f) / 2.5f) * 0.75f; return c; }
                }
                return new Color(0, 0, 0, 0);
            });
        }

        // ───────── 작은 그림 (흰색, USS 에서 색을 입힌다) ─────────

        static float Segment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 pa = p - a, ba = b - a;
            float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
            return (pa - ba * h).magnitude;
        }

        static void Icon(string name, Func<Vector2, float> sdf)
        {
            const int S = 64;
            Save(name, S, S, (x, y) =>
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                float d = sdf(p);
                return new Color(1, 1, 1, Mathf.Clamp01(0.5f - d));
            });
        }

        static void Icons()
        {
            // 마이크: 둥근 머리 + U 자 받침 + 기둥
            Func<Vector2, float> mic = p =>
            {
                float head = Segment(p, new Vector2(32, 36), new Vector2(32, 48)) - 9f;
                float cup = Mathf.Abs((p - new Vector2(32, 38)).magnitude - 15f) - 2.6f;
                if (p.y > 38) cup = 99f;
                float stem = Segment(p, new Vector2(32, 23), new Vector2(32, 13)) - 2.6f;
                float foot = Segment(p, new Vector2(23, 12), new Vector2(41, 12)) - 2.6f;
                return Mathf.Min(Mathf.Min(head, cup), Mathf.Min(stem, foot));
            };
            Icon("icon_mic", mic);
            Icon("icon_mic_off", p => Mathf.Min(Mathf.Max(mic(p), -(Segment(p, new Vector2(12, 54), new Vector2(54, 10)) - 5.5f)), Segment(p, new Vector2(14, 52), new Vector2(52, 12)) - 2.8f));
            // 말하는 중: 확성기 + 물결 둘
            Icon("icon_talk", p =>
            {
                float box = Mathf.Max(Mathf.Abs(p.x - 15) - 5, Mathf.Abs(p.y - 32) - 7);
                float cone = Mathf.Max(Mathf.Max(p.x - 34, 20 - p.x), Mathf.Abs(p.y - 32) - (6 + (p.x - 20) * 0.9f));
                float body = Mathf.Min(box, cone);
                float r = (p - new Vector2(30, 32)).magnitude;
                float ang = Mathf.Abs(Mathf.Atan2(p.y - 32, p.x - 30));
                float w1 = ang < 0.75f ? Mathf.Abs(r - 14f) - 2.4f : 99f;
                float w2 = ang < 0.8f ? Mathf.Abs(r - 23f) - 2.4f : 99f;
                return Mathf.Min(body, Mathf.Min(w1, w2));
            });
            // 음소거: 확성기 + X
            Icon("icon_muted", p =>
            {
                float box = Mathf.Max(Mathf.Abs(p.x - 13) - 5, Mathf.Abs(p.y - 32) - 7);
                float cone = Mathf.Max(Mathf.Max(p.x - 32, 18 - p.x), Mathf.Abs(p.y - 32) - (6 + (p.x - 18) * 0.9f));
                float x1 = Segment(p, new Vector2(40, 22), new Vector2(56, 42)) - 3f;
                float x2 = Segment(p, new Vector2(40, 42), new Vector2(56, 22)) - 3f;
                return Mathf.Min(Mathf.Min(box, cone), Mathf.Min(x1, x2));
            });
            // 능력: 눈 (아몬드 테 + 눈동자)
            Icon("icon_ability", p =>
            {
                var q = (p - new Vector2(32, 32));
                float lens = Mathf.Max((q - new Vector2(0, -20)).magnitude - 30f, (q - new Vector2(0, 20)).magnitude - 30f);
                float outline = Mathf.Abs(lens) - 2.6f;
                float pupil = q.magnitude - 6.5f;
                return Mathf.Min(outline, pupil);
            });
            // 입찰 패들: 둥근 판 + 손잡이
            Icon("icon_paddle", p => Mathf.Min(Mathf.Abs((p - new Vector2(32, 40)).magnitude - 13f) - 3.4f, Segment(p, new Vector2(32, 26), new Vector2(32, 6)) - 3.6f));
            // 판매자: 가격표 (왼쪽 끝이 뾰족하고 구멍 하나), 비스듬히
            Icon("icon_tag", p =>
            {
                float a = -32f * Mathf.Deg2Rad, cs = Mathf.Cos(a), sn = Mathf.Sin(a);
                var c = p - new Vector2(32, 32);
                var q = new Vector2(c.x * cs - c.y * sn, c.x * sn + c.y * cs);
                float body = RoundRect(q.x + 10f, q.y + 12f, 32f, 24f, 4f);
                float head = Mathf.Max((Mathf.Abs(q.y) - (q.x + 23f)) * 0.707f, q.x + 8f);
                float hole = (q - new Vector2(-11f, 0f)).magnitude - 4f;
                return Mathf.Max(Mathf.Min(body, head), -hole);
            });
            // 근접 무기: 장난감 망치
            Icon("icon_hammer", p => Mathf.Min(RoundRect(p.x - 10, p.y - 36, 44, 18, 6), Segment(p, new Vector2(32, 36), new Vector2(32, 6)) - 3.4f));
            // 말풍선
            Icon("icon_chat", p =>
            {
                float bubble = RoundRect(p.x - 8, p.y - 18, 48, 36, 12);
                float tail = Mathf.Max(Mathf.Max(-(p.y - 12), p.y - 22), Mathf.Abs(p.x - 20 - (22 - p.y) * 0.6f) - (p.y - 12) * 0.45f);
                return Mathf.Min(bubble, tail);
            });
        }

        // ───────── 도구 ─────────

        static void Save(string name, int w, int h, Func<int, int, Color> px)
        {
            string path = Dir + "/" + name + ".png";
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var buf = new Color[w * h];
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) buf[y * w + x] = px(x, y);
            tex.SetPixels(buf);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Default;
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = false;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.filterMode = FilterMode.Bilinear;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.npotScale = TextureImporterNPOTScale.None;
            imp.SaveAndReimport();
        }

        /// <summary>둥근 사각형의 가장자리까지 부호 있는 거리 (안쪽이 음수).</summary>
        static float RoundRect(float x, float y, float w, float h, float r)
        {
            float qx = Mathf.Abs(x - w * 0.5f) - (w * 0.5f - r);
            float qy = Mathf.Abs(y - h * 0.5f) - (h * 0.5f - r);
            float ox = Mathf.Max(qx, 0), oy = Mathf.Max(qy, 0);
            return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0) - r;
        }

        static float Cover(float d) { return Mathf.Clamp01(0.5f - d); }

        static float Line(float d, float at, float width) { return Mathf.Clamp01(width * 0.5f + 0.5f - Mathf.Abs(d - at)); }

        static Color Over(Color under, Color over, float a)
        {
            a *= over.a;
            float outA = a + under.a * (1 - a);
            if (outA <= 1e-5f) return new Color(0, 0, 0, 0);
            Color c = (over * a + under * under.a * (1 - a)) / outA;
            c.a = outA;
            return c;
        }

        static float Hash(int x, int y)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263;
                h = (h ^ (h >> 13)) * 1274126177;
                return ((h ^ (h >> 16)) & 0xffff) / 65535f;
            }
        }

        // ───────── 그림 ─────────

        static void Panel()
        {
            const int S = 96;
            var gold = new Color(0.88f, 0.72f, 0.4f, 1f);
            Save("panel", S, S, (x, y) =>
            {
                float px = x + 0.5f, py = y + 0.5f;
                float d = RoundRect(px, py, S, S, 16);
                float v = py / S;
                var fill = Color.Lerp(new Color(0.055f, 0.034f, 0.028f, 0.94f), new Color(0.13f, 0.085f, 0.068f, 0.94f), v);
                var c = new Color(fill.r, fill.g, fill.b, fill.a * Cover(d));
                c = Over(c, new Color(0, 0, 0, 0.9f), Line(d, -0.5f, 1.2f) * Cover(d));
                c = Over(c, gold, Line(d, -6.5f, 1.3f) * 0.8f);
                // 모서리 다이아몬드
                foreach (var cx in new[] { 6.5f, S - 6.5f })
                    foreach (var cy in new[] { 6.5f, S - 6.5f })
                    {
                        float dd = Mathf.Abs(px - cx) + Mathf.Abs(py - cy);
                        c = Over(c, gold, Mathf.Clamp01(3.6f - dd));
                    }
                return c;
            });
        }

        static void PanelSoft()
        {
            const int S = 64;
            Save("panel_soft", S, S, (x, y) =>
            {
                float d = RoundRect(x + 0.5f, y + 0.5f, S, S, 14);
                var c = new Color(0.04f, 0.025f, 0.02f, 0.62f * Cover(d));
                c = Over(c, new Color(0.88f, 0.72f, 0.4f, 0.35f), Line(d, -1.5f, 1f));
                return c;
            });
        }

        static void Card()
        {
            const int S = 96;
            Save("card", S, S, (x, y) =>
            {
                float px = x + 0.5f, py = y + 0.5f;
                float d = RoundRect(px, py, S, S, 10);
                float n = Hash(x, y) * 0.035f + Hash(x / 3, y / 3) * 0.025f;
                var paper = new Color(0.96f - n, 0.925f - n, 0.84f - n, 1f);
                var c = new Color(paper.r, paper.g, paper.b, Cover(d));
                c = Over(c, new Color(0.7f, 0.55f, 0.26f, 1f), Line(d, -3.5f, 2.2f));
                c = Over(c, new Color(0.42f, 0.29f, 0.22f, 0.45f), Line(d, -9f, 1f));
                return c;
            });
        }

        static void Button(string name, Color top, Color bottom, Color rim)
        {
            const int W = 64, H = 48;
            Save(name, W, H, (x, y) =>
            {
                float px = x + 0.5f, py = y + 0.5f;
                float d = RoundRect(px, py, W, H, 11);
                float v = py / H;
                var fill = Color.Lerp(bottom, top, Mathf.SmoothStep(0, 1, v));
                // 위쪽 광택
                fill = Color.Lerp(fill, Color.white, Mathf.Clamp01((v - 0.62f) * 2.2f) * 0.12f);
                var c = new Color(fill.r, fill.g, fill.b, Cover(d));
                c = Over(c, rim, Line(d, -1.2f, 1.4f) * Cover(d));
                c = Over(c, new Color(1, 1, 1, 0.22f), Line(d, -3.2f, 0.8f) * Mathf.Clamp01((v - 0.5f) * 4f));
                return c;
            });
        }

        static void Ghost()
        {
            const int W = 64, H = 48;
            Save("btn_ghost", W, H, (x, y) =>
            {
                float d = RoundRect(x + 0.5f, y + 0.5f, W, H, 11);
                var c = new Color(0.05f, 0.03f, 0.025f, 0.45f * Cover(d));
                return Over(c, new Color(0.88f, 0.72f, 0.4f, 0.7f), Line(d, -1.2f, 1.2f));
            });
        }

        static void Divider()
        {
            const int W = 512, H = 24;
            var gold = new Color(0.9f, 0.75f, 0.42f, 1f);
            Save("divider", W, H, (x, y) =>
            {
                float px = x + 0.5f, py = y + 0.5f;
                float cx = W * 0.5f, cy = H * 0.5f;
                float fade = Mathf.Clamp01(1f - Mathf.Abs(px - cx) / (W * 0.5f));
                fade = Mathf.Pow(fade, 0.6f);
                var c = new Color(0, 0, 0, 0);
                c = Over(c, gold, Mathf.Clamp01(1.1f - Mathf.Abs(py - cy)) * fade);
                float dd = Mathf.Abs(px - cx) * 0.8f + Mathf.Abs(py - cy);
                c = Over(c, gold, Mathf.Clamp01(7.5f - dd));
                c = Over(c, new Color(0.15f, 0.08f, 0.05f, 1f), Mathf.Clamp01(3.2f - dd));
                foreach (float o in new[] { -22f, 22f })
                {
                    float dot = Vector2.Distance(new Vector2(px, py), new Vector2(cx + o, cy));
                    c = Over(c, gold, Mathf.Clamp01(2.6f - dot));
                }
                return c;
            });
        }

        static void Vignette()
        {
            const int S = 256;
            Save("vignette", S, S, (x, y) =>
            {
                float u = (x + 0.5f) / S * 2 - 1, v = (y + 0.5f) / S * 2 - 1;
                float r = Mathf.Sqrt(u * u * 0.8f + v * v);
                float a = Mathf.SmoothStep(0.45f, 1.25f, r) * 0.85f;
                return new Color(0.02f, 0.01f, 0.008f, a);
            });
        }

        static void Crosshair(string name, bool ring)
        {
            const int S = 64;
            Save(name, S, S, (x, y) =>
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(S * 0.5f, S * 0.5f));
                if (!ring)
                {
                    float core = Mathf.Clamp01(3.4f - d);
                    float halo = Mathf.Clamp01(1f - d / 9f) * 0.35f;
                    var c = new Color(0, 0, 0, halo * 0.8f);
                    return Over(c, new Color(1f, 0.95f, 0.85f, 1f), core);
                }
                float ringA = Mathf.Clamp01(1.6f - Mathf.Abs(d - 14f));
                float dotA = Mathf.Clamp01(2.6f - d);
                var cc = new Color(0, 0, 0, Mathf.Clamp01(1f - Mathf.Abs(d - 14f) / 5f) * 0.4f);
                cc = Over(cc, new Color(1f, 0.85f, 0.5f, 1f), ringA);
                return Over(cc, new Color(1f, 0.95f, 0.85f, 1f), dotA);
            });
        }

        /// <summary>들고 있는 물건으로 사람을 겨눌 때: 굵은 흰 고리 + 네 방향 눈금 + 진한 테두리 (어떤 배경에서도 보이게, 색은 USS 로).</summary>
        static void CrosshairTarget()
        {
            const int S = 96;
            Save("crosshair_target", S, S, (x, y) =>
            {
                var p = new Vector2(x + 0.5f - S * 0.5f, y + 0.5f - S * 0.5f);
                float d = p.magnitude;
                // 고리 (반지름 22, 굵기 4)
                float ring = Mathf.Abs(d - 22f) - 2f;
                // 눈금: 고리 바깥 26~36 사이, 굵기 3
                float along = Mathf.Max(Mathf.Abs(p.x), Mathf.Abs(p.y));
                float across = Mathf.Min(Mathf.Abs(p.x), Mathf.Abs(p.y));
                float tick = Mathf.Max(across - 1.5f, Mathf.Max(26f - along, along - 36f));
                float shape = Mathf.Min(ring, tick);
                // 가운데 점
                shape = Mathf.Min(shape, d - 2.6f);
                float fill = Mathf.Clamp01(0.5f - shape);
                float edge = Mathf.Clamp01(0.5f - (shape - 2.2f)) * 0.85f;
                var c = new Color(0.08f, 0.04f, 0.02f, edge);
                return Over(c, Color.white, fill);
            });
        }

        static void Glow()
        {
            const int S = 128;
            Save("glow", S, S, (x, y) =>
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(S * 0.5f, S * 0.5f)) / (S * 0.5f);
                float a = Mathf.Clamp01(1f - d);
                return new Color(1f, 0.82f, 0.45f, a * a * 0.8f);
            });
        }
    }
}
