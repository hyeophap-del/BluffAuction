using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace BluffAuction.Characters
{
    /// <summary>
    /// 캐릭터 초상화: 경매장에서 멀리 떨어진 곳(아래 100m)에 손님을 한 명씩 앉혀 얼굴을 찍어 Texture2D 로 남긴다
    /// (상대 고르기 카드·대기실·참가자 목록). 처음에 정해진 생김새를 모두 찍고, 꾸민 생김새는 요청이 오면 그때 찍는다.
    /// (렌더 텍스처는 나중에 내용이 사라질 수 있어 찍자마자 일반 텍스처로 옮겨 둔다.)
    /// </summary>
    public sealed class PortraitStudio : MonoBehaviour
    {
        const int Size = 256;
        const int MaxCustom = 48;
        static readonly Dictionary<string, Texture2D> shots = new Dictionary<string, Texture2D>();
        static readonly Queue<CharacterLook> queue = new Queue<CharacterLook>();
        static readonly HashSet<string> queued = new HashSet<string>();
        static readonly List<string> customOrder = new List<string>();
        static PortraitStudio instance;
        public static bool Ready { get; private set; }

        public static Texture2D Get(string key)
        {
            Texture2D t;
            return shots.TryGetValue(key, out t) ? t : null;
        }

        /// <summary>이 생김새의 초상화 열쇠 (아직 없으면 찍도록 줄을 세운다).</summary>
        public static string Request(CharacterLook look)
        {
            if (look == null) return "";
            string key = IsPreset(look) ? look.Id : look.Key;
            if (shots.ContainsKey(key) || queued.Contains(key)) return key;
            queued.Add(key);
            queue.Enqueue(look.Clone());
            return key;
        }

        static bool IsPreset(CharacterLook look)
        {
            foreach (var l in CharacterLook.Guests) if (l == look) return true;
            foreach (var l in CharacterLook.Players) if (l == look) return true;
            return false;
        }

        public static void RenderAll(MonoBehaviour host)
        {
            if (instance != null) return;
            var go = new GameObject("PortraitStudio");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<PortraitStudio>();
            foreach (var l in CharacterLook.Guests) { queued.Add(l.Id); queue.Enqueue(l); }
            foreach (var l in CharacterLook.Players) { queued.Add(l.Id); queue.Enqueue(l); }
            host.StartCoroutine(instance.Run());
        }

        RenderTexture rt;
        Camera cam;
        Transform camT;

        void Setup()
        {
            transform.position = new Vector3(0, -100f, 0);
            rt = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { name = "PortraitRT" };
            rt.Create();
            var cgo = new GameObject("PortraitCam");
            cgo.transform.SetParent(transform, false);
            camT = cgo.transform;
            cam = cgo.AddComponent<Camera>();
            cam.fieldOfView = 24f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 6f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.16f, 0.05f, 0.06f, 1f);
            cam.targetTexture = rt;
            cam.enabled = false;
            var data = cgo.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = false;
            data.renderShadows = false;
        }

        IEnumerator Run()
        {
            Setup();
            while (true)
            {
                if (queue.Count == 0)
                {
                    Ready = true;
                    yield return null;
                    continue;
                }
                var look = queue.Dequeue();
                string key = IsPreset(look) ? look.Id : look.Key;
                var g = GuestBuilder.Build(transform, look, new Color(0.9f, 0.75f, 0.4f));
                g.transform.localRotation = Quaternion.Euler(0, 180, 0);
                g.LookAt(transform.TransformPoint(new Vector3(0.15f, 1.2f, -1.2f)));
                // 패들이 얼굴 옆으로 비쳐 들어오지 않게
                if (g.Paddle != null) g.Paddle.gameObject.SetActive(false);
                var headY = 0.5f + 0.535f * look.Height + 0.13f;
                var lights = new List<GameObject>
                {
                    AddLight(new Vector3(-0.6f, headY + 0.5f, -0.8f), new Color(1f, 0.86f, 0.7f), 1.15f, 3f),
                    AddLight(new Vector3(0.7f, headY + 0.2f, 0.6f), new Color(0.95f, 0.55f, 0.32f), 1.3f, 2.5f),
                    AddLight(new Vector3(0.3f, headY - 0.3f, -0.9f), new Color(0.55f, 0.6f, 0.8f), 0.35f, 2.5f)
                };
                // 몸짓·손가락이 자리를 잡도록 몇 프레임 기다린 뒤 찍는다
                for (int f = 0; f < 3; f++) yield return null;
                camT.localPosition = new Vector3(0.13f, headY + 0.05f, -1.1f);
                camT.LookAt(transform.TransformPoint(new Vector3(0, headY + 0.01f, 0)));
                try
                {
                    var req = new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = rt };
                    if (UnityEngine.Rendering.RenderPipeline.SupportsRenderRequest(cam, req))
                        UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(cam, req);
                    else cam.Render();
                    var prev = RenderTexture.active;
                    RenderTexture.active = rt;
                    var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, true) { name = "Portrait_" + key, wrapMode = TextureWrapMode.Clamp, anisoLevel = 2 };
                    tex.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
                    tex.Apply(true, false);
                    RenderTexture.active = prev;
                    Store(key, tex, !IsPreset(look));
                }
                catch (System.Exception e) { Dev.AutoShots.Log("portrait render failed: " + e.Message); }
                queued.Remove(key);
                Destroy(g.gameObject);
                foreach (var l in lights) Destroy(l);
                yield return null;
            }
        }

        static void Store(string key, Texture2D tex, bool custom)
        {
            Texture2D old;
            if (shots.TryGetValue(key, out old) && old != null) Destroy(old);
            shots[key] = tex;
            if (!custom) return;
            customOrder.Remove(key);
            customOrder.Add(key);
            // 꾸민 생김새는 최근 것만 남긴다
            while (customOrder.Count > MaxCustom)
            {
                string drop = customOrder[0];
                customOrder.RemoveAt(0);
                if (shots.TryGetValue(drop, out old) && old != null) Destroy(old);
                shots.Remove(drop);
            }
        }

        GameObject AddLight(Vector3 localPos, Color c, float intensity, float range)
        {
            var go = new GameObject("Light");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = c;
            l.intensity = intensity;
            l.range = range;
            l.shadows = LightShadows.None;
            return go;
        }
    }
}
