using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace BluffAuction.Art
{
    /// <summary>
    /// 빌드에 들어가는 자산 목록 (Resources/ArtLibrary). 재질·텍스처·소리·글꼴을 이름으로 찾는다.
    /// 에디터의 ContentBuilder 가 채운다. 실행 중에는 재질을 직접 만들지 않고 여기 재질을 복제해 쓴다(셰이더 변형 보장).
    /// </summary>
    public sealed class ArtLibrary : ScriptableObject
    {
        [Serializable]
        public struct NamedMaterial { public string Name; public Material Material; }
        [Serializable]
        public struct NamedTexture { public string Name; public Texture2D Texture; }
        [Serializable]
        public struct NamedClip { public string Name; public AudioClip Clip; }

        public List<NamedMaterial> Materials = new List<NamedMaterial>();
        public List<NamedTexture> Textures = new List<NamedTexture>();
        public List<NamedClip> Clips = new List<NamedClip>();
        public PanelSettings Panel;
        public StyleSheet Style;
        public UnityEngine.TextCore.Text.FontAsset UiFont;
        public UnityEngine.TextCore.Text.FontAsset UiFontBold;
        public Material ParticleAdditive;
        public Material ParticleConfetti;

        static ArtLibrary instance;
        Dictionary<string, Material> mats;
        Dictionary<string, Texture2D> texs;
        Dictionary<string, AudioClip> clips;

        public static ArtLibrary I
        {
            get
            {
                if (instance == null) instance = Resources.Load<ArtLibrary>("ArtLibrary");
                return instance;
            }
        }

        void Index()
        {
            if (mats != null) return;
            mats = new Dictionary<string, Material>();
            texs = new Dictionary<string, Texture2D>();
            clips = new Dictionary<string, AudioClip>();
            foreach (var m in Materials) if (m.Material != null) mats[m.Name] = m.Material;
            foreach (var t in Textures) if (t.Texture != null) texs[t.Name] = t.Texture;
            foreach (var c in Clips) if (c.Clip != null) clips[c.Name] = c.Clip;
        }

        public Material Mat(string name)
        {
            Index();
            Material m;
            if (mats.TryGetValue(name, out m)) return m;
            Debug.LogWarning("[Art] missing material " + name);
            return mats.Count > 0 ? Materials[0].Material : null;
        }

        public bool HasMat(string name)
        {
            Index();
            return mats.ContainsKey(name);
        }

        /// <summary>색만 바꾼 복제 재질 (캐릭터 옷 색 등). 같은 이름·색이면 한 번만 만든다.</summary>
        readonly Dictionary<string, Material> tinted = new Dictionary<string, Material>();

        /// <summary>금속 재질을 덜 금속처럼 (어두운 방에서도 색이 보이고, 반짝임이 덜 번진다).</summary>
        public Material Satin(string name, float metallic = 0.35f, float smoothness = 0.45f)
        {
            string key = name + "#satin#" + metallic.ToString("F2") + smoothness.ToString("F2");
            Material m;
            if (tinted.TryGetValue(key, out m) && m != null) return m;
            m = new Material(Mat(name)) { name = name + "_satin" };
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            tinted[key] = m;
            return m;
        }

        public Material Tinted(string name, Color color, float emission = 0f)
        {
            string key = name + "#" + ColorUtility.ToHtmlStringRGBA(color) + "#" + emission.ToString("F2");
            Material m;
            if (tinted.TryGetValue(key, out m) && m != null) return m;
            m = new Material(Mat(name)) { name = name + "_tint" };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (emission > 0 && m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", color * emission);
            }
            tinted[key] = m;
            return m;
        }

        public Texture2D Tex(string name)
        {
            Index();
            Texture2D t;
            return texs.TryGetValue(name, out t) ? t : null;
        }

        public AudioClip Clip(string name)
        {
            Index();
            AudioClip c;
            return clips.TryGetValue(name, out c) ? c : null;
        }
    }
}
