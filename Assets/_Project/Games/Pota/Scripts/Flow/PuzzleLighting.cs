using Project.ColorHunt;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Project.Flow
{
    /// <summary>
    /// Blends PotaFlow's world from the approved basket lighting (f = 0) to Pota's puzzle (f = 1, saha.ts): sun, fill, trilight ambient,
    /// fog colour, sky colour and the puzzle post — exposure, bloom and vignette by weight; Neutral tonemapping is not interpolable, so
    /// it switches on at the flight's midpoint, where the camera moves fastest. The director drives <see cref="Blend"/>. URP's
    /// project-wide default volume profile carries vignette, bloom and tonemapping of its own: a "Basket Neutral" volume (weight 1 − f)
    /// switches them off so the basket looks as approved with post on.
    /// </summary>
    public class PuzzleLighting : MonoBehaviour
    {
        public Light Sun;
        public Light Fill;
        public Camera Cam;
        [Tooltip("ColorHuntSettings.PuzzleProfile: split at runtime into a tonemapping volume and a post volume.")]
        public VolumeProfile Source;

        public Volume Tone { get; private set; }
        public Volume Post { get; private set; }
        public Volume Neutral { get; private set; }
        public float Current { get; private set; } = -1f;

        private Quaternion _sunRot;
        private Color _sunColor, _ambSky, _ambEq, _ambGround, _fog, _sky;
        private float _sunI, _fillI;
        private bool _captured;

        private void Awake() => Build();

        public void Build()
        {
            if (Tone != null || Source == null) return;
            Split(Source, out var tone, out var post);
            Neutral = MakeVolume("Basket Neutral", NeutralProfile());
            Neutral.priority = -1f;                                 // blended first; the puzzle volumes act on top of it
            Tone = MakeVolume("Puzzle Tone", tone);
            Post = MakeVolume("Puzzle Post", post);
        }

        /// <summary>The effects URP's default profile turns on (vignette, bloom, tonemapping), switched off.</summary>
        public static VolumeProfile NeutralProfile()
        {
            var p = ScriptableObject.CreateInstance<VolumeProfile>();
            p.Add<Vignette>(false).intensity.Override(0f);
            p.Add<Bloom>(false).intensity.Override(0f);
            p.Add<Tonemapping>(false).mode.Override(TonemappingMode.None);
            return p;
        }

        private Volume MakeVolume(string name, VolumeProfile profile)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var v = go.AddComponent<Volume>();
            v.isGlobal = true;
            v.weight = 0f;
            v.sharedProfile = profile;
            return v;
        }

        /// <summary>Two runtime profiles: the tonemapping override alone, and everything else.</summary>
        public static void Split(VolumeProfile puzzle, out VolumeProfile tone, out VolumeProfile post)
        {
            tone = ScriptableObject.CreateInstance<VolumeProfile>();
            if (puzzle.TryGet(out Tonemapping src)) tone.Add<Tonemapping>(false).mode.Override(src.mode.value);
            post = Object.Instantiate(puzzle);
            post.Remove<Tonemapping>();
        }

        /// <summary>Remembers the basket's lighting (called on the first blend).</summary>
        public void Capture()
        {
            _captured = true;
            if (Sun != null) { _sunRot = Sun.transform.rotation; _sunColor = Sun.color; _sunI = Sun.intensity; }
            if (Fill != null) _fillI = Fill.intensity;
            _ambSky = RenderSettings.ambientSkyColor;
            _ambEq = RenderSettings.ambientEquatorColor;
            _ambGround = RenderSettings.ambientGroundColor;
            _fog = RenderSettings.fogColor;
            if (Cam != null) _sky = Cam.backgroundColor;
        }

        public void Blend(float f)
        {
            if (!_captured) Capture();
            f = Mathf.Clamp01(f);
            Current = f;
            if (Sun != null)
            {
                Sun.transform.rotation = Quaternion.Slerp(_sunRot, Quaternion.LookRotation(PotaLook.SunDirection), f);
                Sun.color = Color.Lerp(_sunColor, PotaLook.SunColor, f);
                Sun.intensity = Mathf.Lerp(_sunI, PotaLook.SunIntensity * PotaLook.ThreeToUnityLight, f);
            }
            if (Fill != null) Fill.intensity = Mathf.Lerp(_fillI, 0f, f);
            RenderSettings.ambientSkyColor = Color.Lerp(_ambSky, PotaLook.AmbientSky.gamma, f);
            RenderSettings.ambientEquatorColor = Color.Lerp(_ambEq, PotaLook.AmbientEquator.gamma, f);
            RenderSettings.ambientGroundColor = Color.Lerp(_ambGround, PotaLook.AmbientGround.gamma, f);
            RenderSettings.fogColor = Color.Lerp(_fog, PotaLook.Fog, f);
            if (Cam != null) Cam.backgroundColor = Color.Lerp(_sky, PotaLook.Sky, f);
            if (Post != null) Post.weight = f;
            if (Tone != null) Tone.weight = f >= 0.5f ? 1f : 0f;
            if (Neutral != null) Neutral.weight = 1f - f;
        }
    }
}
