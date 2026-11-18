using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BluffAuction.Art
{
    /// <summary>
    /// 코드로 매끈한 모형을 만드는 도구. 돌림판(Lathe)·타원체·둥근 상자·도넛·관(Tube)·각진 보석 등을 하위 메시(재질 칸)별로 쌓아
    /// 하나의 Mesh 로 만든다. 모든 도형은 현재 변환 행렬(Push/Pop) 기준으로 놓인다.
    /// </summary>
    public sealed class MeshKit
    {
        readonly List<Vector3> pos = new List<Vector3>();
        readonly List<Vector3> nrm = new List<Vector3>();
        readonly List<Vector2> uv = new List<Vector2>();
        readonly List<Color32> col = new List<Color32>();
        readonly List<List<int>> subs = new List<List<int>>();
        readonly Stack<Matrix4x4> stack = new Stack<Matrix4x4>();
        Matrix4x4 m = Matrix4x4.identity;
        Matrix4x4 mn = Matrix4x4.identity;
        public Color32 VertexColor = new Color32(255, 255, 255, 255);

        public int VertexCount { get { return pos.Count; } }

        // ───────── 변환 ─────────

        public MeshKit Push(Vector3 t, Quaternion r, Vector3 s)
        {
            stack.Push(m);
            m = m * Matrix4x4.TRS(t, r, s);
            mn = m.inverse.transpose;
            return this;
        }

        public MeshKit Push(Vector3 t) { return Push(t, Quaternion.identity, Vector3.one); }
        public MeshKit Push(Vector3 t, Vector3 euler) { return Push(t, Quaternion.Euler(euler), Vector3.one); }
        public MeshKit Push(Vector3 t, Vector3 euler, float s) { return Push(t, Quaternion.Euler(euler), Vector3.one * s); }

        public MeshKit Pop()
        {
            m = stack.Count > 0 ? stack.Pop() : Matrix4x4.identity;
            mn = m.inverse.transpose;
            return this;
        }

        List<int> Sub(int s)
        {
            while (subs.Count <= s) subs.Add(new List<int>());
            return subs[s];
        }

        int V(Vector3 p, Vector3 n, Vector2 t)
        {
            pos.Add(m.MultiplyPoint3x4(p));
            nrm.Add(mn.MultiplyVector(n).normalized);
            uv.Add(t);
            col.Add(VertexColor);
            return pos.Count - 1;
        }

        void Tri(int s, int a, int b, int c)
        {
            var l = Sub(s);
            l.Add(a);
            l.Add(b);
            l.Add(c);
        }

        void QuadIdx(int s, int a, int b, int c, int d)
        {
            Tri(s, a, b, c);
            Tri(s, a, c, d);
        }

        // ───────── 도형 ─────────

        /// <summary>
        /// 돌림판: profile 은 (반지름, 높이) 점 목록 (아래→위). Y 축을 중심으로 돌린다.
        /// 한 점이 반지름 0 이면 그 자리가 막힌다. hard 가 true 면 마디마다 각진 면.
        /// </summary>
        public MeshKit Lathe(int s, IList<Vector2> profile, int segments = 32, bool capBottom = false, bool capTop = false,
            float arcDegrees = 360f, bool hard = false, float vScale = 1f)
        {
            int n = profile.Count;
            if (n < 2) return this;
            bool full = arcDegrees >= 359.99f;
            int cols = segments + 1;
            // 프로필 길이를 v 좌표로
            var len = new float[n];
            for (int i = 1; i < n; i++) len[i] = len[i - 1] + Vector2.Distance(profile[i], profile[i - 1]);
            float total = Mathf.Max(1e-5f, len[n - 1]);

            if (!hard)
            {
                // 점마다 매끈한 법선 (앞뒤 선분의 평균)
                var pn = new Vector2[n];
                for (int i = 0; i < n; i++)
                {
                    Vector2 a = profile[Mathf.Max(0, i - 1)], b = profile[Mathf.Min(n - 1, i + 1)];
                    Vector2 d = (b - a).normalized;
                    pn[i] = new Vector2(d.y, -d.x);
                }
                int start = pos.Count;
                for (int i = 0; i < n; i++)
                {
                    for (int j = 0; j < cols; j++)
                    {
                        float ang = arcDegrees * Mathf.Deg2Rad * j / segments;
                        float cs = Mathf.Cos(ang), sn = Mathf.Sin(ang);
                        var p = new Vector3(profile[i].x * cs, profile[i].y, profile[i].x * sn);
                        var nn = new Vector3(pn[i].x * cs, pn[i].y, pn[i].x * sn);
                        if (profile[i].x < 1e-5f) nn = new Vector3(0, Mathf.Sign(pn[i].y == 0 ? 1 : pn[i].y), 0);
                        V(p, nn, new Vector2((float)j / segments, len[i] / total * vScale));
                    }
                }
                for (int i = 0; i < n - 1; i++)
                    for (int j = 0; j < segments; j++)
                    {
                        int a = start + i * cols + j, b = a + 1, c = a + cols + 1, d = a + cols;
                        QuadIdx(s, a, d, c, b);
                    }
            }
            else
            {
                for (int i = 0; i < n - 1; i++)
                {
                    Vector2 a2 = profile[i], b2 = profile[i + 1];
                    Vector2 dd = (b2 - a2).normalized;
                    var pn = new Vector2(dd.y, -dd.x);
                    int start = pos.Count;
                    for (int j = 0; j < cols; j++)
                    {
                        float ang = arcDegrees * Mathf.Deg2Rad * j / segments;
                        float cs = Mathf.Cos(ang), sn = Mathf.Sin(ang);
                        var nn = new Vector3(pn.x * cs, pn.y, pn.x * sn);
                        V(new Vector3(a2.x * cs, a2.y, a2.x * sn), nn, new Vector2((float)j / segments, len[i] / total * vScale));
                        V(new Vector3(b2.x * cs, b2.y, b2.x * sn), nn, new Vector2((float)j / segments, len[i + 1] / total * vScale));
                    }
                    for (int j = 0; j < segments; j++)
                    {
                        int a = start + j * 2;
                        QuadIdx(s, a, a + 1, a + 3, a + 2);
                    }
                }
            }
            if (capBottom && profile[0].x > 1e-5f) Disc(s, profile[0].y, profile[0].x, segments, false);
            if (capTop && profile[n - 1].x > 1e-5f) Disc(s, profile[n - 1].y, profile[n - 1].x, segments, true);
            return this;
        }

        /// <summary>
        /// 앞(+Z)이 트인 돌림판 (외투·조끼): 높이마다 앞 가운데에서 좌우로 openDeg[i] 도씩 비운다 (0 이면 닫힘).
        /// 트인 가장자리가 높이에 따라 매끈하게 이어져 V 자 깃이 된다.
        /// </summary>
        public MeshKit LatheOpen(int s, IList<Vector2> profile, IList<float> openDeg, int segments = 40)
        {
            int n = profile.Count;
            if (n < 2) return this;
            var len = new float[n];
            for (int i = 1; i < n; i++) len[i] = len[i - 1] + Vector2.Distance(profile[i], profile[i - 1]);
            float total = Mathf.Max(1e-5f, len[n - 1]);
            int cols = segments + 1;
            int start = pos.Count;
            for (int i = 0; i < n; i++)
            {
                Vector2 a = profile[Mathf.Max(0, i - 1)], b = profile[Mathf.Min(n - 1, i + 1)];
                Vector2 d = (b - a).normalized;
                var pn = new Vector2(d.y, -d.x);
                float open = Mathf.Clamp(openDeg[Mathf.Min(i, openDeg.Count - 1)], 0f, 170f);
                float a0 = (90f + open) * Mathf.Deg2Rad, a1 = (450f - open) * Mathf.Deg2Rad;
                for (int j = 0; j < cols; j++)
                {
                    float ang = Mathf.Lerp(a0, a1, (float)j / segments);
                    float cs = Mathf.Cos(ang), sn = Mathf.Sin(ang);
                    V(new Vector3(profile[i].x * cs, profile[i].y, profile[i].x * sn), new Vector3(pn.x * cs, pn.y, pn.x * sn), new Vector2((float)j / segments, len[i] / total));
                }
            }
            for (int i = 0; i < n - 1; i++)
                for (int j = 0; j < segments; j++)
                {
                    int a = start + i * cols + j, b = a + 1, c = a + cols + 1, d = a + cols;
                    QuadIdx(s, a, d, c, b);
                }
            return this;
        }

        /// <summary>LatheOpen 의 트인 가장자리 한쪽 (side -1 왼쪽 / +1 오른쪽) 을 따라가는 점들 (현재 변환 밖, 모형 좌표).</summary>
        public static List<Vector3> OpenEdge(IList<Vector2> profile, IList<float> openDeg, float side, float outward = 0f)
        {
            var l = new List<Vector3>();
            for (int i = 0; i < profile.Count; i++)
            {
                float open = Mathf.Clamp(openDeg[Mathf.Min(i, openDeg.Count - 1)], 0f, 170f);
                float ang = (90f - side * open) * Mathf.Deg2Rad;
                float r = profile[i].x + outward;
                l.Add(new Vector3(Mathf.Cos(ang) * r, profile[i].y, Mathf.Sin(ang) * r));
            }
            return l;
        }

        /// <summary>두 끝이 둥근 막대 (손가락 마디 등).</summary>
        public MeshKit Capsule(int s, Vector3 a, Vector3 b, float r, int sides = 10)
        {
            Tube(s, new List<Vector3> { a, b }, new[] { r }, sides, false);
            Sphere(s, a, r, sides, Mathf.Max(4, sides / 2));
            Sphere(s, b, r, sides, Mathf.Max(4, sides / 2));
            return this;
        }

        /// <summary>Y 높이에 놓인 원판 (up 이면 위를 봄).</summary>
        public MeshKit Disc(int s, float y, float r, int segments = 32, bool up = true)
        {
            var nn = up ? Vector3.up : Vector3.down;
            int c = V(new Vector3(0, y, 0), nn, new Vector2(0.5f, 0.5f));
            int start = pos.Count;
            for (int j = 0; j <= segments; j++)
            {
                float ang = Mathf.PI * 2 * j / segments;
                V(new Vector3(Mathf.Cos(ang) * r, y, Mathf.Sin(ang) * r), nn, new Vector2(0.5f + Mathf.Cos(ang) * 0.5f, 0.5f + Mathf.Sin(ang) * 0.5f));
            }
            for (int j = 0; j < segments; j++)
            {
                if (up) Tri(s, c, start + j + 1, start + j);
                else Tri(s, c, start + j, start + j + 1);
            }
            return this;
        }

        /// <summary>원기둥 (밑면 중심 y0, 높이 h).</summary>
        public MeshKit Cylinder(int s, float r, float y0, float h, int segments = 24, bool caps = true)
        {
            Lathe(s, new[] { new Vector2(r, y0), new Vector2(r, y0 + h) }, segments, false, false, 360f, true);
            if (caps)
            {
                Disc(s, y0, r, segments, false);
                Disc(s, y0 + h, r, segments, true);
            }
            return this;
        }

        /// <summary>타원체 (중심, 반지름 3축).</summary>
        public MeshKit Ellipsoid(int s, Vector3 c, Vector3 r, int lon = 24, int lat = 16)
        {
            int start = pos.Count;
            for (int i = 0; i <= lat; i++)
            {
                float th = Mathf.PI * i / lat;
                float y = -Mathf.Cos(th), rr = Mathf.Sin(th);
                for (int j = 0; j <= lon; j++)
                {
                    float ph = Mathf.PI * 2 * j / lon;
                    var u = new Vector3(rr * Mathf.Cos(ph), y, rr * Mathf.Sin(ph));
                    var p = c + Vector3.Scale(u, r);
                    var nn = new Vector3(u.x / Mathf.Max(1e-5f, r.x), u.y / Mathf.Max(1e-5f, r.y), u.z / Mathf.Max(1e-5f, r.z));
                    V(p, nn, new Vector2((float)j / lon, (float)i / lat));
                }
            }
            int cols = lon + 1;
            for (int i = 0; i < lat; i++)
                for (int j = 0; j < lon; j++)
                {
                    int a = start + i * cols + j, b = a + 1, cc = a + cols + 1, d = a + cols;
                    QuadIdx(s, a, d, cc, b);
                }
            return this;
        }

        public MeshKit Sphere(int s, Vector3 c, float r, int lon = 24, int lat = 16) { return Ellipsoid(s, c, Vector3.one * r, lon, lat); }

        /// <summary>둥근 모서리 상자 (중심, 크기, 모서리 반지름). 각 면을 나눈 뒤 모서리 쪽을 둥글게 민다.</summary>
        public MeshKit RoundedBox(int s, Vector3 c, Vector3 size, float radius, int div = 4)
        {
            Vector3 h = size * 0.5f;
            radius = Mathf.Min(radius, Mathf.Min(h.x, Mathf.Min(h.y, h.z)) * 0.999f);
            Vector3 inner = h - Vector3.one * radius;
            Vector3[] normals = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            int seg = radius > 1e-5f ? div * 2 + 2 : 1;
            foreach (var f in normals)
            {
                Vector3 ax1 = f.x != 0 ? Vector3.forward : Vector3.right;
                Vector3 ax2 = Vector3.Cross(f, ax1);
                if (f.x < 0 || f.y < 0 || f.z < 0) ax1 = -ax1;
                ax2 = Vector3.Cross(f, ax1);
                int start = pos.Count;
                for (int i = 0; i <= seg; i++)
                {
                    for (int j = 0; j <= seg; j++)
                    {
                        float a = (float)i / seg * 2 - 1, b = (float)j / seg * 2 - 1;
                        // 정육면체 위의 점
                        Vector3 cube = f + ax1 * a + ax2 * b;
                        Vector3 p = Vector3.Scale(cube, h);
                        // 안쪽 상자에 가장 가까운 점 → 그 방향으로 radius 만큼
                        Vector3 q = new Vector3(Mathf.Clamp(p.x, -inner.x, inner.x), Mathf.Clamp(p.y, -inner.y, inner.y), Mathf.Clamp(p.z, -inner.z, inner.z));
                        Vector3 d = p - q;
                        Vector3 nn = d.sqrMagnitude > 1e-10f ? d.normalized : f;
                        Vector3 pp = radius > 1e-5f ? q + nn * radius : p;
                        if (radius <= 1e-5f) nn = f;
                        V(c + pp, nn, new Vector2((Vector3.Dot(pp, ax1) / size.magnitude) + 0.5f, (Vector3.Dot(pp, ax2) / size.magnitude) + 0.5f));
                    }
                }
                int cols = seg + 1;
                for (int i = 0; i < seg; i++)
                    for (int j = 0; j < seg; j++)
                    {
                        int a = start + i * cols + j, b = a + 1, cc = a + cols + 1, d = a + cols;
                        QuadIdx(s, a, d, cc, b);
                    }
            }
            return this;
        }

        /// <summary>단순 상자 (면마다 UV 0~1 × tile).</summary>
        public MeshKit Box(int s, Vector3 c, Vector3 size, float tile = 1f)
        {
            Vector3 h = size * 0.5f;
            Vector3[] fn = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            foreach (var f in fn)
            {
                Vector3 ax1 = Mathf.Abs(f.y) > 0.5f ? Vector3.right : Vector3.up;
                Vector3 ax2 = Vector3.Cross(ax1, f);
                float e1 = Mathf.Abs(Vector3.Dot(ax1, size)), e2 = Mathf.Abs(Vector3.Dot(ax2, size));
                Vector3 o = c + Vector3.Scale(f, h);
                Vector3 a1 = ax1 * (e1 * 0.5f), a2 = ax2 * (e2 * 0.5f);
                int a = V(o - a1 - a2, f, new Vector2(0, 0));
                int b = V(o - a1 + a2, f, new Vector2(e2 * tile, 0));
                int cc = V(o + a1 + a2, f, new Vector2(e2 * tile, e1 * tile));
                int d = V(o + a1 - a2, f, new Vector2(0, e1 * tile));
                QuadIdx(s, a, b, cc, d);
            }
            return this;
        }

        /// <summary>도넛: Y 축 둘레 큰 반지름 R, 단면 반지름 r.</summary>
        public MeshKit Torus(int s, float R, float r, int segments = 32, int sides = 12, float arcDegrees = 360f)
        {
            int start = pos.Count;
            for (int i = 0; i <= segments; i++)
            {
                float a = arcDegrees * Mathf.Deg2Rad * i / segments;
                var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                for (int j = 0; j <= sides; j++)
                {
                    float b = Mathf.PI * 2 * j / sides;
                    var nn = dir * Mathf.Cos(b) + Vector3.up * Mathf.Sin(b);
                    V(dir * R + nn * r, nn, new Vector2((float)i / segments, (float)j / sides));
                }
            }
            int cols = sides + 1;
            for (int i = 0; i < segments; i++)
                for (int j = 0; j < sides; j++)
                {
                    int a = start + i * cols + j, b = a + 1, c = a + cols + 1, d = a + cols;
                    QuadIdx(s, a, b, c, d);
                }
            return this;
        }

        /// <summary>점들을 잇는 관 (반지름은 점마다). 끝을 막을 수 있다.</summary>
        public MeshKit Tube(int s, IList<Vector3> path, IList<float> radius, int sides = 10, bool caps = true)
        {
            int n = path.Count;
            if (n < 2) return this;
            Vector3 prevUp = Vector3.up;
            {
                Vector3 t0 = (path[1] - path[0]).normalized;
                if (Mathf.Abs(Vector3.Dot(t0, prevUp)) > 0.95f) prevUp = Vector3.right;
            }
            int start = pos.Count;
            float acc = 0;
            for (int i = 0; i < n; i++)
            {
                Vector3 t = (path[Mathf.Min(n - 1, i + 1)] - path[Mathf.Max(0, i - 1)]).normalized;
                Vector3 side = Vector3.Cross(t, prevUp).normalized;
                Vector3 up = Vector3.Cross(side, t).normalized;
                prevUp = up;
                if (i > 0) acc += Vector3.Distance(path[i], path[i - 1]);
                float r = radius[Mathf.Min(i, radius.Count - 1)];
                for (int j = 0; j <= sides; j++)
                {
                    float a = Mathf.PI * 2 * j / sides;
                    Vector3 nn = side * Mathf.Cos(a) + up * Mathf.Sin(a);
                    V(path[i] + nn * r, nn, new Vector2((float)j / sides, acc));
                }
            }
            int cols = sides + 1;
            for (int i = 0; i < n - 1; i++)
                for (int j = 0; j < sides; j++)
                {
                    int a = start + i * cols + j, b = a + 1, c = a + cols + 1, d = a + cols;
                    QuadIdx(s, a, d, c, b);
                }
            if (caps)
            {
                CapRing(s, start, sides, path[0], -(path[1] - path[0]).normalized, true);
                CapRing(s, start + (n - 1) * cols, sides, path[n - 1], (path[n - 1] - path[n - 2]).normalized, false);
            }
            return this;
        }

        void CapRing(int s, int ringStart, int sides, Vector3 center, Vector3 normal, bool flip)
        {
            // 링 꼭짓점을 복사해 평평한 법선으로 막는다
            var worldCenter = center;
            int c = V(worldCenter, normal, new Vector2(0.5f, 0.5f));
            int first = pos.Count;
            var inv = m.inverse;
            for (int j = 0; j <= sides; j++)
            {
                Vector3 local = inv.MultiplyPoint3x4(pos[ringStart + j]);
                V(local, normal, new Vector2(0.5f, 0.5f));
            }
            for (int j = 0; j < sides; j++)
            {
                if (flip) Tri(s, c, first + j, first + j + 1);
                else Tri(s, c, first + j + 1, first + j);
            }
        }

        /// <summary>2차 베지어 곡선 위의 점들.</summary>
        public static List<Vector3> Bezier(Vector3 a, Vector3 b, Vector3 c, int steps)
        {
            var l = new List<Vector3>();
            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps;
                l.Add((1 - t) * (1 - t) * a + 2 * (1 - t) * t * b + t * t * c);
            }
            return l;
        }

        /// <summary>3차 베지어.</summary>
        public static List<Vector3> Bezier(Vector3 a, Vector3 b, Vector3 c, Vector3 d, int steps)
        {
            var l = new List<Vector3>();
            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps, u = 1 - t;
                l.Add(u * u * u * a + 3 * u * u * t * b + 3 * u * t * t * c + t * t * t * d);
            }
            return l;
        }

        /// <summary>
        /// 각진 보석 (브릴리언트 컷 비슷): 윗면(table)·크라운·거들·파빌리온. 면마다 평평한 법선이라 반짝인다.
        /// 원점이 거들 높이 가운데, 위로 crown, 아래로 pavilion.
        /// </summary>
        public MeshKit Gem(int s, float radius, float crown, float pavilion, int facets = 8, float table = 0.55f)
        {
            var profile = new[]
            {
                new Vector2(0f, -pavilion),
                new Vector2(radius, 0f),
                new Vector2(radius, crown * 0.15f),
                new Vector2(radius * table, crown),
                new Vector2(0f, crown)
            };
            Lathe(s, profile, facets, false, false, 360f, true);
            return this;
        }

        /// <summary>두께 있는 평면 도형 (XY 평면의 다각형을 Z 로 depth 만큼). 볼록 다각형만.</summary>
        public MeshKit Extrude(int s, IList<Vector2> poly, float depth)
        {
            int n = poly.Count;
            float hz = depth * 0.5f;
            // 앞(-Z)·뒤(+Z)
            for (int side = 0; side < 2; side++)
            {
                float z = side == 0 ? -hz : hz;
                var nn = side == 0 ? Vector3.back : Vector3.forward;
                int start = pos.Count;
                for (int i = 0; i < n; i++) V(new Vector3(poly[i].x, poly[i].y, z), nn, poly[i]);
                for (int i = 1; i < n - 1; i++)
                {
                    if (side == 0) Tri(s, start, start + i + 1, start + i);
                    else Tri(s, start, start + i, start + i + 1);
                }
            }
            for (int i = 0; i < n; i++)
            {
                Vector2 a = poly[i], b = poly[(i + 1) % n];
                Vector2 d = (b - a).normalized;
                var nn = new Vector3(d.y, -d.x, 0);
                int p0 = V(new Vector3(a.x, a.y, -hz), nn, new Vector2(0, 0));
                int p1 = V(new Vector3(b.x, b.y, -hz), nn, new Vector2(1, 0));
                int p2 = V(new Vector3(b.x, b.y, hz), nn, new Vector2(1, 1));
                int p3 = V(new Vector3(a.x, a.y, hz), nn, new Vector2(0, 1));
                QuadIdx(s, p0, p1, p2, p3);
            }
            return this;
        }

        /// <summary>
        /// 휘어진 띠 (가면·리본): 가로 폭 w, 세로 h, 원통처럼 반지름 bendR 로 휨 (Z- 쪽이 앞).
        /// shape(u) 는 u(-1~1) 에서의 (아래 끝, 위 끝) 높이 배율.
        /// </summary>
        public MeshKit CurvedPanel(int s, float w, float h, float bendR, System.Func<float, Vector2> shape, int cols = 24, int rows = 6, float thickness = 0.004f)
        {
            for (int layer = 0; layer < 2; layer++)
            {
                float off = layer == 0 ? 0 : -thickness;
                int start = pos.Count;
                for (int i = 0; i <= cols; i++)
                {
                    float u = (float)i / cols * 2 - 1;
                    Vector2 range = shape(u);
                    float ang = u * w * 0.5f / bendR;
                    for (int j = 0; j <= rows; j++)
                    {
                        float t = (float)j / rows;
                        float y = Mathf.Lerp(range.x, range.y, t) * h * 0.5f;
                        float rr = bendR + off;
                        var p = new Vector3(Mathf.Sin(ang) * rr, y, -Mathf.Cos(ang) * rr + bendR);
                        var nn = new Vector3(Mathf.Sin(ang), 0, -Mathf.Cos(ang));
                        if (layer == 1) nn = -nn;
                        V(p, nn, new Vector2((u + 1) * 0.5f, t));
                    }
                }
                int cc = rows + 1;
                for (int i = 0; i < cols; i++)
                    for (int j = 0; j < rows; j++)
                    {
                        int a = start + i * cc + j, b = a + 1, c = a + cc + 1, d = a + cc;
                        if (layer == 0) QuadIdx(s, a, b, c, d);
                        else QuadIdx(s, a, d, c, b);
                    }
            }
            return this;
        }

        /// <summary>평면 사각형 (XY, -Z 를 봄).</summary>
        public MeshKit Quad(int s, Vector2 size, Vector2 uvMin, Vector2 uvMax)
        {
            var nn = Vector3.back;
            Vector2 h = size * 0.5f;
            int a = V(new Vector3(-h.x, -h.y, 0), nn, uvMin);
            int b = V(new Vector3(-h.x, h.y, 0), nn, new Vector2(uvMin.x, uvMax.y));
            int c = V(new Vector3(h.x, h.y, 0), nn, uvMax);
            int d = V(new Vector3(h.x, -h.y, 0), nn, new Vector2(uvMax.x, uvMin.y));
            QuadIdx(s, a, b, c, d);
            return this;
        }

        public Mesh Build(string name, int minSubmeshes = 1)
        {
            var mesh = new Mesh { name = name };
            if (pos.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(pos);
            mesh.SetNormals(nrm);
            mesh.SetUVs(0, uv);
            mesh.SetColors(col);
            int count = Mathf.Max(minSubmeshes, subs.Count);
            mesh.subMeshCount = count;
            for (int i = 0; i < count; i++) mesh.SetTriangles(i < subs.Count ? subs[i] : new List<int>(), i, false);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }

        /// <summary>쓰인 하위 메시 번호 목록 (빈 칸 제외).</summary>
        public int SubmeshCount { get { return subs.Count; } }
    }
}
