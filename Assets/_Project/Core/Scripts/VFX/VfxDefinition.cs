using UnityEngine;

namespace Project.VFX
{
    /// <summary>
    /// Designer-editable parameters for one visual effect. Referenced by <see cref="VfxLibrary"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "Project/VFX/VFX Definition", fileName = "VFX_New")]
    public class VfxDefinition : ScriptableObject
    {
        [Tooltip("Identifier used in code: VfxService.Play(\"hit\", ...)")]
        public string id = "new_effect";

        [Tooltip("Prefab with one or more ParticleSystems (and optionally lights/trails).")]
        public GameObject prefab;

        [Tooltip("Seconds before the instance is returned to the pool. 0 = wait until every particle system stops.")]
        [Min(0f)] public float lifetime = 0f;

        [Tooltip("Instances created up-front when the service starts.")]
        [Min(0)] public int prewarmCount = 2;

        [Tooltip("Maximum pooled (inactive) instances kept around.")]
        [Min(1)] public int maxPoolSize = 16;

        [Tooltip("Uniform scale applied to spawned instances.")]
        [Min(0.01f)] public float scale = 1f;
    }
}
