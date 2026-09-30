using NUnit.Framework;
using Project.ColorHunt;
using UnityEngine;

namespace Project.Tests
{
    public class BlockViewTests
    {
        private GameObject _go;
        private ColorHuntSettings _s;

        [SetUp]
        public void SetUp()
        {
            _s = ScriptableObject.CreateInstance<ColorHuntSettings>();
            _go = new GameObject("Block");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
            Object.DestroyImmediate(_s);
        }

        private BlockView Make(Block b)
        {
            var v = _go.AddComponent<BlockView>();
            v.Init(_s, PillowBlockMesh.Create(), BlockView.BodyMaterial(), BlockView.LabelMaterial(null));
            v.SetBlock(b, 0);
            v.Tick(0f);
            return v;
        }

        private static Vector4 Prop(Transform t, string name)
        {
            var mpb = new MaterialPropertyBlock();
            t.GetComponent<Renderer>().GetPropertyBlock(mpb);
            return mpb.GetVector(name);
        }

        [Test]
        public void Number_SitsBottomRight_InFrontOfTheBulge_AndKeepsItsOffsetWhileSwelling()
        {
            var v = Make(new Block(0, 12));
            var number = _go.transform.Find("Number");
            Assert.AreEqual(0.26f, number.position.x, 1e-4f);
            Assert.AreEqual(-0.28f, number.position.y, 1e-4f);
            Assert.AreEqual(-0.59f, number.position.z, 1e-4f, "Z_ON + 0.03 + bulge 0.1, number 0.01 in front of the item");
            Assert.AreEqual(0.92f * 0.44f, number.lossyScale.x, 1e-4f);
            v.Swell = 1.24f;
            v.Tick(0f);
            Assert.AreEqual(0.26f, number.position.x, 1e-4f, "Pota does not push the corner offset out with the swell");
            Assert.AreEqual(-0.58f * 1.24f - 0.01f, number.position.z, 1e-4f);
            Assert.AreEqual(0.92f * 0.44f * 1.24f, number.lossyScale.x, 1e-4f, "labels grow with the block");
        }

        [Test]
        public void Item_IsBigAndCentredAbove()
        {
            Make(new Block(3, 5));
            var item = _go.transform.Find("Item");
            Assert.AreEqual(0f, item.position.x, 1e-4f);
            Assert.AreEqual(0.06f, item.position.y, 1e-4f);
            Assert.AreEqual(0.92f * 0.72f / 0.88f, item.lossyScale.x, 1e-4f);
            Assert.AreEqual(LabelAtlas.UvRect(103), Prop(item, "_UvRect"));
        }

        [Test]
        public void Number_AboveNinetyNine_ShowsNinetyNine()
        {
            Make(new Block(1, 150));
            Assert.AreEqual(LabelAtlas.UvRect(98), Prop(_go.transform.Find("Number"), "_UvRect"));
        }

        [Test]
        public void LabelAngle_TurnsTheCornerOffset()
        {
            var v = Make(new Block(0, 7));
            v.LabelAngle = 90f;
            v.Tick(0f);
            var number = _go.transform.Find("Number");
            Assert.AreEqual(0.28f, number.position.x, 1e-4f);
            Assert.AreEqual(0.26f, number.position.y, 1e-4f);
        }

        [Test]
        public void ShortOfAmmo_GreysTheBody_AndReddensTheNumber()
        {
            var v = Make(new Block(0, 7));
            v.Dim = true;
            v.Tick(0f);
            Assert.AreEqual((Vector4)PotaLook.BlockColor(PotaLook.Tones[0], 0f, true), Prop(_go.transform, "_BaseColor"));
            Assert.AreEqual((Vector4)PotaLook.NumberShort.linear, Prop(_go.transform.Find("Number"), "_Color"));
            Assert.AreEqual((Vector4)PotaLook.ItemShort.linear, Prop(_go.transform.Find("Item"), "_Color"));
        }

        [Test]
        public void LandingSquash_IsUniform()
        {
            var v = Make(new Block(0, 7));
            v.Squash = 1f;
            v.Tick(0f);
            Assert.Less(Vector3.Distance(Vector3.one * 0.88f, _go.transform.localScale), 1e-5f, "olcek = 1 − 0.12·carpma on every axis");
        }
    }
}
