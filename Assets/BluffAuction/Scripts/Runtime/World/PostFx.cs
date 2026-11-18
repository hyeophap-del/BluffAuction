using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BluffAuction.World
{
    /// <summary>후처리 (블룸·ACES·색 보정·비네트·피사계 심도·필름 그레인). 설정에서 끄고 켤 수 있다.</summary>
    public sealed class PostFx : MonoBehaviour
    {
        public Volume Volume;
        public DepthOfField Dof;
        public Bloom Bloom;
        public Vignette Vignette;
        public ColorAdjustments Color;
        public FilmGrain Grain;
        public ChromaticAberration Chroma;
        float focusTarget = 3f;
        float focusCur = 3f;

        public static PostFx Create(Transform parent)
        {
            var go = new GameObject("PostFx");
            go.transform.SetParent(parent, false);
            var fx = go.AddComponent<PostFx>();
            var vol = go.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.priority = 1;
            var prof = ScriptableObject.CreateInstance<VolumeProfile>();
            fx.Bloom = prof.Add<Bloom>(true);
            fx.Bloom.threshold.Override(1.2f);
            fx.Bloom.intensity.Override(0.6f);
            fx.Bloom.scatter.Override(0.7f);
            fx.Bloom.tint.Override(new UnityEngine.Color(1f, 0.88f, 0.72f));
            fx.Bloom.highQualityFiltering.Override(true);
            var tm = prof.Add<Tonemapping>(true);
            tm.mode.Override(TonemappingMode.ACES);
            fx.Color = prof.Add<ColorAdjustments>(true);
            fx.Color.postExposure.Override(0.55f);
            fx.Color.contrast.Override(12f);
            fx.Color.saturation.Override(4f);
            var wb = prof.Add<WhiteBalance>(true);
            wb.temperature.Override(6f);
            var lgg = prof.Add<LiftGammaGain>(true);
            lgg.lift.Override(new Vector4(1.0f, 0.97f, 0.95f, -0.02f));
            lgg.gain.Override(new Vector4(1.0f, 0.98f, 0.95f, 0.02f));
            fx.Vignette = prof.Add<Vignette>(true);
            fx.Vignette.intensity.Override(0.32f);
            fx.Vignette.smoothness.Override(0.5f);
            fx.Vignette.color.Override(new UnityEngine.Color(0.05f, 0.02f, 0.01f));
            fx.Dof = prof.Add<DepthOfField>(true);
            fx.Dof.mode.Override(DepthOfFieldMode.Bokeh);
            fx.Dof.focusDistance.Override(3f);
            fx.Dof.aperture.Override(4.5f);
            fx.Dof.focalLength.Override(45f);
            fx.Dof.bladeCount.Override(6);
            fx.Grain = prof.Add<FilmGrain>(true);
            fx.Grain.type.Override(FilmGrainLookup.Thin2);
            fx.Grain.intensity.Override(0.18f);
            fx.Grain.response.Override(0.85f);
            fx.Chroma = prof.Add<ChromaticAberration>(true);
            fx.Chroma.intensity.Override(0.06f);
            vol.sharedProfile = prof;
            fx.Volume = vol;
            return fx;
        }

        public void SetFocus(float distance) { focusTarget = Mathf.Max(0.3f, distance); }

        public void SetAperture(float f) { if (Dof != null) Dof.aperture.value = f; }

        public void SetQuality(bool dof, bool grain, bool bloom)
        {
            Dof.active = dof;
            Grain.active = grain;
            Bloom.active = bloom;
        }

        void Update()
        {
            focusCur = Mathf.Lerp(focusCur, focusTarget, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 5f));
            if (Dof != null) Dof.focusDistance.value = focusCur;
        }
    }
}
