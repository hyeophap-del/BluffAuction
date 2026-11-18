using System.Collections.Generic;
using UnityEngine;

namespace BluffAuction.Characters
{
    public enum Gesture
    {
        None,
        Bid,        // 패들 들기
        BigBid,     // 패들을 높이 흔들며
        Cheer,      // 두 팔 번쩍 (낙찰·1등)
        Slump,      // 축 처짐 (놓침·손해)
        Think,      // 턱 괴기
        Shock,      // 뒤로 젖히며 놀람
        Laugh,      // 들썩이며 웃음
        Point,      // 가리키기 (왼손 검지)
        Gavel,      // 의사봉 내리치기 (경매사)
        Shrug,      // 어깨 으쓱
        Talk,       // 말할 때 고개 끄덕
        Clap,       // 박수
        Throw,      // 오른팔로 던지기
        Swing,      // 왼손에 든 근접 무기로 내려치기
        // ── 감정 표현 (R 키)
        Wave,       // 손 흔들기
        ThumbsUp,   // 엄지 척
        MiddleFinger, // 가운데 손가락
        Facepalm,   // 얼굴 감싸기
        Angry,      // 두 주먹 흔들며 화내기
        Peace,      // 브이
        Mock        // 손가락질하며 비웃기
    }

    /// <summary>
    /// 손님 한 명의 움직임 (관절을 코드로 돌린다): 숨쉬기, 시선, 깜빡임, 눈썹·입 표정, 손가락, 몸짓.
    /// 몸짓은 정해진 시간 동안 자세를 덮어쓰고, 끝나면 기본 자세로 부드럽게 돌아온다.
    /// </summary>
    public sealed class Guest : MonoBehaviour
    {
        public CharacterLook Look;
        public bool Standing;
        /// <summary>내 좌석 (1인칭) 의 쉬는 자세.</summary>
        public bool LocalPose;
        public Transform Torso, Head, ShoulderL, ElbowL, HandL, ShoulderR, ElbowR, HandR, Paddle, EyeL, EyeR, BrowL, BrowR, Mouth;
        public GloveHand GloveL, GloveR;
        /// <summary>몸짓이 끝났을 때 (경매사 의사봉 소리 맞추기 등).</summary>
        public System.Action<Gesture> OnImpact;
        /// <summary>마이크 음성 크기 (0~1) — 입을 벌린다.</summary>
        public float VoiceLevel;
        /// <summary>1인칭 내 몸: 팔은 그림자만 (감정 표현 중에는 왼팔을 보인다).</summary>
        public bool ArmsHidden;

        Vector3 torsoBase;
        Quaternion headBase = Quaternion.identity;
        Vector3 lookTarget;
        bool hasLook;
        Quaternion headCur = Quaternion.identity;
        float phase;
        float nextBlink;
        float blinkT = -1;
        Gesture gesture;
        float gT;
        float gLen;
        bool impactSent;
        Vector3 pointTarget;
        bool hasPointTarget;
        float idleShift;
        float nextIdle;
        float excitement;
        float mouthOpen;
        Vector3 browBaseL, browBaseR;
        Quaternion browRotL, browRotR;
        List<MeshRenderer> leftArm;
        bool leftArmShown;

        struct Pose
        {
            public Quaternion Torso, SL, EL, SR, ER, Paddle, HeadAdd;
            public float Lift;
        }

        Pose cur;

        public void Init()
        {
            torsoBase = Torso.localPosition;
            phase = Random.value * 10f;
            nextBlink = Time.time + Random.Range(1f, 4f);
            if (BrowL != null) { browBaseL = BrowL.localPosition; browRotL = BrowL.localRotation; }
            if (BrowR != null) { browBaseR = BrowR.localPosition; browRotR = BrowR.localRotation; }
            cur = RestPose();
            Apply(cur, 1f);
        }

        public void LookAt(Vector3 world)
        {
            lookTarget = world;
            hasLook = true;
        }

        public void ClearLook() { hasLook = false; }

        /// <summary>잠시 뒤 할 일 (이 손님이 사라지면 함께 취소된다).</summary>
        public void StartCoroutineSafe(float delay, System.Action action)
        {
            if (isActiveAndEnabled) StartCoroutine(Later(delay, action));
        }

        System.Collections.IEnumerator Later(float delay, System.Action action)
        {
            yield return new WaitForSecondsRealtime(delay);
            if (action != null) action();
        }

        public bool Busy { get { return gesture != Gesture.None && gT < gLen; } }
        public Gesture Current { get { return Busy ? gesture : Gesture.None; } }

        public static bool IsEmote(Gesture g) { return g >= Gesture.Wave; }

        public void Play(Gesture g, float length = -1f)
        {
            gesture = g;
            gT = 0;
            impactSent = false;
            gLen = length > 0 ? length : DefaultLength(g);
            if (g != Gesture.Point && g != Gesture.Mock) hasPointTarget = false;
            if (g == Gesture.Cheer || g == Gesture.Shock || g == Gesture.BigBid || g == Gesture.Angry) excitement = 1f;
        }

        float knock;

        /// <summary>머리를 맞아 휘청 (근접 무기).</summary>
        public void Knock() { knock = 1f; }

        /// <summary>대상 쪽으로 내려치기.</summary>
        public void SwingAt(Vector3 world)
        {
            Play(Gesture.Swing);
            pointTarget = world;
            hasPointTarget = true;
            LookAt(world);
        }

        public void PointAt(Vector3 world, float length = 1.6f)
        {
            pointTarget = world;
            Play(Gesture.Point, length);
            hasPointTarget = true;
        }

        /// <summary>감정 표현 (대상이 있으면 그쪽을 향해 — 가운데 손가락·비웃기·엄지).</summary>
        public void Emote(Gesture g, Vector3? toward)
        {
            Play(g);
            if (toward.HasValue)
            {
                pointTarget = toward.Value;
                hasPointTarget = true;
                LookAt(toward.Value);
            }
        }

        static float DefaultLength(Gesture g)
        {
            switch (g)
            {
                case Gesture.Bid: return 1.3f;
                case Gesture.BigBid: return 1.8f;
                case Gesture.Cheer: return 2.4f;
                case Gesture.Slump: return 2.4f;
                case Gesture.Think: return 3.2f;
                case Gesture.Shock: return 1.4f;
                case Gesture.Laugh: return 1.8f;
                case Gesture.Gavel: return 1.1f;
                case Gesture.Shrug: return 1.3f;
                case Gesture.Talk: return 1.4f;
                case Gesture.Clap: return 2.2f;
                case Gesture.Throw: return 0.9f;
                case Gesture.Swing: return 1.0f;
                case Gesture.Wave: return 2.0f;
                case Gesture.Angry: return 2.0f;
                case Gesture.Mock: return 2.2f;
                default: return IsEmote(g) ? 2.2f : 1.5f;
            }
        }

        // ───────── 자세 ─────────

        static Quaternion E(float x, float y, float z) { return Quaternion.Euler(x, y, z); }

        Pose RestPose()
        {
            var p = new Pose { Torso = Quaternion.identity, HeadAdd = Quaternion.identity, Paddle = E(180, 0, 0) };
            if (Standing)
            {
                // 연설대에 두 손을 얹은 자세
                p.SL = E(-30, 10, -6);
                p.EL = E(-58, 0, 0);
                p.SR = E(-30, -10, 6);
                p.ER = E(-58, 0, 0);
            }
            else
            {
                // 탁자에 팔을 얹은 자세 (팔꿈치를 굽혀 손이 탁자 가장자리에)
                p.SL = E(-46, 16, -4);
                p.EL = E(-40, 0, 0);
                p.SR = E(-46, -16, 4);
                p.ER = E(-40, 0, 0);
            }
            return p;
        }

        /// <summary>대상 쪽으로 팔을 뻗는 어깨 회전 (몸통 기준).</summary>
        Quaternion AimArm(Transform shoulder, Vector3 target, float lift)
        {
            Vector3 local = Torso.InverseTransformPoint(target) - shoulder.localPosition;
            float yaw = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            float pitch = Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg;
            return Quaternion.Euler(0, Mathf.Clamp(yaw, -70f, 70f), 0) * E(-90 - pitch - lift, 0, 0);
        }

        Pose GesturePose(Pose rest, float t, float u)
        {
            var p = rest;
            float inOut = Mathf.Clamp01(u / 0.18f) * Mathf.Clamp01((1f - u) / 0.25f);
            inOut = inOut * inOut * (3 - 2 * inOut);
            float s = Mathf.Sin(t * 14f);
            Vector3 fwd = Torso.position + Torso.forward * 2f + Vector3.up * 0.2f;
            Vector3 aim = hasPointTarget ? pointTarget : fwd;
            switch (gesture)
            {
                case Gesture.Bid:
                case Gesture.BigBid:
                {
                    float wave = gesture == Gesture.BigBid ? Mathf.Sin(t * 11f) * 12f : Mathf.Sin(t * 6f) * 3f;
                    p.SR = Quaternion.Slerp(rest.SR, E(-168, -8, 10 + wave), inOut);
                    p.ER = Quaternion.Slerp(rest.ER, E(-12, 0, 0), inOut);
                    p.Paddle = Quaternion.Slerp(rest.Paddle, E(180, 0, 0), inOut);
                    p.Torso = Quaternion.Slerp(Quaternion.identity, E(-4, -6, 2), inOut);
                    p.Lift = 0.01f * inOut;
                    break;
                }
                case Gesture.Cheer:
                {
                    float bounce = Mathf.Abs(Mathf.Sin(t * 9f));
                    p.SL = Quaternion.Slerp(rest.SL, E(-165, 0, -22 - bounce * 8), inOut);
                    p.SR = Quaternion.Slerp(rest.SR, E(-165, 0, 22 + bounce * 8), inOut);
                    p.EL = Quaternion.Slerp(rest.EL, E(-18, 0, 0), inOut);
                    p.ER = Quaternion.Slerp(rest.ER, E(-18, 0, 0), inOut);
                    p.Lift = bounce * 0.035f * inOut;
                    p.HeadAdd = Quaternion.Slerp(Quaternion.identity, E(-14, 0, 0), inOut);
                    break;
                }
                case Gesture.Slump:
                    p.Torso = Quaternion.Slerp(Quaternion.identity, E(14, 0, 0), inOut);
                    p.HeadAdd = Quaternion.Slerp(Quaternion.identity, E(24, 0, Mathf.Sin(t * 2f) * 4f), inOut);
                    p.SL = Quaternion.Slerp(rest.SL, E(-38, 10, -2), inOut);
                    p.SR = Quaternion.Slerp(rest.SR, E(-38, -10, 2), inOut);
                    p.Lift = -0.02f * inOut;
                    break;
                case Gesture.Think:
                    p.SL = Quaternion.Slerp(rest.SL, E(-112, 28, 4), inOut);
                    p.EL = Quaternion.Slerp(rest.EL, E(-118, 0, 0), inOut);
                    p.HeadAdd = Quaternion.Slerp(Quaternion.identity, E(6, -6, 8 + Mathf.Sin(t * 1.3f) * 3), inOut);
                    p.Torso = Quaternion.Slerp(Quaternion.identity, E(4, 4, 0), inOut);
                    break;
                case Gesture.Shock:
                    p.Torso = Quaternion.Slerp(Quaternion.identity, E(-12, 0, 0), inOut);
                    p.HeadAdd = Quaternion.Slerp(Quaternion.identity, E(-10, 0, 0), inOut);
                    p.SL = Quaternion.Slerp(rest.SL, E(-80, 30, -30), inOut);
                    p.SR = Quaternion.Slerp(rest.SR, E(-80, -30, 30), inOut);
                    p.EL = Quaternion.Slerp(rest.EL, E(-80, 0, 0), inOut);
                    p.ER = Quaternion.Slerp(rest.ER, E(-80, 0, 0), inOut);
                    break;
                case Gesture.Laugh:
                    p.Torso = Quaternion.Slerp(Quaternion.identity, E(-8 + s * 3, 0, 0), inOut);
                    p.HeadAdd = Quaternion.Slerp(Quaternion.identity, E(-18 + s * 5, 0, 0), inOut);
                    p.Lift = Mathf.Abs(s) * 0.012f * inOut;
                    p.SL = Quaternion.Slerp(rest.SL, E(-50, 30, -8), inOut);
                    p.EL = Quaternion.Slerp(rest.EL, E(-70, 0, 0), inOut);
                    break;
                case Gesture.Shrug:
                    p.SL = Quaternion.Slerp(rest.SL, E(-40, 40, -40), inOut);
                    p.SR = Quaternion.Slerp(rest.SR, E(-40, -40, 40), inOut);
                    p.EL = Quaternion.Slerp(rest.EL, E(-85, 0, 0), inOut);
                    p.ER = Quaternion.Slerp(rest.ER, E(-85, 0, 0), inOut);
                    p.HeadAdd = Quaternion.Slerp(Quaternion.identity, E(0, 0, 12), inOut);
                    p.Lift = 0.015f * inOut;
                    break;
                case Gesture.Talk:
                    p.HeadAdd = Quaternion.Slerp(Quaternion.identity, E(Mathf.Sin(t * 9f) * 5f, Mathf.Sin(t * 3f) * 6f, 0), inOut);
                    p.SL = Quaternion.Slerp(rest.SL, E(-85, 25 + Mathf.Sin(t * 5f) * 10, -10), inOut * 0.8f);
                    p.EL = Quaternion.Slerp(rest.EL, E(-70, 0, 0), inOut * 0.8f);
                    break;
                case Gesture.Clap:
                {
                    float c = Mathf.Abs(Mathf.Sin(t * 12f));
                    p.SL = Quaternion.Slerp(rest.SL, E(-75, 32 - c * 10, -6), inOut);
                    p.SR = Quaternion.Slerp(rest.SR, E(-75, -32 + c * 10, 6), inOut);
                    p.EL = Quaternion.Slerp(rest.EL, E(-55, 0, 0), inOut);
                    p.ER = Quaternion.Slerp(rest.ER, E(-55, 0, 0), inOut);
                    break;
                }
                case Gesture.Point:
                    // 왼팔을 대상 쪽으로 쭉 뻗는다 (오른손은 패들·의사봉을 쥐고 있다)
                    p.SL = Quaternion.Slerp(rest.SL, AimArm(ShoulderL, aim, 0f), inOut);
                    p.EL = Quaternion.Slerp(rest.EL, E(-8, 0, 0), inOut);
                    p.Torso = Quaternion.Slerp(Quaternion.identity, E(4, 0, 0), inOut);
                    break;
                case Gesture.Gavel:
                {
                    // 들어 올렸다가 (0~0.55) 내리친다 (0.55~0.65), 잠깐 멈춤
                    float up = u < 0.55f ? Mathf.SmoothStep(0, 1, u / 0.55f) : (u < 0.65f ? 1f - (u - 0.55f) / 0.1f : 0f);
                    p.SR = Quaternion.Slerp(E(-55, -12, 6), E(-150, -12, 10), up);
                    p.ER = Quaternion.Slerp(E(-45, 0, 0), E(-70, 0, 0), up);
                    p.Paddle = E(-90, 0, 0);
                    p.Torso = E(up * -4f + (u > 0.55f && u < 0.75f ? 5f : 0f), 0, 0);
                    if (!impactSent && u >= 0.62f)
                    {
                        impactSent = true;
                        if (OnImpact != null) OnImpact(Gesture.Gavel);
                    }
                    break;
                }
                case Gesture.Throw:
                {
                    // 뒤로 젖혔다가 (0~0.32) 앞으로 휙
                    float wind = u < 0.32f ? Mathf.SmoothStep(0, 1, u / 0.32f) : 1f - Mathf.SmoothStep(0, 1, (u - 0.32f) / 0.2f);
                    float follow = u > 0.32f ? Mathf.Clamp01((u - 0.32f) / 0.2f) * Mathf.Clamp01((1f - u) / 0.35f) : 0f;
                    p.SR = Quaternion.Slerp(Quaternion.Slerp(rest.SR, E(-70, -20, 12), follow), E(-172, -20, 30), wind);
                    p.ER = Quaternion.Slerp(rest.ER, E(-60, 0, 0), Mathf.Max(wind, follow));
                    p.Torso = Quaternion.Slerp(Quaternion.identity, E(-6 * wind + 8 * follow, -10 * wind + 6 * follow, 0), 1f);
                    break;
                }
                case Gesture.Swing:
                {
                    // 높이 치켜들었다가 (0~0.3) 대상 쪽으로 내려친다 (0.3~0.42), 잠깐 멈춤
                    float raise = u < 0.3f ? Mathf.SmoothStep(0, 1, u / 0.3f) : 1f - Mathf.SmoothStep(0, 1, (u - 0.3f) / 0.12f);
                    float chop = u > 0.3f ? Mathf.Clamp01((u - 0.3f) / 0.12f) * Mathf.Clamp01((1f - u) / 0.3f) : 0f;
                    var down = AimArm(ShoulderL, aim, -22f);
                    p.SL = Quaternion.Slerp(Quaternion.Slerp(rest.SL, down, chop), E(-172, 18, -24), raise);
                    p.EL = Quaternion.Slerp(rest.EL, E(-70, 0, 0), raise) * Quaternion.Slerp(Quaternion.identity, E(30, 0, 0), chop);
                    p.Torso = Quaternion.Slerp(Quaternion.identity, E(-5 * raise + 10 * chop, 8 * raise - 6 * chop, 0), 1f);
                    break;
                }
                // ── 감정 표현: 손가락을 쓰는 것은 왼손 (오른손은 패들)
                case Gesture.Wave:
                    p.SL = Quaternion.Slerp(rest.SL, E(-150, 10, -20 + Mathf.Sin(t * 10f) * 16f), inOut);
                    p.EL = Quaternion.Slerp(rest.EL, E(-30, 0, 0), inOut);
                    p.HeadAdd = Quaternion.Slerp(Quaternion.identity, E(0, 0, -8), inOut);
                    break;
                case Gesture.ThumbsUp:
                    // 팔을 대상 쪽으로 뻗고 팔뚝을 90° 비틀어 엄지가 위로 (비틀고 나서 굽힌다: 굽힘 * 비틀기)
                    p.SL = Quaternion.Slerp(rest.SL, AimArm(ShoulderL, aim, -15f), inOut);
                    p.EL = Quaternion.Slerp(rest.EL, E(-40, 0, 0) * E(0, -90, 0), inOut);
                    p.HeadAdd = Quaternion.Slerp(Quaternion.identity, E(-6, 0, 6), inOut);
                    break;
                case Gesture.MiddleFinger:
                case Gesture.Peace:
                {
                    // 위팔은 대상 쪽, 팔뚝은 세운다. 가운데 손가락은 손등이 대상을 보게 180° 비튼다 (브이는 손바닥이 보이게)
                    float jab = gesture == Gesture.MiddleFinger ? Mathf.Max(0, Mathf.Sin(t * 7f)) * 6f : 0f;
                    p.SL = Quaternion.Slerp(rest.SL, AimArm(ShoulderL, aim, jab), inOut);
                    var bend = E(-82, 0, 0);
                    p.EL = Quaternion.Slerp(rest.EL, gesture == Gesture.MiddleFinger ? bend * E(0, 180, 0) : bend, inOut);
                    p.Torso = Quaternion.Slerp(Quaternion.identity, E(5, 8, 0), inOut);
                    p.HeadAdd = Quaternion.Slerp(Quaternion.identity, E(-6, 0, gesture == Gesture.Peace ? 10 : -6), inOut);
                    break;
                }
                case Gesture.Facepalm:
                    p.SL = Quaternion.Slerp(rest.SL, E(-118, 34, 6), inOut);
                    p.EL = Quaternion.Slerp(rest.EL, E(-128, 0, 0), inOut);
                    p.HeadAdd = Quaternion.Slerp(Quaternion.identity, E(18, 0, Mathf.Sin(t * 5f) * 5f), inOut);
                    p.Torso = Quaternion.Slerp(Quaternion.identity, E(8, 0, 0), inOut);
                    break;
                case Gesture.Angry:
                {
                    float sh = Mathf.Sin(t * 22f) * 7f;
                    p.SL = Quaternion.Slerp(rest.SL, E(-88, 22, -14 + sh), inOut);
                    p.SR = Quaternion.Slerp(rest.SR, E(-88, -22, 14 - sh), inOut);
                    p.EL = Quaternion.Slerp(rest.EL, E(-96, 0, 0), inOut);
                    p.ER = Quaternion.Slerp(rest.ER, E(-96, 0, 0), inOut);
                    p.Torso = Quaternion.Slerp(Quaternion.identity, E(10, 0, 0), inOut);
                    p.HeadAdd = Quaternion.Slerp(Quaternion.identity, E(8, Mathf.Sin(t * 16f) * 6f, 0), inOut);
                    break;
                }
                case Gesture.Mock:
                    p.SL = Quaternion.Slerp(rest.SL, AimArm(ShoulderL, aim, 0f), inOut);
                    p.EL = Quaternion.Slerp(rest.EL, E(-8, 0, 0), inOut);
                    p.Torso = Quaternion.Slerp(Quaternion.identity, E(-10 + s * 4, 0, 0), inOut);
                    p.HeadAdd = Quaternion.Slerp(Quaternion.identity, E(-16 + s * 6, 0, 0), inOut);
                    p.Lift = Mathf.Abs(s) * 0.014f * inOut;
                    break;
            }
            return p;
        }

        void Apply(Pose p, float k)
        {
            Torso.localRotation = Quaternion.Slerp(Torso.localRotation, p.Torso, k);
            ShoulderL.localRotation = Quaternion.Slerp(ShoulderL.localRotation, p.SL, k);
            ElbowL.localRotation = Quaternion.Slerp(ElbowL.localRotation, p.EL, k);
            ShoulderR.localRotation = Quaternion.Slerp(ShoulderR.localRotation, p.SR, k);
            ElbowR.localRotation = Quaternion.Slerp(ElbowR.localRotation, p.ER, k);
            if (Paddle != null) Paddle.localRotation = Quaternion.Slerp(Paddle.localRotation, p.Paddle, k);
        }

        void Hands()
        {
            var g = Busy ? gesture : Gesture.None;
            HandPose l = HandPose.Relaxed, r = Paddle != null ? HandPose.Grip : HandPose.Relaxed;
            switch (g)
            {
                case Gesture.Cheer: case Gesture.Shrug: case Gesture.Shock: case Gesture.Clap: l = HandPose.Open; if (Paddle == null) r = HandPose.Open; break;
                case Gesture.Wave: case Gesture.Facepalm: case Gesture.Talk: l = HandPose.Open; break;
                case Gesture.Point: case Gesture.Mock: l = HandPose.Point; break;
                case Gesture.ThumbsUp: l = HandPose.ThumbsUp; break;
                case Gesture.MiddleFinger: l = HandPose.Middle; break;
                case Gesture.Peace: l = HandPose.Peace; break;
                case Gesture.Angry: l = HandPose.Fist; r = Paddle != null ? HandPose.Grip : HandPose.Fist; break;
                case Gesture.Think: l = HandPose.Fist; break;
                case Gesture.Swing: l = HandPose.Grip; break;
            }
            if (GloveL != null && GloveL.Pose != l) GloveL.SetPose(l);
            if (GloveR != null && GloveR.Pose != r) GloveR.SetPose(r);
        }

        void Face(float dt, float t)
        {
            var g = Busy ? gesture : Gesture.None;
            // 입: 말·웃음·놀람·음성
            float want = 0f;
            switch (g)
            {
                case Gesture.Talk: want = 0.25f + 0.45f * Mathf.Abs(Mathf.Sin(t * 13f)); break;
                case Gesture.Laugh: case Gesture.Mock: want = 0.55f + 0.35f * Mathf.Abs(Mathf.Sin(t * 16f)); break;
                case Gesture.Cheer: want = 0.8f; break;
                case Gesture.Shock: want = 0.95f; break;
                case Gesture.Angry: want = 0.35f + 0.45f * Mathf.Abs(Mathf.Sin(t * 18f)); break;
                case Gesture.Wave: want = 0.3f; break;
            }
            want = Mathf.Max(want, Mathf.Clamp01(VoiceLevel * 1.6f));
            mouthOpen = Mathf.Lerp(mouthOpen, want, 1f - Mathf.Exp(-dt * 22f));
            if (Mouth != null) Mouth.localScale = new Vector3(1f - mouthOpen * 0.15f, Mathf.Max(0.05f, mouthOpen), 1f);

            // 눈썹: 화남(안쪽이 내려감) · 놀람(올라감) · 슬픔(안쪽이 올라감)
            float tilt = 0f, raise = 0f;
            switch (g)
            {
                case Gesture.Angry: case Gesture.MiddleFinger: tilt = -20f; raise = -0.002f; break;
                case Gesture.Shock: raise = 0.008f; break;
                case Gesture.Slump: case Gesture.Facepalm: tilt = 16f; break;
                case Gesture.Cheer: case Gesture.Laugh: case Gesture.Mock: case Gesture.Wave: raise = 0.004f; break;
                case Gesture.Think: raise = 0.003f; break;
            }
            float k = 1f - Mathf.Exp(-dt * 12f);
            if (BrowL != null)
            {
                BrowL.localPosition = Vector3.Lerp(BrowL.localPosition, browBaseL + Vector3.up * raise * Look.Head, k);
                BrowL.localRotation = Quaternion.Slerp(BrowL.localRotation, browRotL * Quaternion.Euler(0, 0, tilt), k);
            }
            if (BrowR != null)
            {
                float rt = g == Gesture.Think ? tilt - 10f : tilt;
                BrowR.localPosition = Vector3.Lerp(BrowR.localPosition, browBaseR + Vector3.up * (g == Gesture.Think ? 0f : raise) * Look.Head, k);
                BrowR.localRotation = Quaternion.Slerp(BrowR.localRotation, browRotR * Quaternion.Euler(0, 0, -rt), k);
            }
        }

        /// <summary>1인칭 내 몸: 감정 표현을 하는 동안에만 왼팔을 보인다.</summary>
        void LocalArms()
        {
            if (!ArmsHidden || ShoulderL == null) return;
            if (leftArm == null) leftArm = new List<MeshRenderer>(ShoulderL.GetComponentsInChildren<MeshRenderer>(true));
            bool show = Busy && (IsEmote(gesture) || gesture == Gesture.Point);
            if (show == leftArmShown) return;
            leftArmShown = show;
            foreach (var r in leftArm)
                if (r != null) r.shadowCastingMode = show ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            float t = Time.time + phase;
            var rest = RestPose();
            // 가만히 있을 때의 작은 움직임
            if (Time.time > nextIdle)
            {
                nextIdle = Time.time + Random.Range(2.5f, 6f);
                idleShift = Random.Range(-1f, 1f);
                if (!Busy && Random.value < 0.12f && !Standing && !LocalPose) Play(Random.value < 0.5f ? Gesture.Think : Gesture.Talk, Random.Range(2f, 3.5f));
            }
            rest.Torso = E(Mathf.Sin(t * 0.7f) * 1.2f, idleShift * 4f, Mathf.Sin(t * 0.5f) * 1.5f);
            var target = rest;
            float u = 1f;
            if (gesture != Gesture.None)
            {
                gT += dt;
                u = Mathf.Clamp01(gT / gLen);
                target = GesturePose(rest, gT, u);
                if (gT >= gLen) gesture = Gesture.None;
            }
            bool sharp = gesture == Gesture.Gavel || gesture == Gesture.Bid || gesture == Gesture.BigBid;
            Apply(target, 1f - Mathf.Exp(-dt * (sharp ? 30f : 10f)));
            Hands();
            Face(dt, t);
            LocalArms();

            // 숨쉬기 · 들썩임
            excitement = Mathf.MoveTowards(excitement, 0, dt * 0.4f);
            float breath = Mathf.Sin(t * 1.7f) * 0.004f;
            Torso.localPosition = torsoBase + Vector3.up * (breath + target.Lift);

            // 시선: 머리를 대상 쪽으로 (좌우 ±75°, 위아래 ±35°)
            Quaternion want = Quaternion.identity;
            if (hasLook)
            {
                Vector3 dir = Torso.InverseTransformDirection(lookTarget - Head.position);
                float yaw = Mathf.Clamp(Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg, -75f, 75f);
                float pitch = Mathf.Clamp(-Mathf.Atan2(dir.y, new Vector2(dir.x, dir.z).magnitude) * Mathf.Rad2Deg, -35f, 35f);
                want = Quaternion.Euler(pitch, yaw, 0);
            }
            want = want * target.HeadAdd * Quaternion.Euler(Mathf.Sin(t * 0.9f) * 2f, Mathf.Sin(t * 0.43f) * 3f, Mathf.Sin(t * 0.61f) * 2f);
            if (knock > 0f)
            {
                // 맞은 뒤 고개가 흔들리며 돌아온다
                knock = Mathf.MoveTowards(knock, 0f, dt * 1.4f);
                float wob = Mathf.Sin(knock * 30f) * knock;
                want = want * Quaternion.Euler(18f * knock + wob * 6f, wob * 14f, wob * 10f);
                headCur = Quaternion.Slerp(headCur, want, 0.5f);
            }
            headCur = Quaternion.Slerp(headCur, want, 1f - Mathf.Exp(-dt * 6f));
            Head.localRotation = headBase * headCur;

            // 깜빡임
            if (blinkT < 0 && Time.time > nextBlink) blinkT = 0;
            float eyeY = 1f;
            if (blinkT >= 0)
            {
                blinkT += dt;
                eyeY = blinkT < 0.07f ? 1f - blinkT / 0.07f * 0.9f : (blinkT < 0.15f ? 0.1f + (blinkT - 0.07f) / 0.08f * 0.9f : 1f);
                if (blinkT >= 0.15f)
                {
                    blinkT = -1;
                    nextBlink = Time.time + Random.Range(2f, 5.5f);
                }
            }
            if (gesture == Gesture.Shock) eyeY = 1.3f;
            if (gesture == Gesture.Laugh || gesture == Gesture.Cheer || gesture == Gesture.Mock) eyeY = Mathf.Min(eyeY, 0.35f);
            if (gesture == Gesture.Angry || gesture == Gesture.MiddleFinger) eyeY = Mathf.Min(eyeY, 0.7f);
            if (EyeL != null) EyeL.localScale = new Vector3(1, eyeY, 1);
            if (EyeR != null) EyeR.localScale = new Vector3(1, eyeY, 1);
        }
    }
}
