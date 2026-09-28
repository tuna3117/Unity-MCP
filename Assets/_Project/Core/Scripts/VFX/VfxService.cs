using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace Project.VFX
{
    /// <summary>
    /// One-line effect playback with pooling:  VfxService.Play("hit", position, rotation).
    /// Put one VfxService in the scene and assign a <see cref="VfxLibrary"/> in the Inspector.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class VfxService : MonoBehaviour
    {
        public static VfxService Instance { get; private set; }

        [SerializeField] private VfxLibrary library;

        private readonly Dictionary<string, ObjectPool<VfxInstance>> _pools = new Dictionary<string, ObjectPool<VfxInstance>>();
        private readonly Dictionary<string, VfxDefinition> _definitions = new Dictionary<string, VfxDefinition>();
        private Transform _poolRoot;

        public VfxLibrary Library => library;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            _poolRoot = new GameObject("VFX Pool").transform;
            _poolRoot.SetParent(transform, false);
            if (library == null)
            {
                Debug.LogWarning("[VfxService] No VfxLibrary assigned; effects will not play.", this);
                return;
            }
            foreach (var def in library.effects)
            {
                if (def == null || string.IsNullOrEmpty(def.id) || def.prefab == null) continue;
                _definitions[def.id] = def;
                var pool = CreatePool(def);
                _pools[def.id] = pool;
                for (int i = 0; i < def.prewarmCount; i++) pool.Release(pool.Get());
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Plays an effect by id. Returns the instance (or null if unknown/no service).</summary>
        public static VfxInstance Play(string id, Vector3 position, Quaternion rotation, Transform parent = null)
        {
            if (Instance == null)
            {
                Debug.LogWarning($"[VfxService] No VfxService in scene; cannot play '{id}'.");
                return null;
            }
            return Instance.PlayInternal(id, position, rotation, parent);
        }

        public static VfxInstance Play(string id, Vector3 position) => Play(id, position, Quaternion.identity);

        public bool Has(string id) => _pools.ContainsKey(id);

        private VfxInstance PlayInternal(string id, Vector3 position, Quaternion rotation, Transform parent)
        {
            if (!_pools.TryGetValue(id, out var pool))
            {
                Debug.LogWarning($"[VfxService] Unknown effect id '{id}'.");
                return null;
            }
            var def = _definitions[id];
            var instance = pool.Get();
            var t = instance.transform;
            t.SetParent(parent != null ? parent : _poolRoot, false);
            t.SetPositionAndRotation(position, rotation);
            t.localScale = Vector3.one * def.scale;
            instance.Play(def.lifetime);
            return instance;
        }

        private ObjectPool<VfxInstance> CreatePool(VfxDefinition def)
        {
            ObjectPool<VfxInstance> pool = null;
            pool = new ObjectPool<VfxInstance>(
                createFunc: () =>
                {
                    var go = Instantiate(def.prefab, _poolRoot);
                    go.name = def.prefab.name;
                    var inst = go.GetComponent<VfxInstance>();
                    if (inst == null) inst = go.AddComponent<VfxInstance>();
                    inst.Initialize(i => pool.Release(i));
                    go.SetActive(false);
                    return inst;
                },
                actionOnGet: null,
                actionOnRelease: inst =>
                {
                    inst.gameObject.SetActive(false);
                    inst.transform.SetParent(_poolRoot, false);
                },
                actionOnDestroy: inst => { if (inst != null) Destroy(inst.gameObject); },
                collectionCheck: false,
                defaultCapacity: Mathf.Max(1, def.prewarmCount),
                maxSize: Mathf.Max(1, def.maxPoolSize));
            return pool;
        }
    }
}
