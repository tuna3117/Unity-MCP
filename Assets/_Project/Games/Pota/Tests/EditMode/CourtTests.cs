using NUnit.Framework;
using Project.ColorHunt;
using UnityEditor;
using UnityEngine;

namespace Project.Tests
{
    /// <summary>2 · Court (spec 2026-10-01 §2 "Basketbol"): its numbers, its clock against the trigger plan, the dunk, the pools, its assets.</summary>
    public class CourtTests
    {
        private const string SettingsPath = "Assets/_Project/Games/Pota/Settings/ColorHuntSettings.asset";
        private const float Swell = 0.15f, Ms = 1e-3f;
        private static float Peak => Swell * PotaLook.SwellPeakAt;

        private GameObject _go;
        private ColorHuntSettings _fresh;

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            if (_fresh != null) Object.DestroyImmediate(_fresh);
        }

        /// <summary>7×7 colour (2r + c) mod 5 (no two neighbours alike), hp 5, all specials on (SpecialScheduleTests' board).</summary>
        private static Board Board7(params (int cell, SpecialKind kind)[] specials)
        {
            var cells = new Block[49];
            for (int i = 0; i < 49; i++) cells[i] = new Block((2 * (i / 7) + i % 7) % 5, 5);
            foreach (var (cell, kind) in specials) cells[cell] = Block.MakeSpecial(kind);
            return new Board
            {
                Size = 7, Cells = cells, Ammo = 158, Targets = new[] { new Target(0, 22), new Target(1, 16) },
                ColorCount = 5, HpMin = 3, HpMax = 14, Rng = 1, Features = Features.All,
            };
        }

        /// <summary>The trigger step the rules give for a tap on <paramref name="origin"/>.</summary>
        private static Step Blast(Board b, int origin)
        {
            var area = ColorHuntRules.Blast(b, origin);
            var step = new Step();
            step.Exploded.AddRange(area.Cells);
            step.Triggered.AddRange(area.Triggered);
            return step;
        }

        private static TriggerPlan Plan(Board b, int origin, CourtTuning t) =>
            SpecialSchedule.Plan(7, Blast(b, origin), origin, CourtSpecials.TimingFor(t), Swell);

        private static ColorHuntSettings Asset() => AssetDatabase.LoadAssetAtPath<ColorHuntSettings>(SettingsPath);

        [Test]
        public void Tuning_DefaultsAreTheSpecs()
        {
            var t = new CourtTuning();
            Assert.AreEqual(0.22f, t.GatherTime, "Doğuş: 0,22 sn");
            Assert.AreEqual(0.6f, t.BornSquash, "çarpma 0,6");
            Assert.AreEqual(2f, t.IdleHz, "2 Hz dripling");
            Assert.AreEqual(0.08f, t.DribbleTime, "Çizgi: 0,08 sn ezilme");
            Assert.AreEqual(0.3f, t.LineSweep, "iki uca 0,3 sn");
            Assert.AreEqual(6f, t.SweepCells, "a 7×7's far edge");
            Assert.AreEqual(0.14f, t.HopTime, "smaç: 0,14 sn zıplar");
            Assert.AreEqual(1.6f, t.HopScale, "1 → 1,6");
            Assert.AreEqual(0.08f, t.HangTime, "0,08 sn asılı");
            Assert.AreEqual(0.07f, t.SlamTime, "0,07 sn çakılır");
            Assert.AreEqual(0.03f, t.BombRing);
            Assert.AreEqual(0.6f, t.CrackFade, "çatlak 0,6 sn'de söner");
            Assert.AreEqual(0.25f, t.BlockHop, "3×3 0,25 hücre fırlar");
            Assert.AreEqual(0.5f, t.LetterTime, "SLAM! 0,5 sn");
            Assert.AreEqual(1.15f, t.LetterPeak);
            Assert.AreEqual(6, t.FlashbulbsMin, "6–10 fotoğraf flaşı");
            Assert.AreEqual(10, t.FlashbulbsMax);
            Assert.AreEqual(0.4f, t.FlashbulbWindow);
            Assert.AreEqual(0.24f, t.SlamShake, "sarsıntı 0,24");
            Assert.AreEqual(0.5f, t.ComboSlowScale, "aynı yavaş çekim: ×0,5");
            Assert.AreEqual(0.2f, t.ComboSlowTime, "0,2 sn");
            Assert.AreEqual(0.4f, t.DustTime, "vfx-dust-ring: 0.4 s");
            Assert.AreEqual(0.3f, t.FlameLoop, "vfx-flame-jet: one loop 0.3 s");
            Assert.AreEqual(0.25f, t.SpeedTime, "vfx-speed-lines: 0.25 s");
            _fresh = ScriptableObject.CreateInstance<ColorHuntSettings>();
            Assert.IsNotNull(_fresh.Court, "ColorHuntSettings.Court starts at the defaults");
            Assert.AreEqual(t.HopScale, _fresh.Court.HopScale);
            Assert.IsNotNull(Asset().Court, "the settings asset carries Court");
        }

        [Test]
        public void Timing_ComesFromTheTuning_AndALoneSpecialFitsTheStep()
        {
            var t = new CourtTuning();
            var timing = CourtSpecials.TimingFor(t);
            Assert.AreEqual(20f, timing.LineSpeed, 1e-4f, "6 cells in 0.3 s");
            Assert.AreEqual(0.08f, timing.LineLead, 1e-6f, "the hard dribble");
            Assert.AreEqual(0.29f, timing.BombLead, 1e-5f, "0.14 + 0.08 + 0.07: the impact");
            Assert.AreEqual(0.03f, timing.BombRing, 1e-6f);
            Assert.AreEqual(0.22f, timing.GatherTime, 1e-6f);
            var edge = Plan(Board7((21, SpecialKind.LineH)), 21, t);
            Assert.AreEqual(1f, edge.Pace, "a line from the edge fits uncompressed");
            Assert.AreEqual(0.08f + 0.3f + Swell, edge.End, 1e-4f);
            var bomb = Plan(Board7((24, SpecialKind.Bomb)), 24, t);
            Assert.AreEqual(1f, bomb.Pace);
            Assert.AreEqual(0.29f + 0.03f + Swell, bomb.End, 1e-4f, "ring 1 starts 0.32 s after the fire");
            Assert.LessOrEqual(bomb.End, SpecialSchedule.MaxStep);
        }

        [Test]
        public void LineBalls_ReachEveryCellOfAColumn_WhenTheScheduleBurstsIt()
        {
            var t = new CourtTuning();
            var plan = Plan(Board7((24, SpecialKind.LineV)), 24, t);
            var fire = plan.Fires[0];
            Assert.AreEqual(7, plan.Start.Count, "column 3");
            Assert.AreEqual(1f, plan.Pace);
            for (int r = 0; r < 7; r++)
            {
                int c = 3 + 7 * r, k = Mathf.Abs(r - 3);
                if (k == 0) continue;
                float burst = plan.Start[c] + Peak;
                Assert.AreEqual(burst, CourtSpecials.ReachTime(fire, t, k), Ms, $"({r}, 3): the ball arrives as the cell bursts");
                Assert.AreEqual(k, CourtSpecials.LineDistance(fire, t, burst - fire.Time), Ms * 20f, $"({r}, 3): where the ball is at the burst");
            }
            Assert.AreEqual(0f, CourtSpecials.LineDistance(fire, t, 0.08f), 1e-4f, "still dribbling");
        }

        [Test]
        public void LineAndDunk_FollowACompressedChain()
        {
            var t = new CourtTuning();
            var plan = Plan(Board7((21, SpecialKind.LineH), (27, SpecialKind.Bomb)), 21, t);
            Assert.Less(plan.Pace, 1f, "0.08 + 0.3 + 0.29 + 0.03 + 0.15 > 0.6: compressed");
            Assert.AreEqual(SpecialSchedule.MaxStep, plan.End, 1e-4f);
            var line = plan.Fires[0];
            var bomb = plan.Fires[1];
            Assert.AreEqual(27, bomb.Cell);
            Assert.AreEqual(plan.Pace, line.Pace, 1e-6f);
            Assert.AreEqual(plan.Pace, bomb.Pace, 1e-6f);
            for (int k = 1; k <= 5; k++)
                Assert.AreEqual(plan.Start[21 + k] + Peak, CourtSpecials.ReachTime(line, t, k), Ms, $"(3, {k}): ball and burst, Pace included");
            Assert.AreEqual(CourtSpecials.ReachTime(line, t, 6), bomb.Time, Ms, "the bomb fires the moment the ball reaches it");
            foreach (int c in new[] { 19, 20, 33, 34 })
                Assert.AreEqual(plan.Start[c] + Peak, CourtSpecials.BombBurstTime(bomb, t, 1), Ms, $"cell {c}: ring 1, right after the impact");
            Assert.AreEqual(bomb.Time + 0.29f * plan.Pace, CourtSpecials.ImpactTime(bomb, t), Ms);
        }

        [Test]
        public void Dunk_HopsHangsSlams_ImpactAtBombLead_PeakScale16()
        {
            var t = new CourtTuning();
            float lead = CourtSpecials.TimingFor(t).BombLead;
            var start = CourtSpecials.Dunk(0f, 1f, t);
            Assert.AreEqual(1f, start.Scale);
            Assert.AreEqual(0f, start.Toward);
            Assert.IsFalse(start.Impact);
            float most = 0f, nearest = 0f;
            for (int i = 0; i <= 290; i++)
            {
                var p = CourtSpecials.Dunk(i * 0.001f, 1f, t);
                most = Mathf.Max(most, p.Scale);
                nearest = Mathf.Max(nearest, p.Toward);
            }
            Assert.AreEqual(1.6f, most, 1e-4f, "1 → 1.6");
            Assert.AreEqual(t.HopToward, nearest, 1e-4f, "toward the camera");
            var top = CourtSpecials.Dunk(0.14f, 1f, t);
            Assert.AreEqual(1.6f, top.Scale, 1e-4f, "the hop ends at 1.6");
            var hang = CourtSpecials.Dunk(0.18f, 1f, t);
            Assert.AreEqual(1.6f, hang.Scale, 1e-4f);
            Assert.AreEqual(t.HopToward, hang.Toward, 1e-4f, "it hangs");
            Assert.IsFalse(CourtSpecials.Dunk(lead - 1e-3f, 1f, t).Impact, "still slamming");
            var hit = CourtSpecials.Dunk(lead, 1f, t);
            Assert.IsTrue(hit.Impact, "impact at BombLead");
            Assert.AreEqual(0f, hit.Toward, 1e-5f, "back on the board");
            Assert.AreEqual(0f, hit.Up, 1e-5f);
            Assert.Less(hit.Squash, 1f, "flattened on the floor");
            Assert.IsFalse(CourtSpecials.Dunk(lead * 0.5f - 1e-3f, 0.5f, t).Impact, "a compressed chain dunks at Pace");
            Assert.IsTrue(CourtSpecials.Dunk(lead * 0.5f, 0.5f, t).Impact);
            Assert.AreEqual(1.6f, CourtSpecials.Dunk(0.07f, 0.5f, t).Scale, 1e-4f);
        }

        [Test]
        public void Lettering_PopsTo115_AndIsGoneAtHalfASecond()
        {
            var t = new CourtTuning();
            Assert.AreEqual(0f, CourtSpecials.LetterScale(0f, t));
            Assert.AreEqual(1.15f, CourtSpecials.LetterScale(0.1f, t), 1e-4f);
            Assert.AreEqual(1f, CourtSpecials.LetterScale(0.3f, t), 1e-4f);
            float most = 0f;
            for (int i = 0; i <= 500; i++) most = Mathf.Max(most, CourtSpecials.LetterScale(i * 0.001f, t));
            Assert.AreEqual(1.15f, most, 1e-3f, "0 → 1.15 → 1");
            Assert.AreEqual(1f, CourtSpecials.LetterAlpha(0.3f, t), 1e-4f);
            Assert.AreEqual(0f, CourtSpecials.LetterAlpha(0.5f, t), 1e-4f);
        }

        [Test]
        public void NetWave_FlexesTheSixRingsInTurn()
        {
            var t = new CourtTuning();
            Assert.AreEqual(0.5f, CourtSpecials.NetWaveTime(t, 6), 1e-5f);
            float last = -1f;
            for (int k = 0; k < 6; k++)
            {
                Assert.AreEqual(1f, CourtSpecials.NetRing(0f, k, t), 1e-6f, $"ring {k} at rest first");
                float peakAt = k * t.NetRingGap + t.NetRingTime * 0.5f;
                Assert.AreEqual(1f + t.NetRingAmount, CourtSpecials.NetRing(peakAt, k, t), 1e-4f, $"ring {k}");
                Assert.Greater(peakAt, last, "top to bottom, in turn");
                last = peakAt;
                Assert.AreEqual(1f, CourtSpecials.NetRing(CourtSpecials.NetWaveTime(t, 6), k, t), 1e-6f, $"ring {k} settled");
            }
        }

        [Test]
        public void GatherArc_LeavesItsCell_BouncesOnce_LandsInTheBirthCell()
        {
            var t = new CourtTuning();
            var from = new Vector2(-2f, 0f);
            var to = new Vector2(1f, 0f);
            Assert.AreEqual(from, CourtSpecials.GatherPos(from, to, 0f, t));
            Assert.AreEqual(to, CourtSpecials.GatherPos(from, to, 1f, t));
            Assert.AreEqual(0f, CourtSpecials.GatherPos(from, to, t.GatherBounceAt, t).y, 1e-4f, "touches down once on the way");
            Assert.Greater(CourtSpecials.GatherPos(from, to, t.GatherBounceAt * 0.5f, t).y, 0.5f, "a real arc first");
            Assert.AreEqual(t.GatherBounce, CourtSpecials.GatherPos(from, to, (1f + t.GatherBounceAt) * 0.5f, t).y, 1e-4f, "a small second hop");
        }

        [Test]
        public void CourtDrawsTheImpactOverTheBursts()
        {
            const int potaHighest = 9;                                              // ColorHuntFx: stars 9, flashes 8, shards 7
            foreach (int order in new[] { CourtSpecials.CrackOrder, CourtSpecials.DustOrder, CourtSpecials.SpeedOrder, CourtSpecials.FlameOrder,
                         CourtSpecials.SwishOrder, CourtSpecials.FlashOrder })
                Assert.Greater(order, potaHighest, "the crack, the dust ring and the trails stay readable over the 3×3's flashes");
            Assert.Less(CourtSpecials.RingOrder, 2, "the birth's court ring lies under the block labels");
            Assert.Greater(CourtSpecials.DustOrder, CourtSpecials.CrackOrder, "the dust ring over the crack");
            Assert.Greater(CourtSpecials.FlameOrder, CourtSpecials.SpeedOrder, "the flame over the speed lines");
            Assert.Greater(CourtSpecials.LetterOrder, CourtSpecials.FlashOrder, "the lettering over everything");
            Assert.Greater(CourtSpecials.DecalZ, (PotaLook.FrontZ + PotaLook.PillowBulge) * PotaLook.ChainSwell, "the crack in front of a 1.24× swollen pillow");
            var t = new CourtTuning();
            Assert.Greater(t.FlameTint.g, t.FlameTint.r, "a multiply only turns the orange sheet yellower by raising green");
            Assert.Greater(t.FlameWide, 1f, "the sheet's jet is thin: the tail is widened across the path");
            Assert.Greater(t.GatherLift, PotaLook.LabelZ - PotaLook.FrontZ, "a gathered block passes in front of the neighbours' labels");
            Assert.Greater(t.SwishBack, 0f, "SWISH! stands behind the landing ball and the confetti");
        }

        [Test]
        public void FlameTail_NeverReachesPastItsTwin()
        {
            var t = new CourtTuning();
            for (int i = 0; i <= 100; i++)
            {
                float d = i * 0.02f;
                Assert.LessOrEqual(CourtSpecials.FlameGrow(d, t) * 0.9f * t.FlameCells, 2f * d + 0.5f * t.BallCells + 1e-5f,
                    $"{d:0.00} cells out: the tail stays inside the twin ball's half");
            }
            Assert.AreEqual(1f, CourtSpecials.FlameGrow(1f, t), 1e-6f, "full length once the balls are a cell apart");
            Assert.Greater(CourtSpecials.FlameGrow(0f, t), 0f, "a tail from the first frame");
        }

        [Test]
        public void Pools_StayUnderWhatThePotaPoolsLeave()
        {
            Assert.AreEqual(BurstBudget.Max - BurstBudget.Pota, CourtSpecials.Cap);
            Assert.LessOrEqual(CourtSpecials.PoolTotal, CourtSpecials.Cap, "spec §1.6: ≤ 1.5 × Pota with Pota's own bursts");
            Assert.GreaterOrEqual(CourtSpecials.BallMax, 3 * 2 + 2, "three lines and two dunks in one chain");
            Assert.GreaterOrEqual(CourtSpecials.GoldenMax, GateRings.Pool, "a golden ball per gate");
            Assert.GreaterOrEqual(CourtSpecials.FlashMax, 2 * new CourtTuning().FlashbulbsMax, "two dunks' flashbulbs");
            Assert.GreaterOrEqual(CourtSpecials.LetterMax, 3, "SLAM!, COMBO! and SWISH! at once");
            Assert.GreaterOrEqual(CourtSpecials.FlameMax, CourtSpecials.BallMax, "a tail per ball");
            Assert.GreaterOrEqual(CourtSpecials.SpeedMax, CourtSpecials.BallMax, "speed lines per ball");
        }

        [Test]
        public void Frame_IsRowMajorFromTheTopLeft()
        {
            Assert.AreEqual(new Vector4(0f, 0.5f, 0.25f, 0.5f), CourtSpecials.Frame(0, 4, 2));
            Assert.AreEqual(new Vector4(0.75f, 0.5f, 0.25f, 0.5f), CourtSpecials.Frame(3, 4, 2));
            Assert.AreEqual(new Vector4(0f, 0f, 0.25f, 0.5f), CourtSpecials.Frame(4, 4, 2));
            Assert.AreEqual(new Vector4(0.75f, 0f, 0.25f, 0.5f), CourtSpecials.Frame(7, 4, 2));
        }

        [Test]
        public void RingTexture_IsAThinWhiteRing()
        {
            var tex = CourtSpecials.RingTexture(128);
            Assert.AreEqual(0f, tex.GetPixel(64, 64).a, 1e-3f, "open centre");
            Assert.Greater(tex.GetPixel(64 + 58, 64).a, 0.9f, "the line at r ≈ 0.91");
            Assert.AreEqual(1f, tex.GetPixel(64 + 58, 64).r, 1e-3f, "white");
            Assert.AreEqual(0f, tex.GetPixel(64 + 44, 64).a, 1e-3f, "thin: clear at r ≈ 0.70");
            Assert.AreEqual(0f, tex.GetPixel(0, 0).a, 1e-3f);
        }

        [Test]
        public void Sounds_PassWhooshAndShortCrowd()
        {
            var pass = CourtSpecials.MakePass();
            try
            {
                Assert.AreEqual("court-pas", pass.name);
                Assert.AreEqual(Mathf.CeilToInt(0.24f * 44100), pass.samples);
                var data = new float[pass.samples];
                pass.GetData(data, 0);
                float most = 0f;
                foreach (var x in data) most = Mathf.Max(most, Mathf.Abs(x));
                Assert.That(most, Is.InRange(0.08f, 0.95f), "audible, unclipped");
                Assert.Less(Mathf.Abs(data[data.Length - 1]), 0.01f, "fades out");
            }
            finally { Object.DestroyImmediate(pass); }
            var crowd = Asset().SfxCrowd;
            Assert.IsNotNull(crowd, "Task 3's kalabalik");
            var cut = CourtSpecials.ShortCrowd(crowd, 1.2f, 0.35f);
            try
            {
                Assert.AreNotSame(crowd, cut, "a readable sample is cut (Decompress On Load, PCM)");
                Assert.AreEqual("kalabalik-kisa", cut.name);
                Assert.AreEqual(1.2f, cut.length, 0.01f, "kısa seyirci sesi");
                Assert.AreEqual(crowd.channels, cut.channels);
                var tail = new float[cut.channels];
                cut.GetData(tail, cut.samples - 1);
                foreach (var x in tail) Assert.Less(Mathf.Abs(x), 0.01f, "faded to silence");
            }
            finally { if (cut != crowd) Object.DestroyImmediate(cut); }
        }

        [Test]
        public void Ready_NeedsEveryCourtAsset()
        {
            Assert.IsFalse(CourtSpecials.AssetsReady(null));
            _fresh = ScriptableObject.CreateInstance<ColorHuntSettings>();
            Assert.IsFalse(CourtSpecials.AssetsReady(_fresh), "in-memory settings: no Blender hand-offs");
            Assert.IsTrue(CourtSpecials.AssetsReady(Asset()), "run ai_studio_import (Task 9)");
            _go = new GameObject("Game");
            var game = _go.AddComponent<ColorHuntGame>();
            game.S = _fresh;
            var court = new GameObject("CourtSpecials").AddComponent<CourtSpecials>();
            court.transform.SetParent(_go.transform, false);
            court.Build(game);
            Assert.IsFalse(court.Ready);
            Assert.IsFalse(court.gameObject.activeSelf, "an unready look stays off");
        }

        [Test]
        public void SettingsAsset_ResolvesTheCourtStyle()
        {
            _go = new GameObject("Game");
            var game = _go.AddComponent<ColorHuntGame>();
            game.S = Asset();
            game.SetSpecialLook(SpecialLook.Court);
            var court = game.Specials as CourtSpecials;
            Assert.IsNotNull(court, "no fallback: the Court assets are in place");
            Assert.IsTrue(court.Ready);
            Assert.IsTrue(court.gameObject.activeSelf);
            Assert.AreSame(game.transform, court.transform.parent, "on the game root (the hoop's space)");
            Assert.AreEqual(CourtSpecials.TimingFor(game.S.Court), court.Timing, "the plan reads the settings' tuning");
            Assert.IsTrue(court.GathersBirthGroup);
            Assert.IsTrue(court.ConsumesSpecialView);
            Assert.AreEqual(0, court.LiveCount);
            game.SetSpecialLook(SpecialLook.Pota);
            Assert.IsFalse(court.gameObject.activeSelf, "switched off with the look");
            Assert.IsInstanceOf<PotaSpecials>(game.Specials);
        }
    }
}
