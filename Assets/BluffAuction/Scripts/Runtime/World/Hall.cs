using System.Collections.Generic;
using BluffAuction.Art;
using UnityEngine;
using UnityEngine.Rendering;
using static BluffAuction.Art.ModelBuilder;

namespace BluffAuction.World
{
    /// <summary>
    /// 경매장 (코드로 생성): 둥근 경매 탁자와 가운데 회전판, 의자, 경매사 단, 벽·천장·창·그림·커튼, 샹들리에, 조명, 반사 프로브.
    /// 좌표: 탁자 중심이 원점, 플레이어 자리는 -Z 쪽, 경매사는 +Z 쪽. 높이 단위 m.
    /// </summary>
    public sealed class Hall : MonoBehaviour
    {
        /// <summary>탁자 모형을 만든 반지름. 인원에 맞춰 이 크기에서 줄이거나 그대로 쓴다 (SetSize).</summary>
        public const float TableRadius = 1.55f;
        public const float TableHeight = 0.78f;
        /// <summary>지금 탁자 반지름 / 의자(앉은 사람) 반지름.</summary>
        public float TableR = TableRadius;
        public float SeatR { get { return TableR + 0.4f; } }
        Transform tableT, rugT;
        public const float TurntableTop = TableHeight + 0.115f;
        public static readonly Vector3 AuctioneerPos = new Vector3(0, 0.36f, 3.25f);

        public Transform Turntable;
        public Transform ItemAnchor;
        public Light ItemSpot;
        public Light TableLight;
        public Transform[] Chairs = new Transform[0];
        public readonly List<Light> Flicker = new List<Light>();
        public ReflectionProbe Probe;
        float spin;
        public float SpinSpeed = 18f;

        /// <summary>n 명일 때 좌석 k (0 = 플레이어, -Z) 의 각도 (도, -Z 에서 시계 방향).</summary>
        public static float SeatAngle(int k, int n)
        {
            return k * 360f / Mathf.Max(1, n);
        }

        public static Vector3 SeatPos(int k, int n, float radius)
        {
            float a = SeatAngle(k, n) * Mathf.Deg2Rad;
            return new Vector3(-Mathf.Sin(a) * radius, 0, -Mathf.Cos(a) * radius);
        }

        /// <summary>좌석에서 탁자 중심을 보는 방향.</summary>
        public static Quaternion SeatRot(int k, int n)
        {
            var p = SeatPos(k, n, 1f);
            return Quaternion.LookRotation(-p.normalized, Vector3.up);
        }

        void Update()
        {
            if (Turntable != null)
            {
                spin += SpinSpeed * Time.deltaTime;
                Turntable.localRotation = Quaternion.Euler(0, spin, 0);
            }
            // 촛불 흔들림
            float t = Time.time;
            for (int i = 0; i < Flicker.Count; i++)
            {
                var l = Flicker[i];
                if (l == null) continue;
                float n = Mathf.PerlinNoise(t * 3.1f + i * 7.3f, i * 1.7f) * 0.18f + Mathf.PerlinNoise(t * 9f + i, 3.3f) * 0.06f;
                l.intensity = baseIntensity[i] * (0.9f + n);
            }
        }

        readonly List<float> baseIntensity = new List<float>();

        void AddFlicker(Light l)
        {
            Flicker.Add(l);
            baseIntensity.Add(l.intensity);
        }

        /// <summary>인원에 맞춰 탁자 크기를 정한다: 적을수록 작은 탁자에 가까이 (라이어스 바처럼 얼굴이 잘 보이게).</summary>
        public void SetSize(int n)
        {
            TableR = n <= 4 ? 1.05f : (n <= 6 ? 1.25f : 1.5f);
            float k = TableR / TableRadius;
            if (tableT != null) tableT.localScale = new Vector3(k, 1, k);
            if (rugT != null) rugT.localScale = Vector3.one * Mathf.Lerp(0.8f, 1f, (k - 0.6f) / 0.4f);
        }

        /// <summary>좌석 수에 맞춰 의자를 놓는다 (남는 의자는 숨김). 내 자리(0)의 의자는 카메라를 가리므로 숨긴다.</summary>
        public void ArrangeChairs(int n)
        {
            SetSize(n);
            for (int i = 0; i < Chairs.Length; i++)
            {
                bool on = i < n && i != 0;
                Chairs[i].gameObject.SetActive(on);
                if (!on) continue;
                Chairs[i].localPosition = SeatPos(i, n, SeatR);
                Chairs[i].localRotation = SeatRot(i, n);
            }
        }

        // ═════════════════ 만들기 ═════════════════

        public static Hall Build(Transform parent)
        {
            var root = new GameObject("Hall");
            root.transform.SetParent(parent, false);
            var hall = root.AddComponent<Hall>();
            hall.BuildRoom();
            hall.BuildTable();
            hall.BuildChairs(GameRulesMaxSeats);
            hall.BuildPodium();
            hall.BuildDecor();
            hall.BuildLights();
            hall.BuildAtmosphere();
            return hall;
        }

        const int GameRulesMaxSeats = 8;

        // 방 크기
        const float X0 = -7f, X1 = 7f, Z0 = -6.5f, Z1 = 8.5f, H = 6.2f;

        void BuildRoom()
        {
            var b = new ModelBuilder();
            var k = b.Kit;
            int floor = b.M("Floor"), wall = b.M("Wallpaper"), wood = b.M("Mahogany"), gold = b.M("GoldDark"), ceil = b.M("Ceiling"), dark = b.M("DarkWood"), plaster = b.M("Plaster");
            float w = X1 - X0, d = Z1 - Z0, cx = (X0 + X1) * 0.5f, cz = (Z0 + Z1) * 0.5f;
            k.Box(floor, new Vector3(cx, -0.05f, cz), new Vector3(w, 0.1f, d));
            k.Box(ceil, new Vector3(cx, H + 0.05f, cz), new Vector3(w, 0.1f, d));
            // 격자 천장 보
            for (float x = X0 + 1.75f; x < X1; x += 1.75f) k.Box(dark, new Vector3(x, H - 0.12f, cz), new Vector3(0.18f, 0.24f, d));
            for (float z = Z0 + 1.875f; z < Z1; z += 1.875f) k.Box(dark, new Vector3(cx, H - 0.1f, z), new Vector3(w, 0.2f, 0.16f));
            k.Box(gold, new Vector3(cx, H - 0.26f, Z0 + 0.02f), new Vector3(w, 0.04f, 0.04f));

            // 벽 네 면: 아래 판넬(1.1m) · 금빛 몰딩 · 벽지 · 위 몰딩
            WallSide(k, wall, wood, gold, plaster, new Vector3(cx, 0, Z1), new Vector3(1, 0, 0), w, Vector3.back);
            WallSide(k, wall, wood, gold, plaster, new Vector3(cx, 0, Z0), new Vector3(1, 0, 0), w, Vector3.forward);
            WallSide(k, wall, wood, gold, plaster, new Vector3(X0, 0, cz), new Vector3(0, 0, 1), d, Vector3.right);
            WallSide(k, wall, wood, gold, plaster, new Vector3(X1, 0, cz), new Vector3(0, 0, 1), d, Vector3.left);

            // 기둥 (옆벽에 일정 간격)
            for (float z = Z0 + 2.2f; z < Z1 - 1f; z += 3.2f)
            {
                Pilaster(k, wood, gold, new Vector3(X0 + 0.12f, 0, z));
                Pilaster(k, wood, gold, new Vector3(X1 - 0.12f, 0, z));
            }
            b.Build("Room", transform);

            // 창문 (왼쪽 벽) · 그림 (오른쪽 벽)
            for (int i = 0; i < 3; i++)
            {
                float z = Z0 + 3.8f + i * 3.2f;
                Window(new Vector3(X0 + 0.06f, 0, z), -90f);
                Painting(new Vector3(X1 - 0.06f, 2.5f, z), 90f, i);
            }
        }

        static void WallSide(MeshKit k, int wall, int wood, int gold, int plaster, Vector3 center, Vector3 along, float length, Vector3 inward)
        {
            Vector3 thick = new Vector3(Mathf.Abs(inward.x), 0, Mathf.Abs(inward.z));
            Vector3 alongAbs = new Vector3(Mathf.Abs(along.x), 0, Mathf.Abs(along.z));
            Vector3 S(float a, float h, float t) { return alongAbs * a + Vector3.up * h + thick * t; }
            k.Box(wall, center + Vector3.up * (H * 0.5f) - inward * 0.05f, S(length, H, 0.1f));
            k.Box(wood, center + Vector3.up * 0.55f + inward * 0.04f, S(length, 1.1f, 0.08f));
            k.Box(gold, center + Vector3.up * 1.12f + inward * 0.09f, S(length, 0.06f, 0.06f));
            k.Box(wood, center + Vector3.up * 0.06f + inward * 0.1f, S(length, 0.12f, 0.06f));
            k.Box(gold, center + Vector3.up * (H - 0.45f) + inward * 0.06f, S(length, 0.08f, 0.08f));
            k.Box(plaster, center + Vector3.up * (H - 0.22f) + inward * 0.03f, S(length, 0.38f, 0.06f));
            // 판넬 안쪽의 볼록한 사각 무늬
            int n = Mathf.Max(1, Mathf.RoundToInt(length / 1.4f));
            float step = length / n;
            for (int i = 0; i < n; i++)
            {
                float a = -length * 0.5f + step * (i + 0.5f);
                Vector3 c = center + along * a + Vector3.up * 0.6f + inward * 0.09f;
                k.Box(wood, c, S(step - 0.3f, 0.6f, 0.03f));
                k.Box(gold, c + inward * 0.018f + Vector3.up * 0.31f, S(step - 0.3f, 0.015f, 0.01f));
                k.Box(gold, c + inward * 0.018f - Vector3.up * 0.31f, S(step - 0.3f, 0.015f, 0.01f));
            }
        }

        static void Pilaster(MeshKit k, int wood, int gold, Vector3 at)
        {
            k.RoundedBox(wood, at + Vector3.up * (H * 0.5f), new Vector3(0.42f, H, 0.42f), 0.03f, 2);
            k.RoundedBox(gold, at + Vector3.up * 0.15f, new Vector3(0.5f, 0.3f, 0.5f), 0.02f, 2);
            k.RoundedBox(gold, at + Vector3.up * (H - 0.62f), new Vector3(0.5f, 0.18f, 0.5f), 0.03f, 2);
            k.RoundedBox(gold, at + Vector3.up * 1.12f, new Vector3(0.46f, 0.06f, 0.46f), 0.02f, 2);
        }

        void Window(Vector3 at, float yaw)
        {
            var b = new ModelBuilder();
            var k = b.Kit;
            int frame = b.M("Mahogany"), night = b.M("NightWindow"), glass = b.M("Glass"), gold = b.M("GoldDark"), cur = b.M("Velvet");
            float ww = 1.5f, wh = 3.0f, sill = 1.4f;
            k.Push(Vector3.zero);
            k.Quad(night, new Vector2(ww, wh), Vector2.zero, Vector2.one);
            k.Pop();
            k.Push(new Vector3(0, 0, -0.03f));
            k.Quad(glass, new Vector2(ww, wh), Vector2.zero, Vector2.one);
            k.Pop();
            // 창틀 + 십자 살
            k.Box(frame, new Vector3(0, wh * 0.5f + 0.06f, -0.04f), new Vector3(ww + 0.24f, 0.12f, 0.12f));
            k.Box(frame, new Vector3(0, -wh * 0.5f - 0.06f, -0.06f), new Vector3(ww + 0.3f, 0.12f, 0.2f));
            k.Box(frame, new Vector3(-ww * 0.5f - 0.06f, 0, -0.04f), new Vector3(0.12f, wh + 0.24f, 0.12f));
            k.Box(frame, new Vector3(ww * 0.5f + 0.06f, 0, -0.04f), new Vector3(0.12f, wh + 0.24f, 0.12f));
            k.Box(frame, new Vector3(0, 0, -0.035f), new Vector3(0.05f, wh, 0.05f));
            for (int i = 1; i < 4; i++) k.Box(frame, new Vector3(0, -wh * 0.5f + wh * i / 4f, -0.035f), new Vector3(ww, 0.04f, 0.05f));
            k.Box(gold, new Vector3(0, wh * 0.5f + 0.14f, -0.06f), new Vector3(ww + 0.5f, 0.05f, 0.05f));
            // 양옆 커튼 (주름진 휜 판)
            foreach (float s in new[] { -1f, 1f })
            {
                k.Push(new Vector3(s * (ww * 0.5f + 0.18f), 0.05f, -0.16f));
                for (int f = 0; f < 4; f++)
                {
                    k.Push(new Vector3((f - 1.5f) * 0.1f, 0, Mathf.Sin(f * 1.7f) * 0.03f));
                    k.CurvedPanel(cur, 0.14f, wh + 0.3f, 0.09f, u => new Vector2(-1, 1), 8, 6, 0.01f);
                    k.Pop();
                }
                k.Pop();
            }
            var go = b.Build("Window", transform, false);
            go.transform.localPosition = at + Vector3.up * (sill + wh * 0.5f);
            go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
        }

        void Painting(Vector3 at, float yaw, int idx)
        {
            var b = new ModelBuilder();
            var k = b.Kit;
            int gold = b.M("Gold"), pic = b.M("Painting" + (idx % 4)), brass = b.M("Brass"), glow = b.M("WarmGlow");
            float pw = 1.7f, ph = 1.25f, bar = 0.12f;
            k.Quad(pic, new Vector2(pw, ph), Vector2.zero, Vector2.one);
            k.RoundedBox(gold, new Vector3(0, ph * 0.5f + bar * 0.4f, 0.01f), new Vector3(pw + bar * 2, bar, 0.08f), 0.03f, 2);
            k.RoundedBox(gold, new Vector3(0, -ph * 0.5f - bar * 0.4f, 0.01f), new Vector3(pw + bar * 2, bar, 0.08f), 0.03f, 2);
            k.RoundedBox(gold, new Vector3(-pw * 0.5f - bar * 0.4f, 0, 0.01f), new Vector3(bar, ph, 0.08f), 0.03f, 2);
            k.RoundedBox(gold, new Vector3(pw * 0.5f + bar * 0.4f, 0, 0.01f), new Vector3(bar, ph, 0.08f), 0.03f, 2);
            // 그림 조명
            k.Push(new Vector3(0, ph * 0.5f + 0.25f, -0.16f), new Vector3(0, 0, 90));
            k.Cylinder(brass, 0.035f, -0.4f, 0.8f, 16, true);
            k.Pop();
            k.Box(glow, new Vector3(0, ph * 0.5f + 0.22f, -0.18f), new Vector3(0.75f, 0.01f, 0.03f));
            var go = b.Build("Painting", transform, false);
            go.transform.localPosition = at;
            go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            var l = AddSpot(go.transform, new Vector3(0, ph * 0.5f + 0.2f, -0.3f), Quaternion.Euler(60, 0, 0), new Color(1f, 0.8f, 0.55f), 3.5f, 4f, 70f, false);
            AddFlicker(l);
        }

        void BuildTable()
        {
            var b = new ModelBuilder();
            var k = b.Kit;
            int wood = b.M("Mahogany"), felt = b.M("Felt"), gold = b.M("Gold"), marble = b.M("MarbleDark");
            float r = TableRadius, h = TableHeight;
            // 받침 기둥 (돌림판) + 네 다리
            k.Lathe(wood, new[]
            {
                P(0, 0.02f), P(0.42f, 0.02f), P(0.44f, 0.05f), P(0.38f, 0.09f), P(0.2f, 0.14f), P(0.16f, 0.22f), P(0.2f, 0.3f),
                P(0.15f, 0.38f), P(0.12f, 0.5f), P(0.14f, 0.6f), P(0.22f, 0.66f), P(0.5f, h - 0.07f), P(0, h - 0.07f)
            }, 40);
            for (int i = 0; i < 4; i++)
            {
                float a = (i * 90f + 45f) * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                var path = MeshKit.Bezier(dir * 0.3f + Vector3.up * 0.12f, dir * 0.55f + Vector3.up * 0.1f, dir * 0.72f + Vector3.up * 0.02f, dir * 0.78f + Vector3.up * 0.06f, 12);
                k.Tube(wood, path, new[] { 0.06f, 0.035f }, 10, true);
                k.Sphere(gold, dir * 0.8f + Vector3.up * 0.04f, 0.045f, 12, 8);
            }
            // 상판: 마호가니 테 + 금줄 + 녹색 펠트
            k.Lathe(wood, new[] { P(0.5f, h - 0.07f), P(r - 0.03f, h - 0.07f), P(r, h - 0.05f), P(r + 0.012f, h - 0.02f), P(r, h + 0.01f), P(r - 0.03f, h + 0.02f), P(r - 0.16f, h + 0.02f), P(r - 0.18f, h + 0.005f) }, 64);
            k.Lathe(gold, new[] { P(r + 0.006f, h - 0.042f), P(r + 0.016f, h - 0.035f), P(r + 0.006f, h - 0.028f) }, 64);
            k.Lathe(gold, new[] { P(r - 0.19f, h + 0.004f), P(r - 0.185f, h + 0.012f), P(r - 0.175f, h + 0.004f) }, 64);
            k.Disc(felt, h + 0.004f, r - 0.18f, 64, true);
            tableT = b.Build("Table", transform).transform;

            // 회전판 받침 (대리석) — 탁자 크기를 바꿔도 그대로
            var bb = new ModelBuilder();
            // 금테는 반짝임이 덜한 금 (1인칭에서 바로 앞 카드 위로 빛이 번지지 않게)
            int bm = bb.M("MarbleDark"), bg = bb.M("gold_satin", ArtLibrary.I.Satin("Gold", 0.6f, 0.5f));
            bb.Kit.Lathe(bm, new[] { P(0.36f, h + 0.004f), P(0.36f, h + 0.03f), P(0.33f, h + 0.06f), P(0.3f, h + 0.07f), P(0.28f, h + 0.08f), P(0, h + 0.08f) }, 48, false, false, 360f, true);
            bb.Kit.Lathe(bg, new[] { P(0.365f, h + 0.004f), P(0.372f, h + 0.016f), P(0.365f, h + 0.028f) }, 48);
            bb.Build("TurntableBase", transform);

            // 회전판 (돌아가는 부분)
            var tb = new ModelBuilder();
            var tk = tb.Kit;
            int tg = tb.M("gold_satin", ArtLibrary.I.Satin("Gold", 0.6f, 0.5f)), tv = tb.M("turntable", ArtLibrary.I.Tinted("VelvetPlain", new Color(0.13f, 0.015f, 0.03f)));
            tk.Lathe(tg, new[] { P(0, h + 0.08f), P(0.27f, h + 0.08f), P(0.28f, h + 0.095f), P(0.275f, h + 0.11f), P(0.26f, h + 0.115f) }, 48);
            tk.Disc(tv, h + 0.113f, 0.26f, 48, true);
            var tt = tb.Build("Turntable", transform);
            Turntable = tt.transform;
            var anchor = new GameObject("ItemAnchor").transform;
            anchor.SetParent(Turntable, false);
            anchor.localPosition = new Vector3(0, TurntableTop, 0);
            ItemAnchor = anchor;
        }

        void BuildChairs(int count)
        {
            Chairs = new Transform[count];
            for (int i = 0; i < count; i++)
            {
                var b = new ModelBuilder();
                var k = b.Kit;
                int wood = b.M("Mahogany"), vel = b.M("Velvet"), gold = b.M("GoldDark");
                // 좌석 (원점 = 의자 앞쪽 가운데 바닥, 의자 등받이는 +Z(바깥)쪽이 아니라 탁자 반대쪽)
                k.RoundedBox(vel, new Vector3(0, 0.48f, 0.05f), new Vector3(0.56f, 0.1f, 0.52f), 0.04f, 3);
                k.RoundedBox(wood, new Vector3(0, 0.41f, 0.05f), new Vector3(0.6f, 0.06f, 0.56f), 0.02f, 2);
                // 등받이: 의자는 탁자(+Z)를 보도록 돌리므로 등받이는 -Z 쪽
                k.Push(new Vector3(0, 0.95f, -0.24f), new Vector3(8, 0, 0));
                k.RoundedBox(wood, Vector3.zero, new Vector3(0.6f, 0.95f, 0.07f), 0.035f, 3);
                k.RoundedBox(vel, new Vector3(0, 0, 0.03f), new Vector3(0.48f, 0.8f, 0.05f), 0.03f, 3);
                for (int y = 0; y < 3; y++)
                    for (int x = 0; x < 3; x++)
                        k.Sphere(gold, new Vector3((x - 1) * 0.13f, (y - 1) * 0.22f, 0.058f), 0.011f, 8, 6);
                k.Lathe(gold, new[] { P(0, 0.47f), P(0.03f, 0.49f), P(0.02f, 0.52f), P(0, 0.54f) }, 12);
                k.Pop();
                var go = b.Build("Chair" + i, transform);
                Chairs[i] = go.transform;
                AddLegs(go.transform);
            }
        }

        static void AddLegs(Transform chair)
        {
            var b = new ModelBuilder();
            var k = b.Kit;
            int wood = b.M("Mahogany");
            foreach (var c in new[] { new Vector2(-0.25f, -0.18f), new Vector2(0.25f, -0.18f), new Vector2(-0.25f, 0.28f), new Vector2(0.25f, 0.28f) })
            {
                k.Push(new Vector3(c.x, 0, c.y));
                k.Lathe(wood, new[] { P(0.022f, 0), P(0.03f, 0.05f), P(0.024f, 0.2f), P(0.03f, 0.38f), P(0.032f, 0.4f) }, 12);
                k.Pop();
            }
            b.Build("Legs", chair);
        }

        void BuildPodium()
        {
            var b = new ModelBuilder();
            var k = b.Kit;
            int marble = b.M("MarbleDark"), gold = b.M("Gold"), wood = b.M("Mahogany"), vel = b.M("Velvet"), brass = b.M("Brass");
            var c = new Vector3(AuctioneerPos.x, 0, AuctioneerPos.z + 0.15f);
            k.Push(c);
            k.Lathe(marble, new[] { P(1.15f, 0), P(1.15f, 0.17f), P(0.95f, 0.18f), P(0.95f, 0.34f), P(0.9f, 0.36f), P(0, 0.36f) }, 64, false, false, 360f, true);
            k.Lathe(gold, new[] { P(1.155f, 0.15f), P(1.165f, 0.16f), P(1.155f, 0.17f) }, 64);
            k.Lathe(gold, new[] { P(0.955f, 0.32f), P(0.965f, 0.33f), P(0.955f, 0.34f) }, 64);
            k.Pop();
            // 경매사 단상 (탁자 쪽 앞에 서 있는 연설대)
            var lc = new Vector3(0, 0.36f, AuctioneerPos.z - 0.45f);
            k.Push(lc);
            k.Lathe(wood, new[] { P(0.32f, 0), P(0.34f, 0.04f), P(0.26f, 0.12f), P(0.2f, 0.2f), P(0.17f, 0.6f), P(0.22f, 0.75f), P(0.36f, 0.9f), P(0.4f, 0.98f), P(0.42f, 1.02f), P(0, 1.02f) }, 40, false, false, 360f);
            k.Lathe(gold, new[] { P(0.405f, 0.96f), P(0.425f, 0.99f), P(0.405f, 1.02f) }, 40);
            k.Lathe(gold, new[] { P(0.172f, 0.4f), P(0.185f, 0.42f), P(0.172f, 0.44f) }, 24);
            // 의사봉 받침
            k.Lathe(wood, new[] { P(0.07f, 1.02f), P(0.075f, 1.04f), P(0.06f, 1.05f), P(0, 1.05f) }, 24);
            k.Pop();
            b.Build("Podium", transform);

            // 무대 뒤 벨벳 막 + 금테
            var bb = new ModelBuilder();
            var bk = bb.Kit;
            int bv = bb.M("Velvet"), bg = bb.M("Gold");
            bk.Push(new Vector3(0, 0, Z1 - 0.3f));
            for (int i = 0; i < 26; i++)
            {
                float x = -4.6f + i * 0.36f;
                bk.Push(new Vector3(x, H * 0.5f - 0.2f, Mathf.Sin(i * 1.3f) * 0.04f));
                bk.CurvedPanel(bv, 0.42f, H - 0.4f, 0.2f, u => new Vector2(-1, 1), 10, 8, 0.01f);
                bk.Pop();
            }
            bk.Box(bg, new Vector3(0, H - 0.5f, -0.3f), new Vector3(9.8f, 0.24f, 0.08f));
            // 늘어진 밸런스 (위 장식 천)
            for (int i = 0; i < 9; i++)
            {
                bk.Push(new Vector3(-4.4f + i * 1.1f, H - 0.95f, -0.36f));
                bk.CurvedPanel(bv, 1.2f, 0.8f, 2.5f, u => new Vector2(-0.2f - 0.8f * (1 - u * u), 1f), 16, 4, 0.01f);
                bk.Pop();
            }
            bk.Pop();
            bb.Build("Backdrop", transform, false);
        }

        void BuildDecor()
        {
            // 큰 원형 러그
            var b = new ModelBuilder();
            var k = b.Kit;
            int rug = b.M("Rug");
            k.Push(new Vector3(0, 0.006f, 0), new Vector3(90, 0, 0));
            k.Quad(rug, new Vector2(6.2f, 6.2f), Vector2.zero, Vector2.one);
            k.Pop();
            // Quad 는 -Z 를 보므로 X 축으로 90° 눕히면 위를 본다
            var rgo = b.Build("Rug", transform, false);
            rugT = rgo.transform;
            rgo.transform.localPosition = new Vector3(0, 0.006f, 0);

            // 무대 양옆 받침대 위 진열품 (유리 돔)
            int[] showcase = { 0, 5, 8, 6 };
            Vector3[] spots = { new Vector3(-3.4f, 0, 5.6f), new Vector3(3.4f, 0, 5.6f), new Vector3(-5.6f, 0, 2.2f), new Vector3(5.6f, 0, 2.2f) };
            for (int i = 0; i < spots.Length; i++)
            {
                var pb = new ModelBuilder();
                var pk = pb.Kit;
                int marble = pb.M("Marble"), gold = pb.M("GoldDark"), glass = pb.M("Glass");
                pk.Lathe(marble, new[] { P(0.32f, 0), P(0.32f, 0.08f), P(0.25f, 0.12f), P(0.2f, 0.2f), P(0.18f, 0.9f), P(0.24f, 0.98f), P(0.3f, 1.02f), P(0.3f, 1.06f), P(0, 1.06f) }, 32);
                pk.Lathe(gold, new[] { P(0.305f, 1.0f), P(0.315f, 1.03f), P(0.305f, 1.06f) }, 32);
                pk.Lathe(glass, new[] { P(0.26f, 1.06f), P(0.26f, 1.45f), P(0.22f, 1.56f), P(0.12f, 1.62f), P(0, 1.64f) }, 32);
                var ped = pb.Build("Showcase" + i, transform);
                ped.transform.localPosition = spots[i];
                var item = ItemModels.Build(showcase[i], ped.transform);
                item.transform.localPosition = new Vector3(0, 1.065f, 0);
                item.transform.localRotation = Quaternion.Euler(0, 180 + spots[i].x * 8f, 0);
                item.transform.localScale = Vector3.one * 0.9f;
                var l = AddSpot(ped.transform, new Vector3(0, 2.6f, -0.4f), Quaternion.Euler(78, 0, 0), new Color(1f, 0.92f, 0.8f), 4f, 4f, 30f, false);
                l.name = "ShowcaseLight";
            }

            // 벽 촛대 (기둥마다)
            for (float z = Z0 + 2.2f; z < Z1 - 1f; z += 3.2f)
            {
                Sconce(new Vector3(X0 + 0.34f, 2.35f, z), -90f);
                Sconce(new Vector3(X1 - 0.34f, 2.35f, z), 90f);
            }
            // 구석 야자 화분
            foreach (var p in new[] { new Vector3(-6.2f, 0, 7.6f), new Vector3(6.2f, 0, 7.6f), new Vector3(-6.2f, 0, -5.7f), new Vector3(6.2f, 0, -5.7f) }) Palm(p);
        }

        void Sconce(Vector3 at, float yaw)
        {
            var b = new ModelBuilder();
            var k = b.Kit;
            int brass = b.M("Brass"), wax = b.M("CandleWax"), flame = b.M("Flame"), glow = b.M("WarmGlow");
            k.RoundedBox(brass, new Vector3(0, 0, 0.0f), new Vector3(0.14f, 0.24f, 0.03f), 0.012f, 2);
            foreach (float s in new[] { -1f, 1f })
            {
                var path = MeshKit.Bezier(new Vector3(0, -0.04f, -0.02f), new Vector3(s * 0.06f, -0.1f, -0.12f), new Vector3(s * 0.16f, 0.0f, -0.14f), new Vector3(s * 0.16f, 0.06f, -0.14f), 12);
                k.Tube(brass, path, new[] { 0.01f }, 8, true);
                k.Push(new Vector3(s * 0.16f, 0.06f, -0.14f));
                k.Lathe(brass, new[] { P(0.01f, -0.01f), P(0.04f, 0.0f), P(0.042f, 0.01f), P(0.02f, 0.015f) }, 16);
                k.Lathe(wax, new[] { P(0.016f, 0.01f), P(0.016f, 0.13f), P(0, 0.132f) }, 12);
                k.Lathe(flame, new[] { P(0, 0.136f), P(0.009f, 0.145f), P(0.011f, 0.16f), P(0.005f, 0.18f), P(0, 0.195f) }, 10);
                // 불빛 둘레의 은은한 유리 갓
                k.Lathe(glow, new[] { P(0.02f, 0.11f), P(0.05f, 0.16f), P(0.045f, 0.22f), P(0.03f, 0.24f) }, 16);
                k.Pop();
            }
            var go = b.Build("Sconce", transform, false);
            go.transform.localPosition = at;
            go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            var l = AddPoint(go.transform, new Vector3(0, 0.25f, -0.25f), new Color(1f, 0.66f, 0.36f), 3.2f, 5f, false);
            AddFlicker(l);
        }

        void Palm(Vector3 at)
        {
            var b = new ModelBuilder();
            var k = b.Kit;
            int pot = b.M("PorcelainBlue"), gold = b.M("GoldDark"), leaf = b.M("Feather"), soil = b.M("DarkWood");
            k.Lathe(pot, new[] { P(0, 0), P(0.2f, 0), P(0.24f, 0.08f), P(0.3f, 0.35f), P(0.32f, 0.5f), P(0.34f, 0.55f), P(0.31f, 0.56f), P(0.3f, 0.52f) }, 32);
            k.Lathe(gold, new[] { P(0.3f, 0.3f), P(0.306f, 0.32f), P(0.3f, 0.34f) }, 32);
            k.Disc(soil, 0.5f, 0.3f, 24, true);
            var rnd = new System.Random(at.GetHashCode());
            for (int i = 0; i < 9; i++)
            {
                float a = i * 40f + (float)rnd.NextDouble() * 20f;
                float tilt = 35f + (float)rnd.NextDouble() * 25f;
                k.Push(new Vector3(0, 0.55f, 0), new Vector3(0, a, 0));
                k.Tube(soil, MeshKit.Bezier(Vector3.zero, new Vector3(0, 0.5f, 0.15f), new Vector3(0, 0.75f, 0.55f), 10), new[] { 0.015f, 0.006f }, 6, false);
                k.Push(new Vector3(0, 0.62f, 0.32f), new Vector3(tilt, 0, 0));
                k.CurvedPanel(leaf, 0.32f, 0.9f, 0.25f, u => new Vector2(-0.15f - 0.85f * (1 - Mathf.Abs(u)), 0.15f + 0.85f * (1 - Mathf.Abs(u))), 10, 8, 0.004f);
                k.Pop();
                k.Pop();
            }
            var go = b.Build("Palm", transform);
            go.transform.localPosition = at;
            var mr = go.GetComponent<MeshRenderer>();
            var mats = mr.sharedMaterials;
            for (int i = 0; i < mats.Length; i++) if (mats[i] != null && mats[i].name.StartsWith("Feather")) mats[i] = ArtLibrary.I.Tinted("Feather", new Color(0.16f, 0.32f, 0.16f));
            mr.sharedMaterials = mats;
        }

        void BuildLights()
        {
            // 탁자 위 샹들리에: 아래를 비추는 그림자 스포트 + 천장을 밝히는 점광
            List<Vector3> tips;
            var ch = Chandelier.Build(transform, new Vector3(0, 3.55f, 0), 1.15f, H - 3.55f, out tips);
            TableLight = AddSpot(transform, new Vector3(0, 3.3f, -0.2f), Quaternion.Euler(90, 0, 0), new Color(1f, 0.74f, 0.48f), 26f, 9f, 115f, true);
            TableLight.shadows = LightShadows.Soft;
            TableLight.shadowStrength = 0.85f;
            AddFlicker(TableLight);
            var glow = AddPoint(transform, new Vector3(0, 3.9f, 0), new Color(1f, 0.7f, 0.42f), 3.5f, 7f, false);
            AddFlicker(glow);

            // 경매품 조명: 앞(카메라 쪽) 위에서 주 조명 + 뒤에서 윤곽 조명
            var itemTarget = new Vector3(0, TurntableTop + 0.18f, 0);
            var keyPos = new Vector3(0.7f, 2.7f, -1.5f);
            ItemSpot = AddSpot(transform, keyPos, Quaternion.LookRotation(itemTarget - keyPos), new Color(1f, 0.95f, 0.88f), 11f, 5f, 24f, true);
            ItemSpot.shadows = LightShadows.Soft;
            ItemSpot.innerSpotAngle = 10f;
            var rimPos = new Vector3(-0.4f, 2.9f, 1.5f);
            AddSpot(transform, rimPos, Quaternion.LookRotation(itemTarget - rimPos), new Color(1f, 0.8f, 0.6f), 7f, 5f, 24f, false);
            // 벽을 은은하게 쓸어 올리는 조명 (방의 깊이)
            for (int side = -1; side <= 1; side += 2)
                for (int i = 0; i < 4; i++)
                {
                    var p = new Vector3(side * (X1 - 1.2f), 0.4f, Z0 + 2.4f + i * 3.4f);
                    var to = new Vector3(side * X1, 3.6f, p.z);
                    AddSpot(transform, p, Quaternion.LookRotation(to - p), new Color(1f, 0.62f, 0.38f), 7f, 7f, 70f, false);
                }
            var backPos = new Vector3(0, 0.5f, Z1 - 2.6f);
            AddSpot(transform, backPos, Quaternion.LookRotation(new Vector3(0, 3.5f, Z1) - backPos), new Color(1f, 0.5f, 0.35f), 10f, 7f, 80f, false);

            // 무대 막을 비추는 두 스포트 (뒤 배경의 깊이)
            AddSpot(transform, new Vector3(-3f, 5.5f, 4.5f), Quaternion.LookRotation(new Vector3(-1.5f, 2.2f, 8.2f) - new Vector3(-3f, 5.5f, 4.5f)), new Color(1f, 0.55f, 0.4f), 18f, 9f, 55f, false);
            AddSpot(transform, new Vector3(3f, 5.5f, 4.5f), Quaternion.LookRotation(new Vector3(1.5f, 2.2f, 8.2f) - new Vector3(3f, 5.5f, 4.5f)), new Color(1f, 0.55f, 0.4f), 18f, 9f, 55f, false);
            // 경매사 얼굴을 비추는 조명
            AddSpot(transform, new Vector3(0, 3.6f, 1.2f), Quaternion.LookRotation(AuctioneerPos + Vector3.up * 1.4f - new Vector3(0, 3.6f, 1.2f)), new Color(1f, 0.82f, 0.62f), 10f, 6f, 28f, false);
            // 카메라 뒤쪽에서 오는 약한 채움 조명 (얼굴이 너무 어둡지 않게)
            AddPoint(transform, new Vector3(0, 2.4f, -4.6f), new Color(0.75f, 0.68f, 0.85f), 1.6f, 7f, false);

            // 반사 프로브 (방 크기 상자 투영)
            var pgo = new GameObject("Probe");
            pgo.transform.SetParent(transform, false);
            pgo.transform.localPosition = new Vector3(0, 1.4f, 0.5f);
            Probe = pgo.AddComponent<ReflectionProbe>();
            Probe.mode = ReflectionProbeMode.Realtime;
            Probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            Probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            Probe.boxProjection = true;
            Probe.size = new Vector3(X1 - X0, H, Z1 - Z0);
            Probe.center = new Vector3(0, H * 0.5f - 1.4f, (Z0 + Z1) * 0.5f - 0.5f);
            Probe.resolution = 256;
            Probe.hdr = true;
            Probe.intensity = 1f;
        }

        void BuildAtmosphere()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.2f, 0.16f, 0.14f);
            RenderSettings.ambientEquatorColor = new Color(0.15f, 0.11f, 0.085f);
            RenderSettings.ambientGroundColor = new Color(0.07f, 0.05f, 0.04f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.06f, 0.04f, 0.035f);
            RenderSettings.fogDensity = 0.022f;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;

            // 조명 속 떠다니는 먼지
            var go = new GameObject("Dust");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0, 2.2f, 0.5f);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.startLifetime = 14f;
            main.startSpeed = 0.02f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.008f, 0.02f);
            main.startColor = new Color(1f, 0.85f, 0.6f, 0.16f);
            main.maxParticles = 250;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.prewarm = true;
            var em = ps.emission;
            em.rateOverTime = 26f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(5f, 3.4f, 5f);
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.05f;
            noise.frequency = 0.2f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.2f), new GradientAlphaKey(1, 0.8f), new GradientAlphaKey(0, 1) });
            col.color = grad;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = ArtLibrary.I.ParticleAdditive;
            r.shadowCastingMode = ShadowCastingMode.Off;
            // 렌즈 바로 앞을 지나는 먼지가 화면을 덮지 않게 (화면 대비 최대 크기)
            r.maxParticleSize = 0.006f;
            ps.Play();
        }

        static Light AddSpot(Transform parent, Vector3 pos, Quaternion rot, Color c, float intensity, float range, float angle, bool shadows)
        {
            var go = new GameObject("Spot");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot;
            var l = go.AddComponent<Light>();
            l.type = LightType.Spot;
            l.color = c;
            l.intensity = intensity;
            l.range = range;
            l.spotAngle = angle;
            l.innerSpotAngle = angle * 0.6f;
            l.shadows = shadows ? LightShadows.Soft : LightShadows.None;
            l.shadowBias = 0.02f;
            l.shadowNormalBias = 0.3f;
            return l;
        }

        static Light AddPoint(Transform parent, Vector3 pos, Color c, float intensity, float range, bool shadows)
        {
            var go = new GameObject("Point");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = c;
            l.intensity = intensity;
            l.range = range;
            l.shadows = shadows ? LightShadows.Soft : LightShadows.None;
            return l;
        }
    }
}
