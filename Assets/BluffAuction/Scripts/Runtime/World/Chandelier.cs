using System.Collections.Generic;
using BluffAuction.Art;
using UnityEngine;
using static BluffAuction.Art.ModelBuilder;

namespace BluffAuction.World
{
    /// <summary>
    /// 샹들리에 (VRChat판 메시를 옮김): 선반에 돌린 듯한 기둥, S자 팔 두 단(8 + 6)과 초받침·초·불꽃,
    /// 팔 끝 사이에 늘어진 크리스털 구슬 줄과 각진 물방울. 재질별 하위 메시 4개.
    /// </summary>
    public static class Chandelier
    {
        public static GameObject Build(Transform parent, Vector3 pos, float scale, float ropeLength, out List<Vector3> candleTips)
        {
            var b = new ModelBuilder();
            var k = b.Kit;
            int brass = b.M("Brass"), wax = b.M("CandleWax"), flame = b.M("Flame"), crystal = b.M("Diamond");
            candleTips = new List<Vector3>();

            // 천장 받침과 매다는 막대
            float top = ropeLength / scale;
            k.Lathe(brass, Up(P(0.17f, top), P(0.16f, top - 0.02f), P(0.12f, top - 0.055f), P(0.06f, top - 0.085f), P(0.03f, top - 0.11f), P(0.018f, top - 0.13f)), 28);
            float rodTop = top - 0.12f, rodBottom = 0.6f;
            k.Tube(brass, new List<Vector3> { new Vector3(0, rodBottom, 0), new Vector3(0, rodTop, 0) }, new[] { 0.011f }, 10, false);
            for (float y = rodTop - 0.14f; y > rodBottom + 0.08f; y -= 0.16f)
                k.Lathe(brass, Up(P(0, y + 0.03f), P(0.012f, y + 0.026f), P(0.022f, y + 0.012f), P(0.024f, y), P(0.022f, y - 0.012f), P(0.012f, y - 0.026f), P(0, y - 0.03f)), 20);

            // 가운데 기둥
            k.Lathe(brass, Up(
                P(0, 0.64f), P(0.02f, 0.62f), P(0.032f, 0.58f), P(0.022f, 0.54f), P(0.05f, 0.49f), P(0.066f, 0.44f), P(0.05f, 0.39f),
                P(0.026f, 0.35f), P(0.03f, 0.32f), P(0.095f, 0.305f), P(0.1f, 0.285f), P(0.05f, 0.27f), P(0.03f, 0.24f), P(0.035f, 0.2f),
                P(0.07f, 0.15f), P(0.115f, 0.085f), P(0.138f, 0.03f), P(0.142f, 0f), P(0.136f, -0.03f), P(0.162f, -0.075f), P(0.172f, -0.125f),
                P(0.145f, -0.195f), P(0.085f, -0.265f), P(0.042f, -0.315f), P(0.03f, -0.355f), P(0.046f, -0.395f), P(0.032f, -0.44f), P(0, -0.5f)), 32);

            var lowerTips = new List<Vector3>();
            Arms(k, brass, wax, flame, 8, 0f, P(0.13f, 0f), P(0.34f, -0.24f), P(0.64f, -0.17f), P(0.64f, 0.03f), 0.015f, 1f, lowerTips, candleTips);
            Arms(k, brass, wax, flame, 6, 30f, P(0.085f, 0.29f), P(0.22f, 0.17f), P(0.36f, 0.24f), P(0.36f, 0.35f), 0.011f, 0.8f, null, candleTips);

            for (int i = 0; i < lowerTips.Count; i++)
            {
                Vector3 a = lowerTips[i] + Vector3.down * 0.03f;
                Vector3 c = lowerTips[(i + 1) % lowerTips.Count] + Vector3.down * 0.03f;
                const float sag = 0.15f;
                int beads = Mathf.Max(6, Mathf.RoundToInt(Vector3.Distance(a, c) / 0.026f));
                for (int j = 1; j < beads; j++)
                {
                    float u = j / (float)beads;
                    Bead(k, crystal, Vector3.Lerp(a, c, u) + Vector3.down * (sag * 4f * u * (1f - u)), 0.0105f);
                }
                Drop(k, crystal, Vector3.Lerp(a, c, 0.5f) + Vector3.down * (sag + 0.012f), 0.022f, 0.075f);
                Drop(k, crystal, lowerTips[i] + Vector3.down * 0.05f, 0.02f, 0.07f);
            }
            for (int i = 0; i < 12; i++)
            {
                float ang = (i + 0.5f) * Mathf.PI * 2f / 12f;
                Drop(k, crystal, new Vector3(Mathf.Cos(ang) * 0.15f, -0.17f, Mathf.Sin(ang) * 0.15f), 0.018f, 0.065f);
            }
            Drop(k, crystal, new Vector3(0, -0.52f, 0), 0.04f, 0.14f);

            var go = b.Build("Chandelier", parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = Vector3.one * scale;
            for (int i = 0; i < candleTips.Count; i++) candleTips[i] = go.transform.TransformPoint(candleTips[i]);
            return go;
        }

        static void Arms(MeshKit k, int brass, int wax, int flame, int count, float yawOffset, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3,
            float radius, float cup, List<Vector3> tips, List<Vector3> flames)
        {
            for (int i = 0; i < count; i++)
            {
                float ang = (yawOffset + i * 360f / count) * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                var path = new List<Vector3>();
                for (int s = 0; s <= 26; s++)
                {
                    Vector2 q = Bez(p0, p1, p2, p3, s / 26f);
                    path.Add(dir * q.x + Vector3.up * q.y);
                }
                k.Tube(brass, path, new[] { radius }, 10, true);
                Vector2 low = Bez(p0, p1, p2, p3, 0.45f);
                k.Push(dir * low.x + Vector3.up * (low.y - radius * 0.6f), Vector3.zero, cup);
                k.Lathe(brass, Up(P(0, 0.012f), P(0.01f, 0.006f), P(0.012f, -0.004f), P(0.006f, -0.018f), P(0, -0.028f)), 12);
                k.Pop();
                Vector3 tip = dir * p3.x + Vector3.up * p3.y;
                k.Push(tip, Vector3.zero, cup);
                k.Lathe(brass, Up(P(0.022f, 0.064f), P(0.028f, 0.062f), P(0.025f, 0.036f), P(0.03f, 0.026f), P(0.05f, 0.02f), P(0.053f, 0.013f),
                    P(0.036f, 0.002f), P(0.016f, -0.008f), P(0, -0.016f)), 20);
                float ch = 0.15f, cr = 0.021f;
                k.Lathe(wax, new[] { P(cr, 0.04f), P(cr, 0.04f + ch - 0.005f), P(cr * 0.8f, 0.04f + ch), P(0, 0.04f + ch) }, 16);
                float fy = 0.04f + ch + 0.006f;
                k.Lathe(flame, new[] { P(0, fy), P(0.011f, fy + 0.007f), P(0.013f, fy + 0.02f), P(0.007f, fy + 0.04f), P(0, fy + 0.055f) }, 10);
                k.Pop();
                if (tips != null) tips.Add(tip);
                if (flames != null) flames.Add(tip + Vector3.up * (fy + 0.025f) * cup);
            }
        }

        static Vector2 Bez(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float u)
        {
            float w = 1f - u;
            return w * w * w * a + 3f * w * w * u * b + 3f * w * u * u * c + u * u * u * d;
        }

        static void Bead(MeshKit k, int s, Vector3 p, float r)
        {
            k.Push(p);
            k.Lathe(s, new[] { P(0, -r * 1.15f), P(r, 0), P(0, r * 1.15f) }, 8, false, false, 360f, true);
            k.Pop();
        }

        static void Drop(MeshKit k, int s, Vector3 top, float r, float len)
        {
            k.Push(top, new Vector3(0, 22.5f, 0));
            k.Lathe(s, Up(P(0, 0), P(r * 0.55f, -len * 0.12f), P(r, -len * 0.32f), P(r * 0.8f, -len * 0.55f), P(0, -len)), 8, false, false, 360f, true);
            k.Pop();
            Bead(k, s, top + Vector3.up * 0.006f, 0.006f);
        }
    }
}
