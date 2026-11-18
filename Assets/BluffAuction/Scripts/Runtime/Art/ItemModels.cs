using System.Collections.Generic;
using UnityEngine;
using static BluffAuction.Art.ModelBuilder;

namespace BluffAuction.Art
{
    /// <summary>
    /// 경매품 12종 (외형 번호 순서는 VRChat판과 같다). 원점이 바닥 가운데, 앞이 -Z, 높이 0.3~0.45m.
    /// 회전판 위·진열장·공개 화면이 같은 모형을 쓴다.
    /// </summary>
    public static class ItemModels
    {
        public const int Count = 12;

        public static GameObject Build(int index, Transform parent)
        {
            var b = new ModelBuilder();
            switch (((index % Count) + Count) % Count)
            {
                case 0: Amphora(b); break;
                case 1: MantelClock(b); break;
                case 2: Candelabra(b); break;
                case 3: Ring(b); break;
                case 4: Painting(b); break;
                case 5: Celadon(b); break;
                case 6: Globe(b); break;
                case 7: CrystalBall(b); break;
                case 8: Crown(b); break;
                case 9: PocketWatch(b); break;
                case 10: Figurine(b); break;
                default: Books(b); break;
            }
            var go = b.Build("Item" + index, parent);
            // 작은 물건은 키워 다른 물건과 비슷하게 보이게
            int k = ((index % Count) + Count) % Count;
            float extra = k == 9 ? 1.7f : (k == 3 ? 1.35f : (k == 11 ? 1.15f : 1f));
            go.transform.localScale = Vector3.one * extra;
            return go;
        }

        /// <summary>
        /// 직접 넣은 그림: 받침대 위 금테 액자 (앞뒤 모두 그림이 보이게 — 회전판이 돌아도).
        /// 그림 비율대로 긴 변 0.34m.
        /// </summary>
        public static GameObject BuildPicture(Texture picture, Transform parent)
        {
            float aspect = picture.height > 0 ? picture.width / (float)picture.height : 1f;
            float w = aspect >= 1f ? 0.34f : 0.34f * aspect, h = aspect >= 1f ? 0.34f / aspect : 0.34f;
            var b = new ModelBuilder();
            var k = b.Kit;
            int gold = b.M("Gold"), wood = b.M("Mahogany"), vel = b.M("VelvetPlain");
            var canvasMat = new Material(ArtLibrary.I.Mat("Paint")) { name = "PictureCanvas" };
            if (canvasMat.HasProperty("_BaseMap")) canvasMat.SetTexture("_BaseMap", picture);
            if (canvasMat.HasProperty("_BaseColor")) canvasMat.SetColor("_BaseColor", Color.white);
            if (canvasMat.HasProperty("_Smoothness")) canvasMat.SetFloat("_Smoothness", 0.25f);
            int canvas = b.M("canvas", canvasMat);
            float baseY = 0.05f, cy = baseY + 0.04f + h * 0.5f;
            // 받침: 둥근 대 + 기둥
            k.Lathe(wood, new[] { P(0, 0), P(0.09f, 0), P(0.095f, 0.012f), P(0.08f, 0.024f), P(0.03f, 0.03f), P(0.02f, baseY + 0.02f), P(0, baseY + 0.02f) }, 32);
            k.Lathe(gold, new[] { P(0.0955f, 0.004f), P(0.098f, 0.008f), P(0.0955f, 0.012f) }, 32);
            k.Box(gold, new Vector3(0, baseY + 0.03f, 0), new Vector3(w * 0.5f, 0.02f, 0.03f));
            // 액자 틀 (굵은 금테 + 안쪽 벨벳 띠)
            float f = 0.026f;
            k.RoundedBox(gold, new Vector3(0, cy + h * 0.5f + f * 0.5f, 0), new Vector3(w + f * 2, f, 0.03f), 0.008f, 2);
            k.RoundedBox(gold, new Vector3(0, cy - h * 0.5f - f * 0.5f, 0), new Vector3(w + f * 2, f, 0.03f), 0.008f, 2);
            k.RoundedBox(gold, new Vector3(-w * 0.5f - f * 0.5f, cy, 0), new Vector3(f, h, 0.03f), 0.008f, 2);
            k.RoundedBox(gold, new Vector3(w * 0.5f + f * 0.5f, cy, 0), new Vector3(f, h, 0.03f), 0.008f, 2);
            foreach (var c in new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(1, 1) })
                k.Sphere(gold, new Vector3(c.x * (w * 0.5f + f * 0.5f), cy + c.y * (h * 0.5f + f * 0.5f), 0), f * 0.62f, 12, 8);
            k.Box(vel, new Vector3(0, cy, 0), new Vector3(w + 0.004f, h + 0.004f, 0.016f));
            // 앞(-Z)·뒤(+Z) 그림 (뒤판은 180° 돌려 붙여 뒤에서 봐도 좌우가 바르게)
            k.Push(new Vector3(0, cy, -0.0085f));
            k.Quad(canvas, new Vector2(w, h), Vector2.zero, Vector2.one);
            k.Pop();
            k.Push(new Vector3(0, cy, 0.0085f), new Vector3(0, 180, 0));
            k.Quad(canvas, new Vector2(w, h), Vector2.zero, Vector2.one);
            k.Pop();
            var go = b.Build("Picture", parent);
            go.AddComponent<MaterialOwner>().Material = canvasMat;
            return go;
        }

        /// <summary>모형의 대략적 높이 (카메라 맞추기).</summary>
        public static float Height(int index)
        {
            switch (((index % Count) + Count) % Count)
            {
                case 3: return 0.24f;
                case 9: return 0.2f;
                case 11: return 0.26f;
                case 4: return 0.5f;
                case 2: return 0.46f;
                default: return 0.4f;
            }
        }

        // ───────── 0 고대 항아리 ─────────

        static void Amphora(ModelBuilder b)
        {
            var k = b.Kit;
            int t = b.M("Terracotta"), d = b.M("TerracottaDark");
            k.Lathe(t, new[]
            {
                P(0, 0), P(0.055f, 0), P(0.06f, 0.012f), P(0.045f, 0.03f), P(0.05f, 0.05f), P(0.09f, 0.1f), P(0.125f, 0.17f),
                P(0.135f, 0.22f), P(0.125f, 0.27f), P(0.095f, 0.315f), P(0.055f, 0.345f), P(0.042f, 0.37f), P(0.042f, 0.405f),
                P(0.062f, 0.42f), P(0.064f, 0.432f), P(0.05f, 0.436f), P(0.036f, 0.42f), P(0.03f, 0.39f)
            }, 40);
            // 검은 띠 무늬 (몸통에 붙인 얇은 띠)
            k.Lathe(d, new[] { P(0.1305f, 0.185f), P(0.1365f, 0.2f), P(0.1365f, 0.24f), P(0.1305f, 0.255f) }, 40);
            k.Lathe(d, new[] { P(0.0915f, 0.1f), P(0.0945f, 0.105f), P(0.098f, 0.112f) }, 40);
            k.Lathe(d, new[] { P(0.102f, 0.3f), P(0.098f, 0.306f), P(0.093f, 0.312f) }, 40);
            // 손잡이 두 개 (목에서 어깨로 휘는 관)
            foreach (float s in new[] { -1f, 1f })
            {
                var path = MeshKit.Bezier(new Vector3(s * 0.04f, 0.385f, 0), new Vector3(s * 0.13f, 0.4f, 0), new Vector3(s * 0.14f, 0.32f, 0), new Vector3(s * 0.105f, 0.29f, 0), 14);
                k.Tube(t, path, new[] { 0.011f }, 10, true);
            }
        }

        // ───────── 1 황금 시계 ─────────

        static void MantelClock(ModelBuilder b)
        {
            var k = b.Kit;
            int g = b.M("Gold"), w = b.M("Mahogany"), face = b.M("ClockFace"), ink = b.M("BlackGloss"), glass = b.M("Glass");
            k.RoundedBox(w, new Vector3(0, 0.022f, 0), new Vector3(0.34f, 0.044f, 0.16f), 0.012f);
            k.RoundedBox(g, new Vector3(0, 0.05f, 0), new Vector3(0.32f, 0.016f, 0.15f), 0.006f);
            k.RoundedBox(g, new Vector3(0, 0.15f, 0), new Vector3(0.24f, 0.19f, 0.12f), 0.018f);
            // 둥근 머리 (반원통) = 옆으로 누운 돌림판
            k.Push(new Vector3(0, 0.245f, 0), new Vector3(90, 0, 0));
            k.Lathe(g, new[] { P(0, -0.06f), P(0.11f, -0.06f), P(0.12f, -0.05f), P(0.12f, 0.05f), P(0.11f, 0.06f), P(0, 0.06f) }, 32, false, false, 180f);
            k.Pop();
            // 문자판 (앞쪽 원판) + 금테 + 유리
            k.Push(new Vector3(0, 0.185f, -0.061f), new Vector3(-90, 0, 0));
            k.Lathe(g, new[] { P(0.092f, -0.002f), P(0.096f, 0.004f), P(0.088f, 0.01f) }, 40);
            k.Disc(face, 0.004f, 0.086f, 40, true);
            k.Lathe(glass, new[] { P(0.086f, 0.006f), P(0.05f, 0.012f), P(0, 0.013f) }, 32);
            k.Pop();
            // 바늘 (10시 10분)
            foreach (var hand in new[] { new Vector3(-55, 0.05f, 0.006f), new Vector3(60, 0.072f, 0.004f) })
            {
                k.Push(new Vector3(0, 0.185f, -0.0675f), new Vector3(0, 0, -hand.x));
                k.Box(ink, new Vector3(0, hand.y * 0.5f, 0), new Vector3(hand.z, hand.y, 0.002f));
                k.Pop();
            }
            k.Sphere(g, new Vector3(0, 0.185f, -0.069f), 0.006f, 12, 8);
            // 꼭대기 장식 · 다리 · 옆 소용돌이
            k.Lathe(g, new[] { P(0.012f, 0.36f), P(0.02f, 0.37f), P(0.016f, 0.385f), P(0.026f, 0.4f), P(0.018f, 0.418f), P(0, 0.43f) }, 20);
            k.Cylinder(g, 0.008f, 0.33f, 0.035f, 12, false);
            foreach (float s in new[] { -1f, 1f })
            {
                foreach (float z in new[] { -0.06f, 0.06f }) k.Sphere(g, new Vector3(s * 0.15f, 0.008f, z), 0.014f, 12, 8);
                var path = MeshKit.Bezier(new Vector3(s * 0.12f, 0.06f, -0.05f), new Vector3(s * 0.17f, 0.12f, -0.05f), new Vector3(s * 0.13f, 0.2f, -0.05f), new Vector3(s * 0.12f, 0.23f, -0.05f), 12);
                k.Tube(g, path, new[] { 0.012f, 0.008f }, 8, true);
            }
        }

        // ───────── 2 은 촛대 ─────────

        static void Candelabra(ModelBuilder b)
        {
            var k = b.Kit;
            int s = b.M("Silver"), wax = b.M("CandleWax"), fl = b.M("Flame");
            k.Lathe(s, new[]
            {
                P(0, 0), P(0.1f, 0), P(0.105f, 0.008f), P(0.09f, 0.016f), P(0.07f, 0.03f), P(0.03f, 0.06f), P(0.02f, 0.08f),
                P(0.032f, 0.095f), P(0.035f, 0.11f), P(0.02f, 0.125f), P(0.016f, 0.2f), P(0.028f, 0.215f), P(0.03f, 0.23f), P(0.016f, 0.245f), P(0.014f, 0.27f)
            }, 32);
            // 가운데 초와 양 팔
            Candle(k, s, wax, fl, new Vector3(0, 0.27f, 0), 1f);
            foreach (float sx in new[] { -1f, 1f })
            {
                var path = MeshKit.Bezier(new Vector3(0, 0.235f, 0), new Vector3(sx * 0.1f, 0.18f, 0), new Vector3(sx * 0.14f, 0.2f, 0), new Vector3(sx * 0.14f, 0.25f, 0), 16);
                k.Tube(s, path, new[] { 0.009f }, 8, false);
                Candle(k, s, wax, fl, new Vector3(sx * 0.14f, 0.25f, 0), 0.9f);
            }
        }

        static void Candle(MeshKit k, int metal, int wax, int flame, Vector3 at, float scale)
        {
            k.Push(at, Vector3.zero, scale);
            k.Lathe(metal, new[] { P(0.008f, -0.01f), P(0.02f, 0), P(0.04f, 0.008f), P(0.042f, 0.013f), P(0.024f, 0.016f), P(0.022f, 0.04f) }, 24);
            k.Lathe(wax, new[] { P(0.017f, 0.02f), P(0.017f, 0.12f), P(0.014f, 0.125f), P(0, 0.126f) }, 20);
            k.Lathe(flame, new[] { P(0, 0.128f), P(0.008f, 0.136f), P(0.01f, 0.148f), P(0.006f, 0.165f), P(0, 0.182f) }, 12);
            k.Pop();
        }

        // ───────── 3 보석 반지 ─────────

        static void Ring(ModelBuilder b)
        {
            var k = b.Kit;
            int v = b.M("VelvetPlain"), g = b.M("Gold"), gem = b.M("PinkGem"), dia = b.M("Diamond");
            k.RoundedBox(v, new Vector3(0, 0.04f, 0), new Vector3(0.2f, 0.08f, 0.2f), 0.035f, 5);
            foreach (var c in new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(1, 1) })
            {
                k.Sphere(g, new Vector3(c.x * 0.095f, 0.025f, c.y * 0.095f), 0.012f, 12, 8);
                k.Lathe(g, new[] { P(0, -0.018f), P(0.009f, -0.012f), P(0.006f, 0) }, 10);
            }
            // 반지 (세워 놓은 도넛) — 쿠션 홈에 꽂힘
            float R = 0.055f;
            k.Push(new Vector3(0, 0.075f + R, 0), new Vector3(90, 0, 0));
            k.Torus(g, R, 0.008f, 40, 12);
            k.Pop();
            // 보석 받침과 큰 보석, 옆 작은 다이아
            k.Push(new Vector3(0, 0.075f + 2 * R + 0.004f, 0));
            k.Lathe(g, new[] { P(0.01f, -0.006f), P(0.022f, 0.006f), P(0.024f, 0.012f) }, 16);
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3;
                var p0 = new Vector3(Mathf.Cos(a) * 0.02f, 0.008f, Mathf.Sin(a) * 0.02f);
                k.Tube(g, new List<Vector3> { p0, p0 * 1.1f + Vector3.up * 0.02f }, new[] { 0.0025f }, 6, true);
            }
            k.Push(new Vector3(0, 0.024f, 0));
            k.Gem(gem, 0.026f, 0.016f, 0.024f, 12);
            k.Pop();
            foreach (float sx in new[] { -1f, 1f })
            {
                k.Push(new Vector3(sx * 0.026f, 0.0f, 0), new Vector3(0, 0, -sx * 35f));
                k.Gem(dia, 0.008f, 0.005f, 0.007f, 8);
                k.Pop();
            }
            k.Pop();
        }

        // ───────── 4 명화 (이젤) ─────────

        static void Painting(ModelBuilder b)
        {
            var k = b.Kit;
            int w = b.M("Mahogany"), g = b.M("Gold"), canvas = b.M("Painting2");
            // 이젤 다리
            k.Push(new Vector3(-0.1f, 0, 0.01f), new Vector3(-10, 0, -8));
            k.RoundedBox(w, new Vector3(0, 0.25f, 0), new Vector3(0.022f, 0.5f, 0.022f), 0.006f);
            k.Pop();
            k.Push(new Vector3(0.1f, 0, 0.01f), new Vector3(-10, 0, 8));
            k.RoundedBox(w, new Vector3(0, 0.25f, 0), new Vector3(0.022f, 0.5f, 0.022f), 0.006f);
            k.Pop();
            k.Push(new Vector3(0, 0, 0.13f), new Vector3(20, 0, 0));
            k.RoundedBox(w, new Vector3(0, 0.22f, 0), new Vector3(0.02f, 0.46f, 0.02f), 0.006f);
            k.Pop();
            k.RoundedBox(w, new Vector3(0, 0.13f, -0.04f), new Vector3(0.28f, 0.018f, 0.05f), 0.005f);
            // 액자 (뒤로 기댄 그림)
            k.Push(new Vector3(0, 0.29f, -0.02f), new Vector3(-10, 0, 0));
            float fw = 0.3f, fh = 0.23f, bar = 0.03f;
            k.RoundedBox(g, new Vector3(0, fh * 0.5f, 0), new Vector3(fw, bar, 0.028f), 0.01f);
            k.RoundedBox(g, new Vector3(0, -fh * 0.5f, 0), new Vector3(fw, bar, 0.028f), 0.01f);
            k.RoundedBox(g, new Vector3(-fw * 0.5f + bar * 0.5f, 0, 0), new Vector3(bar, fh, 0.028f), 0.01f);
            k.RoundedBox(g, new Vector3(fw * 0.5f - bar * 0.5f, 0, 0), new Vector3(bar, fh, 0.028f), 0.01f);
            k.Push(new Vector3(0, 0, -0.004f));
            k.Quad(canvas, new Vector2(fw - bar * 1.6f, fh - bar * 0.6f), Vector2.zero, Vector2.one);
            k.Pop();
            k.Box(w, new Vector3(0, 0, 0.006f), new Vector3(fw - bar, fh - bar * 0.5f, 0.008f));
            k.Pop();
        }

        // ───────── 5 청자 매병 ─────────

        static void Celadon(ModelBuilder b)
        {
            var k = b.Kit;
            int c = b.M("Celadon");
            k.Lathe(c, new[]
            {
                P(0, 0), P(0.06f, 0), P(0.064f, 0.01f), P(0.07f, 0.06f), P(0.085f, 0.14f), P(0.11f, 0.23f), P(0.128f, 0.29f),
                P(0.13f, 0.315f), P(0.118f, 0.345f), P(0.085f, 0.37f), P(0.042f, 0.385f), P(0.032f, 0.4f), P(0.04f, 0.412f),
                P(0.04f, 0.42f), P(0.026f, 0.42f), P(0.022f, 0.395f)
            }, 48);
        }

        // ───────── 6 골동품 지구본 ─────────

        static void Globe(ModelBuilder b)
        {
            var k = b.Kit;
            int w = b.M("Mahogany"), br = b.M("Brass"), gl = b.M("Globe");
            k.Lathe(w, new[] { P(0, 0), P(0.1f, 0), P(0.105f, 0.012f), P(0.095f, 0.022f), P(0.04f, 0.035f), P(0.025f, 0.06f), P(0.035f, 0.08f), P(0.02f, 0.1f), P(0.018f, 0.13f) }, 32);
            float cy = 0.27f, r = 0.12f;
            k.Push(new Vector3(0, cy, 0), new Vector3(0, 0, 23.5f));
            k.Ellipsoid(gl, Vector3.zero, Vector3.one * r, 48, 28);
            // 자오선 고리 (반원)
            k.Push(Vector3.zero, new Vector3(90, 0, 0));
            k.Torus(br, r + 0.012f, 0.0055f, 48, 8, 360f);
            k.Pop();
            k.Cylinder(br, 0.004f, -r - 0.018f, 0.012f, 8, true);
            k.Cylinder(br, 0.004f, r + 0.006f, 0.012f, 8, true);
            k.Pop();
            // 받침 기둥 → 고리 아래
            k.Tube(br, new List<Vector3> { new Vector3(0, 0.13f, 0), new Vector3(-0.035f, 0.14f, 0) }, new[] { 0.008f }, 8, true);
            k.Lathe(br, new[] { P(0.022f, 0.12f), P(0.026f, 0.13f), P(0.016f, 0.14f) }, 16);
        }

        // ───────── 7 수정 구슬 ─────────

        static void CrystalBall(ModelBuilder b)
        {
            var k = b.Kit;
            int g = b.M("GoldDark"), cr = b.M("CrystalBall"), glow = b.M("Sapphire"), stand = b.M("MarbleDark");
            k.Lathe(stand, new[] { P(0, 0), P(0.1f, 0), P(0.1f, 0.03f), P(0.085f, 0.04f), P(0, 0.04f) }, 40, false, false, 360f, true);
            k.Lathe(g, new[] { P(0.07f, 0.04f), P(0.075f, 0.05f), P(0.05f, 0.075f), P(0.045f, 0.1f) }, 32);
            // 구슬을 받치는 발톱 세 개
            for (int i = 0; i < 3; i++)
            {
                float a = i * Mathf.PI * 2 / 3;
                var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                var path = MeshKit.Bezier(dir * 0.045f + Vector3.up * 0.09f, dir * 0.1f + Vector3.up * 0.11f, dir * 0.105f + Vector3.up * 0.17f, dir * 0.07f + Vector3.up * 0.2f, 12);
                k.Tube(g, path, new[] { 0.009f, 0.005f }, 8, true);
            }
            k.Sphere(glow, new Vector3(0, 0.205f, 0), 0.03f, 16, 12);
            k.Sphere(cr, new Vector3(0, 0.205f, 0), 0.105f, 40, 28);
        }

        // ───────── 8 왕관 ─────────

        static void Crown(ModelBuilder b)
        {
            var k = b.Kit;
            int g = b.M("Gold"), vel = b.M("VelvetPlain"), ru = b.M("Ruby"), sa = b.M("Sapphire"), em = b.M("Emerald"), pe = b.M("Pearl");
            float r = 0.11f;
            // 아래 띠
            k.Lathe(g, new[] { P(r - 0.004f, 0), P(r + 0.006f, 0.004f), P(r + 0.008f, 0.02f), P(r + 0.006f, 0.06f), P(r + 0.01f, 0.068f), P(r + 0.004f, 0.075f), P(r - 0.004f, 0.075f), P(r - 0.004f, 0) }, 48);
            // 벨벳 모자
            k.Ellipsoid(vel, new Vector3(0, 0.06f, 0), new Vector3(r - 0.012f, 0.085f, r - 0.012f), 32, 16);
            // 띠의 보석
            int n = 8;
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2 / n;
                var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                k.Push(dir * (r + 0.009f) + Vector3.up * 0.038f, Quaternion.LookRotation(dir) * Quaternion.Euler(90, 0, 0), Vector3.one);
                k.Gem(i % 3 == 0 ? ru : (i % 3 == 1 ? sa : em), 0.012f, 0.008f, 0.004f, 8);
                k.Pop();
            }
            // 위로 솟은 아치 네 개 + 끝의 진주
            for (int i = 0; i < 8; i++)
            {
                float a = (i + 0.5f) * Mathf.PI * 2 / 8;
                var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                var tip = dir * (r * 0.95f) + Vector3.up * 0.15f;
                var path = MeshKit.Bezier(dir * (r + 0.004f) + Vector3.up * 0.07f, dir * (r + 0.02f) + Vector3.up * 0.11f, tip, 10);
                k.Tube(g, path, new[] { 0.008f, 0.005f }, 8, false);
                k.Sphere(pe, tip + Vector3.up * 0.008f, 0.011f, 12, 8);
            }
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI / 2;
                var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                var path = MeshKit.Bezier(dir * (r + 0.004f) + Vector3.up * 0.075f, dir * (r * 0.9f) + Vector3.up * 0.2f, Vector3.up * 0.2f, 14);
                k.Tube(g, path, new[] { 0.007f }, 8, false);
            }
            // 꼭대기 십자 구슬
            k.Sphere(g, new Vector3(0, 0.215f, 0), 0.022f, 16, 12);
            k.Box(g, new Vector3(0, 0.25f, 0), new Vector3(0.01f, 0.04f, 0.01f));
            k.Box(g, new Vector3(0, 0.255f, 0), new Vector3(0.03f, 0.01f, 0.01f));
        }

        // ───────── 9 회중시계 ─────────

        static void PocketWatch(ModelBuilder b)
        {
            var k = b.Kit;
            int g = b.M("Gold"), face = b.M("ClockFace"), ink = b.M("BlackGloss"), glass = b.M("Glass"), v = b.M("VelvetPlain");
            // 작은 벨벳 받침대에 비스듬히 세운 시계
            k.Lathe(v, new[] { P(0, 0), P(0.09f, 0), P(0.095f, 0.01f), P(0.085f, 0.025f), P(0, 0.03f) }, 32);
            k.Push(new Vector3(0, 0.1f, 0.01f), new Vector3(-75, 0, 0));
            float r = 0.06f;
            k.Lathe(g, new[] { P(0, -0.012f), P(r * 0.7f, -0.012f), P(r, -0.006f), P(r + 0.002f, 0), P(r, 0.006f), P(r * 0.96f, 0.008f) }, 40);
            k.Disc(face, 0.006f, r * 0.9f, 40, true);
            k.Lathe(glass, new[] { P(r * 0.9f, 0.007f), P(r * 0.5f, 0.012f), P(0, 0.013f) }, 32);
            foreach (var hand in new[] { new Vector3(-50, 0.03f, 0.004f), new Vector3(70, 0.044f, 0.003f) })
            {
                k.Push(new Vector3(0, 0.0085f, 0), new Vector3(0, hand.x, 0));
                k.Box(ink, new Vector3(0, 0, hand.y * 0.5f), new Vector3(hand.z, 0.0015f, hand.y));
                k.Pop();
            }
            // 용두와 고리
            k.Cylinder(g, 0.008f, 0, 0.001f, 12, false);
            k.Push(new Vector3(0, 0, r + 0.012f), new Vector3(90, 0, 0));
            k.Cylinder(g, 0.009f, -0.006f, 0.012f, 12, true);
            k.Pop();
            k.Push(new Vector3(0, 0, r + 0.03f), new Vector3(0, 0, 90));
            k.Torus(g, 0.014f, 0.003f, 24, 8);
            k.Pop();
            k.Pop();
            // 늘어진 줄 (작은 고리들)
            var chain = MeshKit.Bezier(new Vector3(0, 0.185f, 0.04f), new Vector3(0.09f, 0.2f, 0.02f), new Vector3(0.13f, 0.03f, -0.04f), new Vector3(0.08f, 0.032f, -0.08f), 22);
            for (int i = 0; i < chain.Count; i++)
            {
                var dir = i < chain.Count - 1 ? chain[i + 1] - chain[i] : chain[i] - chain[i - 1];
                k.Push(chain[i], Quaternion.LookRotation(dir.normalized) * Quaternion.Euler(0, 0, i % 2 == 0 ? 0 : 90) * Quaternion.Euler(0, 90, 0), Vector3.one);
                k.Torus(g, 0.005f, 0.0015f, 10, 6);
                k.Pop();
            }
        }

        // ───────── 10 도자기 인형 ─────────

        static void Figurine(ModelBuilder b)
        {
            var k = b.Kit;
            int p = b.M("Porcelain"), blue = b.M("PorcelainBlue"), g = b.M("Gold");
            // 얼굴이 플레이어 쪽(-Z)을 보게
            k.Push(Vector3.zero, new Vector3(0, 180, 0));
            // 둥근 받침 + 금테 + 작은 파란 꽃
            k.Lathe(p, new[] { P(0, 0), P(0.085f, 0), P(0.09f, 0.008f), P(0.085f, 0.02f), P(0.07f, 0.026f), P(0, 0.028f) }, 40);
            k.Lathe(g, new[] { P(0.091f, 0.004f), P(0.093f, 0.008f), P(0.091f, 0.012f) }, 40);
            for (int i = 0; i < 7; i++)
            {
                float a = i * Mathf.PI * 2 / 7 + 0.3f;
                k.Sphere(blue, new Vector3(Mathf.Cos(a) * 0.06f, 0.027f, Mathf.Sin(a) * 0.06f), 0.006f, 8, 6);
            }
            // 발끝으로 선 다리 + 뒤로 들어 올린 다리 (아라베스크)
            k.Tube(p, new List<Vector3> { new Vector3(0.004f, 0.028f, 0.002f), new Vector3(0.007f, 0.08f, 0.003f), new Vector3(0.01f, 0.122f, 0) }, new[] { 0.006f, 0.009f, 0.0135f }, 12, true);
            k.Lathe(p, new[] { P(0.007f, 0.026f), P(0.011f, 0.028f), P(0.0045f, 0.034f) }, 12);
            k.Tube(p, MeshKit.Bezier(new Vector3(-0.006f, 0.124f, -0.004f), new Vector3(-0.012f, 0.12f, -0.055f), new Vector3(-0.016f, 0.15f, -0.1f), 10), new[] { 0.0125f, 0.01f, 0.0075f, 0.0055f }, 12, true);
            // 튀튀: 두툼하게 퍼진 주름 치마 + 파란 단
            k.Lathe(p, new[] { P(0.026f, 0.11f), P(0.052f, 0.114f), P(0.074f, 0.12f), P(0.082f, 0.129f), P(0.074f, 0.138f), P(0.05f, 0.144f), P(0.028f, 0.148f) }, 48);
            for (int i = 0; i < 18; i++)
            {
                float a = i * Mathf.PI * 2 / 18;
                var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                k.Push(dir * 0.076f + Vector3.up * (0.128f + (i % 2) * 0.004f), Quaternion.LookRotation(dir), Vector3.one);
                k.Ellipsoid(p, Vector3.zero, new Vector3(0.016f, 0.01f, 0.012f), 10, 6);
                k.Pop();
            }
            k.Push(new Vector3(0, 0.124f, 0));
            k.Torus(blue, 0.081f, 0.0025f, 48, 6);
            k.Pop();
            // 코르셋 몸통 · 목
            k.Lathe(p, new[] { P(0.026f, 0.144f), P(0.021f, 0.165f), P(0.025f, 0.19f), P(0.027f, 0.204f), P(0.02f, 0.214f), P(0.009f, 0.222f), P(0.0075f, 0.233f) }, 32);
            foreach (float y in new[] { 0.158f, 0.176f })
                k.Lathe(blue, new[] { P(0.0225f, y - 0.003f), P(0.0235f, y), P(0.0225f, y + 0.003f) }, 32);
            // 머리: 얼굴 · 작은 코 · 파란 눈 · 올림머리
            k.Ellipsoid(p, new Vector3(0, 0.252f, 0.002f), new Vector3(0.02f, 0.024f, 0.021f), 24, 16);
            k.Sphere(p, new Vector3(0, 0.249f, 0.022f), 0.0035f, 8, 6);
            foreach (float s in new[] { -1f, 1f }) k.Ellipsoid(blue, new Vector3(s * 0.0075f, 0.256f, 0.0195f), new Vector3(0.0028f, 0.0018f, 0.0012f), 8, 6);
            k.Ellipsoid(blue, new Vector3(0, 0.262f, -0.004f), new Vector3(0.0215f, 0.017f, 0.02f), 20, 12);
            k.Sphere(blue, new Vector3(0, 0.276f, -0.01f), 0.012f, 14, 10);
            // 팔: 한 팔은 머리 위로 둥글게, 한 팔은 옆으로 뻗어 (손끝까지)
            var up = MeshKit.Bezier(new Vector3(0.022f, 0.205f, 0), new Vector3(0.055f, 0.245f, 0.004f), new Vector3(0.012f, 0.3f, 0.006f), 12);
            k.Tube(p, up, new[] { 0.0058f, 0.0052f, 0.0045f, 0.004f, 0.0036f }, 8, true);
            k.Ellipsoid(p, up[up.Count - 1] + new Vector3(-0.004f, 0.003f, 0), new Vector3(0.006f, 0.0045f, 0.003f), 8, 6);
            var side = MeshKit.Bezier(new Vector3(-0.022f, 0.205f, 0), new Vector3(-0.06f, 0.214f, 0.012f), new Vector3(-0.094f, 0.198f, 0.022f), 12);
            k.Tube(p, side, new[] { 0.0058f, 0.0052f, 0.0045f, 0.004f, 0.0036f }, 8, true);
            k.Ellipsoid(p, side[side.Count - 1] + new Vector3(-0.005f, -0.002f, 0.001f), new Vector3(0.006f, 0.0045f, 0.003f), 8, 6);
            k.Pop();
        }

        // ───────── 11 고서 ─────────

        static void Books(ModelBuilder b)
        {
            var k = b.Kit;
            int lr = b.M("LeatherRed"), lg = b.M("LeatherGreen"), lb = b.M("Leather"), pg = b.M("Pages"), g = b.M("GoldDark");
            Book(k, lb, pg, g, new Vector3(0, 0, 0), 4f, new Vector3(0.26f, 0.06f, 0.19f));
            Book(k, lg, pg, g, new Vector3(0.01f, 0.06f, -0.005f), -7f, new Vector3(0.23f, 0.05f, 0.17f));
            Book(k, lr, pg, g, new Vector3(-0.005f, 0.11f, 0.004f), 12f, new Vector3(0.2f, 0.045f, 0.15f));
            // 깃펜
            k.Tube(pg, MeshKit.Bezier(new Vector3(-0.08f, 0.16f, -0.02f), new Vector3(0, 0.17f, -0.05f), new Vector3(0.1f, 0.2f, -0.03f), 10), new[] { 0.002f }, 6, true);
            k.Push(new Vector3(0.04f, 0.185f, -0.04f), new Vector3(0, 70, 20));
            k.CurvedPanel(pg, 0.12f, 0.03f, 0.3f, u => new Vector2(-1f + 0.6f * u * u, 1f - 0.8f * u * u), 16, 3, 0.002f);
            k.Pop();
        }

        static void Book(MeshKit k, int cover, int pages, int gold, Vector3 at, float yaw, Vector3 size)
        {
            k.Push(at, new Vector3(0, yaw, 0));
            float t = 0.007f;
            // 표지 두 장 + 책등, 안쪽 종이 덩어리
            k.RoundedBox(cover, new Vector3(0, t * 0.5f, 0), new Vector3(size.x, t, size.z), 0.003f);
            k.RoundedBox(cover, new Vector3(0, size.y - t * 0.5f, 0), new Vector3(size.x, t, size.z), 0.003f);
            k.RoundedBox(cover, new Vector3(-size.x * 0.5f + 0.008f, size.y * 0.5f, 0), new Vector3(0.018f, size.y, size.z), 0.008f);
            k.RoundedBox(pages, new Vector3(0.004f, size.y * 0.5f, 0), new Vector3(size.x - 0.014f, size.y - 2 * t, size.z - 0.01f), 0.002f);
            foreach (float z in new[] { -0.3f, 0.3f })
                k.Box(gold, new Vector3(-size.x * 0.5f - 0.0015f + 0.003f, size.y * 0.5f, z * size.z), new Vector3(0.004f, size.y * 0.9f, 0.008f));
            k.Pop();
        }
    }
}
