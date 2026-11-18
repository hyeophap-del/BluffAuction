using BluffAuction.Art;
using UnityEngine;

namespace BluffAuction.Characters
{
    public enum HandPose { Relaxed, Grip, Fist, Open, Point, Middle, ThumbsUp, Peace }

    /// <summary>
    /// 장갑 낀 손 (손바닥 + 손가락 넷 두 마디 + 엄지 두 마디). 손가락은 손목에서 -Y 로 뻗고 손바닥은 -Z 를 본다.
    /// 자세(HandPose)를 고르면 마디를 부드럽게 굽힌다.
    /// </summary>
    public sealed class GloveHand : MonoBehaviour
    {
        // 검지·중지·약지·새끼·엄지
        readonly Transform[] a = new Transform[5], b = new Transform[5];
        readonly float[] curl = new float[5], curlTo = new float[5];
        float spread, spreadTo, thumbOut, thumbOutTo;
        float side = 1f;
        public HandPose Pose { get; private set; }
        /// <summary>패들·의사봉을 쥐는 자리 (손바닥 가운데).</summary>
        public Transform Grip;

        static readonly float[] BaseX = { 0.024f, 0.008f, -0.009f, -0.025f };
        static readonly float[] LenA = { 0.03f, 0.033f, 0.03f, 0.024f, 0.026f };
        static readonly float[] LenB = { 0.026f, 0.028f, 0.025f, 0.02f, 0.022f };

        /// <summary>손을 만든다. s 는 몸의 왼쪽(-1)/오른쪽(+1).</summary>
        public static GloveHand Build(Transform wrist, Material glove, float s)
        {
            var hand = wrist.gameObject.AddComponent<GloveHand>();
            hand.side = s;
            var pb = new ModelBuilder();
            int g = pb.M("glove", glove);
            // 손바닥: 손등 쪽이 조금 볼록한 납작한 타원 + 손목 띠
            pb.Kit.Ellipsoid(g, new Vector3(0, -0.036f, 0.002f), new Vector3(0.036f, 0.04f, 0.018f), 16, 10);
            pb.Kit.Ellipsoid(g, new Vector3(-s * 0.012f, -0.05f, -0.004f), new Vector3(0.026f, 0.026f, 0.016f), 12, 8);
            pb.Kit.Cylinder(g, 0.031f, -0.012f, 0.016f, 14, false);
            pb.Build("Palm", wrist);

            var grip = new GameObject("Grip").transform;
            grip.SetParent(wrist, false);
            grip.localPosition = new Vector3(0, -0.045f, -0.016f);
            hand.Grip = grip;

            for (int f = 0; f < 5; f++)
            {
                bool thumb = f == 4;
                var root = new GameObject(thumb ? "ThumbA" : "Finger" + f + "A").transform;
                root.SetParent(wrist, false);
                root.localPosition = thumb ? new Vector3(-s * 0.03f, -0.022f, -0.006f) : new Vector3(-s * BaseX[f], -0.07f, 0);
                float r = thumb ? 0.0108f : (f == 3 ? 0.0082f : 0.0094f);
                var mb = new ModelBuilder();
                mb.Kit.Capsule(mb.M("glove", glove), Vector3.zero, new Vector3(0, -LenA[f], 0), r, 10);
                mb.Build("Seg", root);
                var tip = new GameObject(thumb ? "ThumbB" : "Finger" + f + "B").transform;
                tip.SetParent(root, false);
                tip.localPosition = new Vector3(0, -LenA[f], 0);
                var mb2 = new ModelBuilder();
                mb2.Kit.Capsule(mb2.M("glove", glove), Vector3.zero, new Vector3(0, -LenB[f], 0), r * 0.94f, 10);
                mb2.Build("Seg", tip);
                hand.a[f] = root;
                hand.b[f] = tip;
            }
            hand.SetPose(HandPose.Relaxed, true);
            return hand;
        }

        public void SetPose(HandPose p, bool instant = false)
        {
            Pose = p;
            float sp = 0.15f, tout = 0.35f;
            float[] c;
            switch (p)
            {
                case HandPose.Grip: c = new[] { 0.82f, 0.86f, 0.9f, 0.92f, 0.6f }; break;
                case HandPose.Fist: c = new[] { 1f, 1f, 1f, 1f, 0.85f }; tout = 0.25f; break;
                case HandPose.Open: c = new[] { 0f, 0f, 0f, 0f, 0f }; sp = 1f; tout = 0.7f; break;
                case HandPose.Point: c = new[] { 0f, 1f, 1f, 1f, 0.8f }; tout = 0.2f; break;
                case HandPose.Middle: c = new[] { 1f, 0f, 1f, 1f, 0.9f }; tout = 0.2f; sp = 0f; break;
                case HandPose.ThumbsUp: c = new[] { 1f, 1f, 1f, 1f, 0f }; tout = 1.1f; break;
                case HandPose.Peace: c = new[] { 0f, 0f, 1f, 1f, 0.9f }; sp = 2.6f; tout = 0.2f; break;
                default: c = new[] { 0.25f, 0.3f, 0.35f, 0.42f, 0.25f }; tout = 0.15f; break;
            }
            for (int i = 0; i < 5; i++) curlTo[i] = c[i];
            spreadTo = sp;
            thumbOutTo = tout;
            if (instant)
            {
                for (int i = 0; i < 5; i++) curl[i] = curlTo[i];
                spread = spreadTo;
                thumbOut = thumbOutTo;
                ApplyJoints();
            }
        }

        void LateUpdate()
        {
            float k = 1f - Mathf.Exp(-Time.deltaTime * 16f);
            for (int i = 0; i < 5; i++) curl[i] = Mathf.Lerp(curl[i], curlTo[i], k);
            spread = Mathf.Lerp(spread, spreadTo, k);
            thumbOut = Mathf.Lerp(thumbOut, thumbOutTo, k);
            ApplyJoints();
        }

        void ApplyJoints()
        {
            for (int f = 0; f < 4; f++)
            {
                // 굽힘: +X 회전이면 손끝이 손바닥(-Z) 쪽으로. 벌림: 검지는 엄지 쪽, 새끼는 바깥쪽
                float fan = (1.5f - f) * 11f * spread * side;
                if (a[f] != null) a[f].localRotation = Quaternion.Euler(curl[f] * 88f, 0, fan);
                if (b[f] != null) b[f].localRotation = Quaternion.Euler(curl[f] * 96f, 0, 0);
            }
            // 엄지: 바깥으로 벌린 각도(Z) + 손바닥을 가로질러 굽힘
            float abduct = Mathf.Lerp(20f, 95f, Mathf.Clamp01(thumbOut));
            if (a[4] != null) a[4].localRotation = Quaternion.Euler(18f + curl[4] * 40f, -side * curl[4] * 30f, -side * abduct);
            if (b[4] != null) b[4].localRotation = Quaternion.Euler(curl[4] * 70f, 0, 0);
        }
    }
}
