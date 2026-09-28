using System;
using UnityEngine;

namespace Project.VFX
{
    /// <summary>
    /// Attached to every spawned effect. Restarts its particle systems on play and hands itself
    /// back to the pool when the effect is finished (lifetime elapsed or all systems stopped).
    /// </summary>
    public class VfxInstance : MonoBehaviour
    {
        private ParticleSystem[] _systems;
        private Action<VfxInstance> _release;
        private float _lifetime;
        private float _elapsed;
        private bool _playing;

        internal void Initialize(Action<VfxInstance> release)
        {
            _release = release;
            _systems = GetComponentsInChildren<ParticleSystem>(true);
        }

        internal void Play(float lifetime)
        {
            _lifetime = lifetime;
            _elapsed = 0f;
            _playing = true;
            gameObject.SetActive(true);
            for (int i = 0; i < _systems.Length; i++)
            {
                _systems[i].Clear(true);
                _systems[i].Play(true);
            }
        }

        public void Stop()
        {
            if (!_playing) return;
            _playing = false;
            for (int i = 0; i < _systems.Length; i++) _systems[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            gameObject.SetActive(false);
            _release?.Invoke(this);
        }

        private void Update()
        {
            if (!_playing) return;
            _elapsed += Time.deltaTime;
            bool finished = _lifetime > 0f ? _elapsed >= _lifetime : !AnyAlive();
            if (finished) Stop();
        }

        private bool AnyAlive()
        {
            for (int i = 0; i < _systems.Length; i++)
                if (_systems[i].IsAlive(true)) return true;
            return false;
        }
    }
}
