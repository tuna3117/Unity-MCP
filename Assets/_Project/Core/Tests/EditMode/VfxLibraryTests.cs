using NUnit.Framework;
using Project.VFX;
using UnityEngine;

namespace Project.Tests
{
    public class VfxLibraryTests
    {
        [Test]
        public void Find_ReturnsDefinitionById()
        {
            var library = ScriptableObject.CreateInstance<VfxLibrary>();
            var hit = ScriptableObject.CreateInstance<VfxDefinition>();
            hit.id = "hit";
            library.effects.Add(hit);

            Assert.AreSame(hit, library.Find("hit"));
            Assert.IsNull(library.Find("missing"));

            Object.DestroyImmediate(hit);
            Object.DestroyImmediate(library);
        }

        [Test]
        public void Find_SkipsNullEntries()
        {
            var library = ScriptableObject.CreateInstance<VfxLibrary>();
            library.effects.Add(null);
            Assert.IsNull(library.Find("anything"));
            Object.DestroyImmediate(library);
        }
    }
}
