using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace BluffAuction.World
{
    public enum Shot
    {
        Seat,        // 내 자리에서 탁자 너머 (기본)
        Item,        // 회전판 위 물건 가까이
        ItemLow,     // 물건을 낮게 올려다보기 (소개)
        Auctioneer,  // 경매사
        Bidder,      // 특정 손님 쪽
        Wide,        // 높이서 전체
        Menu,        // 메뉴 배경 (천천히 도는 카메라)
        Reveal,      // 공개: 물건과 주인
        Register     // 등록: 물건이 화면 위쪽에 오게 (아래는 등록 카드)
    }

    /// <summary>
    /// 연출 카메라. 정해진 구도(Shot) 사이를 부드럽게 옮기고, 약한 손떨림·숨쉬기로 살아 있는 느낌을 준다.
    /// 피사계 심도 초점은 바라보는 점까지의 거리로 맞춘다.
    /// </summary>
    public sealed class CameraRig : MonoBehaviour
    {
        public Camera Cam;
        public PostFx Fx;
        public Shot Current = Shot.Seat;
        public float Intensity = 1f;   // 설정의 '카메라 움직임' (0 이면 늘 기본 구도)
        Vector3 pos, look;
        Vector3 posTarget, lookTarget;
        float fov = 48f, fovTarget = 48f;
        float speed = 2.5f;
        float shake;
        Vector3 focusPoint;
        float orbit;

        public static CameraRig Create(Transform parent, PostFx fx)
        {
            var go = new GameObject("MainCamera");
            go.tag = "MainCamera";
            go.transform.SetParent(parent, false);
            var cam = go.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 80f;
            cam.fieldOfView = 48f;
            cam.allowHDR = true;
            cam.allowMSAA = true;
            var data = go.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            data.renderShadows = true;
            data.requiresDepthTexture = true;
            go.AddComponent<AudioListener>();
            var rig = go.AddComponent<CameraRig>();
            rig.Cam = cam;
            rig.Fx = fx;
            rig.Cut(Shot.Seat);
            return rig;
        }

        static readonly Vector3 SeatPos = new Vector3(0, 1.58f, -3.0f);
        static readonly Vector3 SeatLook = new Vector3(0, 1.02f, 0.6f);

        void Resolve(Shot s, Vector3 subject, out Vector3 p, out Vector3 l, out float f)
        {
            float top = Hall.TurntableTop;
            switch (s)
            {
                case Shot.Item:
                    p = new Vector3(0.18f, top + 0.42f, -0.95f);
                    l = new Vector3(0, top + 0.17f, 0);
                    f = 34f;
                    break;
                case Shot.ItemLow:
                    p = new Vector3(-0.35f, top + 0.12f, -0.8f);
                    l = new Vector3(0, top + 0.2f, 0);
                    f = 38f;
                    break;
                case Shot.Auctioneer:
                    p = new Vector3(0.55f, 1.75f, 1.0f);
                    l = Hall.AuctioneerPos + new Vector3(0, 1.45f, 0);
                    f = 36f;
                    break;
                case Shot.Bidder:
                {
                    // 탁자 가운데 쪽에서 그 손님을 비스듬히
                    // 탁자 너머에서 그 손님을 비스듬히 (상반신이 다 들어오게)
                    Vector3 dir = new Vector3(subject.x, 0, subject.z).normalized;
                    Vector3 side = Vector3.Cross(Vector3.up, dir);
                    p = -dir * 0.35f + side * 0.7f + Vector3.up * 1.55f;
                    l = subject + Vector3.up * 1.2f;
                    f = 36f;
                    break;
                }
                case Shot.Wide:
                    p = new Vector3(0, 3.4f, -5.2f);
                    l = new Vector3(0, 0.9f, 0.8f);
                    f = 50f;
                    break;
                case Shot.Menu:
                {
                    float a = orbit * 0.05f;
                    p = new Vector3(Mathf.Sin(a) * 4.6f, 2.1f + Mathf.Sin(orbit * 0.07f) * 0.2f, -Mathf.Cos(a) * 4.6f + 0.8f);
                    l = new Vector3(0, 1.1f, 0.6f);
                    f = 44f;
                    break;
                }
                case Shot.Reveal:
                    // 공개 목록이 오른쪽에 있으므로 물건을 화면 왼쪽에
                    p = new Vector3(0.85f, top + 0.75f, -2.0f);
                    l = new Vector3(0.62f, top + 0.12f, 0);
                    f = 38f;
                    break;
                case Shot.Register:
                    p = new Vector3(0, top + 0.42f, -1.05f);
                    l = new Vector3(0, top - 0.12f, 0.1f);
                    f = 40f;
                    break;
                default:
                    p = SeatPos;
                    l = SeatLook;
                    f = 48f;
                    break;
            }
        }

        public void Go(Shot s, float seconds = 1.2f, Vector3 subject = default(Vector3))
        {
            if (Intensity <= 0.01f && s != Shot.Menu) s = Shot.Seat;
            Current = s;
            Resolve(s, subject, out posTarget, out lookTarget, out fovTarget);
            speed = seconds <= 0.01f ? 100f : 3.2f / seconds;
        }

        public void Cut(Shot s, Vector3 subject = default(Vector3))
        {
            Go(s, 0f, subject);
            pos = posTarget;
            look = lookTarget;
            fov = fovTarget;
            Apply();
        }

        /// <summary>순간 흔들림 (큰 입찰·의사봉).</summary>
        public void Kick(float amount) { shake = Mathf.Max(shake, amount * Intensity); }

        void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            orbit += dt;
            if (Current == Shot.Menu) Resolve(Shot.Menu, Vector3.zero, out posTarget, out lookTarget, out fovTarget);
            float k = 1f - Mathf.Exp(-dt * speed);
            pos = Vector3.Lerp(pos, posTarget, k);
            look = Vector3.Lerp(look, lookTarget, k);
            fov = Mathf.Lerp(fov, fovTarget, k);
            UpdateFirstPerson(dt);
            Apply();
        }

        // ═════════════════ 1인칭 (내 자리에 앉아 고개를 돌린다) ═════════════════

        /// <summary>1인칭으로 볼지 (게임 중). 끄면 연출 구도(Shot)로 돌아간다.</summary>
        public bool FirstPerson;
        /// <summary>마우스로 고개를 돌리는 중 (커서가 잠겨 있음). 꺼져 있으면 오른쪽 버튼을 누른 채 끌어서 돌린다.</summary>
        public bool MouseLook;
        /// <summary>감정 표현 바퀴를 여는 동안 마우스로 고개가 돌지 않게.</summary>
        public bool FreezeLook;
        /// <summary>개발용 자동 촬영: 1인칭 화각 고정 (0 이면 끔).</summary>
        public float DevFov;
        public float Sensitivity = 1f;
        public bool InvertY;
        /// <summary>눈 위치 (내 좌석 머리)와 정면 방향(탁자 중심을 보는 각도, 도).</summary>
        public Vector3 Eye = new Vector3(0, 1.36f, -1.4f);
        public float BaseYaw;
        public float Yaw, Pitch = -12f;
        float fpBlend, zoom;
        float yawVel, pitchVel;
        float yawTarget, pitchTarget = -12f;
        public const float YawLimit = 118f, PitchMin = -62f, PitchMax = 38f;
        /// <summary>지금 탁자 반지름 (1인칭 초점을 탁자 상판에 맞출 때).</summary>
        public float TableRadiusNow = 1.25f;

        /// <summary>바로 이 방향을 보게 한다 (등록 화면 등에서 회전판 쪽으로).</summary>
        public void LookTowards(float yaw, float pitch, bool instant = false)
        {
            yawTarget = Mathf.Clamp(yaw, -YawLimit, YawLimit);
            pitchTarget = Mathf.Clamp(pitch, PitchMin, PitchMax);
            if (instant)
            {
                Yaw = yawTarget;
                Pitch = pitchTarget;
            }
        }

        /// <summary>월드 지점을 보는 데 필요한 (yaw, pitch).</summary>
        public Vector2 AnglesTo(Vector3 world)
        {
            Vector3 d = world - Eye;
            float yaw = Mathf.DeltaAngle(BaseYaw, Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg);
            float pitch = -Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg;
            return new Vector2(yaw, -pitch);
        }

        public bool ZoomHeld { get; private set; }

        void UpdateFirstPerson(float dt)
        {
            fpBlend = Mathf.MoveTowards(fpBlend, FirstPerson ? 1f : 0f, dt * 1.6f);
            if (fpBlend <= 0f) return;
            var mouse = UnityEngine.InputSystem.Mouse.current;
            var pad = UnityEngine.InputSystem.Gamepad.current;
            bool drag = mouse != null && !MouseLook && mouse.rightButton.isPressed;
            if (mouse != null && (MouseLook || drag) && !FreezeLook)
            {
                Vector2 d = mouse.delta.ReadValue() * 0.085f * Sensitivity;
                yawTarget += d.x;
                pitchTarget += (InvertY ? -d.y : d.y);
            }
            if (pad != null)
            {
                Vector2 s = pad.rightStick.ReadValue();
                yawTarget += s.x * 140f * dt * Sensitivity;
                pitchTarget += (InvertY ? -s.y : s.y) * 100f * dt * Sensitivity;
            }
            yawTarget = Mathf.Clamp(yawTarget, -YawLimit, YawLimit);
            pitchTarget = Mathf.Clamp(pitchTarget, PitchMin, PitchMax);
            // 아주 짧게 부드럽게 따라가 손떨림을 없앤다
            Yaw = Mathf.SmoothDamp(Yaw, yawTarget, ref yawVel, 0.035f, Mathf.Infinity, dt);
            Pitch = Mathf.SmoothDamp(Pitch, pitchTarget, ref pitchVel, 0.035f, Mathf.Infinity, dt);
            // 확대: 보는 중이면 오른쪽 버튼, 끄는 중이면 가운데 버튼 / 패드 왼쪽 트리거
            ZoomHeld = (mouse != null && ((MouseLook && mouse.rightButton.isPressed) || mouse.middleButton.isPressed)) || (pad != null && pad.leftTrigger.isPressed);
            zoom = Mathf.MoveTowards(zoom, ZoomHeld ? 1f : 0f, dt * 4f);
        }

        void Apply()
        {
            float t = Time.unscaledTime;
            // 손에 든 카메라처럼 아주 약하게 흔들린다
            Vector3 drift = new Vector3(Mathf.PerlinNoise(t * 0.23f, 1.3f) - 0.5f, Mathf.PerlinNoise(3.1f, t * 0.19f) - 0.5f, 0) * 0.025f * Intensity;
            shake = Mathf.MoveTowards(shake, 0, Time.unscaledDeltaTime * 2.5f);
            Vector3 jolt = shake > 0 ? Random.insideUnitSphere * shake * 0.03f : Vector3.zero;
            Vector3 cinePos = pos + drift + jolt;
            Quaternion cineRot = Quaternion.LookRotation(look - cinePos + drift * 0.5f, Vector3.up);
            float cineFov = fov;
            float focus = Vector3.Distance(cinePos, look);

            if (fpBlend > 0f)
            {
                // 목을 축으로 머리를 돌리면 눈이 조금 앞뒤로 움직인다 (시차)
                var rot = Quaternion.Euler(-Pitch, BaseYaw + Yaw, 0);
                Vector3 neck = Eye + new Vector3(0, -0.1f, 0) - Quaternion.Euler(0, BaseYaw, 0) * new Vector3(0, 0, 0.06f);
                Vector3 breath = new Vector3(0, Mathf.Sin(t * 1.6f) * 0.003f, 0);
                Vector3 fpPos = neck + rot * new Vector3(0, 0.1f, 0.08f) + breath + jolt * 0.5f;
                Quaternion fpRot = rot * Quaternion.Euler(Mathf.PerlinNoise(t * 0.3f, 7f) - 0.5f, Mathf.PerlinNoise(t * 0.27f, 3f) - 0.5f, 0);
                float fpFov = DevFov > 0f ? DevFov : Mathf.Lerp(58f, 26f, zoom * zoom * (3 - 2 * zoom));
                float e = fpBlend * fpBlend * (3 - 2 * fpBlend);
                transform.position = Vector3.Lerp(cinePos, fpPos, e);
                transform.rotation = Quaternion.Slerp(cineRot, fpRot, e);
                if (Cam != null) Cam.fieldOfView = Mathf.Lerp(cineFov, fpFov, e);
                // 초점은 바라보는 곳까지 (가까운 물건을 보면 뒤가 흐려진다)
                // 충돌체가 없는 탁자 상판은 평면으로 따로 잰다
                RaycastHit hit;
                float fpFocus = Physics.Raycast(transform.position, transform.forward, out hit, 12f) ? hit.distance : 2.6f;
                Vector3 f = transform.forward;
                if (f.y < -0.05f)
                {
                    float d = (transform.position.y - Hall.TableHeight) / -f.y;
                    Vector3 p = transform.position + f * d;
                    if (d < fpFocus && new Vector2(p.x, p.z).magnitude < TableRadiusNow) fpFocus = d;
                }
                if (Fx != null)
                {
                    Fx.SetFocus(Mathf.Lerp(focus, fpFocus, e));
                    // 1인칭에서는 피사계 심도를 얕게만 (가까운 카드가 뭉개지지 않게)
                    Fx.SetAperture(Mathf.Lerp(4.5f, 11f, e));
                }
                return;
            }
            transform.position = cinePos;
            transform.rotation = cineRot;
            if (Cam != null) Cam.fieldOfView = cineFov;
            if (Fx != null)
            {
                Fx.SetFocus(focus);
                Fx.SetAperture(4.5f);
            }
        }
    }
}
