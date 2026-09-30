using NUnit.Framework;
using Project.ColorHunt;
using Project.Flow;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Project.Tests
{
    public class PuzzleLightingTests
    {
        private GameObject _go;
        private Color _sky, _eq, _ground, _fog;

        [SetUp]
        public void SetUp()
        {
            _sky = RenderSettings.ambientSkyColor; _eq = RenderSettings.ambientEquatorColor;
            _ground = RenderSettings.ambientGroundColor; _fog = RenderSettings.fogColor;
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            RenderSettings.ambientSkyColor = _sky; RenderSettings.ambientEquatorColor = _eq;
            RenderSettings.ambientGroundColor = _ground; RenderSettings.fogColor = _fog;
        }

        private static VolumeProfile Puzzle()
        {
            var p = ScriptableObject.CreateInstance<VolumeProfile>();
            p.Add<Tonemapping>(true).mode.value = TonemappingMode.Neutral;
            p.Add<Bloom>(true);
            p.Add<Vignette>(true);
            p.Add<ColorAdjustments>(true);
            return p;
        }

        [Test]
        public void Split_PutsOnlyTonemappingInTheToneProfile()
        {
            PuzzleLighting.Split(Puzzle(), out var tone, out var post);
            Assert.IsTrue(tone.Has<Tonemapping>());
            Assert.AreEqual(1, tone.components.Count);
            Assert.IsFalse(post.Has<Tonemapping>());
            Assert.IsTrue(post.Has<Bloom>() && post.Has<Vignette>() && post.Has<ColorAdjustments>());
        }

        [Test]
        public void Blend_GoesFromTheBasketToPotasPuzzle()
        {
            _go = new GameObject("Look");
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.transform.SetParent(_go.transform);
            sun.intensity = 1.5f; sun.color = Color.white; sun.transform.rotation = Quaternion.Euler(50f, 30f, 0f);
            var fill = new GameObject("Fill").AddComponent<Light>();
            fill.transform.SetParent(_go.transform);
            fill.intensity = 0.35f;
            var cam = new GameObject("Cam").AddComponent<Camera>();
            cam.transform.SetParent(_go.transform);
            cam.backgroundColor = Color.red;
            var look = _go.AddComponent<PuzzleLighting>();
            look.Sun = sun; look.Fill = fill; look.Cam = cam; look.Source = Puzzle();
            look.Build();
            look.Blend(0f);
            Assert.AreEqual(1.5f, sun.intensity, 1e-5f, "basket as approved");
            Assert.AreEqual(1f, look.Neutral.weight, "URP's default post is neutralised in the basket phase");
            Assert.AreEqual(0f, look.Post.weight);
            Assert.AreEqual(0f, look.Tone.weight);
            look.Blend(0.4f);
            Assert.AreEqual(0.4f, look.Post.weight, 1e-5f, "exposure, bloom, vignette blend in");
            Assert.AreEqual(0f, look.Tone.weight, "Neutral switches on at the flight's midpoint");
            look.Blend(1f);
            Assert.AreEqual(PotaLook.SunIntensity / Mathf.PI, sun.intensity, 1e-4f, "saha.ts sun 2.1, ÷π");
            Assert.Less(Quaternion.Angle(sun.transform.rotation, Quaternion.LookRotation(PotaLook.SunDirection)), 0.01f);
            Assert.AreEqual(0f, fill.intensity, 1e-5f, "Pota has no fill light");
            Assert.AreEqual(1f, look.Tone.weight);
            Assert.AreEqual(0f, look.Neutral.weight);
            Assert.AreEqual(PotaLook.Sky.b, cam.backgroundColor.b, 1e-4f);
            Assert.AreEqual(PotaLook.Fog.g, RenderSettings.fogColor.g, 1e-4f);
            look.Blend(0f);
            Assert.AreEqual(1.5f, sun.intensity, 1e-5f, "and back for the next basket round");
            Assert.AreEqual(Color.red, cam.backgroundColor);
        }
    }
}
