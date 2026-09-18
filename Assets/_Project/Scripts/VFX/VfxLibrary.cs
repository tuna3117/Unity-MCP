using System.Collections.Generic;
using UnityEngine;

namespace Project.VFX
{
    /// <summary>
    /// Registry of every effect the game can play. Assign it to the VfxService in the scene.
    /// </summary>
    [CreateAssetMenu(menuName = "Project/VFX/VFX Library", fileName = "VfxLibrary")]
    public class VfxLibrary : ScriptableObject
    {
        public List<VfxDefinition> effects = new List<VfxDefinition>();

        public VfxDefinition Find(string id)
        {
            for (int i = 0; i < effects.Count; i++)
            {
                var e = effects[i];
                if (e != null && e.id == id) return e;
            }
            return null;
        }
    }
}
