using BluffAuction.Art;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace BluffAuction.Table
{
    /// <summary>
    /// 경매장 안의 물건(카드·장부·가격판) 표면에 그리는 UI. UI Toolkit 패널을 RenderTexture 에 그리고
    /// 그 텍스처를 쓰는 재질을 만든다. 화면 입력은 받지 않는다(보기 전용).
    /// </summary>
    public sealed class WorldPanel
    {
        public readonly RenderTexture RT;
        public readonly VisualElement Root;
        public readonly Material Material;
        readonly GameObject host;

        public WorldPanel(Transform parent, string name, int width, int height, Color clear, float glow = 0.3f)
        {
            RT = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { name = name, useMipMap = true, autoGenerateMips = true, anisoLevel = 4 };
            RT.Create();
            host = new GameObject(name + "Panel");
            host.transform.SetParent(parent, false);
            host.SetActive(false);
            var main = ArtLibrary.I.Panel;
            var ps = ScriptableObject.CreateInstance<PanelSettings>();
            ps.name = name;
            ps.themeStyleSheet = main.themeStyleSheet;
            ps.textSettings = main.textSettings;
            ps.targetTexture = RT;
            ps.scaleMode = PanelScaleMode.ConstantPixelSize;
            ps.scale = 1f;
            ps.clearColor = true;
            ps.colorClearValue = clear;
            ps.sortingOrder = -10;
            ps.SetScreenToPanelSpaceFunction(p => new Vector2(float.NaN, float.NaN));
            var doc = host.AddComponent<UIDocument>();
            doc.panelSettings = ps;
            host.SetActive(true);
            Root = doc.rootVisualElement;
            if (ArtLibrary.I.Style != null) Root.styleSheets.Add(ArtLibrary.I.Style);
            if (ArtLibrary.I.UiFont != null) Root.style.unityFontDefinition = FontDefinition.FromSDFFont(ArtLibrary.I.UiFont);
            Root.AddToClassList("root");
            Root.AddToClassList("world-panel");
            Root.pickingMode = PickingMode.Ignore;

            Material = new Material(ArtLibrary.I.Mat("Glow")) { name = name + "Mat" };
            Material.SetTexture("_BaseMap", RT);
            Material.SetColor("_BaseColor", new Color(0.82f, 0.82f, 0.82f, 1f));
            Material.SetTexture("_EmissionMap", RT);
            Material.SetColor("_EmissionColor", Color.white * glow);
            Material.SetFloat("_Smoothness", 0.15f);
        }

        /// <summary>패널을 붙인 평면 (앞면 -Z). size 는 m.</summary>
        public GameObject Quad(Transform parent, string name, Vector2 size)
        {
            var b = new ModelBuilder();
            b.Kit.Quad(b.M("screen", Material), size, Vector2.zero, Vector2.one);
            var go = b.Build(name, parent, false);
            go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        public void Destroy()
        {
            if (host != null) Object.Destroy(host);
            if (RT != null) RT.Release();
        }
    }
}
