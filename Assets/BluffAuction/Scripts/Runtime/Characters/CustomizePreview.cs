using BluffAuction.Art;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace BluffAuction.Characters
{
    /// <summary>
    /// 꾸미기 화면의 움직이는 미리보기: 경매장 아래 촬영장에 내 손님을 앉혀 카메라가 렌더 텍스처로 찍는다.
    /// 끌어서 돌리고, 얼굴 / 전신을 오간다. 생김새가 바뀌면 모형을 다시 만든다.
    /// </summary>
    public sealed class CustomizePreview : MonoBehaviour
    {
        public RenderTexture RT { get; private set; }
        Guest guest;
        Camera cam;
        Transform pivot;
        float yaw = 200f, yawTarget = 200f;
        float zoom, zoomTarget;
        float nextIdle;

        public static CustomizePreview Create(int width, int height)
        {
            var go = new GameObject("CustomizePreview");
            go.transform.position = new Vector3(40f, -100f, 0f);
            var p = go.AddComponent<CustomizePreview>();
            p.Build(width, height);
            return p;
        }

        void Build(int width, int height)
        {
            RT = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { name = "CustomizeRT", antiAliasing = 4 };
            RT.Create();
            pivot = new GameObject("Pivot").transform;
            pivot.SetParent(transform, false);
            // 무대: 둥근 받침과 어두운 벨벳 배경
            var b = new ModelBuilder();
            int floor = b.M("floor", ArtLibrary.I.Tinted("VelvetPlain", new Color(0.2f, 0.05f, 0.06f)));
            int gold = b.M("Gold"), wood = b.M("Mahogany");
            b.Kit.Lathe(floor, new[] { new Vector2(0, 0), new Vector2(0.7f, 0), new Vector2(0.72f, 0.03f), new Vector2(0, 0.03f) }, 48);
            b.Kit.Lathe(gold, new[] { new Vector2(0.72f, 0.005f), new Vector2(0.73f, 0.017f), new Vector2(0.72f, 0.029f) }, 48);
            b.Kit.Cylinder(wood, 0.2f, 0.03f, 0.45f, 24, true);
            b.Build("Stage", transform);
            var back = new ModelBuilder();
            back.Kit.Box(back.M("back", ArtLibrary.I.Tinted("Velvet", new Color(0.35f, 0.05f, 0.08f))), new Vector3(0, 1.5f, 2.2f), new Vector3(6f, 3.2f, 0.05f));
            back.Build("Backdrop", transform);

            var cgo = new GameObject("PreviewCam");
            cgo.transform.SetParent(transform, false);
            cam = cgo.AddComponent<Camera>();
            cam.fieldOfView = 26f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 8f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.02f, 0.03f, 1f);
            cam.targetTexture = RT;
            var data = cgo.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = false;
            data.renderShadows = true;

            AddLight(new Vector3(-1.2f, 2.2f, -1.6f), new Color(1f, 0.88f, 0.74f), 2.6f, 5f, LightShadows.Soft);
            AddLight(new Vector3(1.4f, 1.3f, -1.4f), new Color(0.7f, 0.76f, 0.95f), 1.0f, 5f, LightShadows.None);
            AddLight(new Vector3(0.3f, 2.0f, 1.2f), new Color(1f, 0.62f, 0.4f), 2.0f, 4f, LightShadows.None);
        }

        void AddLight(Vector3 local, Color c, float intensity, float range, LightShadows shadows)
        {
            var go = new GameObject("Light");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = local;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = c;
            l.intensity = intensity;
            l.range = range;
            l.shadows = shadows;
        }

        public void SetLook(CharacterLook look)
        {
            if (guest != null) Destroy(guest.gameObject);
            guest = GuestBuilder.Build(pivot, look, new Color(0.9f, 0.7f, 0.3f));
            guest.transform.localPosition = new Vector3(0, -0.02f, 0);
        }

        public void Rotate(float degrees) { yawTarget += degrees; }
        public void SetFace(bool face) { zoomTarget = face ? 1f : 0f; }
        public bool Face { get { return zoomTarget > 0.5f; } }

        public void Emote(Gesture g)
        {
            if (guest == null) return;
            guest.Emote(g, cam.transform.position);
        }

        void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            yaw = Mathf.Lerp(yaw, yawTarget, 1f - Mathf.Exp(-dt * 8f));
            zoom = Mathf.Lerp(zoom, zoomTarget, 1f - Mathf.Exp(-dt * 6f));
            pivot.localRotation = Quaternion.Euler(0, yaw, 0);
            if (guest == null) return;
            float h = guest.Look.Height;
            var headAt = new Vector3(0, 0.5f + 0.535f * h + 0.16f, 0);
            Vector3 focus = Vector3.Lerp(new Vector3(0, 0.82f * h, 0), headAt, zoom);
            float dist = Mathf.Lerp(2.6f, 1.3f, zoom);
            Vector3 eye = focus + new Vector3(0, Mathf.Lerp(0.25f, 0.04f, zoom), -dist);
            cam.transform.localPosition = eye;
            cam.transform.LookAt(transform.TransformPoint(focus));
            guest.LookAt(cam.transform.position + Vector3.up * 0.05f);
            if (Time.unscaledTime > nextIdle && !guest.Busy)
            {
                nextIdle = Time.unscaledTime + Random.Range(5f, 9f);
                if (Random.value < 0.5f) guest.Play(Random.value < 0.5f ? Gesture.Talk : Gesture.Think, 2.4f);
            }
        }

        void OnDestroy()
        {
            if (RT != null) { RT.Release(); Destroy(RT); }
        }
    }
}
