using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BluffAuction.EditorTools
{
    /// <summary>
    /// 프로시저럴 텍스처 (외부 에셋 없음). VRChat판 경매장 텍스처(나무 바닥·벽지·대리석·벨벳·카펫·풍경화·밤하늘)에
    /// PC판용(녹색 펠트·가죽·시계 판·지구본·청자 빙렬·종이·금속 결)을 더했다. 한 번 만든 PNG 는 다시 쓰지 않는다(지우면 다시 생성).
    /// </summary>
    public static class TextureFactory
    {
        public const string Dir = "Assets/BluffAuction/Generated/Textures";

        public static Texture2D Get(string name, int w, int h, Func<int, int, Color[]> generate, bool normalMap = false, bool sRGB = true, bool clamp = false)
        {
            string path = Dir + "/" + name + ".png";
            if (!File.Exists(path))
            {
                Directory.CreateDirectory(Dir);
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                tex.SetPixels(generate(w, h));
                tex.Apply();
                File.WriteAllBytes(path, tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var imp = (TextureImporter)AssetImporter.GetAtPath(path);
                imp.textureType = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
                imp.sRGBTexture = !normalMap && sRGB;
                imp.wrapMode = clamp ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
                imp.anisoLevel = 8;
                imp.mipmapEnabled = true;
                imp.maxTextureSize = Math.Max(w, h);
                imp.textureCompression = TextureImporterCompression.CompressedHQ;
                imp.alphaIsTransparency = !normalMap;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ───────── 노이즈 ─────────

        static float Fbm(float x, float y, int oct)
        {
            float v = 0f, a = 0.5f, f = 1f;
            for (int i = 0; i < oct; i++)
            {
                v += a * Mathf.PerlinNoise(x * f + 31.7f * i, y * f + 17.3f * i);
                f *= 2f;
                a *= 0.5f;
            }
            return v;
        }

        /// <summary>타일 반복 가능한 노이즈.</summary>
        static float TileNoise(float u, float v, float scale, int oct)
        {
            float a = Fbm(u * scale, v * scale, oct);
            float b = Fbm((u - 1f) * scale, v * scale, oct);
            float c = Fbm(u * scale, (v - 1f) * scale, oct);
            float d = Fbm((u - 1f) * scale, (v - 1f) * scale, oct);
            return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
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

        static Color[] NormalFromHeight(float[] hgt, int w, int h, float strength)
        {
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float l = hgt[y * w + (x - 1 + w) % w];
                    float r = hgt[y * w + (x + 1) % w];
                    float d = hgt[((y - 1 + h) % h) * w + x];
                    float u = hgt[((y + 1) % h) * w + x];
                    var n = new Vector3((l - r) * strength, (d - u) * strength, 1f).normalized;
                    px[y * w + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
                }
            }
            return px;
        }

        // ───────── 나무 바닥 (판자) ─────────

        const int PlankRows = 8;

        static float PlankHeight(int x, int y, int w, int h, out Color color)
        {
            float u = (float)x / w;
            float v = (float)y / h;
            int row = Mathf.FloorToInt(v * PlankRows);
            float rowV = v * PlankRows - row;
            float offset = Hash(row, 7) * 0.9f;
            float uu = (u + offset) % 1f;
            int seg = Mathf.FloorToInt(uu * 2f);
            float segU = uu * 2f - seg;
            float tone = Hash(row * 3 + seg, 11);
            float grain = Mathf.Sin((rowV * 9f + TileNoise(u, v, 6f, 3) * 6f + tone * 20f) * Mathf.PI * 2f) * 0.5f + 0.5f;
            float fine = TileNoise(u, v, 48f, 2);
            var dark = new Color(0.20f, 0.11f, 0.06f);
            var light = new Color(0.42f, 0.25f, 0.13f);
            color = Color.Lerp(dark, light, 0.35f + tone * 0.35f + grain * 0.18f + (fine - 0.5f) * 0.2f);
            float rowGapPx = Mathf.Min(rowV, 1f - rowV) * ((float)h / PlankRows);
            float segGapPx = Mathf.Min(segU, 1f - segU) * (w / 2f);
            float edge = Mathf.Clamp01(Mathf.Min(rowGapPx, segGapPx) / 1.5f);
            color = Color.Lerp(color * 0.35f, color, edge);
            color.a = 0.35f + grain * 0.25f; // 알파 = 광택 (판자마다 조금씩 다르게)
            return edge * 0.6f + grain * 0.08f + fine * 0.04f;
        }

        public static Texture2D WoodFloor() => Get("wood_floor", 1024, 1024, (w, h) =>
        {
            var px = new Color[w * h];
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) { PlankHeight(x, y, w, h, out Color c); px[y * w + x] = c; }
            return px;
        });

        public static Texture2D WoodFloorNormal() => Get("wood_floor_n", 1024, 1024, (w, h) =>
        {
            var hgt = new float[w * h];
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) hgt[y * w + x] = PlankHeight(x, y, w, h, out _);
            return NormalFromHeight(hgt, w, h, 4f);
        }, true);

        // ───────── 마호가니 ─────────

        public static Texture2D Mahogany() => Get("mahogany", 512, 512, (w, h) =>
        {
            var px = new Color[w * h];
            var dark = new Color(0.16f, 0.06f, 0.035f);
            var light = new Color(0.36f, 0.14f, 0.07f);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float u = (float)x / w, v = (float)y / h;
                    float warp = TileNoise(u, v, 3f, 3) * 4f;
                    float g = Mathf.Sin((u * 14f + warp) * Mathf.PI * 2f) * 0.5f + 0.5f;
                    float fine = TileNoise(u, v * 0.25f, 40f, 2);
                    var c = Color.Lerp(dark, light, 0.3f + g * 0.35f + (fine - 0.5f) * 0.4f);
                    c.a = 0.55f + g * 0.15f;
                    px[y * w + x] = c;
                }
            }
            return px;
        });

        // ───────── 대리석 ─────────

        static float MarbleValue(float u, float v)
        {
            float t = TileNoise(u, v, 4f, 5);
            return Mathf.Pow(Mathf.Abs(Mathf.Sin((u * 3f + v * 2f + t * 5f) * Mathf.PI)), 0.25f);
        }

        public static Texture2D Marble() => Get("marble", 1024, 1024, (w, h) =>
        {
            var px = new Color[w * h];
            var vein = new Color(0.55f, 0.5f, 0.45f);
            var baseC = new Color(0.93f, 0.9f, 0.84f);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float u = (float)x / w, v = (float)y / h;
                    float m = MarbleValue(u, v);
                    float cloud = TileNoise(u, v, 8f, 3);
                    var c = Color.Lerp(vein, baseC, m) * (0.92f + cloud * 0.1f);
                    c.a = 0.85f;
                    px[y * w + x] = c;
                }
            }
            return px;
        });

        public static Texture2D MarbleDark() => Get("marble_dark", 1024, 1024, (w, h) =>
        {
            var px = new Color[w * h];
            var vein = new Color(0.75f, 0.62f, 0.4f);
            var baseC = new Color(0.05f, 0.05f, 0.06f);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float u = (float)x / w, v = (float)y / h;
                    float m = MarbleValue(u * 0.7f + 0.3f, v);
                    float cloud = TileNoise(u, v, 8f, 3);
                    var c = Color.Lerp(vein, baseC, Mathf.Pow(m, 0.6f)) * (0.9f + cloud * 0.15f);
                    c.a = 0.9f;
                    px[y * w + x] = c;
                }
            }
            return px;
        });

        // ───────── 벽지 (다마스크) ─────────

        static float DamaskMotif(float u, float v)
        {
            float best = 0f;
            for (int k = 0; k < 2; k++)
            {
                float cx = k == 0 ? 0.5f : 0f, cy = k == 0 ? 0.5f : 0f;
                for (int ox = -1; ox <= 1; ox++)
                {
                    for (int oy = -1; oy <= 1; oy++)
                    {
                        float dx = u - (cx + ox), dy = (v - (cy + oy)) * 0.7f;
                        float r = Mathf.Sqrt(dx * dx + dy * dy);
                        float ang = Mathf.Atan2(dy, dx);
                        float petal = 0.22f + 0.07f * Mathf.Cos(ang * 8f) + 0.03f * Mathf.Cos(ang * 16f);
                        float ring = Mathf.Clamp01(1f - Mathf.Abs(r - petal) * 40f);
                        float core = Mathf.Clamp01(1f - Mathf.Abs(r - 0.08f) * 50f);
                        float spoke = r < petal ? Mathf.Clamp01(1f - Mathf.Abs(Mathf.Sin(ang * 4f)) * 6f) * Mathf.Clamp01((petal - r) * 10f) : 0f;
                        best = Mathf.Max(best, Mathf.Max(ring, Mathf.Max(core, spoke * 0.8f)));
                    }
                }
            }
            return best;
        }

        public static Texture2D Wallpaper() => Get("wallpaper", 512, 512, (w, h) =>
        {
            var px = new Color[w * h];
            var bg = new Color(0.07f, 0.17f, 0.13f);
            var fg = new Color(0.55f, 0.47f, 0.25f);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float u = (float)x / w, v = (float)y / h;
                    float m = DamaskMotif(u, v);
                    float stripe = 0.5f + 0.5f * Mathf.Sin(u * Mathf.PI * 2f * 24f);
                    float n = TileNoise(u, v, 16f, 2);
                    Color c = bg * (0.92f + stripe * 0.06f + n * 0.08f);
                    c = Color.Lerp(c, fg, m * 0.32f);
                    c.a = 0.2f + m * 0.45f; // 무늬만 비단처럼 반짝
                    px[y * w + x] = c;
                }
            }
            return px;
        });

        // ───────── 벨벳 ─────────

        static float Fold(float u, float v)
        {
            float warp = TileNoise(u, v * 0.3f, 2f, 2) * 0.35f;
            float big = Mathf.Sin((u * 4f + warp) * Mathf.PI * 2f) * 0.5f + 0.5f;
            float small = Mathf.Sin((u * 12f + warp * 2f) * Mathf.PI * 2f) * 0.5f + 0.5f;
            return Mathf.Pow(big, 0.8f) * 0.8f + small * 0.2f;
        }

        public static Texture2D Velvet() => Get("velvet", 512, 512, (w, h) =>
        {
            var px = new Color[w * h];
            var deep = new Color(0.22f, 0.02f, 0.04f);
            var lit = new Color(0.55f, 0.06f, 0.09f);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float u = (float)x / w, v = (float)y / h;
                    float f = Fold(u, v);
                    float n = TileNoise(u, v, 64f, 2);
                    var c = Color.Lerp(deep, lit, f * 0.85f + (n - 0.5f) * 0.12f);
                    c.a = 0.35f;
                    px[y * w + x] = c;
                }
            }
            return px;
        });

        public static Texture2D VelvetNormal() => Get("velvet_n", 512, 512, (w, h) =>
        {
            var hgt = new float[w * h];
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) hgt[y * w + x] = Fold((float)x / w, (float)y / h);
            return NormalFromHeight(hgt, w, h, 6f);
        }, true);

        // ───────── 카펫 ─────────

        public static Texture2D Rug() => Get("carpet_rug", 1024, 1024, (w, h) =>
        {
            var px = new Color[w * h];
            var navy = new Color(0.06f, 0.07f, 0.2f);
            var red = new Color(0.45f, 0.06f, 0.07f);
            var gold = new Color(0.8f, 0.62f, 0.27f);
            var cream = new Color(0.85f, 0.78f, 0.62f);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float u = (float)x / w, v = (float)y / h;
                    float cu = (u - 0.5f) * 2f, cv = (v - 0.5f) * 2f;
                    float r = Mathf.Sqrt(cu * cu + cv * cv);
                    float ang = Mathf.Atan2(cv, cu);
                    Color c;
                    // 둥근 러그: 가장자리 무늬 띠 → 붉은 바탕 → 가운데 메달리온
                    if (r > 0.98f) c = new Color(0, 0, 0, 0);
                    else if (r > 0.95f) c = navy;
                    else if (r > 0.935f) c = gold;
                    else if (r > 0.86f) c = Mathf.Sin(ang * 48f) > 0.2f ? cream : navy;
                    else if (r > 0.845f) c = gold;
                    else
                    {
                        float petal = 0.38f + 0.08f * Mathf.Cos(ang * 12f);
                        c = red;
                        if (r < petal) c = Color.Lerp(navy, red, 0.25f);
                        if (Mathf.Abs(r - petal) < 0.012f) c = gold;
                        if (r < 0.12f) c = gold;
                        if (Mathf.Abs(r - 0.2f) < 0.01f) c = cream;
                        float vine = Mathf.Abs(Mathf.Sin(ang * 16f + r * 30f));
                        if (r > 0.55f && r < 0.8f && vine < 0.08f) c = Color.Lerp(red, gold, 0.6f);
                    }
                    float n = TileNoise(u, v, 120f, 2);
                    float a = c.a;
                    c = c * (0.86f + n * 0.24f);
                    c.a = a == 0 ? 0 : 1;
                    px[y * w + x] = c;
                }
            }
            return px;
        }, clamp: true);

        // ───────── 녹색 펠트 (탁자 상판) ─────────

        public static Texture2D Felt() => Get("felt", 512, 512, (w, h) =>
        {
            var px = new Color[w * h];
            var a = new Color(0.04f, 0.18f, 0.12f);
            var b = new Color(0.08f, 0.27f, 0.18f);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = (float)x / w, v = (float)y / h;
                    float n = TileNoise(u, v, 90f, 3);
                    float n2 = TileNoise(u, v, 6f, 2);
                    var c = Color.Lerp(a, b, n * 0.6f + n2 * 0.4f);
                    c.a = 0.08f;
                    px[y * w + x] = c;
                }
            return px;
        });

        // ───────── 가죽 ─────────

        static float LeatherH(float u, float v)
        {
            // 작은 세포 무늬 (가죽 결)
            float s = 40f;
            float x = u * s, y = v * s;
            int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
            float best = 9f;
            for (int oy = -1; oy <= 1; oy++)
                for (int ox = -1; ox <= 1; ox++)
                {
                    int cx = ix + ox, cy = iy + oy;
                    int wx = ((cx % 40) + 40) % 40, wy = ((cy % 40) + 40) % 40;
                    float px = cx + Hash(wx, wy), py = cy + Hash(wy + 7, wx + 3);
                    float d = (x - px) * (x - px) + (y - py) * (y - py);
                    if (d < best) best = d;
                }
            return Mathf.Sqrt(best);
        }

        public static Texture2D Leather() => Get("leather", 512, 512, (w, h) =>
        {
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = (float)x / w, v = (float)y / h;
                    float c = LeatherH(u, v);
                    float n = TileNoise(u, v, 5f, 3);
                    float g = 0.75f + c * 0.25f + (n - 0.5f) * 0.25f;
                    px[y * w + x] = new Color(g, g, g, 0.35f + c * 0.2f);
                }
            return px;
        });

        public static Texture2D LeatherNormal() => Get("leather_n", 512, 512, (w, h) =>
        {
            var hgt = new float[w * h];
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) hgt[y * w + x] = LeatherH((float)x / w, (float)y / h);
            return NormalFromHeight(hgt, w, h, 3f);
        }, true);

        // ───────── 금속 결 (반짝임의 얼룩) ─────────

        public static Texture2D MetalSmudge() => Get("metal_smudge", 512, 512, (w, h) =>
        {
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = (float)x / w, v = (float)y / h;
                    float n = TileNoise(u, v, 6f, 4);
                    float s = TileNoise(u, v * 0.05f, 60f, 2);
                    float g = 0.86f + n * 0.14f;
                    px[y * w + x] = new Color(g, g, g, Mathf.Clamp01(0.86f + (n - 0.5f) * 0.3f + (s - 0.5f) * 0.1f));
                }
            return px;
        });

        // ───────── 청자 빙렬 ─────────

        public static Texture2D Crackle() => Get("crackle", 512, 512, (w, h) =>
        {
            var px = new Color[w * h];
            var baseC = new Color(0.55f, 0.72f, 0.62f);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = (float)x / w, v = (float)y / h;
                    // 보로노이 경계 = 갈라진 금
                    float s = 14f;
                    float fx = u * s, fy = v * s;
                    int ix = Mathf.FloorToInt(fx), iy = Mathf.FloorToInt(fy);
                    float d1 = 9, d2 = 9;
                    for (int oy = -1; oy <= 1; oy++)
                        for (int ox = -1; ox <= 1; ox++)
                        {
                            int cx = ix + ox, cy = iy + oy;
                            int wx = ((cx % 14) + 14) % 14, wy = ((cy % 14) + 14) % 14;
                            float ppx = cx + Hash(wx * 3, wy * 5), ppy = cy + Hash(wy * 7, wx * 11);
                            float d = Mathf.Sqrt((fx - ppx) * (fx - ppx) + (fy - ppy) * (fy - ppy));
                            if (d < d1) { d2 = d1; d1 = d; }
                            else if (d < d2) d2 = d;
                        }
                    float crack = Mathf.Clamp01(1f - (d2 - d1) * 18f);
                    float n = TileNoise(u, v, 4f, 3);
                    var c = baseC * (0.9f + n * 0.18f);
                    c = Color.Lerp(c, c * 0.62f, crack * 0.7f);
                    c.a = 0.92f;
                    px[y * w + x] = c;
                }
            return px;
        });

        // ───────── 시계 판 ─────────

        public static Texture2D ClockFace() => Get("clock_face", 512, 512, (w, h) =>
        {
            var px = new Color[w * h];
            var paper = new Color(0.95f, 0.91f, 0.82f);
            var ink = new Color(0.08f, 0.06f, 0.05f);
            var gold = new Color(0.75f, 0.58f, 0.25f);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = (float)x / w * 2 - 1, v = (float)y / h * 2 - 1;
                    float r = Mathf.Sqrt(u * u + v * v);
                    float ang = Mathf.Atan2(u, v); // 12시가 0
                    Color c = paper * (0.95f + Fbm(u * 8, v * 8, 2) * 0.08f);
                    if (r > 0.97f) c = gold;
                    else if (Mathf.Abs(r - 0.9f) < 0.008f || Mathf.Abs(r - 0.78f) < 0.006f) c = ink;
                    else if (r > 0.78f && r < 0.9f)
                    {
                        // 분 눈금 60개, 시 눈금 12개는 굵게
                        float t = ang / (Mathf.PI * 2) * 60f;
                        float dm = Mathf.Abs(t - Mathf.Round(t));
                        float th = Mathf.Abs(t / 5f - Mathf.Round(t / 5f)) < 0.02f ? 0.18f : 0.06f;
                        if (dm < th && r > (th > 0.1f ? 0.79f : 0.84f)) c = ink;
                    }
                    else if (r < 0.75f && r > 0.55f)
                    {
                        // 숫자 대신 로마 숫자 느낌의 막대 무리
                        float t = ang / (Mathf.PI * 2) * 12f;
                        float dm = Mathf.Abs(t - Mathf.Round(t));
                        if (dm < 0.05f && r > 0.6f && r < 0.72f) c = ink;
                    }
                    if (r < 0.06f) c = gold;
                    c.a = 0.55f;
                    px[y * w + x] = c;
                }
            return px;
        }, clamp: true);

        // ───────── 지구본 ─────────

        public static Texture2D Globe() => Get("globe", 1024, 512, (w, h) =>
        {
            var px = new Color[w * h];
            var sea = new Color(0.16f, 0.3f, 0.32f);
            var seaDeep = new Color(0.1f, 0.2f, 0.24f);
            var land = new Color(0.78f, 0.66f, 0.42f);
            var landDark = new Color(0.55f, 0.42f, 0.24f);
            var ink = new Color(0.25f, 0.17f, 0.1f);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = (float)x / w, v = (float)y / h;
                    float lat = (v - 0.5f) * Mathf.PI;
                    float lon = u * Mathf.PI * 2;
                    // 구 위의 3D 점으로 노이즈를 뽑아 이음매 없이
                    float cx = Mathf.Cos(lat) * Mathf.Cos(lon), cy = Mathf.Sin(lat), cz = Mathf.Cos(lat) * Mathf.Sin(lon);
                    float n = Fbm(cx * 1.6f + 10, cz * 1.6f + cy * 0.8f + 20, 5) + Fbm(cy * 2.2f + 5, cx * 1.1f, 3) * 0.4f;
                    Color c;
                    if (n > 0.78f)
                    {
                        c = Color.Lerp(land, landDark, Mathf.Clamp01((n - 0.78f) * 4f));
                        if (Mathf.Abs(n - 0.79f) < 0.006f) c = ink;
                    }
                    else c = Color.Lerp(seaDeep, sea, Mathf.Clamp01(n * 1.3f));
                    // 경위선
                    float gl = Mathf.Abs(Mathf.Sin(lat * 6f)), go = Mathf.Abs(Mathf.Sin(lon * 6f));
                    if (gl < 0.02f || go < 0.012f) c = Color.Lerp(c, ink, 0.45f);
                    c *= 0.92f + Fbm(u * 40, v * 40, 2) * 0.12f;
                    c.a = n > 0.78f ? 0.3f : 0.6f;
                    px[y * w + x] = c;
                }
            return px;
        });

        // ───────── 종이 (책 단면) ─────────

        public static Texture2D Pages() => Get("pages", 256, 256, (w, h) =>
        {
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float v = (float)y / h;
                    float line = Mathf.Abs(Mathf.Sin(v * Mathf.PI * 2 * 60f));
                    float g = 0.86f + line * 0.08f - Hash(x / 6, y) * 0.04f;
                    px[y * w + x] = new Color(g, g * 0.95f, g * 0.84f, 0.1f);
                }
            return px;
        });

        // ───────── 풍경화 ─────────

        public static Texture2D Painting(int seed) => Get("painting_" + seed, 512, 384, (w, h) =>
        {
            var rng = new System.Random(seed * 97 + 13);
            Color[][] palettes =
            {
                new[] { new Color(0.98f, 0.62f, 0.35f), new Color(0.55f, 0.32f, 0.55f), new Color(0.3f, 0.18f, 0.3f), new Color(0.12f, 0.08f, 0.15f) },
                new[] { new Color(0.62f, 0.8f, 0.95f), new Color(0.4f, 0.55f, 0.45f), new Color(0.25f, 0.38f, 0.3f), new Color(0.12f, 0.2f, 0.15f) },
                new[] { new Color(0.95f, 0.85f, 0.6f), new Color(0.65f, 0.5f, 0.35f), new Color(0.42f, 0.3f, 0.22f), new Color(0.2f, 0.14f, 0.1f) },
                new[] { new Color(0.35f, 0.4f, 0.65f), new Color(0.25f, 0.3f, 0.5f), new Color(0.15f, 0.18f, 0.32f), new Color(0.06f, 0.07f, 0.14f) },
            };
            var pal = palettes[seed % palettes.Length];
            float sunX = 0.25f + (float)rng.NextDouble() * 0.5f;
            float sunY = 0.62f + (float)rng.NextDouble() * 0.2f;
            float off = (float)rng.NextDouble() * 50f;
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float u = (float)x / w, v = (float)y / h;
                    Color sky = Color.Lerp(pal[0], pal[1] * 0.9f, Mathf.Pow(v, 0.8f));
                    float sd = Vector2.Distance(new Vector2(u * 1.33f, v), new Vector2(sunX * 1.33f, sunY));
                    sky += new Color(1f, 0.95f, 0.8f) * (Mathf.Clamp01(0.06f - sd) * 12f + Mathf.Clamp01(0.35f - sd) * 0.35f);
                    float cloud = Fbm(u * 4f + off, v * 10f, 4);
                    sky = Color.Lerp(sky, Color.Lerp(sky, Color.white, 0.35f), Mathf.Clamp01((cloud - 0.55f) * 3f) * Mathf.Clamp01((v - 0.5f) * 3f));
                    Color c = sky;
                    for (int layer = 0; layer < 3; layer++)
                    {
                        float baseH = 0.52f - layer * 0.1f;
                        float m = baseH + (Fbm(u * (2f + layer) + off + layer * 10f, layer * 3.1f, 4) - 0.5f) * (0.35f - layer * 0.06f);
                        if (v < m)
                        {
                            Color mc = Color.Lerp(pal[2], pal[3], layer / 2f);
                            mc = Color.Lerp(mc, sky, 0.45f - layer * 0.18f);
                            c = mc * (0.9f + Fbm(u * 30f, v * 30f, 2) * 0.2f);
                        }
                    }
                    if (v < 0.2f)
                    {
                        Color refl = Color.Lerp(pal[1], pal[0], v * 3f) * 0.6f;
                        float streak = Fbm(u * 3f, v * 80f, 2);
                        c = Color.Lerp(c, refl * (0.85f + streak * 0.3f), 0.85f);
                    }
                    float brush = Fbm(u * 60f + v * 10f, v * 60f, 2);
                    c *= 0.9f + brush * 0.2f;
                    float vig = Mathf.Clamp01(1.1f - Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 1.1f);
                    c *= 0.65f + vig * 0.4f;
                    c = Color.Lerp(c, new Color(0.6f, 0.45f, 0.2f) * c.grayscale * 1.6f, 0.12f);
                    c.a = 0.45f;
                    px[y * w + x] = c;
                }
            }
            return px;
        }, clamp: true);

        // ───────── 밤하늘 창문 ─────────

        public static Texture2D NightWindow() => Get("night_window", 256, 512, (w, h) =>
        {
            var px = new Color[w * h];
            var top = new Color(0.02f, 0.03f, 0.1f);
            var bottom = new Color(0.15f, 0.12f, 0.3f);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float u = (float)x / w, v = (float)y / h;
                    Color c = Color.Lerp(bottom, top, Mathf.Pow(v, 0.7f));
                    float star = Hash(x, y);
                    if (star > 0.996f) c += Color.white * (star - 0.996f) * 200f * Mathf.Clamp01(v * 1.5f);
                    float moon = Vector2.Distance(new Vector2(u, v * 2f), new Vector2(0.7f, 1.6f));
                    c += new Color(0.9f, 0.9f, 1f) * (Mathf.Clamp01(0.12f - moon) * 9f + Mathf.Clamp01(0.4f - moon) * 0.25f);
                    if (v < 0.12f && Hash(x / 3, y / 3) > 0.93f) c += new Color(1f, 0.75f, 0.4f) * 0.6f;
                    if (v < 0.08f) c = Color.Lerp(c, new Color(0.03f, 0.03f, 0.05f), 0.6f);
                    c.a = 1f;
                    px[y * w + x] = c;
                }
            }
            return px;
        }, clamp: true);

        // ───────── 입자 (부드러운 점) ─────────

        public static Texture2D SoftDot() => Get("soft_dot", 128, 128, (w, h) =>
        {
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = (float)x / (w - 1) * 2 - 1, v = (float)y / (h - 1) * 2 - 1;
                    float r = Mathf.Sqrt(u * u + v * v);
                    float a = Mathf.Clamp01(1 - r);
                    a = a * a * (3 - 2 * a);
                    px[y * w + x] = new Color(1, 1, 1, a);
                }
            return px;
        }, clamp: true);

        /// <summary>색종이 (사각 조각 4색 아틀라스).</summary>
        public static Texture2D Confetti() => Get("confetti", 128, 128, (w, h) =>
        {
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int q = (x * 2 / w) + (y * 2 / h) * 2;
                    float lx = (x % (w / 2)) / (float)(w / 2), ly = (y % (h / 2)) / (float)(h / 2);
                    bool inside = lx > 0.15f && lx < 0.85f && ly > 0.3f && ly < 0.7f;
                    Color c = q == 0 ? new Color(1f, 0.82f, 0.3f) : q == 1 ? new Color(0.95f, 0.3f, 0.35f) : q == 2 ? new Color(0.35f, 0.7f, 1f) : new Color(1f, 1f, 1f);
                    c.a = inside ? 1 : 0;
                    px[y * w + x] = c;
                }
            return px;
        }, clamp: true);
    }
}
