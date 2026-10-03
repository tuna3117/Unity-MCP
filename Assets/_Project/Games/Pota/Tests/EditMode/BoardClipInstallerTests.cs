using NUnit.Framework;
using Project.ColorHunt;
using Project.Editor.AITools;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Project.Tests
{
    /// <summary>F7: the board clip's renderer features and layer (spec 2026-10-03). Works on an in-memory renderer, not the project's.</summary>
    public class BoardClipInstallerTests
    {
        private UniversalRendererData _data;

        [SetUp]
        public void SetUp() => _data = ScriptableObject.CreateInstance<UniversalRendererData>();

        [TearDown]
        public void TearDown()
        {
            foreach (var f in _data.rendererFeatures) if (f != null) Object.DestroyImmediate(f);
            Object.DestroyImmediate(_data);
        }

        [Test]
        public void Install_AddsTwoStencilTestedPasses_AndTakesTheLayerOutOfTheDefaultPasses()
        {
            const int layer = 20;
            int added = BoardClipInstaller.Install(_data, layer);
            Assert.AreEqual(2, added);
            Assert.AreEqual(2, _data.rendererFeatures.Count);
            Assert.AreEqual(0, _data.opaqueLayerMask.value & (1 << layer), "opaque pass skips the board layer");
            Assert.AreEqual(0, _data.transparentLayerMask.value & (1 << layer), "transparent pass skips the board layer");
            foreach (var f in _data.rendererFeatures)
            {
                var ro = f as RenderObjects;
                Assert.IsNotNull(ro, "a RenderObjects feature");
                Assert.AreEqual(1 << layer, ro.settings.filterSettings.LayerMask.value);
                Assert.IsTrue(ro.settings.stencilSettings.overrideStencilState);
                Assert.AreEqual(BoardClip.StencilRef, ro.settings.stencilSettings.stencilReference);
                Assert.AreEqual(CompareFunction.Equal, ro.settings.stencilSettings.stencilCompareFunction);
            }
            Assert.AreEqual(RenderQueueType.Opaque, ((RenderObjects)_data.rendererFeatures[0]).settings.filterSettings.RenderQueueType);
            Assert.AreEqual(RenderPassEvent.AfterRenderingOpaques, ((RenderObjects)_data.rendererFeatures[0]).settings.Event);
            Assert.AreEqual(RenderQueueType.Transparent, ((RenderObjects)_data.rendererFeatures[1]).settings.filterSettings.RenderQueueType);
            // before the default transparents: the burst debris, confetti and selection ring (Default layer) then draw over the labels
            Assert.AreEqual(RenderPassEvent.BeforeRenderingTransparents, ((RenderObjects)_data.rendererFeatures[1]).settings.Event);
        }

        [Test]
        public void Install_IsIdempotent()
        {
            BoardClipInstaller.Install(_data, 20);
            int added = BoardClipInstaller.Install(_data, 20);
            Assert.AreEqual(0, added);
            Assert.AreEqual(2, _data.rendererFeatures.Count);
        }

        [Test]
        public void Install_BringsAnExistingFeatureUpToDate()
        {
            BoardClipInstaller.Install(_data, 20);
            var transparent = (RenderObjects)_data.rendererFeatures[1];
            transparent.settings.Event = RenderPassEvent.AfterRenderingTransparents;        // an older install
            Assert.AreEqual(0, BoardClipInstaller.Install(_data, 20));
            Assert.AreEqual(RenderPassEvent.BeforeRenderingTransparents, transparent.settings.Event);
        }

        [Test]
        public void MaskSize_CoversTheFrameInteriorAndTheRunBelowIt()
        {
            var size = BoardClip.MaskLocalSize(7);
            float scale = PotaLook.GridScale(7);
            Assert.AreEqual(PotaLook.PanoSize / scale, size.x, 1e-4f, "as wide as the pano's inside");
            Assert.Greater(size.y, PotaLook.PanoSize / scale, "taller: the run under the frame (golden balls) stays visible");
            Assert.AreEqual(PotaLook.PanoSize * 0.5f / scale, BoardClip.MaskLocalCentre(7).y + size.y * 0.5f, 1e-4f, "its top edge is the frame's top");
            Assert.AreEqual(-0.1f / scale, BoardClip.MaskLocalZ(7), 1e-4f, "at the frame bars' front face, so the clip line is the bar's inner edge on screen");
        }
    }
}
