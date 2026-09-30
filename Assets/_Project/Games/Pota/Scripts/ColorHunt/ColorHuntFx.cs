using System;
using Project.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.ColorHunt
{
    /// <summary>
    /// Pota's burst layers on particle systems (patlamaEfekt.ts balon kipi, efekt.ts Parcaciklar, yapbozSahne.ts puf/konfeti): white and
    /// tone flash, ring, glossy flat shards, four-point star sparks, one point light, an additive puff sphere, hot points and paper
    /// confetti. Every particle is emitted with Pota's own formulas (<see cref="PotaFx"/>) at board-cell positions (Pota's tahtaGrup),
    /// so sizes follow the grid scale. Game time throughout: a hit-stop dip freezes them too.
    /// </summary>
    public class ColorHuntFx : MonoBehaviour
    {
        /// <summary>Additive materials multiply by this: 8-bit vertex colours carry Pota's value ÷ 2 (flash colours reach 1.8).</summary>
        public const float Headroom = 2f;
        /// <summary>Shard material colour ×1.35 (the lightest shards are tone·1.35).</summary>
        public const float ShardHeadroom = 1.35f;

        public ColorHuntSettings S;

        private ParticleSystem _flashWhite, _flashTone, _ring, _stars, _points, _puff, _shards, _confetti;
        private Light _light;
        private float _lightT, _lightTime, _lightPeak;
        private Transform _board;
        private float _s = 1f;
        private readonly System.Random _rng = new System.Random();
        private bool _built;

        public void Build()
        {
            if (_built) return;
            _built = true;
            var disc = Additive(S.SoftDisc, Headroom);
            _flashWhite = System("FlashWhite", disc, ParticleSystemRenderMode.Billboard, 12, 8);
            SizeOverLife(_flashWhite, u => Mathf.Lerp(0.5f / 1.9f, 1f, PotaFx.Grow(u)));
            AlphaOverLife(_flashWhite, u => 1f - u);
            _flashTone = System("FlashTone", disc, ParticleSystemRenderMode.Billboard, 12, 8);
            SizeOverLife(_flashTone, u => Mathf.Lerp(0.4f / 1.4f, 1f, PotaFx.Grow(u)));
            AlphaOverLife(_flashTone, u => 1f - u);
            _ring = System("Ring", Additive(S.SoftRing, Headroom), ParticleSystemRenderMode.Billboard, 12, 8);
            // 0.3 → ring size; the start ratio is depth 0's (a deeper ring starts ≤ 0.2 larger for one frame).
            SizeOverLife(_ring, u => Mathf.Lerp(0.3f / 3.4f, 1f, PotaFx.Grow(u)));
            AlphaOverLife(_ring, u => Mathf.Sqrt(Mathf.Max(0f, 1f - u)));
            _stars = System("Stars", Additive(S.StarSprite, Headroom), ParticleSystemRenderMode.Billboard, 72, 9);
            SizeOverLife(_stars, PotaFx.StarSize, 64);
            AlphaOverLife(_stars, a => 1f - a * a);
            var drag = _stars.limitVelocityOverLifetime;           // v *= 1 − 4·dt
            drag.enabled = true;
            drag.limit = 1000f;
            drag.drag = 4f;
            drag.multiplyDragByParticleSize = false;
            drag.multiplyDragByParticleVelocity = false;
            _points = System("Points", Additive(PotaFx.PointSprite(64), 1.6f), ParticleSystemRenderMode.Billboard, 160, 6);
            SizeOverLife(_points, a => 0.6f + 0.4f * (1f - a));
            AlphaOverLife(_points, a => 1f - a);
            _puff = System("Puff", Additive(Texture2D.whiteTexture, 1f), ParticleSystemRenderMode.Mesh, 16, 5);
            _puff.GetComponent<ParticleSystemRenderer>().mesh = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
            SizeOverLife(_puff, a => 0.4f + 1.5f * a);
            AlphaOverLife(_puff, a => 0.9f * (1f - a));
            _shards = System("Shards", LitMaterial(S.ParticleLitTemplate, 0.8f, 0f, ShardHeadroom), ParticleSystemRenderMode.Mesh, 160, 7);
            _shards.GetComponent<ParticleSystemRenderer>().mesh = PotaFx.Icosphere(0.62f);
            SizeOverLife(_shards, PotaFx.ShardScale);
            _confetti = System("Confetti", LitMaterial(S.ParticleLitTemplate, 0.55f, 0.05f, 1f), ParticleSystemRenderMode.Mesh, 240, 7);
            _confetti.GetComponent<ParticleSystemRenderer>().mesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            var lightGo = new GameObject("BurstLight");
            lightGo.transform.SetParent(transform, false);
            _light = lightGo.AddComponent<Light>();
            _light.type = LightType.Point;
            _light.shadows = LightShadows.None;
            _light.intensity = 0f;
            _light.enabled = false;
            SetBoard(_board);
        }

        /// <summary>The board the effects live on (Pota's tahtaGrup, olcekAyarla): cell positions, grid scale for sizes, gravity, light range.</summary>
        public void SetBoard(Transform board)
        {
            _board = board;
            _s = board != null ? board.lossyScale.x : 1f;
            if (!_built) return;
            Gravity(_shards, 12f * _s);
            Gravity(_stars, 2.5f * _s);
            Gravity(_points, 6f);            // Parcaciklar lives in the unscaled root
            Gravity(_confetti, 2.2f);        // konfeti divides by the scale: world units
            _light.range = 6f * _s;          // ISIK_MENZIL · s
        }

        /// <summary>renkAviFaz.ts patlamaKatmanlari → patlat: glow (flashes, ring, light) + shards + star sparks.</summary>
        public void Burst(Vector2 cell, Color tone, int depth)
        {
            if (!_built) return;
            var c = tone.linear;
            Glow(cell, c, S.FlashSize + depth * S.FlashSizePerDepth, 3.4f + depth * 0.5f, S.LightFlashIntensity * (1f + depth * 0.25f), S.LightFlashTime);
            float z = PotaLook.FrontZ + 0.3f;
            int n = PotaFx.ShardCount(S, depth);
            for (int i = 0; i < n; i++)
            {
                var p = PotaFx.NewShard(_rng, i, depth);
                var col = c * (p.Tone / ShardHeadroom);
                col.a = 1f;
                _shards.Emit(new ParticleSystem.EmitParams
                {
                    position = World(new Vector3(cell.x + p.Offset.x, cell.y + p.Offset.y, z)),
                    velocity = WorldVector(p.Velocity),
                    startSize3D = p.Size * _s,
                    rotation3D = p.Rotation * Mathf.Rad2Deg,
                    angularVelocity3D = p.Spin * Mathf.Rad2Deg,
                    startLifetime = p.Life,
                    startColor = col,
                    applyShapeToPosition = false,
                }, 1);
            }
            int stars = PotaFx.StarCount(depth);
            for (int i = 0; i < stars; i++)
            {
                var st = PotaFx.NewStar(_rng, i);
                var col = st.Warm ? new Color(1.6f, 1.5f, 1.1f) : new Color(0.6f + c.r * 1.2f, 0.6f + c.g * 1.2f, 0.6f + c.b * 1.2f);
                col /= Headroom;
                col.a = 1f;
                _stars.Emit(new ParticleSystem.EmitParams
                {
                    position = World(new Vector3(cell.x, cell.y, z + 0.25f + st.ZOffset)),
                    velocity = WorldVector(st.Velocity),
                    startSize = st.Size * _s,
                    rotation = st.Rotation * Mathf.Rad2Deg,
                    angularVelocity = 2f / st.Life * Mathf.Rad2Deg,          // donus + 2·age/life
                    startLifetime = st.Life,
                    startColor = col,
                    applyShapeToPosition = false,
                }, 1);
            }
        }

        /// <summary>patlamaEfekt.ts parla: white flash 0.16 s (0.5F → 1.9F), tone flash 0.22 s (0.4F → 1.4F), ring 0.38 s, one point light.</summary>
        public void Glow(Vector2 cell, Color lin, float flash, float ring, float light, float lightTime)
        {
            if (!_built) return;
            float z = PotaLook.FrontZ + 0.3f;
            Emit1(_flashWhite, World(new Vector3(cell.x, cell.y, z)), flash * 1.9f * _s, 0.16f, new Color(1.7f, 1.7f, 1.6f) / Headroom);
            Emit1(_flashTone, World(new Vector3(cell.x, cell.y, z + 0.01f)), flash * 1.4f * _s, 0.22f, lin * (1.8f / Headroom));
            Emit1(_ring, World(new Vector3(cell.x, cell.y, z + 0.02f)), ring * _s, 0.38f,
                new Color(0.5f + lin.r * 1.2f, 0.5f + lin.g * 1.2f, 0.5f + lin.b * 1.2f) / Headroom);
            if (light <= 0f) return;
            _light.color = new Color(0.35f + lin.r * 0.65f, 0.35f + lin.g * 0.65f, 0.35f + lin.b * 0.65f).gamma;   // Light.color is sRGB
            _light.transform.position = World(new Vector3(cell.x, cell.y, z + 1.9f));
            // Pota keeps the stronger of a running and a new flash; ×s^1.6 compensates the scaled group (olcekAyarla). URP has no
            // decay exponent: LightIntensityScale maps three's 1/d^1.6 (÷π) onto URP's 1/d² at the block plane.
            _lightPeak = Mathf.Max(_lightT < _lightTime ? _lightPeak : 0f, light * Mathf.Pow(_s, 1.6f) * S.LightIntensityScale);
            _lightTime = lightTime;
            _lightT = 0f;
            _light.enabled = true;
            _light.intensity = _lightPeak;
        }

        /// <summary>yapbozSahne.ts parcala → efekt.ts Parcaciklar.patlat: hot additive points from the front plane (root units).</summary>
        public void Particles(Vector2 cell, Color tone, int count, float speed, float size)
        {
            if (!_built || count <= 0) return;
            var c = tone.linear;
            c.a = 1f;
            var cam = Camera.main;
            float px = cam != null ? cam.pixelHeight : 1920f, fov = cam != null ? cam.fieldOfView : PotaLook.Fov;
            var pos = World(new Vector3(cell.x, cell.y, PotaLook.FrontZ));
            for (int i = 0; i < count; i++)
            {
                var p = PotaFx.NewPoint(_rng, speed * _s, size * _s);
                _points.Emit(new ParticleSystem.EmitParams
                {
                    position = pos,
                    velocity = new Vector3(p.Velocity.x, p.Velocity.y, -p.Velocity.z),
                    startSize = PotaFx.PointWorldSize(p.Size, fov, px),
                    startLifetime = p.Life,
                    startColor = c,
                    applyShapeToPosition = false,
                }, 1);
            }
        }

        /// <summary>yapbozSahne.ts pufPatlat: additive white sphere r 0.45, (0.4 + 1.5o)·strength over 0.36 s, 0.9 → 0, on the front plane.</summary>
        public void Puff(Vector2 cell, float strength)
        {
            if (!_built) return;
            Emit1(_puff, World(new Vector3(cell.x, cell.y, PotaLook.FrontZ)), 0.9f * strength * _s, 0.36f, Color.white);
        }

        /// <summary>yapbozSahne.ts konfeti → patlamaEfekt.ts konfeti: paper boxes from above the frame across n + 1, world-sized, 2–3.2 s.</summary>
        public void Confetti(int n, int count)
        {
            if (!_built) return;
            float top = n * 0.5f + PotaLook.Edge + 0.8f, width = n + 1f;
            for (int i = 0; i < count; i++)
            {
                var c = PotaLook.Confetti[i % PotaLook.Confetti.Length].linear;
                c.a = 1f;
                _confetti.Emit(new ParticleSystem.EmitParams
                {
                    position = World(new Vector3((R() - 0.5f) * width, top + R() * 1.5f, PotaLook.FrontZ + 0.2f + R() * 1.2f)),
                    velocity = new Vector3((R() - 0.5f) * 2.5f, -0.5f - R() * 1.5f, -(R() - 0.5f) * 0.6f),
                    startSize3D = new Vector3(0.22f, 0.14f, 0.03f),
                    rotation3D = new Vector3(R(), R(), R()) * 360f,
                    angularVelocity3D = new Vector3((R() - 0.5f) * 10f, (R() - 0.5f) * 10f, (R() - 0.5f) * 5f) * Mathf.Rad2Deg,
                    startLifetime = 2f + R() * 1.2f,
                    startColor = c,
                    applyShapeToPosition = false,
                }, 1);
            }
        }

        /// <summary>patlamaEfekt.ts temizle (new board).</summary>
        public void Clear()
        {
            if (!_built) return;
            foreach (var ps in new[] { _flashWhite, _flashTone, _ring, _stars, _points, _puff, _shards, _confetti }) ps.Clear();
            _lightPeak = 0f;
            _light.intensity = 0f;
            _light.enabled = false;
        }

        private void Update()
        {
            if (!_built || _lightPeak <= 0f) return;
            _lightT += Time.deltaTime;
            float u = Mathf.Min(1f, _lightT / Mathf.Max(0.01f, _lightTime));
            _light.intensity = _lightPeak * (1f - u) * (1f - u);
            if (u >= 1f) { _lightPeak = 0f; _light.intensity = 0f; _light.enabled = false; }
        }

        private float R() => (float)_rng.NextDouble();

        /// <summary>Pota board point (+z toward the camera) → world.</summary>
        private Vector3 World(Vector3 pota)
        {
            var local = new Vector3(pota.x, pota.y, -pota.z);
            return _board != null ? _board.TransformPoint(local) : transform.TransformPoint(local);
        }

        private Vector3 WorldVector(Vector3 pota)
        {
            var local = new Vector3(pota.x, pota.y, -pota.z);
            return _board != null ? _board.TransformVector(local) : local;
        }

        private static void Emit1(ParticleSystem ps, Vector3 pos, float size, float life, Color color)
        {
            color.a = 1f;
            ps.Emit(new ParticleSystem.EmitParams { position = pos, startSize = size, startLifetime = life, startColor = color, applyShapeToPosition = false }, 1);
        }

        private static void Gravity(ParticleSystem ps, float g)
        {
            var main = ps.main;
            main.gravityModifier = g / Mathf.Max(0.01f, -Physics.gravity.y);
        }

        private ParticleSystem System(string name, Material mat, ParticleSystemRenderMode mode, int max, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);        // AddComponent starts playing (CLAUDE.md lesson)
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = max;
            main.startColor = Color.white;
            main.useUnscaledTime = false;
            var em = ps.emission; em.enabled = false;
            var sh = ps.shape; sh.enabled = false;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.renderMode = mode;
            r.sortingOrder = order;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.applyActiveColorSpace = false;                                         // vertex colours are Pota's linear values
            r.alignment = mode == ParticleSystemRenderMode.Mesh ? ParticleSystemRenderSpace.World : ParticleSystemRenderSpace.View;
            ps.Play();
            return ps;
        }

        private Material Additive(Texture tex, float intensity)
        {
            var m = RuntimeMaterial.From(S.AdditiveTemplate, "Project/PotaAdditive");
            m.SetTexture("_BaseMap", tex != null ? tex : Texture2D.whiteTexture);
            m.SetFloat("_Intensity", intensity);
            return m;
        }

        /// <summary>URP Particles/Lit (Pota MeshStandard: shards roughness 0.2, confetti 0.45 / metalness 0.05), colour ×<paramref name="scale"/>.</summary>
        public static Material LitMaterial(Material template, float smoothness, float metallic, float scale)
        {
            var m = RuntimeMaterial.From(template, "Universal Render Pipeline/Particles/Lit");
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", metallic);
            m.SetColor("_BaseColor", new Color(scale, scale, scale, 1f).gamma);   // a Color property reaches the shader as .linear (even via SetVector)
            return m;
        }

        private static void SizeOverLife(ParticleSystem ps, Func<float, float> f, int keys = 24)
        {
            var curve = new AnimationCurve();
            for (int i = 0; i <= keys; i++) curve.AddKey(i / (float)keys, f(i / (float)keys));
            for (int i = 0; i < curve.length; i++) curve.SmoothTangents(i, 0f);
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, curve);
        }

        private static void AlphaOverLife(ParticleSystem ps, Func<float, float> f)
        {
            var alphas = new GradientAlphaKey[8];
            for (int i = 0; i < 8; i++) alphas[i] = new GradientAlphaKey(Mathf.Clamp01(f(i / 7f)), i / 7f);
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, alphas);
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(g);
        }
    }
}
