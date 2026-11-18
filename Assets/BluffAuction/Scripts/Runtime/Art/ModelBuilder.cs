using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BluffAuction.Art
{
    /// <summary>
    /// MeshKit 위에 재질 이름 → 하위 메시 번호를 붙인 도구. 다 쌓은 뒤 GameObject 하나(MeshFilter + MeshRenderer)로 만든다.
    /// </summary>
    public sealed class ModelBuilder
    {
        public readonly MeshKit Kit = new MeshKit();
        readonly List<string> mats = new List<string>();
        readonly Dictionary<string, int> index = new Dictionary<string, int>();
        readonly List<Material> overrides = new List<Material>();

        /// <summary>재질 이름의 하위 메시 번호.</summary>
        public int M(string material)
        {
            int i;
            if (index.TryGetValue(material, out i)) return i;
            i = mats.Count;
            mats.Add(material);
            overrides.Add(null);
            index[material] = i;
            return i;
        }

        /// <summary>이름 대신 재질 객체를 직접 쓰는 칸 (색을 바꾼 캐릭터 재질 등).</summary>
        public int M(string key, Material material)
        {
            int i = M(key);
            overrides[i] = material;
            return i;
        }

        public GameObject Build(string name, Transform parent, bool castShadows = true)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            Attach(go, castShadows);
            return go;
        }

        public Mesh Attach(GameObject go, bool castShadows = true)
        {
            var mesh = Kit.Build(go.name, mats.Count);
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            var arr = new Material[mats.Count];
            for (int i = 0; i < mats.Count; i++) arr[i] = overrides[i] != null ? overrides[i] : ArtLibrary.I.Mat(mats[i]);
            mr.sharedMaterials = arr;
            mr.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            mr.receiveShadows = true;
            mr.lightProbeUsage = LightProbeUsage.BlendProbes;
            mr.reflectionProbeUsage = ReflectionProbeUsage.BlendProbesAndSkybox;
            go.AddComponent<MeshOwner>().Mesh = mesh;
            return mesh;
        }

        public static Vector2 P(float r, float y) { return new Vector2(r, y); }

        /// <summary>위→아래로 적은 프로필을 아래→위로 뒤집는다 (Lathe 는 아래→위 순서를 기대).</summary>
        public static Vector2[] Up(params Vector2[] topDown)
        {
            var r = new Vector2[topDown.Length];
            for (int i = 0; i < topDown.Length; i++) r[i] = topDown[topDown.Length - 1 - i];
            return r;
        }
    }
}
