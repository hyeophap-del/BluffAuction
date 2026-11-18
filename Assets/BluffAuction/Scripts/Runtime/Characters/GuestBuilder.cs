using System.Collections.Generic;
using BluffAuction.Art;
using UnityEngine;
using static BluffAuction.Art.ModelBuilder;

namespace BluffAuction.Characters
{
    /// <summary>
    /// 가면 손님 모형을 부위별(다리·몸통·머리·팔 위/아래·손가락·패들)로 만든다. 원점은 의자 앞 바닥 가운데, 얼굴은 +Z.
    /// 움직임은 <see cref="Guest"/> 가 관절 Transform 을 돌려 만든다.
    /// 옷은 앞이 V 자로 트인 외투(LatheOpen) 안에 셔츠를 겹쳐 만들고, 트인 가장자리를 금빛 테로 두른다.
    /// </summary>
    public static class GuestBuilder
    {
        sealed class Mats
        {
            public Material Coat, Trim, Shirt, Pants, Skin, Mask, MaskTrim, Hair, Hat, Accent, Glove, Seat, Iris, Sclera, MouthIn, Sleeve, Lid;
        }

        public static Guest Build(Transform parent, CharacterLook look, Color seatColor, bool standing = false)
        {
            var lib = ArtLibrary.I;
            var m = new Mats
            {
                Coat = lib.Tinted(look.Gown ? "Satin" : "VelvetCloth", look.Coat),
                Trim = lib.Tinted("Satin", look.Trim),
                Shirt = lib.Tinted("Cloth", look.Shirt),
                Pants = lib.Tinted("Cloth", look.Pants),
                Skin = lib.Tinted("Skin", look.Skin),
                Mask = lib.Tinted("Lacquer", look.MaskColor),
                MaskTrim = lib.Tinted("Satin", look.MaskTrim),
                Hair = lib.Tinted("Hair", look.Hair),
                Hat = lib.Tinted("Satin", look.HatColor),
                Accent = lib.Tinted("Satin", look.Accent),
                Glove = lib.Tinted("Cloth", look.Glove),
                Seat = lib.Tinted("Glow", seatColor, 0.6f),
                Iris = lib.Tinted("Lacquer", look.EyeColor),
                Sclera = lib.Tinted("Lacquer", new Color(0.97f, 0.96f, 0.93f)),
                MouthIn = lib.Tinted("Lacquer", new Color(0.28f, 0.05f, 0.07f)),
                Lid = lib.Tinted("Lacquer", look.Mask == MaskStyle.None || look.Mask == MaskStyle.EyePatch ? look.Skin : look.MaskColor),
            };
            m.Sleeve = look.Outfit == Outfit.Vest ? m.Shirt : m.Coat;
            var root = new GameObject("Guest_" + look.Id);
            root.transform.SetParent(parent, false);
            var g = root.AddComponent<Guest>();
            g.Look = look;
            g.Standing = standing;

            float W = look.Width, H = look.Height;
            float hip = standing ? 0.95f * H : 0.5f;

            BuildLegs(root.transform, look, m, standing, hip, W);

            // ── 몸통 (숨쉬기·기울기 기준점 = 엉덩이)
            var torsoPivot = new GameObject("Torso").transform;
            torsoPivot.SetParent(root.transform, false);
            torsoPivot.localPosition = new Vector3(0, hip, 0);
            g.Torso = torsoPivot;
            BuildTorso(torsoPivot, look, m, W, H);

            // ── 머리
            var headPivot = new GameObject("Head").transform;
            headPivot.SetParent(torsoPivot, false);
            headPivot.localPosition = new Vector3(0, 0.535f * H, 0.012f);
            g.Head = headPivot;
            BuildHead(g, headPivot, look, m);

            // ── 팔 (어깨 관절 → 팔꿈치 관절 → 손목 → 손가락)
            for (int side = 0; side < 2; side++)
            {
                float s = side == 0 ? -1f : 1f;
                var shoulder = new GameObject(side == 0 ? "ShoulderL" : "ShoulderR").transform;
                shoulder.SetParent(torsoPivot, false);
                shoulder.localPosition = new Vector3(s * 0.178f * W, 0.405f * H, -0.005f);
                BuildUpperArm(shoulder, look, m, W);
                var elbow = new GameObject("Elbow").transform;
                elbow.SetParent(shoulder, false);
                elbow.localPosition = new Vector3(0, -0.215f, 0);
                BuildForearm(elbow, look, m);
                var wrist = new GameObject("Hand").transform;
                wrist.SetParent(elbow, false);
                wrist.localPosition = new Vector3(0, -0.212f, 0.004f);
                var gh = GloveHand.Build(wrist, m.Glove, s);
                if (side == 0) { g.ShoulderL = shoulder; g.ElbowL = elbow; g.HandL = wrist; g.GloveL = gh; }
                else { g.ShoulderR = shoulder; g.ElbowR = elbow; g.HandR = wrist; g.GloveR = gh; }
            }

            // ── 패들 (오른손에 쥠) — 경매사는 의사봉
            var pb = new ModelBuilder();
            var pk = pb.Kit;
            if (look.Id == "auctioneer")
            {
                int pw = pb.M("Mahogany"), pbr = pb.M("Brass");
                pk.Cylinder(pw, 0.011f, -0.05f, 0.22f, 10, true);
                pk.Push(new Vector3(0, 0.19f, 0), new Vector3(0, 0, 90));
                pk.Lathe(pw, new[] { P(0.032f, -0.06f), P(0.036f, -0.05f), P(0.036f, 0.05f), P(0.032f, 0.06f) }, 18, true, true);
                pk.Lathe(pbr, new[] { P(0.0365f, -0.035f), P(0.039f, -0.03f), P(0.0365f, -0.025f) }, 18);
                pk.Lathe(pbr, new[] { P(0.0365f, 0.025f), P(0.039f, 0.03f), P(0.0365f, 0.035f) }, 18);
                pk.Pop();
            }
            else
            {
                int pw = pb.M("Mahogany"), face = pb.M("Paint"), rim = pb.M("seat", m.Seat), pgold = pb.M("GoldDark");
                pk.Cylinder(pw, 0.011f, -0.06f, 0.19f, 10, true);
                pk.Push(new Vector3(0, 0.2f, 0), new Vector3(90, 0, 0));
                pk.Lathe(rim, new[] { P(0, -0.008f), P(0.09f, -0.008f), P(0.094f, 0), P(0.09f, 0.008f), P(0, 0.008f) }, 32, false, false, 360f, false);
                pk.Disc(face, 0.0085f, 0.072f, 32, true);
                pk.Disc(face, -0.0085f, 0.072f, 32, false);
                pk.Pop();
                pk.Lathe(pgold, new[] { P(0.013f, 0.1f), P(0.015f, 0.11f), P(0.013f, 0.12f) }, 10);
            }
            // 손잡이가 주먹 안을 지나도록 손바닥 가운데에 (손가락 방향과 직각)
            var paddle = pb.Build("Paddle", g.GloveR.Grip);
            paddle.transform.localPosition = new Vector3(0, -0.012f, 0);
            g.Paddle = paddle.transform;
            g.Init();
            return g;
        }

        // ═════════════════ 다리·치마 ═════════════════

        static void BuildLegs(Transform root, CharacterLook look, Mats m, bool standing, float hip, float W)
        {
            var body = new ModelBuilder();
            int pants = body.M("pants", m.Pants), shoe = body.M("BlackGloss"), coatB = body.M("coat", m.Coat), trimB = body.M("trim", m.Trim);
            var bk = body.Kit;
            if (look.Gown)
            {
                // 치마: 무릎 쪽으로 쏠린 종 모양 + 밑단 테
                bk.Push(new Vector3(0, 0, standing ? 0 : 0.1f), Quaternion.identity, new Vector3(W, 1, standing ? W : 1.08f));
                var skirt = new[] { P(0.37f, 0.0f), P(0.37f, 0.05f), P(0.345f, standing ? 0.4f : 0.28f), P(0.29f, standing ? 0.7f : 0.45f), P(0.21f, hip + 0.02f), P(0.16f, hip + 0.06f) };
                bk.Lathe(coatB, skirt, 40, true);
                bk.Lathe(trimB, new[] { P(0.372f, 0.025f), P(0.378f, 0.04f), P(0.372f, 0.055f) }, 40);
                bk.Pop();
            }
            else if (standing)
            {
                foreach (float s in new[] { -1f, 1f })
                {
                    bk.Tube(pants, new List<Vector3> { new Vector3(s * 0.09f * W, hip, 0), new Vector3(s * 0.095f * W, 0.08f, 0) }, new[] { 0.075f * W, 0.055f }, 14, true);
                    Shoe(bk, shoe, new Vector3(s * 0.095f * W, 0, 0.03f));
                }
            }
            else
            {
                foreach (float s in new[] { -1f, 1f })
                {
                    var hipP = new Vector3(s * 0.088f * W, 0.52f, -0.02f);
                    var knee = new Vector3(s * 0.1f * W, 0.53f, 0.39f);
                    var ankle = new Vector3(s * 0.102f * W, 0.1f, 0.43f);
                    bk.Tube(pants, MeshKit.Bezier(hipP, hipP + new Vector3(0, 0.012f, 0.2f), knee, 6), new[] { 0.084f * W, 0.078f * W, 0.07f, 0.066f, 0.064f, 0.063f, 0.062f }, 14, true);
                    bk.Sphere(pants, knee, 0.062f, 14, 10);
                    bk.Tube(pants, new List<Vector3> { knee, knee + new Vector3(0, -0.2f, 0.02f), ankle }, new[] { 0.058f, 0.05f, 0.046f }, 14, true);
                    Shoe(bk, shoe, new Vector3(ankle.x, 0, ankle.z + 0.03f));
                }
            }
            body.Build("Body", root);
        }

        static void Shoe(MeshKit k, int mat, Vector3 at)
        {
            // 둥근 앞코 + 굽
            k.Ellipsoid(mat, at + new Vector3(0, 0.042f, 0.045f), new Vector3(0.05f, 0.04f, 0.105f), 16, 10);
            k.RoundedBox(mat, at + new Vector3(0, 0.012f, -0.035f), new Vector3(0.07f, 0.024f, 0.06f), 0.008f, 2);
        }

        // ═════════════════ 몸통 ═════════════════

        static readonly float[] OpenTailcoat = { 38, 30, 16, 5, 14, 24, 32, 42, 54, 70, 88, 100 };
        static readonly float[] OpenVest = { 26, 18, 8, 4, 10, 18, 26, 36, 50, 68, 88, 100 };
        static readonly float[] OpenClosed = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 30, 62, 95 };

        static void BuildTorso(Transform torso, CharacterLook look, Mats m, float W, float H)
        {
            var tb = new ModelBuilder();
            var tk = tb.Kit;
            int coat = tb.M("coat", m.Coat), shirt = tb.M("shirt", m.Shirt), trim = tb.M("trim", m.Trim), acc = tb.M("acc", m.Accent),
                skin = tb.M("skin", m.Skin), gold = tb.M("Gold"), pearl = tb.M("Pearl"), dark = tb.M("BlackGloss");
            float belly = Mathf.Clamp01((W - 1f) * 2.5f);
            var prof = new[]
            {
                P(0.15f, -0.02f), P(0.163f + 0.01f * belly, 0.03f), P(0.17f + 0.032f * belly, 0.09f), P(0.178f + 0.04f * belly, 0.16f), P(0.188f + 0.026f * belly, 0.24f),
                P(0.196f + 0.01f * belly, 0.31f), P(0.194f, 0.36f), P(0.18f, 0.405f), P(0.15f, 0.44f), P(0.105f, 0.465f), P(0.07f, 0.482f), P(0.05f, 0.5f)
            };
            float depth = 0.8f;
            var scale = new Vector3(W, H, W * depth);
            System.Func<Vector3, Vector3> S = p => Vector3.Scale(p, scale);

            float[] open = look.Outfit == Outfit.Vest ? OpenVest : (look.Outfit == Outfit.Uniform || look.Outfit == Outfit.Sweater || look.Gown ? OpenClosed : OpenTailcoat);

            tk.Push(Vector3.zero, Quaternion.identity, scale);
            if (look.Gown)
            {
                // 드레스: 가슴 위까지 옷, 그 위는 살 (어깨가 드러난 목선)
                var lower = new List<Vector2>();
                var upper = new List<Vector2>();
                foreach (var p in prof) { if (p.y <= 0.405f) lower.Add(p); if (p.y >= 0.405f) upper.Add(p); }
                tk.Lathe(coat, lower, 40);
                tk.Lathe(skin, upper, 40);
                // 목선 테·허리띠
                tk.Lathe(trim, new[] { P(0.181f, 0.396f), P(0.186f, 0.405f), P(0.181f, 0.414f) }, 40);
                tk.Lathe(trim, new[] { P(0.17f, 0.08f), P(0.176f + 0.03f * belly, 0.1f), P(0.172f, 0.12f) }, 40);
            }
            else
            {
                // 셔츠 (트인 곳으로만 보인다)
                var inner = new List<Vector2>();
                foreach (var p in prof) if (p.y >= 0.06f) inner.Add(new Vector2(p.x * 0.975f, p.y));
                tk.Lathe(shirt, inner, 40);
                tk.LatheOpen(coat, prof, open, 44);
            }
            tk.Pop();

            if (!look.Gown && open != OpenClosed)
            {
                // 트인 가장자리 금빛 테 (깃)
                foreach (float s in new[] { -1f, 1f })
                {
                    var edge = MeshKit.OpenEdge(prof, open, s, 0.002f);
                    var path = new List<Vector3>();
                    for (int i = 0; i < edge.Count; i++)
                    {
                        if (open[i] >= 86f) break;
                        path.Add(S(edge[i]));
                    }
                    if (path.Count >= 2) tk.Tube(trim, path, new[] { 0.0062f }, 6, true);
                }
            }

            float frontZ(float y, float x)
            {
                // 몸 앞면의 z (단추·장식 붙일 곳)
                float r = 0.15f;
                for (int i = 1; i < prof.Length; i++)
                    if (y <= prof[i].y) { float t = Mathf.InverseLerp(prof[i - 1].y, prof[i].y, y); r = Mathf.Lerp(prof[i - 1].x, prof[i].x, t); break; }
                float xx = Mathf.Clamp(x / W, -r * 0.95f, r * 0.95f);
                return Mathf.Sqrt(r * r - xx * xx) * W * depth;
            }

            switch (look.Outfit)
            {
                case Outfit.Tailcoat:
                case Outfit.Cloak:
                    foreach (float y in new[] { 0.09f, 0.16f }) tk.Sphere(gold, new Vector3(0, y * H, frontZ(y, 0) + 0.004f), 0.0085f, 10, 8);
                    // 주머니 장식 수건
                    tk.Push(new Vector3(-0.1f * W, 0.29f * H, frontZ(0.29f, -0.1f * W) + 0.002f), new Vector3(0, -30, 0));
                    tk.Ellipsoid(acc, Vector3.zero, new Vector3(0.022f, 0.012f, 0.006f), 10, 6);
                    tk.Pop();
                    break;
                case Outfit.Vest:
                    for (int i = 0; i < 4; i++) { float y = 0.05f + i * 0.055f; tk.Sphere(gold, new Vector3(0, y * H, frontZ(y, 0) + 0.003f), 0.0075f, 10, 8); }
                    break;
                case Outfit.Uniform:
                    for (int i = 0; i < 3; i++)
                        foreach (float x in new[] { -0.055f, 0.055f })
                        {
                            float y = 0.12f + i * 0.09f;
                            tk.Sphere(gold, new Vector3(x * W, y * H, frontZ(y, x * W) + 0.003f), 0.0085f, 10, 8);
                        }
                    // 앞단 금줄 · 허리띠
                    tk.Tube(trim, new List<Vector3> { new Vector3(0, 0.0f, frontZ(0f, 0) + 0.002f), new Vector3(0, 0.2f * H, frontZ(0.2f, 0) + 0.002f), new Vector3(0, 0.4f * H, frontZ(0.4f, 0) + 0.002f) }, new[] { 0.004f }, 5, true);
                    tk.Push(Vector3.zero, Quaternion.identity, scale);
                    tk.Lathe(trim, new[] { P(0.172f + 0.04f * belly, 0.13f), P(0.18f + 0.042f * belly, 0.15f), P(0.18f + 0.042f * belly, 0.175f), P(0.172f + 0.04f * belly, 0.19f) }, 40);
                    tk.Pop();
                    // 견장
                    foreach (float s in new[] { -1f, 1f })
                    {
                        tk.Ellipsoid(gold, new Vector3(s * 0.158f * W, 0.425f * H, -0.004f), new Vector3(0.058f, 0.016f, 0.058f), 16, 8);
                        for (int f = 0; f < 7; f++)
                        {
                            float a = (f / 6f - 0.5f) * 1.7f;
                            var top = new Vector3(s * (0.158f * W + 0.05f * Mathf.Cos(a)), 0.42f * H, Mathf.Sin(a) * 0.048f - 0.004f);
                            tk.Tube(gold, new List<Vector3> { top, top + new Vector3(s * 0.006f, -0.045f, 0) }, new[] { 0.0045f }, 5, true);
                        }
                    }
                    break;
                case Outfit.Sweater:
                    // 골지 무늬 (가로 줄) 와 목 폴라
                    tk.Push(Vector3.zero, Quaternion.identity, scale);
                    for (int i = 0; i < 3; i++)
                    {
                        float y = 0.0f + i * 0.018f;
                        tk.Lathe(coat, new[] { P(0.154f, y), P(0.158f, y + 0.008f), P(0.154f, y + 0.016f) }, 40);
                    }
                    tk.Pop();
                    break;
            }

            // 망토: 어깨에서 등 뒤로 퍼지는 반원통 + 높은 깃
            if (look.Outfit == Outfit.Cloak)
            {
                // 돌림판 0° 를 150° 로 돌려 등 뒤 240° 만 (앞은 비운다)
                tk.Push(Vector3.zero, Quaternion.Euler(0, -150, 0), new Vector3(W, H, W * 0.9f));
                var cape = new[] { P(0.31f, -0.08f), P(0.27f, 0.1f), P(0.235f, 0.25f), P(0.215f, 0.36f), P(0.19f, 0.43f), P(0.13f, 0.47f) };
                tk.Lathe(coat, cape, 28, false, false, 240f);
                // 안감: 반지름을 조금 줄이고 순서를 뒤집어 안쪽을 보게
                var lining = new Vector2[cape.Length];
                for (int i = 0; i < cape.Length; i++) lining[i] = new Vector2(cape[cape.Length - 1 - i].x - 0.005f, cape[cape.Length - 1 - i].y);
                tk.Lathe(acc, lining, 28, false, false, 240f);
                tk.Lathe(coat, new[] { P(0.13f, 0.47f), P(0.135f, 0.53f), P(0.15f, 0.6f) }, 28, false, false, 240f);
                tk.Lathe(acc, new[] { P(0.145f, 0.6f), P(0.13f, 0.53f), P(0.125f, 0.47f) }, 28, false, false, 240f);
                tk.Pop();
            }

            // ── 목과 깃
            tk.Cylinder(skin, 0.046f, 0.43f * H, 0.115f * H, 16, false);
            if (look.Outfit == Outfit.Sweater)
            {
                tk.Lathe(coat, new[] { P(0.062f, 0.44f * H), P(0.064f, 0.47f * H), P(0.063f, 0.5f * H), P(0.058f, 0.53f * H), P(0.05f, 0.535f * H) }, 24);
            }
            else if (look.Outfit == Outfit.Uniform)
            {
                tk.Lathe(coat, new[] { P(0.06f, 0.445f * H), P(0.058f, 0.505f * H), P(0.05f, 0.51f * H) }, 24);
                tk.Lathe(trim, new[] { P(0.0585f, 0.498f * H), P(0.061f, 0.503f * H), P(0.058f, 0.508f * H) }, 24);
            }
            else if (!look.Gown)
            {
                // 셔츠 깃: 목을 두른 띠 + 앞의 두 날개
                tk.Lathe(shirt, new[] { P(0.055f, 0.445f * H), P(0.056f, 0.49f * H), P(0.05f, 0.505f * H) }, 24);
                foreach (float s in new[] { -1f, 1f })
                {
                    tk.Push(new Vector3(s * 0.022f, 0.468f * H, 0.052f), new Vector3(20, s * 30, s * 35));
                    tk.Ellipsoid(shirt, Vector3.zero, new Vector3(0.018f, 0.022f, 0.005f), 10, 6);
                    tk.Pop();
                }
            }

            // ── 목 장식
            float ny = 0.475f * H, nz = 0.06f;
            switch (look.Neck)
            {
                case Neckwear.Bowtie:
                    foreach (float s in new[] { -1f, 1f })
                    {
                        // 리본 날개 (앞뒤로 납작하게)
                        tk.Push(new Vector3(s * 0.026f, ny, nz), Quaternion.Euler(0, 0, s * 90), new Vector3(1f, 1f, 0.5f));
                        tk.Lathe(acc, new[] { P(0, -0.028f), P(0.016f, -0.02f), P(0.021f, -0.004f), P(0.008f, 0.016f), P(0, 0.02f) }, 16);
                        tk.Pop();
                    }
                    tk.Ellipsoid(acc, new Vector3(0, ny, nz + 0.003f), new Vector3(0.011f, 0.013f, 0.009f), 10, 8);
                    break;
                case Neckwear.Necktie:
                    tk.Ellipsoid(acc, new Vector3(0, ny - 0.004f, nz + 0.002f), new Vector3(0.014f, 0.014f, 0.01f), 10, 8);
                    tk.Push(new Vector3(0, ny - 0.012f, nz), new Vector3(-24, 0, 0));
                    tk.Extrude(acc, new[] { new Vector2(-0.011f, 0), new Vector2(0.011f, 0), new Vector2(0.026f, -0.17f), new Vector2(0, -0.2f), new Vector2(-0.026f, -0.17f) }, 0.006f);
                    tk.Pop();
                    break;
                case Neckwear.Pearls:
                    for (int i = 0; i <= 16; i++)
                    {
                        float a = Mathf.Lerp(-75, 75, i / 16f) * Mathf.Deg2Rad;
                        tk.Sphere(pearl, new Vector3(Mathf.Sin(a) * 0.072f * W, 0.45f * H - Mathf.Cos(a) * 0.04f, Mathf.Cos(a) * 0.058f * W + 0.012f), 0.0088f, 8, 6);
                    }
                    tk.Sphere(pearl, new Vector3(0, 0.395f * H, 0.085f * W), 0.014f, 10, 8);
                    break;
                case Neckwear.Cravat:
                    for (int i = 0; i < 3; i++)
                        tk.Ellipsoid(shirt, new Vector3(0, ny - 0.01f - i * 0.026f, nz + 0.014f + i * 0.01f), new Vector3(0.03f - i * 0.003f, 0.016f, 0.014f), 12, 8);
                    tk.Sphere(gold, new Vector3(0, ny - 0.012f, nz + 0.03f), 0.007f, 8, 6);
                    break;
                case Neckwear.Medal:
                    tk.Push(new Vector3(-0.085f * W, 0.33f * H, frontZ(0.33f, -0.085f * W) + 0.004f), new Vector3(-8, -25, 0));
                    tk.RoundedBox(acc, new Vector3(0, 0.02f, 0), new Vector3(0.026f, 0.034f, 0.004f), 0.002f, 1);
                    tk.Push(new Vector3(0, -0.01f, 0.002f), new Vector3(90, 0, 0));
                    tk.Cylinder(gold, 0.016f, -0.002f, 0.004f, 18, true);
                    tk.Pop();
                    tk.Pop();
                    break;
                case Neckwear.Scarf:
                    tk.Push(new Vector3(0, 0.455f * H, 0), Quaternion.identity, new Vector3(1, 0.8f, 0.95f));
                    tk.Torus(acc, 0.06f, 0.022f, 24, 10);
                    tk.Pop();
                    tk.Tube(acc, MeshKit.Bezier(new Vector3(0.03f, 0.45f * H, 0.07f), new Vector3(0.05f, 0.36f * H, 0.12f * W), new Vector3(0.06f, 0.24f * H, frontZ(0.24f, 0.06f) + 0.01f), 8), new[] { 0.02f, 0.016f }, 8, true);
                    break;
            }
            tb.Build("TorsoMesh", torso);
        }

        // ═════════════════ 팔 ═════════════════

        static void BuildUpperArm(Transform shoulder, CharacterLook look, Mats m, float W)
        {
            var ub = new ModelBuilder();
            int sleeve = ub.M("sleeve", m.Sleeve), skin = ub.M("skin", m.Skin), coat = ub.M("coat", m.Coat), trim = ub.M("trim", m.Trim);
            float r0 = 0.054f * Mathf.Sqrt(W);
            if (look.Gown)
            {
                // 부푼 어깨 소매 + 맨팔
                ub.Kit.Ellipsoid(coat, new Vector3(0, -0.02f, 0), new Vector3(0.066f, 0.07f, 0.062f), 16, 10);
                ub.Kit.Tube(skin, new List<Vector3> { new Vector3(0, -0.03f, 0), new Vector3(0, -0.215f, 0) }, new[] { 0.042f, 0.036f }, 14, false);
                ub.Kit.Lathe(trim, new[] { P(0.058f, -0.088f), P(0.062f, -0.082f), P(0.058f, -0.076f) }, 16);
            }
            else
            {
                ub.Kit.Ellipsoid(sleeve, new Vector3(0, -0.006f, 0), new Vector3(r0 * 1.08f, r0 * 1.05f, r0 * 1.05f), 16, 10);
                ub.Kit.Tube(sleeve, MeshKit.Bezier(Vector3.zero, new Vector3(0, -0.1f, 0.004f), new Vector3(0, -0.215f, 0), 4), new[] { r0, r0 * 0.96f, r0 * 0.9f, 0.047f, 0.045f }, 14, false);
            }
            ub.Build("UpperArm", shoulder);
        }

        static void BuildForearm(Transform elbow, CharacterLook look, Mats m)
        {
            var fb = new ModelBuilder();
            var k = fb.Kit;
            int sleeve = fb.M("sleeve", m.Sleeve), shirt = fb.M("shirt", m.Shirt), trim = fb.M("trim", m.Trim), glove = fb.M("glove", m.Glove);
            if (look.Gown)
            {
                // 긴 장갑 (팔꿈치까지)
                k.Sphere(glove, Vector3.zero, 0.038f, 12, 8);
                k.Tube(glove, new List<Vector3> { Vector3.zero, new Vector3(0, -0.205f, 0) }, new[] { 0.038f, 0.032f }, 14, false);
                k.Lathe(glove, new[] { P(0.036f, -0.02f), P(0.042f, -0.008f), P(0.04f, 0.004f) }, 14);
            }
            else
            {
                k.Sphere(sleeve, Vector3.zero, 0.046f, 14, 10);
                k.Tube(sleeve, new List<Vector3> { Vector3.zero, new Vector3(0, -0.1f, 0.003f), new Vector3(0, -0.176f, 0) }, new[] { 0.045f, 0.042f, 0.04f }, 14, false);
                // 소매 끝이 살짝 넓어지는 커프 + 가는 테, 셔츠 소맷부리
                k.Lathe(sleeve, new[] { P(0f, -0.2f), P(0.036f, -0.2f), P(0.046f, -0.196f), P(0.047f, -0.185f), P(0.04f, -0.172f) }, 16);
                k.Lathe(trim, new[] { P(0.0466f, -0.194f), P(0.0482f, -0.19f), P(0.0466f, -0.186f) }, 16);
                if (look.Outfit != Outfit.Vest) k.Lathe(shirt, new[] { P(0.033f, -0.214f), P(0.036f, -0.21f), P(0.036f, -0.199f) }, 14);
            }
            fb.Build("Forearm", elbow);
        }

        // ═════════════════ 머리 ═════════════════

        const float R = 0.105f;

        static System.Func<float, Vector2> MaskShape(MaskStyle style, out float mw, out float mh, ref float eyeY, Vector3 c)
        {
            mw = 0.215f;
            mh = 0.075f;
            switch (style)
            {
                case MaskStyle.CatEye:
                    return u => new Vector2(-0.72f + 0.5f * Mathf.Exp(-u * u / 0.03f) + 0.32f * Mathf.Abs(u), 0.55f + 0.95f * Mathf.Pow(Mathf.Abs(u), 3));
                case MaskStyle.Half:
                case MaskStyle.Fox:
                    mh = 0.12f;
                    eyeY = c.y + 0.012f;
                    return u => new Vector2(-0.55f + 0.3f * Mathf.Exp(-u * u / 0.02f) + 0.2f * Mathf.Abs(u), 1f - 0.25f * u * u);
                case MaskStyle.Butterfly:
                    mw = 0.25f;
                    mh = 0.1f;
                    return u => new Vector2(-0.5f - 0.35f * Mathf.Abs(u) + 0.45f * Mathf.Exp(-u * u / 0.015f), 0.55f + 0.5f * Mathf.Pow(Mathf.Abs(u), 1.4f) - 0.35f * Mathf.Exp(-u * u / 0.01f));
                case MaskStyle.Lace:
                    // 아래 가장자리가 물결 (레이스)
                    return u => new Vector2(-0.85f + 0.55f * Mathf.Exp(-u * u / 0.025f) + 0.3f * Mathf.Abs(u) - 0.08f * Mathf.Abs(Mathf.Sin(u * 18f)), 0.82f - 0.2f * u * u);
                case MaskStyle.EyePatch:
                    mw = 0.07f;
                    return u => new Vector2(-1f + 0.3f * u * u, 1f - 0.3f * u * u);
                default:
                    return u => new Vector2(-0.9f + 0.6f * Mathf.Exp(-u * u / 0.025f) + 0.3f * Mathf.Abs(u), 0.8f - 0.2f * u * u);
            }
        }

        static void BuildHead(Guest g, Transform head, CharacterLook look, Mats m)
        {
            float S = look.Head;
            var hb = new ModelBuilder();
            var k = hb.Kit;
            int skin = hb.M("skin", m.Skin), hair = hb.M("hair", m.Hair), hat = hb.M("hat", m.Hat), mask = hb.M("mask", m.Mask), mtrim = hb.M("mtrim", m.MaskTrim),
                acc = hb.M("acc", m.Accent), gold = hb.M("Gold"), dia = hb.M("Diamond"), glass = hb.M("Glass"), dark = hb.M("BlackGloss"), pearl = hb.M("Pearl"),
                blush = hb.M("blush", ArtLibrary.I.Tinted("Skin", Color.Lerp(look.Skin, new Color(0.95f, 0.45f, 0.45f), 0.35f))),
                feather = hb.M("feather", ArtLibrary.I.Tinted("Feather", look.MaskTrim));
            k.Push(Vector3.zero, Vector3.zero, S);
            Vector3 c = new Vector3(0, 0.12f, 0);
            // 얼굴: 아래가 살짝 좁은 달걀형 + 볼 + 코 + 귀
            k.Lathe(skin, new[] { P(0, c.y - 0.122f), P(0.05f, c.y - 0.113f), P(0.083f, c.y - 0.078f), P(0.101f, c.y - 0.024f), P(0.106f, c.y + 0.03f), P(0.097f, c.y + 0.075f), P(0.066f, c.y + 0.108f), P(0, c.y + 0.12f) }, 32);
            foreach (float s in new[] { -1f, 1f })
            {
                k.Ellipsoid(blush, c + new Vector3(s * 0.058f, -0.035f, 0.075f), new Vector3(0.024f, 0.016f, 0.012f), 10, 6);
                k.Ellipsoid(skin, c + new Vector3(s * 0.103f, -0.008f, -0.008f), new Vector3(0.013f, 0.027f, 0.019f), 10, 8);
            }
            bool beak = look.Mask == MaskStyle.Beak || look.Mask == MaskStyle.Fox;
            if (!beak)
            {
                k.Ellipsoid(skin, c + new Vector3(0, -0.014f, R * 0.97f), new Vector3(0.015f, 0.024f, 0.02f), 12, 8);
                k.Sphere(skin, c + new Vector3(0, -0.03f, R * 1.06f), 0.012f, 10, 8);
            }

            BuildHair(k, hair, acc, gold, c, look);
            BuildFacialHair(k, hair, c, look);

            // 가면: 머리를 감싸는 휜 판 + 가장자리 테
            float eyeY = c.y + 0.02f;
            float mw, mh;
            var shape = MaskShape(look.Mask, out mw, out mh, ref eyeY, c);
            bool hasMask = look.Mask != MaskStyle.None;
            if (look.Mask == MaskStyle.EyePatch)
            {
                k.Push(new Vector3(-0.04f, eyeY, 0), new Vector3(0, 180 + 22, 0));
                k.Push(new Vector3(0, 0, -R - 0.004f));
                k.CurvedPanel(mask, mw, 0.06f, R + 0.004f, shape, 10, 6, 0.004f);
                k.Pop();
                k.Pop();
                k.Push(new Vector3(0, eyeY + 0.014f, 0), new Vector3(-12, 0, 0));
                k.Torus(mask, R + 0.004f, 0.0028f, 36, 6);
                k.Pop();
            }
            else if (hasMask)
            {
                k.Push(new Vector3(0, eyeY, 0), new Vector3(0, 180, 0));
                k.Push(new Vector3(0, 0, -R - 0.004f));
                k.CurvedPanel(mask, mw, mh, R + 0.004f, shape, 36, 8, 0.006f);
                // 가장자리를 따라 도는 테 (위 → 아래로 한 바퀴)
                var rim = new List<Vector3>();
                float bendR = R + 0.004f;
                const int N = 36;
                for (int i = 0; i <= N; i++) rim.Add(PanelPoint(i / (float)N * 2 - 1, true, shape, mw, mh, bendR));
                for (int i = N; i >= 0; i--) rim.Add(PanelPoint(i / (float)N * 2 - 1, false, shape, mw, mh, bendR));
                k.Tube(mtrim, rim, new[] { 0.0026f }, 5, false);
                k.Pop();
                k.Pop();
            }
            if (look.Mask == MaskStyle.Beak)
            {
                // 아래로 굽은 긴 부리 (옆에서 보면 갈고리 모양)
                k.Push(c + new Vector3(0, -0.006f, R + 0.002f), new Vector3(70, 0, 0));
                k.Lathe(mask, new[] { P(0.03f, 0), P(0.026f, 0.04f), P(0.017f, 0.08f), P(0.008f, 0.11f), P(0, 0.12f) }, 16);
                k.Push(new Vector3(0, 0.11f, 0), new Vector3(40, 0, 0));
                k.Lathe(mask, new[] { P(0.008f, 0), P(0.005f, 0.025f), P(0, 0.04f) }, 10);
                k.Pop();
                k.Lathe(mtrim, new[] { P(0.0305f, 0.004f), P(0.0315f, 0.01f), P(0.0305f, 0.016f) }, 16);
                k.Pop();
            }
            if (look.Mask == MaskStyle.Fox)
            {
                k.Push(c + new Vector3(0, -0.032f, R - 0.012f), new Vector3(80, 0, 0));
                k.Lathe(mask, new[] { P(0.046f, 0), P(0.036f, 0.04f), P(0.016f, 0.072f), P(0, 0.082f) }, 18);
                k.Pop();
                k.Sphere(dark, c + new Vector3(0, -0.047f, R + 0.07f), 0.011f, 8, 6);
                // 흰 볼털
                foreach (float s in new[] { -1f, 1f })
                    k.Ellipsoid(mtrim, c + new Vector3(s * 0.05f, -0.05f, R * 0.88f), new Vector3(0.03f, 0.022f, 0.016f), 10, 6);
            }
            if (look.MaskFeather && hasMask)
            {
                k.Push(c + new Vector3(0.088f, 0.06f, 0.06f), new Vector3(0, 200, -25));
                k.CurvedPanel(feather, 0.05f, 0.2f, 0.08f, u => new Vector2(-1f + 0.9f * u * u, 1f - 0.6f * u * u), 8, 10, 0.003f);
                k.Pop();
                k.Push(c + new Vector3(0.078f, 0.05f, 0.086f), Vector3.zero);
                k.Gem(dia, 0.012f, 0.007f, 0.01f, 8);
                k.Pop();
            }

            // 안경
            float lensZ = R + (hasMask ? 0.02f : 0.014f);
            switch (look.Eyewear)
            {
                case Eyewear.Round:
                    foreach (float s in new[] { -1f, 1f })
                    {
                        k.Push(c + new Vector3(s * 0.042f, eyeY - c.y, lensZ), new Vector3(90, 0, 0));
                        k.Torus(gold, 0.022f, 0.0025f, 22, 5);
                        k.Pop();
                        k.Tube(gold, new List<Vector3> { c + new Vector3(s * 0.064f, eyeY - c.y, lensZ - 0.004f), c + new Vector3(s * 0.1f, eyeY - c.y + 0.004f, 0.01f) }, new[] { 0.0018f }, 4, true);
                    }
                    k.Tube(gold, MeshKit.Bezier(c + new Vector3(-0.02f, eyeY - c.y + 0.004f, lensZ), c + new Vector3(0, eyeY - c.y + 0.012f, lensZ + 0.004f), c + new Vector3(0.02f, eyeY - c.y + 0.004f, lensZ), 6), new[] { 0.0022f }, 4, true);
                    break;
                case Eyewear.Monocle:
                    k.Push(c + new Vector3(0.041f, eyeY - c.y, lensZ - 0.004f), new Vector3(90, 22, 0));
                    k.Torus(gold, 0.025f, 0.004f, 24, 6);
                    k.Pop();
                    k.Tube(gold, MeshKit.Bezier(c + new Vector3(0.064f, eyeY - c.y - 0.01f, lensZ - 0.008f), c + new Vector3(0.09f, -0.06f, 0.08f), c + new Vector3(0.1f, -0.13f, 0.05f), 10), new[] { 0.0018f }, 4, true);
                    break;
                case Eyewear.Shades:
                    foreach (float s in new[] { -1f, 1f })
                    {
                        k.Push(c + new Vector3(s * 0.043f, eyeY - c.y - 0.002f, lensZ), new Vector3(0, s * 18, 0));
                        k.Ellipsoid(dark, Vector3.zero, new Vector3(0.03f, 0.021f, 0.006f), 16, 8);
                        k.Pop();
                    }
                    k.Tube(dark, new List<Vector3> { c + new Vector3(-0.015f, eyeY - c.y + 0.006f, lensZ + 0.004f), c + new Vector3(0.015f, eyeY - c.y + 0.006f, lensZ + 0.004f) }, new[] { 0.0028f }, 5, true);
                    foreach (float s in new[] { -1f, 1f })
                        k.Tube(dark, new List<Vector3> { c + new Vector3(s * 0.07f, eyeY - c.y + 0.004f, lensZ - 0.012f), c + new Vector3(s * 0.104f, eyeY - c.y + 0.006f, 0.01f) }, new[] { 0.0025f }, 4, true);
                    break;
            }

            // 귀걸이
            if (look.Earrings != Earring.None)
                foreach (float s in new[] { -1f, 1f })
                {
                    var at = c + new Vector3(s * 0.106f, -0.036f, -0.006f);
                    if (look.Earrings == Earring.Gold)
                    {
                        k.Push(at + new Vector3(0, -0.012f, 0), new Vector3(0, 0, 90));
                        k.Torus(gold, 0.012f, 0.0025f, 18, 5);
                        k.Pop();
                    }
                    else
                    {
                        k.Sphere(gold, at, 0.004f, 6, 4);
                        k.Sphere(pearl, at + new Vector3(0, -0.016f, 0), 0.008f, 10, 8);
                    }
                }

            BuildHat(k, hat, acc, gold, dia, feather, mask, c, look);
            k.Pop();
            hb.Build("HeadMesh", head);

            BuildFace(g, head, look, m, S, c, eyeY, hasMask);
        }

        static Vector3 PanelPoint(float u, bool top, System.Func<float, Vector2> shape, float w, float h, float bendR)
        {
            Vector2 range = shape(u);
            float ang = u * w * 0.5f / bendR;
            float y = (top ? range.y : range.x) * h * 0.5f;
            float rr = bendR + 0.0015f;
            return new Vector3(Mathf.Sin(ang) * rr, y, -Mathf.Cos(ang) * rr + bendR);
        }

        /// <summary>눈(가면 구멍 + 흰자·홍채·동공·반짝임)·눈썹·입: 깜빡임·표정·말하기를 위해 따로 움직인다.</summary>
        static void BuildFace(Guest g, Transform head, CharacterLook look, Mats m, float S, Vector3 c, float eyeY, bool hasMask)
        {
            float surface = hasMask && look.Mask != MaskStyle.EyePatch ? R + 0.0102f : 0.1065f;
            for (int side = 0; side < 2; side++)
            {
                float s = side == 0 ? -1f : 1f;
                if (look.Mask == MaskStyle.EyePatch && s < 0) continue;
                float ex = 0.04f;
                float ez = Mathf.Sqrt(surface * surface - ex * ex);
                var eye = new GameObject(side == 0 ? "EyeL" : "EyeR").transform;
                eye.SetParent(head, false);
                eye.localPosition = new Vector3(s * ex, eyeY, ez) * S;
                float tilt = look.Eyes == EyeStyle.Sharp ? s * 14f : 0f;
                eye.localRotation = Quaternion.Euler(0, s * 22f, tilt);
                eye.localScale = Vector3.one * S;

                Vector2 socket, white;
                float iris = 0.0092f;
                switch (look.Eyes)
                {
                    case EyeStyle.Almond: case EyeStyle.Sleepy: case EyeStyle.Sharp: socket = new Vector2(0.026f, 0.016f); white = new Vector2(0.0215f, 0.0125f); break;
                    case EyeStyle.Wide: socket = new Vector2(0.026f, 0.024f); white = new Vector2(0.0225f, 0.021f); iris = 0.0072f; break;
                    default: socket = new Vector2(0.024f, 0.021f); white = new Vector2(0.0198f, 0.0178f); break;
                }
                bool masked = hasMask && look.Mask != MaskStyle.EyePatch;
                if (look.Eyes == EyeStyle.Happy) socket = new Vector2(0.023f, 0.0135f);
                var sb = new ModelBuilder();
                if (masked)
                    sb.Kit.Ellipsoid(sb.M("Pupil"), new Vector3(0, 0, 0.0005f), new Vector3(socket.x, socket.y, 0.0028f), 18, 8);
                if (look.Eyes == EyeStyle.Sleepy)
                {
                    // 반쯤 감긴 위 눈꺼풀
                    sb.Kit.Ellipsoid(sb.M("lid", m.Lid), new Vector3(0, white.y * 0.55f, 0.0062f), new Vector3(socket.x * 0.98f, white.y * 0.75f, 0.0034f), 18, 8);
                }
                if (sb.Kit.VertexCount > 0) sb.Build("Socket", eye, false);

                var ball = new GameObject("Ball").transform;
                ball.SetParent(eye, false);
                var eb = new ModelBuilder();
                if (look.Eyes == EyeStyle.Happy)
                {
                    // 웃는 눈 (^ 모양): 가면 구멍 안에서는 밝게, 맨얼굴에서는 어둡게
                    int arc = masked ? eb.M("sclera", m.Sclera) : eb.M("Pupil");
                    eb.Kit.Tube(arc, MeshKit.Bezier(new Vector3(-0.014f, -0.005f, 0.004f), new Vector3(0, 0.011f, 0.006f), new Vector3(0.014f, -0.005f, 0.004f), 10), new[] { 0.0028f }, 6, true);
                }
                else
                {
                    int ew = eb.M("sclera", m.Sclera), ei = eb.M("iris", m.Iris), ep = eb.M("Pupil"), eg = eb.M("glint", ArtLibrary.I.Tinted("Glow", Color.white, 1.4f));
                    eb.Kit.Ellipsoid(ew, new Vector3(0, 0, 0.0022f), new Vector3(white.x, white.y, 0.003f), 18, 8);
                    eb.Kit.Ellipsoid(ei, new Vector3(0, -0.0005f, 0.0046f), new Vector3(iris, iris, 0.0018f), 16, 8);
                    eb.Kit.Ellipsoid(ep, new Vector3(0, -0.0005f, 0.0058f), new Vector3(iris * 0.5f, iris * 0.5f, 0.0012f), 12, 6);
                    eb.Kit.Sphere(eg, new Vector3(-0.0035f, 0.0035f, 0.0068f), 0.0024f, 8, 6);
                }
                eb.Build("Eye", ball, false);
                if (side == 0) g.EyeL = ball; else g.EyeR = ball;

                // 눈썹 (가면 위 가장자리 바로 위)
                var brow = new GameObject(side == 0 ? "BrowL" : "BrowR").transform;
                brow.SetParent(head, false);
                float by = eyeY + (hasMask && look.Mask != MaskStyle.EyePatch ? 0.034f : 0.024f);
                if (look.Mask == MaskStyle.Half || look.Mask == MaskStyle.Fox) by = eyeY + 0.064f;
                float bz = Mathf.Sqrt(Mathf.Max(0.0001f, 0.1075f * 0.1075f - ex * ex)) + (hasMask ? 0.003f : 0f);
                brow.localPosition = new Vector3(s * 0.042f, by, bz) * S;
                brow.localRotation = Quaternion.Euler(-8f, s * 22f, 0);
                brow.localScale = Vector3.one * S;
                var bb = new ModelBuilder();
                bb.Kit.Tube(bb.M("hair", m.Hair), MeshKit.Bezier(new Vector3(-0.018f, -0.002f, 0), new Vector3(0, 0.005f, 0.002f), new Vector3(0.018f, -0.003f, 0), 8), new[] { 0.0034f, 0.0042f, 0.0036f, 0.0026f }, 6, true);
                bb.Build("Brow", brow, false);
                if (side == 0) g.BrowL = brow; else g.BrowR = brow;
            }

            // 입: 늘 보이는 웃는 선 + 말할 때 벌어지는 안쪽
            bool snout = look.Mask == MaskStyle.Beak || look.Mask == MaskStyle.Fox;
            var mouth = new GameObject("Mouth").transform;
            mouth.SetParent(head, false);
            mouth.localPosition = (c + new Vector3(0, snout ? -0.075f : -0.058f, snout ? 0.088f : 0.096f)) * S;
            mouth.localScale = Vector3.one * S;
            var lb = new ModelBuilder();
            lb.Kit.Tube(lb.M("BlackGloss"), MeshKit.Bezier(new Vector3(-0.024f, 0.004f, 0), new Vector3(0, -0.006f, 0.004f), new Vector3(0.024f, 0.004f, 0), 8), new[] { 0.0032f }, 6, true);
            lb.Build("Line", mouth, false);
            var open = new GameObject("Open").transform;
            open.SetParent(mouth, false);
            open.localPosition = new Vector3(0, -0.006f, -0.002f);
            var ob = new ModelBuilder();
            ob.Kit.Ellipsoid(ob.M("mouth", m.MouthIn), Vector3.zero, new Vector3(0.019f, 0.014f, 0.008f), 14, 8);
            ob.Build("Inside", open, false);
            open.localScale = new Vector3(1, 0.05f, 1);
            g.Mouth = open;
        }

        static void BuildHair(MeshKit k, int hair, int acc, int gold, Vector3 c, CharacterLook look)
        {
            // 기본 머리 덮개: 윗머리·뒷머리 (얼굴 앞쪽은 비워 이마선이 생긴다)
            System.Action cap = () => k.Ellipsoid(hair, c + new Vector3(0, 0.036f, -0.026f), new Vector3(0.114f, 0.104f, 0.113f), 26, 16);
            switch (look.HairStyle)
            {
                case HairStyle.Short:
                    cap();
                    for (int i = -1; i <= 1; i++)
                        k.Ellipsoid(hair, c + new Vector3(i * 0.032f, 0.1f, 0.06f - Mathf.Abs(i) * 0.012f), new Vector3(0.034f, 0.018f, 0.03f), 12, 8);
                    break;
                case HairStyle.Slick:
                    k.Ellipsoid(hair, c + new Vector3(0, 0.03f, -0.024f), new Vector3(0.112f, 0.1f, 0.111f), 26, 16);
                    k.Ellipsoid(hair, c + new Vector3(0.03f, 0.098f, 0.05f), new Vector3(0.07f, 0.022f, 0.045f), 16, 8);
                    k.Tube(hair, MeshKit.Bezier(c + new Vector3(-0.03f, 0.116f, 0.06f), c + new Vector3(-0.034f, 0.13f, -0.02f), c + new Vector3(-0.03f, 0.1f, -0.1f), 8), new[] { 0.004f }, 5, true);
                    break;
                case HairStyle.Bun:
                    cap();
                    k.Sphere(hair, c + new Vector3(0, 0.125f, -0.05f), 0.052f, 18, 12);
                    k.Push(c + new Vector3(0, 0.11f, -0.05f), new Vector3(90, 0, 0));
                    k.Torus(acc, 0.04f, 0.006f, 20, 6);
                    k.Pop();
                    k.Tube(gold, new List<Vector3> { c + new Vector3(-0.06f, 0.16f, -0.04f), c + new Vector3(0.07f, 0.11f, -0.07f) }, new[] { 0.0035f }, 6, true);
                    break;
                case HairStyle.Long:
                    cap();
                    k.Ellipsoid(hair, c + new Vector3(0, -0.07f, -0.06f), new Vector3(0.105f, 0.17f, 0.07f), 22, 14);
                    foreach (float s in new[] { -1f, 1f })
                        k.Tube(hair, MeshKit.Bezier(c + new Vector3(s * 0.09f, 0.08f, 0.05f), c + new Vector3(s * 0.12f, -0.02f, 0.04f), c + new Vector3(s * 0.1f, -0.15f, 0.0f), 10), new[] { 0.022f, 0.026f, 0.024f, 0.018f, 0.012f }, 10, true);
                    break;
                case HairStyle.Bob:
                    cap();
                    foreach (float s in new[] { -1f, 1f })
                        k.Ellipsoid(hair, c + new Vector3(s * 0.092f, -0.025f, 0.005f), new Vector3(0.036f, 0.085f, 0.075f), 16, 10);
                    k.Ellipsoid(hair, c + new Vector3(0, -0.02f, -0.07f), new Vector3(0.11f, 0.09f, 0.06f), 18, 10);
                    k.Ellipsoid(hair, c + new Vector3(0.01f, 0.098f, 0.065f), new Vector3(0.085f, 0.02f, 0.035f), 16, 8);
                    break;
                case HairStyle.Ponytail:
                    cap();
                    k.Sphere(acc, c + new Vector3(0, 0.07f, -0.115f), 0.016f, 10, 8);
                    k.Tube(hair, MeshKit.Bezier(c + new Vector3(0, 0.07f, -0.12f), c + new Vector3(0, 0.04f, -0.2f), c + new Vector3(0, -0.12f, -0.17f), 10), new[] { 0.03f, 0.034f, 0.03f, 0.024f, 0.016f, 0.008f }, 10, true);
                    break;
                case HairStyle.Spiky:
                    cap();
                    for (int i = 0; i < 9; i++)
                    {
                        float a = (i / 8f - 0.5f) * 2.4f;
                        var at = c + new Vector3(Mathf.Sin(a) * 0.075f, 0.1f - Mathf.Abs(a) * 0.012f, Mathf.Cos(a) * 0.02f - 0.02f + (i % 2) * -0.04f);
                        k.Push(at, new Vector3(-25 + (i % 2) * -20, 0, -Mathf.Rad2Deg * a * 0.4f));
                        k.Lathe(hair, new[] { P(0.026f, 0), P(0.016f, 0.04f), P(0, 0.07f) }, 8, true);
                        k.Pop();
                    }
                    break;
                case HairStyle.Curly:
                    k.Ellipsoid(hair, c + new Vector3(0, 0.06f, -0.03f), new Vector3(0.135f, 0.12f, 0.13f), 22, 14);
                    for (int i = 0; i < 26; i++)
                    {
                        float a = i * 2.39996f, y = 0.0f + (i % 7) * 0.026f;
                        var d = new Vector3(Mathf.Sin(a), 0, -Mathf.Abs(Mathf.Cos(a)) * 0.6f - 0.2f).normalized;
                        k.Sphere(hair, c + new Vector3(d.x * 0.12f, y + 0.02f, d.z * 0.12f - 0.02f), 0.038f, 10, 8);
                    }
                    break;
                case HairStyle.Mohawk:
                    for (int i = 0; i < 7; i++)
                    {
                        float t = i / 6f;
                        var at = c + new Vector3(0, 0.1f + Mathf.Sin(t * Mathf.PI) * 0.02f, 0.07f - t * 0.19f);
                        k.Push(at, Quaternion.Euler(-20 - t * 60, 0, 0), new Vector3(0.45f, 1f, 1f));
                        k.Lathe(hair, new[] { P(0.03f, 0), P(0.02f, 0.05f), P(0, 0.085f) }, 10, true);
                        k.Pop();
                    }
                    break;
                case HairStyle.Wig:
                    k.Ellipsoid(hair, c + new Vector3(0, 0.05f, -0.028f), new Vector3(0.116f, 0.1f, 0.115f), 24, 14);
                    foreach (float s in new[] { -1f, 1f })
                        for (int r = 0; r < 3; r++)
                        {
                            k.Push(c + new Vector3(s * 0.105f, 0.03f - r * 0.032f, -0.03f), new Vector3(0, 90, 0));
                            k.Torus(hair, 0.017f, 0.01f, 14, 8);
                            k.Pop();
                        }
                    k.Tube(hair, new List<Vector3> { c + new Vector3(0, -0.02f, -0.105f), c + new Vector3(0, -0.12f, -0.13f) }, new[] { 0.024f, 0.014f }, 10, true);
                    k.Sphere(acc, c + new Vector3(0, -0.036f, -0.114f), 0.018f, 10, 8);
                    break;
                case HairStyle.Tufts:
                    foreach (float s in new[] { -1f, 1f })
                    {
                        k.Sphere(hair, c + new Vector3(s * 0.096f, 0.032f, -0.03f), 0.038f, 12, 8);
                        k.Sphere(hair, c + new Vector3(s * 0.086f, 0.0f, -0.062f), 0.034f, 12, 8);
                    }
                    k.Ellipsoid(hair, c + new Vector3(0, -0.01f, -0.09f), new Vector3(0.08f, 0.05f, 0.03f), 14, 8);
                    break;
            }
        }

        static void BuildFacialHair(MeshKit k, int hair, Vector3 c, CharacterLook look)
        {
            switch (look.Facial)
            {
                case FacialHair.Mustache:
                case FacialHair.CurlMustache:
                    foreach (float s in new[] { -1f, 1f })
                    {
                        var path = look.Facial == FacialHair.CurlMustache
                            ? MeshKit.Bezier(new Vector3(0, -0.04f, 0.104f), new Vector3(s * 0.05f, -0.054f, 0.1f), new Vector3(s * 0.078f, -0.02f, 0.08f), new Vector3(s * 0.064f, -0.01f, 0.084f), 12)
                            : MeshKit.Bezier(new Vector3(0, -0.04f, 0.106f), new Vector3(s * 0.03f, -0.042f, 0.103f), new Vector3(s * 0.052f, -0.054f, 0.092f), 10);
                        for (int i = 0; i < path.Count; i++) path[i] += c;
                        k.Tube(hair, path, new[] { 0.011f, 0.01f, 0.007f, 0.004f }, 8, true);
                    }
                    break;
                case FacialHair.Beard:
                    k.Ellipsoid(hair, c + new Vector3(0, -0.088f, 0.042f), new Vector3(0.084f, 0.066f, 0.07f), 18, 12);
                    foreach (float s in new[] { -1f, 1f })
                        k.Tube(hair, new List<Vector3> { c + new Vector3(s * 0.098f, 0.0f, 0.0f), c + new Vector3(s * 0.08f, -0.08f, 0.04f) }, new[] { 0.02f, 0.03f }, 8, true);
                    break;
                case FacialHair.Goatee:
                    k.Ellipsoid(hair, c + new Vector3(0, -0.106f, 0.072f), new Vector3(0.024f, 0.034f, 0.024f), 12, 8);
                    break;
                case FacialHair.Sideburns:
                    foreach (float s in new[] { -1f, 1f })
                        k.Tube(hair, new List<Vector3> { c + new Vector3(s * 0.1f, 0.03f, 0.01f), c + new Vector3(s * 0.098f, -0.045f, 0.035f) }, new[] { 0.014f, 0.017f }, 8, true);
                    break;
            }
        }

        static void BuildHat(MeshKit k, int hat, int acc, int gold, int dia, int feather, int mask, Vector3 c, CharacterLook look)
        {
            float top = c.y + 0.1f + (look.HairStyle == HairStyle.Curly ? 0.035f : 0f);
            switch (look.Hat)
            {
                case HatStyle.TopHat:
                    k.Push(new Vector3(0, top - 0.015f, -0.005f), new Vector3(-6, 0, 0));
                    k.Lathe(hat, new[] { P(0, 0), P(0.15f, 0.0f), P(0.156f, 0.007f), P(0.142f, 0.013f), P(0.09f, 0.016f), P(0.086f, 0.05f), P(0.088f, 0.17f), P(0.092f, 0.19f), P(0, 0.19f) }, 36);
                    k.Lathe(acc, new[] { P(0.0875f, 0.02f), P(0.089f, 0.045f) }, 36);
                    k.Pop();
                    break;
                case HatStyle.Bowler:
                    k.Push(new Vector3(0, top - 0.02f, -0.005f), new Vector3(-5, 0, 0));
                    k.Lathe(hat, new[] { P(0, 0), P(0.135f, 0), P(0.142f, 0.012f), P(0.1f, 0.018f), P(0.1f, 0.05f), P(0.09f, 0.09f), P(0.06f, 0.115f), P(0, 0.122f) }, 36);
                    k.Lathe(acc, new[] { P(0.1005f, 0.02f), P(0.1f, 0.034f) }, 36);
                    k.Pop();
                    break;
                case HatStyle.Tiara:
                    for (int i = 0; i < 9; i++)
                    {
                        float a = (i - 4) * 14f * Mathf.Deg2Rad;
                        var basep = new Vector3(Mathf.Sin(a) * 0.09f, top - 0.02f, Mathf.Cos(a) * 0.075f);
                        float h = 0.04f - Mathf.Abs(i - 4) * 0.007f;
                        k.Tube(gold, new List<Vector3> { basep, basep + Vector3.up * h }, new[] { 0.004f, 0.002f }, 5, true);
                        k.Sphere(i % 2 == 0 ? dia : gold, basep + Vector3.up * (h + 0.004f), 0.007f, 8, 6);
                    }
                    k.Push(new Vector3(0, top - 0.02f, 0), Vector3.zero);
                    k.Torus(gold, 0.088f, 0.005f, 32, 6, 200f);
                    k.Pop();
                    break;
                case HatStyle.FlatCap:
                    k.Push(new Vector3(0, top - 0.025f, 0.0f), new Vector3(-8, 0, 0));
                    k.Ellipsoid(hat, new Vector3(0, 0.012f, -0.004f), new Vector3(0.122f, 0.046f, 0.13f), 26, 12);
                    k.Ellipsoid(hat, new Vector3(0, -0.012f, 0.118f), new Vector3(0.082f, 0.011f, 0.058f), 18, 8);
                    k.Sphere(hat, new Vector3(0, 0.056f, -0.004f), 0.01f, 8, 6);
                    k.Pop();
                    break;
                case HatStyle.Captain:
                    k.Push(new Vector3(0, top - 0.025f, -0.005f), new Vector3(-6, 0, 0));
                    k.Lathe(hat, new[] { P(0, 0), P(0.1f, 0), P(0.104f, 0.05f), P(0.132f, 0.075f), P(0.13f, 0.09f), P(0, 0.095f) }, 36);
                    k.Lathe(gold, new[] { P(0.1005f, 0.008f), P(0.103f, 0.03f) }, 36);
                    k.Ellipsoid(mask, new Vector3(0, 0.003f, 0.1f), new Vector3(0.075f, 0.01f, 0.05f), 16, 8);
                    k.Sphere(gold, new Vector3(0, 0.045f, 0.106f), 0.016f, 10, 8);
                    k.Pop();
                    break;
                case HatStyle.FoxHood:
                    k.Ellipsoid(hat, c + new Vector3(0, 0.022f, -0.026f), new Vector3(0.126f, 0.134f, 0.126f), 26, 16);
                    foreach (float s in new[] { -1f, 1f })
                    {
                        k.Push(c + new Vector3(s * 0.066f, 0.112f, -0.01f), new Vector3(0, 0, -s * 18));
                        k.Lathe(mask, new[] { P(0.036f, 0), P(0.03f, 0.03f), P(0.012f, 0.07f), P(0, 0.086f) }, 4, false, false, 360f, true);
                        k.Pop();
                    }
                    break;
                case HatStyle.Feathered:
                    k.Push(new Vector3(0, top - 0.02f, -0.01f), new Vector3(-10, 0, 8));
                    k.Lathe(hat, new[] { P(0, 0), P(0.1f, 0), P(0.102f, 0.01f), P(0.07f, 0.02f), P(0.065f, 0.06f), P(0, 0.065f) }, 30);
                    for (int i = 0; i < 3; i++)
                    {
                        k.Push(new Vector3(-0.05f + i * 0.02f, 0.05f, -0.02f), new Vector3(0, 90, -40 + i * 15));
                        k.CurvedPanel(feather, 0.04f, 0.18f, 0.1f, u => new Vector2(-1f + 0.9f * u * u, 1f - 0.6f * u * u), 6, 8, 0.003f);
                        k.Pop();
                    }
                    k.Pop();
                    break;
                case HatStyle.Beret:
                    k.Push(new Vector3(0.012f, top - 0.015f, -0.01f), new Vector3(-8, 0, 12));
                    k.Ellipsoid(hat, new Vector3(0, 0.02f, 0), new Vector3(0.13f, 0.035f, 0.125f), 26, 12);
                    k.Lathe(hat, new[] { P(0.1f, -0.005f), P(0.103f, 0.008f) }, 26);
                    k.Cylinder(hat, 0.006f, 0.05f, 0.012f, 8, true);
                    k.Pop();
                    break;
                case HatStyle.Crown:
                    k.Push(new Vector3(0, top - 0.02f, -0.006f), new Vector3(-5, 0, 0));
                    k.Lathe(gold, new[] { P(0.094f, 0), P(0.098f, 0.012f), P(0.096f, 0.04f), P(0.09f, 0.044f) }, 32, false, false, 360f);
                    for (int i = 0; i < 8; i++)
                    {
                        float a = i / 8f * Mathf.PI * 2;
                        var bp = new Vector3(Mathf.Cos(a) * 0.093f, 0.04f, Mathf.Sin(a) * 0.093f);
                        k.Tube(gold, new List<Vector3> { bp, bp + new Vector3(Mathf.Cos(a) * 0.006f, 0.035f, Mathf.Sin(a) * 0.006f) }, new[] { 0.01f, 0.004f }, 6, true);
                        k.Sphere(gold, bp + new Vector3(0, 0.04f, 0), 0.007f, 8, 6);
                        k.Push(new Vector3(Mathf.Cos(a + 0.39f) * 0.098f, 0.022f, Mathf.Sin(a + 0.39f) * 0.098f), new Vector3(0, -Mathf.Rad2Deg * (a + 0.39f) + 90, 90));
                        k.Gem(i % 2 == 0 ? acc : dia, 0.009f, 0.004f, 0.004f, 6);
                        k.Pop();
                    }
                    k.Ellipsoid(acc, new Vector3(0, 0.03f, 0), new Vector3(0.088f, 0.04f, 0.088f), 20, 10);
                    k.Pop();
                    break;
                case HatStyle.Fedora:
                    k.Push(new Vector3(0, top - 0.018f, -0.005f), new Vector3(-6, 0, -4));
                    k.Lathe(hat, new[] { P(0, 0), P(0.16f, 0.006f), P(0.165f, 0.016f), P(0.14f, 0.012f), P(0.098f, 0.016f), P(0.096f, 0.07f), P(0.08f, 0.1f), P(0.02f, 0.098f), P(0, 0.09f) }, 36);
                    k.Lathe(acc, new[] { P(0.0975f, 0.018f), P(0.097f, 0.04f) }, 36);
                    k.Pop();
                    break;
                case HatStyle.Flower:
                    k.Push(c + new Vector3(0.085f, 0.07f, 0.03f), new Vector3(0, 70, 0));
                    for (int i = 0; i < 6; i++)
                    {
                        float a = i / 6f * Mathf.PI * 2;
                        k.Ellipsoid(acc, new Vector3(Mathf.Cos(a) * 0.017f, Mathf.Sin(a) * 0.017f, 0), new Vector3(0.015f, 0.015f, 0.005f), 10, 6);
                    }
                    k.Sphere(gold, new Vector3(0, 0, 0.004f), 0.009f, 10, 8);
                    k.Pop();
                    break;
            }
        }
    }
}
