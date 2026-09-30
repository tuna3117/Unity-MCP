using System;
using Project.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.ColorHunt
{
    /// <summary>ParticleSystem plumbing for the studio looks: world-space, game-time systems emitted by hand (no emission module).</summary>
    public static class StudioParticles
    {
        public static ParticleSystem System(Transform parent, string name, Material mat, ParticleSystemRenderMode mode, int max, int order,
            bool linearColors = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);   // AddComponent starts playing (CLAUDE.md lesson)
            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.maxParticles = max;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.useUnscaledTime = false;                                     // hit-stops and slow-mo freeze them too
            main.startSpeed = 0f;
            main.startSize3D = mode == ParticleSystemRenderMode.Mesh;
            main.startRotation3D = mode == ParticleSystemRenderMode.Mesh;
            var emission = ps.emission;
            emission.enabled = false;
            var shape = ps.shape;
            shape.enabled = false;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.renderMode = mode;
            r.sortingOrder = order;
            r.applyActiveColorSpace = !linearColors;
            r.alignment = mode == ParticleSystemRenderMode.Mesh ? ParticleSystemRenderSpace.World : ParticleSystemRenderSpace.View;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            ps.Play();
            return ps;
        }

        public static void Emit(ParticleSystem ps, Vector3 pos, float size, float life, Color color, Vector3 velocity = default,
            float rotation = 0f)
        {
            ps.Emit(new ParticleSystem.EmitParams
            {
                position = pos, velocity = velocity, startSize = size, startLifetime = life, startColor = color, rotation = rotation,
                applyShapeToPosition = false,
            }, 1);
        }

        public static void Emit3D(ParticleSystem ps, Vector3 pos, Vector3 velocity, Vector3 size, Vector3 rotation, Vector3 spin,
            float life, Color color)
        {
            ps.Emit(new ParticleSystem.EmitParams
            {
                position = pos, velocity = velocity, startSize3D = size, rotation3D = rotation, angularVelocity3D = spin,
                startLifetime = life, startColor = color, applyShapeToPosition = false,
            }, 1);
        }

        /// <summary>Plays the whole cols × rows sheet once over each particle's life.</summary>
        public static void Sheet(ParticleSystem ps, int cols, int rows)
        {
            var tsa = ps.textureSheetAnimation;
            tsa.enabled = true;
            tsa.mode = ParticleSystemAnimationMode.Grid;
            tsa.numTilesX = cols;
            tsa.numTilesY = rows;
            tsa.animation = ParticleSystemAnimationType.WholeSheet;
            tsa.frameOverTime = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0f, 1f, 1f));
            tsa.cycleCount = 1;
        }

        /// <summary>Downward acceleration in world units/s² (gravityModifier is relative to Physics.gravity 9.81).</summary>
        public static void Gravity(ParticleSystem ps, float g)
        {
            var main = ps.main;
            main.gravityModifier = g / 9.81f;
        }

        public static void SizeOverLife(ParticleSystem ps, Func<float, float> f, int keys = 24)
        {
            var curve = new AnimationCurve();
            for (int i = 0; i <= keys; i++) curve.AddKey(i / (float)keys, f(i / (float)keys));
            for (int i = 0; i < curve.length; i++) curve.SmoothTangents(i, 0f);
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, curve);
        }

        public static void AlphaOverLife(ParticleSystem ps, Func<float, float> f)
        {
            var keys = new GradientAlphaKey[8];
            for (int i = 0; i < keys.Length; i++) { float u = i / (float)(keys.Length - 1); keys[i] = new GradientAlphaKey(f(u), u); }
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, keys);
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(g);
        }

        public static Material Flipbook(ColorHuntSettings s, Texture sheet)
        {
            var m = RuntimeMaterial.From(s.FlipbookTemplate, "Project/PotaFlipbook", "PotaFlipbook");
            m.SetTexture("_BaseMap", sheet);
            return m;
        }

        public static Material Additive(ColorHuntSettings s, Texture tex, float intensity)
        {
            var m = RuntimeMaterial.From(s.AdditiveTemplate, "Project/PotaAdditive", "StudioAdditive");
            m.SetTexture("_BaseMap", tex != null ? tex : Texture2D.whiteTexture);
            m.SetFloat("_Intensity", intensity);
            return m;
        }

        public static int Count(params ParticleSystem[] systems)
        {
            int n = 0;
            foreach (var ps in systems) if (ps != null) n += ps.particleCount;
            return n;
        }
    }
}
