using Project.ColorHunt;
using Project.Rendering;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Project.Flow
{
    /// <summary>
    /// F8 sky life (spec 2026-10-03): six toon clouds drifting on a ring far behind the board, a V of birds crossing the sky every
    /// Period seconds with its flap clip on a PlayableGraph, and the low-poly city band standing below the roof line (U27).
    /// Built once per scene by <see cref="Ensure"/> (the basket environment and the puzzle stage share one). Unscaled time, so
    /// slow motion leaves the sky alone; nothing here is lit by the board or crosses in front of it.
    /// </summary>
    public class SkyLife : MonoBehaviour
    {
        /// <summary>What a scene hands over (both settings assets carry these; the city band only comes from the puzzle's).</summary>
        public struct Props
        {
            public GameObject Clouds, Birds, CityBand;
            public AnimationClip BirdFlap;
            public Material ToonTemplate;
        }

        public const int CloudCountDefault = 6;
        public const float CloudRadius = 92f, CloudSpeed = 0.5f, CloudScale = 1.6f;
        public const float BirdFirstPass = 4f, BirdPeriod = 22f, BirdPassTime = 9f, BirdScale = 5f;
        public const float BirdHeight = 14f, BirdDepth = 42f, BirdSpan = 64f, CityDrop = 12f;

        [SerializeField] private Vector3 _centre;
        [SerializeField] private float _groundY;
        [SerializeField] private bool _city;
        private Transform[] _clouds = new Transform[0];
        private Transform _birds;
        private PlayableGraph _graph;
        private float _t, _passStart = float.NegativeInfinity;
        private bool _forcedPass;

        public int CloudCount => _clouds.Length;
        public Transform Cloud(int i) => i >= 0 && i < _clouds.Length ? _clouds[i] : null;
        public Transform Birds => _birds;
        public Transform City { get; private set; }
        public bool BirdsFlapping => _graph.IsValid() && _graph.IsPlaying();

        /// <summary>
        /// The scene's one SkyLife, built now if missing (idempotent). A later caller that asks for the city (the puzzle stage, after the
        /// basket environment built the clouds first in PotaFlow) adds it to the existing sky.
        /// </summary>
        public static SkyLife Ensure(Transform parent, Props props, Vector3 centre, float groundY, bool city)
        {
            var existing = FindAnyObjectByType<SkyLife>();
            if (existing != null)
            {
                if (city && existing.City == null) existing.AddCity(props, centre, groundY);
                return existing;
            }
            var go = new GameObject("SkyLife");
            go.transform.SetParent(parent, false);
            var sky = go.AddComponent<SkyLife>();
            sky.Build(props, centre, groundY, city);
            return sky;
        }

        public void Build(Props props, Vector3 centre, float groundY, bool city)
        {
            _centre = centre;
            _groundY = groundY;
            _city = city;
            BuildClouds(props);
            BuildBirds(props);
            if (city) BuildCity(props);
        }

        private void BuildClouds(Props props)
        {
            if (props.Clouds == null) return;
            var toon = RuntimeMaterial.From(props.ToonTemplate, "Project/Toon", "SkyCloud");
            toon.SetColor("_BaseColor", Color.white);
            toon.SetColor("_ShadeColor", new Color(0.78f, 0.85f, 0.95f));
            toon.SetFloat("_RimStrength", 0.15f);
            toon.SetFloat("_OutlineWidth", 0f);
            var set = Instantiate(props.Clouds, transform);
            set.name = "CloudSet";
            var shapes = set.GetComponentsInChildren<MeshFilter>(true);
            _clouds = new Transform[CloudCountDefault];
            for (int i = 0; i < _clouds.Length; i++)
            {
                var src = shapes[i % shapes.Length];
                var go = new GameObject("Cloud_" + i, typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(transform, false);
                go.GetComponent<MeshFilter>().sharedMesh = src.sharedMesh;
                var r = go.GetComponent<MeshRenderer>();
                r.sharedMaterial = toon;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                float k = CloudScale * (0.8f + 0.4f * ((i * 7) % 5) / 4f);
                go.transform.localScale = Vector3.one * k;
                _clouds[i] = go.transform;
            }
            set.SetActive(false);
            Place(0f);
        }

        private void BuildBirds(Props props)
        {
            if (props.Birds == null) return;
            var flock = Instantiate(props.Birds, transform);
            flock.name = "BirdFlock";
            flock.transform.localScale = Vector3.one * BirdScale;
            var ink = RuntimeMaterial.From(props.ToonTemplate, "Project/Toon", "SkyBird");
            ink.SetColor("_BaseColor", new Color(0.16f, 0.2f, 0.32f));
            ink.SetColor("_ShadeColor", new Color(0.6f, 0.62f, 0.7f));
            ink.SetFloat("_OutlineWidth", 0f);
            foreach (var r in flock.GetComponentsInChildren<Renderer>(true))
            {
                r.sharedMaterial = ink;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            _birds = flock.transform;
            var animator = flock.GetComponentInChildren<Animator>();
            if (animator != null && props.BirdFlap != null)
            {
                _graph = PlayableGraph.Create("SkyLife.flap");
                _graph.SetTimeUpdateMode(DirectorUpdateMode.UnscaledGameTime);
                var output = AnimationPlayableOutput.Create(_graph, "flap", animator);
                var clip = AnimationClipPlayable.Create(_graph, props.BirdFlap);
                output.SetSourcePlayable(clip);
                _graph.Play();
            }
            flock.SetActive(false);
        }

        /// <summary>Adds the city band around <paramref name="centre"/> to a sky built without one.</summary>
        public void AddCity(Props props, Vector3 centre, float groundY)
        {
            if (City != null) return;
            _groundY = groundY;
            var keep = _centre;
            _centre = centre;
            BuildCity(props);
            _centre = keep;
            _city = City != null;
        }

        private void BuildCity(Props props)
        {
            if (props.CityBand == null) return;
            var city = Instantiate(props.CityBand, transform);
            city.name = "CityBand";
            city.transform.position = new Vector3(_centre.x, _groundY - CityDrop, _centre.z);
            foreach (var r in city.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                var m = r.sharedMaterial;
                if (m != null && m.name.Contains("glass") && m.HasProperty("_EmissionColor"))
                {
                    var glow = new Material(m);
                    glow.EnableKeyword("_EMISSION");
                    glow.SetColor("_EmissionColor", new Color(0.9f, 0.95f, 1f) * 0.15f);
                    r.sharedMaterial = glow;
                }
            }
            City = city.transform;
        }

        /// <summary>Starts a bird pass now (the recorder and tests; the sky otherwise passes every BirdPeriod).</summary>
        public void StartBirdPass()
        {
            _passStart = _t;
            _forcedPass = true;
        }

        private void Update()
        {
            _t += Time.unscaledDeltaTime;
            Place(_t);
        }

        private void Place(float t)
        {
            for (int i = 0; i < _clouds.Length; i++)
                _clouds[i].position = _centre + new Vector3(0f, 18f + 4f * (i % 3), 0f) + FeelCurves.CloudPose(i, t, _clouds.Length, CloudRadius, CloudSpeed);
            if (_birds == null) return;
            float u = _forcedPass ? Mathf.Clamp01((t - _passStart) / BirdPassTime) : FeelCurves.BirdPass(t, BirdFirstPass, BirdPeriod, BirdPassTime);
            if (_forcedPass && t - _passStart >= BirdPassTime) _forcedPass = false;
            bool flying = _forcedPass || u >= 0f;
            if (_birds.gameObject.activeSelf != flying) _birds.gameObject.SetActive(flying);
            if (!flying) return;
            float x = Mathf.Lerp(-BirdSpan * 0.5f, BirdSpan * 0.5f, u);
            _birds.position = _centre + new Vector3(x, BirdHeight + Mathf.Sin(u * Mathf.PI * 3f) * 1.2f, BirdDepth);
            _birds.rotation = Quaternion.Euler(0f, 90f, 0f);                 // the flock faces +z after import; fly along +x
        }

        private void OnDestroy()
        {
            if (_graph.IsValid()) _graph.Destroy();
        }
    }
}
