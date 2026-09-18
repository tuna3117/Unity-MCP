using UnityEngine;
#if UNITY_EDITOR
using System.Reflection;
using UnityEditor;
#endif

namespace MCPForUnity.Runtime.Helpers
{
    // Part of MCP for Unity's compat-shim family. See UnityCompatShims.cs in this
    // folder for the full list of shims, the audit policy, and the reflection pattern.
    /// <summary>
    /// Version-gated wrappers for the InstanceID ↔ EntityId migration introduced in Unity 6.5
    /// and tightened in 6.6.
    ///   Forward (Object → int): <see cref="GetInstanceIDCompat"/>
    ///   Reverse (int → Object, Editor-only): <see cref="InstanceIDToObjectCompat"/>
    /// </summary>
    public static class UnityObjectIdCompat
    {
        /// <summary>
        /// Returns a session-scoped int handle for the object. On 6.5+ truncates the
        /// EntityId's underlying ulong; lossy but stable within a session and preserves
        /// the int-shaped wire format that older consumers expect. For deserialization
        /// round-trips on 6.5+, prefer the full <c>entityID</c> field.
        /// </summary>
        public static int GetInstanceIDCompat(this Object obj)
        {
            if (obj == null)
            {
                return 0;
            }

#if UNITY_6000_5_OR_NEWER
            return (int)EntityId.ToULong(obj.GetEntityId());
#else
            return obj.GetInstanceID();
#endif
        }

#if UNITY_EDITOR
        /// <summary>
        /// Resolves an int instance ID handle back to a UnityEngine.Object.
        ///   Pre-6.0  : EditorUtility.InstanceIDToObject(int)
        ///   6.0–6.5  : EditorUtility.EntityIdToObject(int)  (implicit int→EntityId cast)
        ///   6.6+     : InstanceIDToObject(int) throws NotImplementedException at runtime, so the
        ///              int handle (low 32 bits of the EntityId, see GetInstanceIDCompat) is
        ///              re-expanded by probing the small per-slot version field stored in the
        ///              high 32 bits (observed layout: 0x100 | (version &lt;&lt; 9)). If no probe
        ///              hits, fall back to a linear scan over all loaded objects.
        ///              [Project patch — see CLAUDE.md "Proje durumu".]
        /// </summary>
        public static Object InstanceIDToObjectCompat(int instanceId)
        {
#if UNITY_6000_6_OR_NEWER
            if (instanceId == 0)
            {
                return null;
            }

            ulong low = (uint)instanceId;

            // Fast path: probe candidate version values in the high word.
            for (int version = 0; version < 256; version++)
            {
                ulong high = (ulong)(0x100 | (version << 9));
                Object candidate = EditorUtility.EntityIdToObject(EntityId.FromULong((high << 32) | low));
                if (candidate != null)
                {
                    return candidate;
                }
            }

            // Slow path: exhaustive scan (handles unexpected high-word layouts).
            foreach (Object obj in Resources.FindObjectsOfTypeAll<Object>())
            {
                if (obj != null && (int)EntityId.ToULong(obj.GetEntityId()) == instanceId)
                {
                    return obj;
                }
            }

            return null;
#elif UNITY_6000_3_OR_NEWER
            return EditorUtility.EntityIdToObject(instanceId);
#else
            return EditorUtility.InstanceIDToObject(instanceId);
#endif
        }
#endif
    }
}
