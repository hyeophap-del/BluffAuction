using BluffAuction.Art;
using UnityEngine;
using UnityEngine.Rendering;

namespace BluffAuction.Game
{
    /// <summary>입자 효과: 낙찰 색종이, 능력 사용 반짝임.</summary>
    public static class Fx
    {
        public static ParticleSystem Confetti(Transform parent)
        {
            var go = new GameObject("Confetti");
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.duration = 0.4f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.8f, 2.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.6f, 3.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.045f);
            main.startRotation3D = true;
            main.startRotationX = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
            main.startRotationY = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
            main.startRotationZ = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
            main.gravityModifier = 0.35f;
            main.maxParticles = 300;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission;
            em.rateOverTime = 0;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 160) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 35f;
            shape.radius = 0.1f;
            shape.rotation = new Vector3(-90, 0, 0);
            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.separateAxes = true;
            rot.x = new ParticleSystem.MinMaxCurve(-6f, 6f);
            rot.y = new ParticleSystem.MinMaxCurve(-6f, 6f);
            rot.z = new ParticleSystem.MinMaxCurve(-6f, 6f);
            var vel = ps.limitVelocityOverLifetime;
            vel.enabled = true;
            vel.dampen = 0.12f;
            vel.limit = 1.2f;
            var tex = ps.textureSheetAnimation;
            tex.enabled = true;
            tex.numTilesX = 2;
            tex.numTilesY = 2;
            tex.startFrame = new ParticleSystem.MinMaxCurve(0, 4);
            tex.frameOverTime = new ParticleSystem.MinMaxCurve(0);
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 0.8f), new GradientAlphaKey(0, 1) });
            col.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = ArtLibrary.I.ParticleConfetti;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.alignment = ParticleSystemRenderSpace.Local;
            r.shadowCastingMode = ShadowCastingMode.Off;
            return ps;
        }

        static Material blobMat;

        /// <summary>부드러운 동그란 방울 (반투명, 색은 입자 색) — 과즙·부스러기.</summary>
        static Material BlobMaterial()
        {
            if (blobMat != null) return blobMat;
            blobMat = new Material(ArtLibrary.I.ParticleConfetti) { name = "ParticleBlob" };
            var dot = ArtLibrary.I.Tex("soft_dot");
            if (dot != null)
            {
                if (blobMat.HasProperty("_BaseMap")) blobMat.SetTexture("_BaseMap", dot);
                blobMat.mainTexture = dot;
            }
            return blobMat;
        }

        /// <summary>맞은 자리에서 튀는 방울 (물렁한 것은 과즙, 딱딱한 것은 하얀 먼지). 저절로 사라진다.</summary>
        public static void Splash(Vector3 at, Color color, bool juicy)
        {
            var go = new GameObject("Splash");
            go.transform.position = at;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.duration = 0.2f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, juicy ? 2.2f : 1.2f);
            // 건너편 자리(2~3 m)에서도 보이는 크기
            main.startSize = new ParticleSystem.MinMaxCurve(juicy ? 0.035f : 0.022f, juicy ? 0.085f : 0.05f);
            // 어둡게 섞으면 피처럼 보이므로 밝은 쪽으로만 흔든다
            main.startColor = new ParticleSystem.MinMaxGradient(color, Color.Lerp(color, Color.white, 0.3f));
            main.gravityModifier = juicy ? 1.4f : 0.3f;
            main.maxParticles = 40;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission;
            em.rateOverTime = 0;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, juicy ? 30 : 16) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.03f;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0, 1, 1, 0.3f));
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0.9f, 0.6f), new GradientAlphaKey(0, 1) });
            col.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = BlobMaterial();
            r.shadowCastingMode = ShadowCastingMode.Off;
            ps.Play();
            Object.Destroy(go, 1.5f);
        }

        /// <summary>맞은 자리에서 한 번 튀는 부스러기 (저절로 사라진다).</summary>
        public static void Burst(Vector3 at, Color color)
        {
            var go = new GameObject("Burst");
            go.transform.position = at;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.duration = 0.3f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.03f);
            main.startColor = new ParticleSystem.MinMaxGradient(color, Color.Lerp(color, Color.white, 0.4f));
            main.gravityModifier = 1.2f;
            main.maxParticles = 40;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission;
            em.rateOverTime = 0;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 24) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.03f;
            // 색종이 그림은 2×2 칸이라 칸 하나만 골라 쓴다 (통째로 쓰면 바둑판처럼 보인다)
            var tex = ps.textureSheetAnimation;
            tex.enabled = true;
            tex.numTilesX = 2;
            tex.numTilesY = 2;
            tex.startFrame = new ParticleSystem.MinMaxCurve(0, 4);
            tex.frameOverTime = new ParticleSystem.MinMaxCurve(0);
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0, 1, 1, 0.2f));
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = ArtLibrary.I.ParticleConfetti;
            r.shadowCastingMode = ShadowCastingMode.Off;
            ps.Play();
            Object.Destroy(go, 1.5f);

            // 맞는 순간 번쩍 (부드러운 점 몇 개)
            var fgo = new GameObject("BurstFlash");
            fgo.transform.position = at;
            var fps = fgo.AddComponent<ParticleSystem>();
            fps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var fm = fps.main;
            fm.loop = false;
            fm.playOnAwake = false;
            fm.duration = 0.1f;
            fm.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.3f);
            fm.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.9f);
            fm.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.16f);
            fm.startColor = Color.Lerp(color, Color.white, 0.5f);
            fm.simulationSpace = ParticleSystemSimulationSpace.World;
            var fe = fps.emission;
            fe.rateOverTime = 0;
            fe.SetBursts(new[] { new ParticleSystem.Burst(0f, 6) });
            var fs = fps.sizeOverLifetime;
            fs.enabled = true;
            fs.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0, 1, 1, 0));
            var fr = fgo.GetComponent<ParticleSystemRenderer>();
            fr.sharedMaterial = ArtLibrary.I.ParticleAdditive;
            fr.shadowCastingMode = ShadowCastingMode.Off;
            fps.Play();
            Object.Destroy(fgo, 0.8f);
        }

        public static ParticleSystem Sparkle(Transform parent)
        {
            var go = new GameObject("Sparkle");
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.duration = 0.6f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.3f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.06f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.7f, 0.85f, 1f), new Color(1f, 0.85f, 0.45f));
            main.gravityModifier = -0.15f;
            main.maxParticles = 100;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission;
            em.rateOverTime = 0;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 40) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.25f;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0, 1, 1, 0));
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = ArtLibrary.I.ParticleAdditive;
            r.shadowCastingMode = ShadowCastingMode.Off;
            return ps;
        }
    }
}
