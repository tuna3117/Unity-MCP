using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Project.VFX;
using UnityEngine;
using UnityEngine.TestTools;

namespace Project.Tests
{
    public class VfxPartPlayTests
    {
        private GameObject _service, _template;

        [TearDown]
        public void TearDown()
        {
            if (_service != null) Object.Destroy(_service);
            if (_template != null) Object.Destroy(_template);
        }

        [UnityTest]
        public IEnumerator AMeshOnlyEffect_PlaysItsLifetime_ThenGoesBackToThePool()
        {
            _template = new GameObject("burst_template");
            _template.SetActive(false);
            var burst = _template.AddComponent<MeshBurst>();
            burst.Lifetime = 0.6f;
            for (int i = 0; i < 2; i++)
            {
                var piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.Destroy(piece.GetComponent<Collider>());
                piece.transform.SetParent(_template.transform, false);
                piece.transform.localPosition = new Vector3(i == 0 ? -0.3f : 0.3f, 0f, 0f);
            }
            var def = ScriptableObject.CreateInstance<VfxDefinition>();
            def.id = "burst";
            def.prefab = _template;
            def.lifetime = 0f;                       // wait for the parts, like a delivered mesh effect
            var library = ScriptableObject.CreateInstance<VfxLibrary>();
            library.effects.Add(def);

            _service = new GameObject("vfx");
            _service.SetActive(false);
            var service = _service.AddComponent<VfxService>();
            typeof(VfxService).GetField("library", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(service, library);
            _service.SetActive(true);

            var inst = VfxService.Play("burst", Vector3.zero);
            Assert.IsNotNull(inst);
            yield return new WaitForSeconds(0.25f);
            Assert.IsTrue(inst.gameObject.activeSelf, "a particle-free effect must not end on its first frame");
            Assert.AreNotEqual(-0.3f, inst.transform.GetChild(0).localPosition.x, "pieces are moving");
            yield return new WaitForSeconds(0.6f);
            Assert.IsFalse(inst.gameObject.activeSelf, "back in the pool after its lifetime");

            var again = VfxService.Play("burst", Vector3.zero);
            Assert.AreSame(inst, again, "pooled instance reused");
            Assert.AreEqual(-0.3f, again.transform.GetChild(0).localPosition.x, 1e-4f, "replay starts from the rest pose");
        }
    }
}
