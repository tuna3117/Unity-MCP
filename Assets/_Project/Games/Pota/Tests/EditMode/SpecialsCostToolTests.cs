using System.Collections.Generic;
using NUnit.Framework;
using Project.Editor.AITools;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.Tests
{
    /// <summary>ai_specials_cost's arithmetic: the idle beat before the tap against the second after it, runs combined (Task 12).</summary>
    public class SpecialsCostToolTests
    {
        private static SpecialsCostTool.Sample S(float t, float cpu, float gpu, int live = 0, int draws = 0) =>
            new SpecialsCostTool.Sample { T = t, Cpu = cpu, Gpu = gpu, Render = -1f, Draws = draws, SetPass = draws / 4, Live = live };

        [Test]
        public void Summarize_SplitsTheBeatBeforeTheTap_FromTheSecondAfter()
        {
            var samples = new List<SpecialsCostTool.Sample>
            {
                S(0.05f, 30f, 30f),                                       // staging, before the idle window
                S(0.30f, 4f, 2f), S(0.35f, 4f, 2f), S(0.40f, 5f, 3f),      // the demo's beat before the tap at 0.5
                S(0.50f, 6f, 4f, 300, 120), S(0.60f, 9f, 7f, 900, 160), S(0.70f, 6f, 4f, 500, 140), S(1.40f, 5f, 3f, 100, 110),
                S(2.00f, 40f, 40f, 2000, 500),                            // after the window
            };
            var s = SpecialsCostTool.Summarize(samples, 0.5f);
            Assert.AreEqual(4f, s.IdleCpu, 1e-4f, "median of the beat");
            Assert.AreEqual(2f, s.IdleGpu, 1e-4f);
            Assert.AreEqual(8.55f, s.PeakCpu, 1e-4f, "p95 of 5, 6, 6, 9");
            Assert.AreEqual(9f, s.MaxCpu, 1e-4f);
            Assert.AreEqual(7f, s.MaxGpu, 1e-4f);
            Assert.AreEqual(900, s.MaxLive);
            Assert.AreEqual(160, s.MaxDraws);
            Assert.AreEqual(40, s.MaxSetPass);
            Assert.AreEqual(4, s.Frames);
        }

        [Test]
        public void Summarize_MarksAMissingGpuTime_AsMinusOne()
        {
            var s = SpecialsCostTool.Summarize(new List<SpecialsCostTool.Sample> { S(0.3f, 4f, 0f), S(0.6f, 6f, 0f) }, 0.5f);
            Assert.AreEqual(-1f, s.IdleGpu);
            Assert.AreEqual(-1f, s.PeakGpu);
            Assert.AreEqual(-1f, s.MaxGpu);
            Assert.AreEqual(6f, s.PeakCpu, 1e-4f);
        }

        [Test]
        public void MedianAndPercentile()
        {
            Assert.AreEqual(2.5f, SpecialsCostTool.Median(new List<float> { 4f, 1f, 2f, 3f }), 1e-6f);
            Assert.AreEqual(1.9f, SpecialsCostTool.Percentile(new List<float> { 1f, 2f }, 0.9f), 1e-6f);
            Assert.AreEqual(-1f, SpecialsCostTool.Median(new List<float>()));
        }

        [Test]
        public void Combine_TakesTheMedianOfTheRuns_AndTheWorstLivePeak()
        {
            var runs = new List<SpecialsCostTool.Summary>
            {
                new SpecialsCostTool.Summary { PeakGpu = 3f, MaxLive = 100, Frames = 30 },
                new SpecialsCostTool.Summary { PeakGpu = 9f, MaxLive = 300, Frames = 31 },
                new SpecialsCostTool.Summary { PeakGpu = 4f, MaxLive = 200, Frames = 29 },
            };
            var c = SpecialsCostTool.Combine(runs);
            Assert.AreEqual(4f, c.PeakGpu, 1e-6f);
            Assert.AreEqual(300, c.MaxLive, "BurstBudget is a ceiling: the worst run counts");
            Assert.AreEqual(90, c.Frames);
        }

        private const string PcAsset = "Assets/Settings/PC_RPAsset.asset";

        [Test]
        public void NoPendingMeasurement_LeavesThePipelineAlone()
        {
            // every domain reload runs the restore: with nothing pending it must not touch the quality level (SessionState.GetString
            // returns "" for a missing key, not the null default — that once wiped the PC level's pipeline asset on each reload)
            var before = QualitySettings.renderPipeline;
            var pc = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(PcAsset);
            try
            {
                QualitySettings.renderPipeline = pc;
                SpecialsCostTool.RestorePending();
                Assert.IsFalse(SpecialsCostTool.RestorePending(), "nothing pending: nothing to put back");
                Assert.AreSame(pc, QualitySettings.renderPipeline);
            }
            finally
            {
                QualitySettings.renderPipeline = before;
            }
        }

        [Test]
        public void SwapIn_ThenRestore_PutsThePreviousPipelineBack_Once()
        {
            var before = QualitySettings.renderPipeline;
            var pc = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(PcAsset);
            var mobile = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(SpecialsCostTool.MobileAsset);
            try
            {
                QualitySettings.renderPipeline = pc;
                SpecialsCostTool.SwapIn(mobile);
                Assert.AreSame(mobile, QualitySettings.renderPipeline);
                Assert.IsTrue(SpecialsCostTool.RestorePending());
                Assert.AreSame(pc, QualitySettings.renderPipeline);
                QualitySettings.renderPipeline = mobile;
                Assert.IsFalse(SpecialsCostTool.RestorePending(), "restored once; a second call is a no-op");
                Assert.AreSame(mobile, QualitySettings.renderPipeline);
            }
            finally
            {
                SpecialsCostTool.RestorePending();
                QualitySettings.renderPipeline = before;
            }
        }
    }
}
