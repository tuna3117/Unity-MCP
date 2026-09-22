using System.Collections.Generic;
using UnityEngine;

namespace Project.ColorHunt
{
    /// <summary>
    /// Owns the particle systems of the round and emits into them with a per-call colour, so one system serves
    /// every block colour. Layers of a block explosion: flash sprite, expanding ring, 3D shards in the block's
    /// colour, hot sparks, a short point-light flash. Also: landing dust, ball-hit sparkle, win confetti.
    /// </summary>
    public class ColorHuntFx : MonoBehaviour
    {
        public ColorHuntSettings S;
        public Material ItemMaterial;

        private ParticleSystem _flash, _ring, _shards, _sparks, _puff, _sparkle, _confetti;
        private readonly List<(Light light, float t0, float dur, float peak)> _lights = new List<(Light, float, float, float)>();
        private readonly Stack<Light> _lightPool = new Stack<Light>();
        private Transform _lightRoot;
        private bool _built;
        private class Flyer { public Transform T; public MeshRenderer R; public Vector3 From, To; public float T0, Dur; public System.Action Done; }
        private readonly List<Flyer> _flyers = new List<Flyer>();
        private readonly Stack<Flyer> _flyerPool = new Stack<Flyer>();
        private Transform _flyerRoot;

        public void Build(Mesh shardMesh)
        {
            if (_built) return;
            _built = true;
            var additive = ParticleMaterial(true, BoardBuilder.SoftCircle());
            var ringMat = ParticleMaterial(true, BoardBuilder.SoftCircle(128, 0.62f, 0.98f));
            var alpha = ParticleMaterial(false, BoardBuilder.SoftCircle());
            var lit = new Material(Shader.Find("Universal Render Pipeline/Particles/Lit"));
            lit.SetFloat("_Smoothness", 0.5f);
            lit.SetFloat("_Metallic", 0f);
            var quadLit = new Material(Shader.Find("Universal Render Pipeline/Particles/Lit"));
            quadLit.SetFloat("_Cull", 0f);

            _flash = System("Flash", additive, ParticleSystemRenderMode.Billboard);
            var fm = _flash.main; fm.startLifetime = 0.2f; fm.startSpeed = 0f; fm.startSize = 2.4f;
            SizeCurve(_flash, 0.5f, 1f); Fade(_flash, 1f, 0f);

            _ring = System("Ring", ringMat, ParticleSystemRenderMode.Billboard);
            var rm = _ring.main; rm.startLifetime = 0.38f; rm.startSpeed = 0f; rm.startSize = 1f;
            SizeCurve(_ring, 0.3f, 4.2f); Fade(_ring, 1f, 0f, 0.25f);

            _shards = System("Shards", lit, ParticleSystemRenderMode.Mesh);
            _shards.GetComponent<ParticleSystemRenderer>().mesh = shardMesh;
            var sm = _shards.main;
            sm.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.2f);
            sm.startSpeed = new ParticleSystem.MinMaxCurve(3.5f, 7.5f);
            sm.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.38f);
            sm.startRotation3D = true;
            sm.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            sm.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            sm.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            sm.gravityModifier = 1.4f;
            var srot = _shards.rotationOverLifetime; srot.enabled = true; srot.separateAxes = true;
            srot.x = new ParticleSystem.MinMaxCurve(-8f, 8f); srot.y = new ParticleSystem.MinMaxCurve(-8f, 8f); srot.z = new ParticleSystem.MinMaxCurve(-8f, 8f);
            var sshape = _shards.shape; sshape.enabled = true; sshape.shapeType = ParticleSystemShapeType.Cone;
            sshape.angle = 62f; sshape.radius = 0.2f; sshape.rotation = new Vector3(180f, 0f, 0f); // cone opens toward −z (the viewer)
            SizeCurve(_shards, 1f, 0.35f);
            _shards.GetComponent<ParticleSystemRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

            _sparks = System("Sparks", additive, ParticleSystemRenderMode.Stretch);
            var spm = _sparks.main;
            spm.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
            spm.startSpeed = new ParticleSystem.MinMaxCurve(5f, 11f);
            spm.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.16f);
            spm.gravityModifier = 0.8f;
            var spr = _sparks.GetComponent<ParticleSystemRenderer>(); spr.velocityScale = 0.05f; spr.lengthScale = 2.2f;
            var spshape = _sparks.shape; spshape.enabled = true; spshape.shapeType = ParticleSystemShapeType.Sphere; spshape.radius = 0.1f;
            Fade(_sparks, 1f, 0f);

            _puff = System("Puff", alpha, ParticleSystemRenderMode.Billboard);
            var pm = _puff.main;
            pm.startLifetime = new ParticleSystem.MinMaxCurve(0.28f, 0.42f);
            pm.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.1f);
            pm.startSize = new ParticleSystem.MinMaxCurve(0.16f, 0.28f);
            var pshape = _puff.shape; pshape.enabled = true; pshape.shapeType = ParticleSystemShapeType.Cone; pshape.angle = 70f; pshape.radius = 0.2f;
            pshape.rotation = new Vector3(-90f, 0f, 0f); // sideways along the landing edge
            SizeCurve(_puff, 0.6f, 1.6f); Fade(_puff, 0.55f, 0f);

            _sparkle = System("Sparkle", additive, ParticleSystemRenderMode.Billboard);
            var skm = _sparkle.main;
            skm.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.4f);
            skm.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2.6f);
            skm.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.14f);
            skm.gravityModifier = 0.6f;
            var skshape = _sparkle.shape; skshape.enabled = true; skshape.shapeType = ParticleSystemShapeType.Sphere; skshape.radius = 0.08f;
            SizeCurve(_sparkle, 1f, 0f);

            _confetti = System("Confetti", quadLit, ParticleSystemRenderMode.Mesh);
            _confetti.GetComponent<ParticleSystemRenderer>().mesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            var cm = _confetti.main;
            cm.startLifetime = new ParticleSystem.MinMaxCurve(1.8f, 3f);
            cm.startSpeed = new ParticleSystem.MinMaxCurve(1f, 3f);
            cm.startSize = new ParticleSystem.MinMaxCurve(0.14f, 0.24f);
            cm.startRotation3D = true;
            cm.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            cm.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            cm.gravityModifier = 0.35f;
            cm.maxParticles = 600;
            var crot = _confetti.rotationOverLifetime; crot.enabled = true; crot.separateAxes = true;
            crot.x = new ParticleSystem.MinMaxCurve(-6f, 6f); crot.y = new ParticleSystem.MinMaxCurve(-6f, 6f); crot.z = new ParticleSystem.MinMaxCurve(-3f, 3f);
            var cshape = _confetti.shape; cshape.enabled = true; cshape.shapeType = ParticleSystemShapeType.Box; cshape.scale = new Vector3(7f, 0.3f, 0.3f);
            var cvel = _confetti.velocityOverLifetime; cvel.enabled = true; cvel.x = new ParticleSystem.MinMaxCurve(-0.8f, 0.8f);
            Fade(_confetti, 1f, 0f, 0.8f);

            _lightRoot = new GameObject("Lights").transform;
            _lightRoot.SetParent(transform, false);
        }

        // ---------- public API ----------

        public void Explode(Vector3 pos, Color color, int depth)
        {
            var hdr = color * 1.2f; hdr.a = 1f;
            var white = new Color(2.5f, 2.5f, 2.4f, 1f);
            var front = pos + Vector3.back * 0.45f;
            Emit(_flash, front, Color.Lerp(white, hdr, 0.25f), 1, 1.3f + depth * 0.15f);
            Emit(_flash, front, hdr * 1.5f, 1, 0.8f + depth * 0.1f);
            Emit(_ring, front, Color.Lerp(white, hdr * 1.6f, 0.55f), 1, 1f + depth * 0.2f);
            int shards = Mathf.Min(S.ShardsMax, S.ShardsBase + depth * S.ShardsPerDepth);
            var shardColor = color; shardColor.a = 1f;
            Emit(_shards, pos + Vector3.back * 0.15f, shardColor, shards);
            // A few darker/lighter shards for variety.
            Emit(_shards, pos + Vector3.back * 0.15f, Color.Lerp(shardColor, Color.black, 0.25f), Mathf.Max(2, shards / 4));
            Emit(_shards, pos + Vector3.back * 0.15f, Color.Lerp(shardColor, Color.white, 0.35f), Mathf.Max(2, shards / 5));
            Emit(_sparks, pos + Vector3.back * 0.2f, Color.Lerp(white, hdr * 1.4f, 0.4f), S.SparksBase + depth * 4);
            LightFlash(pos + Vector3.back * 1.2f, color, S.LightFlashIntensity * (1f + depth * 0.25f), S.LightFlashTime);
        }

        public void Puff(Vector3 pos, float strength)
        {
            Emit(_puff, pos + Vector3.back * 0.3f, new Color(0.95f, 0.95f, 1f, 0.45f), Mathf.RoundToInt(3 + 3 * strength), 0.8f + 0.5f * strength);
        }

        public void Sparkle(Vector3 pos, Color color)
        {
            var c = Color.Lerp(color * 1.6f, Color.white * 2f, 0.4f); c.a = 1f;
            Emit(_sparkle, pos + Vector3.back * 0.35f, c, 4);
        }

        public void Confetti(Vector3 topCenter, Color[] palette, int count)
        {
            for (int i = 0; i < count; i++)
            {
                var c = palette[i % palette.Length]; c.a = 1f;
                var p = new ParticleSystem.EmitParams { position = topCenter + new Vector3(Random.Range(-3.5f, 3.5f), Random.Range(0f, 1f), Random.Range(-1f, 0.2f)), startColor = c, applyShapeToPosition = false };
                _confetti.Emit(p, 1);
            }
        }

        /// <summary>Send an item sprite from a block to a HUD point on an arc, scaling down; calls back on arrival.</summary>
        public void FlyItem(Texture2D icon, Vector3 from, Vector3 to, float duration, System.Action onArrive)
        {
            if (icon == null || ItemMaterial == null) return;
            if (_flyerRoot == null) { _flyerRoot = new GameObject("Flyers").transform; _flyerRoot.SetParent(transform, false); }
            Flyer f = _flyerPool.Count > 0 ? _flyerPool.Pop() : NewFlyer();
            f.From = from; f.To = to; f.T0 = Time.time; f.Dur = duration; f.Done = onArrive;
            var mpb = new MaterialPropertyBlock();
            mpb.SetTexture("_BaseMap", icon);
            mpb.SetColor("_BaseColor", Color.white);
            f.R.SetPropertyBlock(mpb);
            float aspect = icon.width / (float)icon.height;
            f.T.localScale = new Vector3(0.7f * Mathf.Min(1f, aspect), 0.7f * Mathf.Min(1f, 1f / aspect), 1f);
            f.T.position = from;
            f.T.gameObject.SetActive(true);
            _flyers.Add(f);
        }

        private Flyer NewFlyer()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "Flyer";
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(_flyerRoot, false);
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = ItemMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return new Flyer { T = go.transform, R = r };
        }

        // ---------- internals ----------

        private void Emit(ParticleSystem ps, Vector3 pos, Color color, int count, float sizeMul = 1f)
        {
            if (ps == null || count <= 0) return;
            var main = ps.main;
            var size = main.startSize;
            for (int i = 0; i < count; i++)
            {
                float s = size.mode == ParticleSystemCurveMode.TwoConstants ? Random.Range(size.constantMin, size.constantMax) : size.constant;
                var p = new ParticleSystem.EmitParams { position = pos, startColor = color, startSize = s * sizeMul, applyShapeToPosition = true };
                ps.Emit(p, 1);
            }
        }

        private void LightFlash(Vector3 pos, Color color, float intensity, float duration)
        {
            Light l = _lightPool.Count > 0 ? _lightPool.Pop() : NewLight();
            l.transform.position = pos;
            l.color = Color.Lerp(color, Color.white, 0.35f);
            l.intensity = intensity;
            l.enabled = true;
            _lights.Add((l, Time.time, duration, intensity));
        }

        private Light NewLight()
        {
            var go = new GameObject("Flash");
            go.transform.SetParent(_lightRoot, false);
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.range = 4.5f;
            l.shadows = LightShadows.None;
            return l;
        }

        private void Update()
        {
            for (int i = _flyers.Count - 1; i >= 0; i--)
            {
                var f = _flyers[i];
                float u = (Time.time - f.T0) / f.Dur;
                if (u >= 1f)
                {
                    f.T.gameObject.SetActive(false);
                    _flyerPool.Push(f);
                    _flyers.RemoveAt(i);
                    f.Done?.Invoke();
                    continue;
                }
                float e = u * u * (3f - 2f * u);
                var p = Vector3.Lerp(f.From, f.To, e);
                p += Vector3.up * Mathf.Sin(Mathf.PI * u) * 0.8f;
                f.T.position = p;
                float s = u < 0.2f ? Mathf.Lerp(1f, 1.35f, u / 0.2f) : Mathf.Lerp(1.35f, 0.45f, (u - 0.2f) / 0.8f);
                f.T.localScale = new Vector3(0.7f * s, 0.7f * s, 1f);
            }
            for (int i = _lights.Count - 1; i >= 0; i--)
            {
                var (l, t0, dur, peak) = _lights[i];
                float u = (Time.time - t0) / dur;
                if (u >= 1f) { l.enabled = false; _lightPool.Push(l); _lights.RemoveAt(i); continue; }
                l.intensity = peak * (1f - u) * (1f - u);
            }
        }

        private ParticleSystem System(string name, Material mat, ParticleSystemRenderMode mode)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.renderMode = mode;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 800;
            main.startColor = Color.white;
            var em = ps.emission; em.enabled = false;
            var sh = ps.shape; sh.enabled = false;
            ps.Play();
            return ps;
        }

        private static Material ParticleMaterial(bool additive, Texture2D tex)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", additive ? 2f : 0f);
            m.SetFloat("_ColorMode", 0f);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_SrcBlend", additive ? (float)UnityEngine.Rendering.BlendMode.SrcAlpha : (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", additive ? (float)UnityEngine.Rendering.BlendMode.One : (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", Color.white);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            if (additive) m.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            m.renderQueue = 3000;
            return m;
        }

        private static void SizeCurve(ParticleSystem ps, float start, float end)
        {
            var m = ps.sizeOverLifetime; m.enabled = true;
            m.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, start, 1f, end));
        }

        private static void Fade(ParticleSystem ps, float a0, float a1, float from = 0.4f)
        {
            var m = ps.colorOverLifetime; m.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(a0, 0f), new GradientAlphaKey(a0, from), new GradientAlphaKey(a1, 1f) });
            m.color = new ParticleSystem.MinMaxGradient(g);
        }
    }
}
