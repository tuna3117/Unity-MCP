using NUnit.Framework;
using Project.ColorHunt;
using UnityEngine;

namespace Project.Tests
{
    public class LabelAtlasTests
    {
        [Test]
        public void Numbers_ClampTo1To99()
        {
            Assert.AreEqual(0, LabelAtlas.NumberCell(1));
            Assert.AreEqual(6, LabelAtlas.NumberCell(7));
            Assert.AreEqual(98, LabelAtlas.NumberCell(99));
            Assert.AreEqual(98, LabelAtlas.NumberCell(150), "yapbozSahne.ts rakamHucresi: above 99 shows 99");
            Assert.AreEqual(0, LabelAtlas.NumberCell(0));
            Assert.AreEqual(0, LabelAtlas.NumberCell(-3));
        }

        [Test]
        public void ItemCells_FollowTheNumbers()
        {
            Assert.AreEqual(100, LabelAtlas.ItemCell(0));
            Assert.AreEqual(104, LabelAtlas.ItemCell(4));
        }

        [Test]
        public void UvRect_CountsRowsFromTheTopOfTheCanvas()
        {
            const float c = 186f / 2048f;
            Assert.AreEqual(new Vector4(0f, 1f - c, c, c), LabelAtlas.UvRect(0));
            var r = LabelAtlas.UvRect(100);                                    // column 1, row 9
            Assert.AreEqual(186f / 2048f, r.x, 1e-6f);
            Assert.AreEqual(1f - (9f * 186f + 186f) / 2048f, r.y, 1e-6f);
            Assert.AreEqual(c, r.z, 1e-6f);
        }
    }
}
