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
            v.Init(_s, PillowBlockMesh.Create(), BlockView.BodyMaterial(), BlockView.LabelMaterial(null, null));
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

        [Test]
        public void Special_HidesTheNumber_WearsItsToneAndCentredSticker()
        {
            var v = Make(Block.MakeSpecial(SpecialKind.Bomb));
            Assert.IsFalse(_go.transform.Find("Number").GetComponent<MeshRenderer>().enabled, "özel top: rakam yok");
            var item = _go.transform.Find("Item");
            Assert.AreEqual(LabelAtlas.UvRect(106), Prop(item, "_UvRect"));
            Assert.AreEqual(0f, item.position.x, 1e-4f);
            Assert.AreEqual(0f, item.position.y, 1e-4f, "centred: no 0.06 lift");
            Assert.AreEqual(0.92f * 0.82f / 0.88f, item.lossyScale.x, 1e-4f, "ozelOlcek");
            Assert.AreEqual(0f, item.localEulerAngles.z, 1e-3f);
            Assert.AreEqual(PotaLook.SpecialTone(SpecialKind.Bomb), v.Tone);
            Assert.AreEqual((Vector4)PotaLook.BlockColor(PotaLook.SpecialTone(SpecialKind.Bomb), 0f, false), Prop(_go.transform, "_BaseColor"));
            Assert.AreEqual((Vector4)Color.white, Prop(item, "_Color"));
        }

        [Test]
        public void LineH_TurnsTheLineSticker90_LineVDoesNot()
        {
            Make(Block.MakeSpecial(SpecialKind.LineH));
            var item = _go.transform.Find("Item");
            Assert.AreEqual(LabelAtlas.UvRect(105), Prop(item, "_UvRect"));
            Assert.AreEqual(90f, item.localEulerAngles.z, 1e-3f, "cizgiYatay: donme + π/2");
            var v = _go.GetComponent<BlockView>();
            v.SetBlock(Block.MakeSpecial(SpecialKind.LineV), 0);
            v.Tick(0f);
            Assert.AreEqual(LabelAtlas.UvRect(105), Prop(item, "_UvRect"));
            Assert.AreEqual(0f, item.localEulerAngles.z, 1e-3f);
        }

        [Test]
        public void Special_IsNeverDimmed()
        {
            var v = Make(Block.MakeSpecial(SpecialKind.Golden));
            v.Dim = true;
            v.Tick(0f);
            Assert.AreEqual((Vector4)PotaLook.BlockColor(PotaLook.SpecialTone(SpecialKind.Golden), 0f, false), Prop(_go.transform, "_BaseColor"),
                "renkAviFaz.ts soluk: … && !g.blok.ozel");
            Assert.AreEqual((Vector4)Color.white, Prop(_go.transform.Find("Item"), "_Color"));
        }

        [Test]
        public void ANormalBlock_AfterASpecial_GetsItsNumberAndLiftBack()
        {
            var v = Make(Block.MakeSpecial(SpecialKind.Golden));
            v.SetBlock(new Block(2, 9), 0);                                    // a pooled view reused
            v.Tick(0f);
            var item = _go.transform.Find("Item");
            Assert.IsTrue(_go.transform.Find("Number").GetComponent<MeshRenderer>().enabled);
            Assert.AreEqual(LabelAtlas.UvRect(102), Prop(item, "_UvRect"));
            Assert.AreEqual(0.06f, item.position.y, 1e-4f);
            Assert.AreEqual(0.92f * 0.72f / 0.88f, item.lossyScale.x, 1e-4f);
            Assert.AreEqual(PotaLook.Tones[2], v.Tone);
        }

        [Test]
        public void Pulse_FloorsTheFlash_AfterTheDecay()
        {
            var v = Make(Block.MakeSpecial(SpecialKind.Golden));
            v.Pulse = 0.12f;
            v.Tick(0.1f);
            Assert.AreEqual(0.12f, v.Flash, 1e-6f);
            v.Flash = 0.5f;
            v.Tick(0.02f);
            Assert.AreEqual(0.5f - 0.02f * PotaLook.FlashDecay, v.Flash, 1e-6f, "a stronger flash decays as before");
            v.Pulse = 0f;
            v.Flash = 0f;
            v.Tick(0.02f);
            Assert.AreEqual(0f, v.Flash, 1e-6f);
        }

        [Test]
        public void BobAndStickerSpin_AddToTheIdlePose_AndResetStudioClearsThem()
        {
            var v = Make(Block.MakeSpecial(SpecialKind.LineH));
            v.Bob = 0.1f;
            v.StickerSpin = 30f;
            v.Tick(0f);
            var item = _go.transform.Find("Item");
            Assert.AreEqual(0.1f, _go.transform.position.y, 1e-5f);
            Assert.AreEqual(120f, item.localEulerAngles.z, 1e-3f, "90° LineH + 30° spin");
            v.ResetStudio();
            v.Tick(0f);
            Assert.AreEqual(0f, v.Bob);
            Assert.AreEqual(0f, v.StickerSpin);
            Assert.AreEqual(0f, _go.transform.position.y, 1e-5f);
            Assert.AreEqual(90f, item.localEulerAngles.z, 1e-3f);
        }

        [Test]
        public void Bounce_SquashesWideThenRests()
        {
            var v = Make(new Block(0, 5));
            v.Bounce = 0f;
            v.Tick(0f);
            Assert.Greater(v.transform.localScale.x, v.transform.localScale.y * 1.1f, "wide and flat on landing");
            v.Tick(_s.Feel.LandBounceTime + 0.01f);
            Assert.Less(v.Bounce, 0f, "the bounce is over");
            Assert.AreEqual(v.transform.localScale.x, v.transform.localScale.y, 1e-4f);
        }
    }
}
