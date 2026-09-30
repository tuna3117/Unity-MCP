using NUnit.Framework;
using Project.Editor.AITools;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.Tests
{
    public class PotaRoomBakerTests
    {
        private GameObject _room;

        [TearDown]
        public void TearDown()
        {
            if (_room != null) Object.DestroyImmediate(_room);
        }

        [Test]
        public void Room_IsThreesRoomEnvironment_MirroredInZ()
        {
            _room = PotaRoomBaker.BuildRoom(new Vector3(0f, -400f, 0f), 31);
            var renderers = _room.GetComponentsInChildren<MeshRenderer>();
            Assert.AreEqual(13, renderers.Length, "room + 6 boxes + 6 panels");
            foreach (var r in renderers) Assert.AreEqual(31, r.gameObject.layer, r.name);
            var room = _room.transform.Find("Room");
            Assert.Less(Vector3.Distance(room.position, new Vector3(-0.757f, -400f + 13.219f, -0.717f)), 1e-4f);
            var mat = room.GetComponent<MeshRenderer>().sharedMaterial;
            Assert.AreEqual((float)CullMode.Front, mat.GetFloat("_Cull"), "three BackSide: the inside faces the probe");
            Assert.AreEqual(900f, mat.GetFloat("_LightIntensity"));
            Assert.Less(Vector3.Distance((Vector3)mat.GetVector("_LightPos"), new Vector3(0.418f, -400f + 16.199f, -0.3f)), 1e-4f);
            var box1 = _room.transform.Find("Box1");
            Assert.AreEqual(0.195f * Mathf.Rad2Deg, Mathf.DeltaAngle(0f, box1.eulerAngles.y), 1e-3f, "three −0.195 rad about y, mirrored");
            float[] e = { 50f, 50f, 17f, 43f, 20f, 100f };
            for (int i = 0; i < 6; i++)
                Assert.AreEqual(e[i], _room.transform.Find($"Light{i + 1}").GetComponent<MeshRenderer>().sharedMaterial.GetFloat("_Emission"));
        }

        [Test]
        public void RoomShader_Exists() => Assert.IsNotNull(Shader.Find("Project/PotaRoom"));
    }
}
