using System.Text.RegularExpressions;
using NUnit.Framework;
using Project.ColorHunt;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Project.Tests
{
    /// <summary>1 · Royal (spec 2026-10-01 §2): readiness, the rockets keep the schedule's beat, the budget, the spec's numbers.</summary>
    public class RoyalTests
    {
        private const string SettingsPath = "Assets/_Project/Games/Pota/Settings/ColorHuntSettings.asset";
        private const string Hint = "run the Blender tools (plan 2026-10-01 Tasks 7–8), then execute_custom_tool ai_studio_import";
        private const float Swell = 0.15f;

        private GameObject _go;
        private ColorHuntSettings _memory;

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            if (_memory != null) Object.DestroyImmediate(_memory);
        }

        private RoyalSpecials Make(ColorHuntSettings s)
        {
            _go = new GameObject("Game");
            var game = _go.AddComponent<ColorHuntGame>();
            game.S = s;
            var style = new GameObject("RoyalSpecials");
            style.transform.SetParent(_go.transform, false);
            var royal = style.AddComponent<RoyalSpecials>();
            royal.Build(game);
            return royal;
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

        /// <summary>The plan the game makes for a tap on <paramref name="origin"/> (its area and triggers from the rules).</summary>
        private static TriggerPlan Plan(Board b, int origin, SpecialTiming t)
        {
            var area = ColorHuntRules.Blast(b, origin);
            var step = new Step();
            step.Exploded.AddRange(area.Cells);
            step.Triggered.AddRange(area.Triggered);
            return SpecialSchedule.Plan(7, step, origin, t, Swell);
        }

        [Test]
        public void WithoutTheHandoffs_RoyalIsNotReady()
        {
            _memory = ScriptableObject.CreateInstance<ColorHuntSettings>();
            Assert.IsFalse(RoyalSpecials.AssetsReady(null));
            Assert.IsFalse(RoyalSpecials.AssetsReady(_memory));
            var royal = Make(_memory);
            Assert.IsFalse(royal.Ready);
            Assert.IsFalse(royal.gameObject.activeSelf, "an unready style stays off");
            Assert.AreEqual(0, royal.LiveCount);
            Assert.AreEqual(RoyalSpecials.TimingOf(_memory.Royal), royal.Timing, "the timing never needs the assets");
        }

        [Test]
        public void WithTheImportedHandoffs_RoyalIsReady()
        {
            var s = AssetDatabase.LoadAssetAtPath<ColorHuntSettings>(SettingsPath);
            Assert.IsNotNull(s, SettingsPath);
            Assert.IsTrue(RoyalSpecials.AssetsReady(s), Hint);
            var copy = Object.Instantiate(s);
            try
            {
                copy.ToonTemplate = null;
                Assert.IsFalse(RoyalSpecials.AssetsReady(copy), "no template: Project/Toon would come from Shader.Find, absent in a build");
            }
            finally { Object.DestroyImmediate(copy); }
            var royal = Make(s);
            Assert.IsTrue(royal.Ready);
            Assert.IsTrue(royal.GathersBirthGroup);
            Assert.IsTrue(royal.ConsumesSpecialView);
            Assert.IsFalse(royal.gameObject.activeSelf, "built off: the game activates the look it uses");
            Assert.AreEqual(7, royal.transform.childCount, "seven particle systems; the pooled meshes come on demand");
            Assert.AreEqual(0, royal.LiveCount);
        }

        [Test]
        public void TheGame_PlaysPotaForRoyal_UntilTheHandoffsAreIn()
        {
            _go = new GameObject("Game");
            var game = _go.AddComponent<ColorHuntGame>();
            game.S = _memory = ScriptableObject.CreateInstance<ColorHuntSettings>();
            LogAssert.Expect(LogType.Warning, new Regex("Royal special look is missing assets"));
            game.SetSpecialLook(SpecialLook.Royal);
            Assert.AreEqual(SpecialLook.Royal, game.SpecialLook);
            Assert.IsInstanceOf<PotaSpecials>(game.Specials);
            Assert.IsNotNull(_go.transform.Find("RoyalSpecials")?.GetComponent<RoyalSpecials>(), "CreateSpecialStyle builds Royal on its own child");
        }

        [Test]
        public void Rockets_ReachEveryCellOfAColumn_WhenTheScheduleBurstsIt()
        {
            var t = RoyalSpecials.TimingOf(new RoyalTuning());
            float peak = Swell * PotaLook.SwellPeakAt;
            var plan = Plan(Board7((3, SpecialKind.LineV)), 3, t);                 // (0, 3): LineV clears the column, one rocket down 6 cells
            Assert.AreEqual(1f, plan.Pace, "0.06 + 6/18.75 + 0.15 fits 0.6 s");
            var fire = plan.Fires[0];
            for (int r = 1; r < 7; r++)
            {
                int c = 3 + 7 * r;
                float burst = plan.Start[c] + peak;
                Assert.AreEqual(burst, RoyalSpecials.RocketReach(fire, t, r), 1e-3f, $"({r}, 3): the rocket is over it when it bursts");
                Assert.AreEqual(r, RoyalSpecials.RocketCells(fire, t, burst - fire.Time), 1e-3f * t.LineSpeed, $"({r}, 3)");
            }
            Assert.AreEqual(0f, RoyalSpecials.RocketCells(fire, t, t.LineLead * 0.5f), "still squashing");
            var mid = Plan(Board7((24, SpecialKind.LineV)), 24, t);                // (3, 3): two rockets, 3 cells each
            foreach (int r in new[] { 0, 1, 2, 4, 5, 6 })
                Assert.AreEqual(mid.Start[3 + 7 * r] + peak, RoyalSpecials.RocketReach(mid.Fires[0], t, Mathf.Abs(r - 3)), 1e-3f, $"({r}, 3) from the middle");
        }

        [Test]
        public void Rockets_KeepTheBeat_WhenTheStepIsCompressed()
        {
            var t = RoyalSpecials.TimingOf(new RoyalTuning());
            float peak = Swell * PotaLook.SwellPeakAt;
            var plan = Plan(Board7((21, SpecialKind.LineH), (27, SpecialKind.Bomb)), 21, t);    // the row to a bomb at its far end
            Assert.Less(plan.Pace, 1f, "compressed to SpecialSchedule.MaxStep");
            var fire = plan.Fires[0];
            Assert.AreEqual(plan.Pace, fire.Pace);
            for (int k = 1; k < 7; k++)
                Assert.AreEqual(plan.Start[21 + k] + peak, RoyalSpecials.RocketReach(fire, t, k), 1e-3f, $"(3, {k})");
            Assert.AreEqual(27, plan.Fires[1].Cell);
            Assert.AreEqual(plan.Fires[1].Time, RoyalSpecials.RocketReach(fire, t, 6), 1e-3f, "the bomb fires as the rocket reaches it");
            Assert.AreEqual(3.8125f, RoyalSpecials.EdgeCells(7), 1e-4f, "the frame's middle line: (4.4 + 0.175) / 1.2");
        }

        [Test]
        public void PoolCaps_FitTheBudget()
        {
            Assert.AreEqual(1026 - 684, RoyalSpecials.Cap, "BurstBudget.Max − Pota's burst pools (normal blocks keep bursting there)");
            Assert.AreEqual(325, RoyalSpecials.PoolCapacity);
            Assert.LessOrEqual(RoyalSpecials.PoolCapacity, RoyalSpecials.Cap);
            var t = new RoyalTuning();
            Assert.GreaterOrEqual(RoyalSpecials.RocketMax, 6, "three lines in one 7×7 step");
            Assert.GreaterOrEqual(RoyalSpecials.ShellMax, 2 * 8, "two bombs' shells at once");
            Assert.GreaterOrEqual(RoyalSpecials.CoinMax, 2 * t.LandCoins, "two golden balls landing 0.12 s apart");
            Assert.GreaterOrEqual(RoyalSpecials.TokenMax, 2);
            Assert.GreaterOrEqual(RoyalSpecials.FireMax, 2);
            Assert.GreaterOrEqual(RoyalSpecials.ProxyMax, 2);
        }

        [Test]
        public void TuningDefaults_AreTheSpecNumbers()
        {
            var t = new RoyalTuning();
            Assert.AreEqual(0.06f, t.LineLead, "Top 0,06 sn ezilip parlar");
            Assert.AreEqual(0.32f, 6f / t.LineSpeed, 1e-5f, "0,32 sn'de en uzak kenara (7×7: 6 hücre)");
            Assert.AreEqual(0.18f, t.BombLead, "Hazırlık 0,18 sn");
            Assert.AreEqual(0.04f, t.BombRing, "halka başına 0,04 sn");
            Assert.AreEqual(0.18f, t.GatherTime, "0,18 sn'de doğum hücresine çekilir");
            Assert.AreEqual(0.25f, t.PopTime, "taşarak (0,25 sn)");
            Assert.AreEqual(1.2f, t.PopPeak, "0 → 1,2 → 1");
            Assert.That(t.BirthTwinkles, Is.InRange(6, 8), "6–8 yıldızcık");
            Assert.AreEqual(3f, t.FuseSparksPerSecond, "saniyede ~3");
            Assert.AreEqual(0.03f, t.BreathAmount, "±%3");
            Assert.AreEqual(1.5f, t.ArrowGlowHz, "1,5 Hz");
            Assert.AreEqual(1f, t.GoldenTwinklesPerSecond, "saniyede ~1");
            Assert.AreEqual(0.5f, t.PuffEvery, "yarım hücrede bir");
            Assert.AreEqual(0.12f, t.LineShake, "eksen boyunca sarsıntı (0,12)");
            Assert.AreEqual(1.25f, t.BombSwell, "1,0 → 1,25 şişer");
            Assert.AreEqual(2, t.BombBlinks, "iki kez kırmızı-beyaz");
            Assert.AreEqual(0.08f, t.CoreFlashTime, "beyaz çekirdek flaşı (0,08 sn)");
            Assert.AreEqual(3.2f, t.FireCells, "~3,2 hücre");
            Assert.AreEqual(0.45f, t.FireTime, "0,45 sn");
            Assert.That(t.BombPuffs, Is.InRange(4, 6), "4–6 duman pufu");
            Assert.AreEqual(12, t.BombSparks, "12 kıvılcım");
            Assert.AreEqual(0.1f, t.PushCells, "0,1 hücre dışa itilir");
            Assert.AreEqual(0.2f, t.BombShake, "Sarsıntı 0,2");
            Assert.AreEqual(0.06f, t.BombHitStop, "0,06 sn anlık yavaşlama");
            Assert.AreEqual(0.5f, t.ComboSlowScale, "×0,5");
            Assert.AreEqual(0.2f, t.ComboSlowTime, "0,2 sn");
            Assert.AreEqual(0.1f, t.ScreenFlashTime, "0,1 sn beyaz ekran flaşı");
            Assert.That(t.LandCoins, Is.InRange(8, 12), "8–12 dönen altın para");
            var timing = RoyalSpecials.TimingOf(t);
            Assert.AreEqual(t.LineSpeed, timing.LineSpeed);
            Assert.AreEqual(t.LineLead, timing.LineLead);
            Assert.AreEqual(t.BombLead, timing.BombLead);
            Assert.AreEqual(t.BombRing, timing.BombRing);
            Assert.AreEqual(t.GatherTime, timing.GatherTime);
            _memory = ScriptableObject.CreateInstance<ColorHuntSettings>();
            Assert.IsNotNull(_memory.Royal, "a fresh settings asset carries the defaults");
            Assert.AreEqual(t.LineSpeed, _memory.Royal.LineSpeed);
        }

        [Test]
        public void FuseTip_SitsOnTheSpritesSpark()
        {
            float q = PotaLook.LabelQuad * PotaLook.SpecialScale;
            Assert.AreEqual(q, RoyalSpecials.StickerQuad, 1e-6f);
            var tip = RoyalSpecials.FuseTip;
            Assert.AreEqual((0.06f + 0.88f * 0.79f - 0.5f) * q, tip.x, 1e-5f, "ozel-bomba-a.png: spark centroid u 0.79, drawn payli into 88 %");
            Assert.AreEqual((0.06f + 0.88f * 0.90f - 0.5f) * q, tip.y, 1e-5f, "v 0.90 from the bottom");
            Assert.That(tip.x, Is.InRange(0.15f, 0.3f), "right of centre, inside the 0.9-cell block");
            Assert.That(tip.y, Is.InRange(0.25f, 0.4f), "above centre");
        }

        [Test]
        public void PopAndJet_Curves()
        {
            Assert.AreEqual(0f, RoyalSpecials.PopScale(0f, 1.2f), 1e-5f, "from nothing");
            Assert.AreEqual(1.2f, RoyalSpecials.PopScale(0.55f, 1.2f), 1e-4f, "taşarak: the overshoot");
            Assert.AreEqual(1f, RoyalSpecials.PopScale(1f, 1.2f), 1e-5f, "settles at its size");
            float most = 0f;
            for (int i = 0; i <= 100; i++) most = Mathf.Max(most, RoyalSpecials.PopScale(i / 100f, 1.2f));
            Assert.AreEqual(1.2f, most, 1e-3f);
            Assert.AreEqual(new Vector4(0.25f, 0.5f, 0f, 0.5f), RoyalSpecials.JetFrame(0f), "frame 0: the sheet's top-left");
            Assert.AreEqual(new Vector4(0.25f, 0.5f, 0.25f, 0f), RoyalSpecials.JetFrame(0.3f * 5.5f / 8f), "frame 5: second row, second column");
            Assert.AreEqual(RoyalSpecials.JetFrame(0.01f), RoyalSpecials.JetFrame(0.31f), "one loop = 0.3 s");
            var quad = RoyalSpecials.JetQuad();
            Assert.AreEqual(-0.9f, quad.bounds.min.y, 1e-5f, "the nozzle (0.5, 0.9) is the pivot; the flame runs down −y");
            Assert.AreEqual(0.1f, quad.bounds.max.y, 1e-5f);
            Assert.AreEqual(-0.5f, quad.bounds.min.x, 1e-5f);
            Assert.AreEqual(4, quad.colors.Length, "white vertex colours: PotaFlipbook multiplies by them");
        }

        [Test]
        public void Idle_LeavesTheScaleAlone_OnAStagedBoard()
        {
            var s = AssetDatabase.LoadAssetAtPath<ColorHuntSettings>(SettingsPath);
            Assert.IsNotNull(s, SettingsPath);
            var royal = Make(s);
            var game = _go.GetComponent<ColorHuntGame>();
            Assert.AreNotEqual(ColorHuntGame.State.Playing, game.Current, "staged, before Begin (PotaFlow's reveal runs now)");
            var block = new GameObject("Block");
            block.transform.SetParent(_go.transform, false);
            var v = block.AddComponent<BlockView>();
            v.Init(s, PillowBlockMesh.Create(), BlockView.BodyMaterial(), BlockView.LabelMaterial(null, null));
            v.SetBlock(Block.MakeSpecial(SpecialKind.Bomb), 0);
            v.Scale = 0.4f;                                                         // BoardReveal's entrance scale
            royal.Idle(v, 0.1f);
            Assert.AreEqual(0.4f, v.Scale, "the reveal owns Scale until the round begins; Royal's breath starts in play");
            Assert.AreEqual(0, royal.LiveCount, "no fuse sparks before play");
        }

        [Test]
        public void FlameJet_NeverReachesPastItsTwin()
        {
            float jetCells = new RoyalTuning().JetCells;
            for (int i = 0; i <= 40; i++)
            {
                float d = i * 0.025f;
                float flame = RoyalSpecials.JetNozzle.y * jetCells * RoyalSpecials.JetGrow(d, jetCells);
                Assert.LessOrEqual(flame, 2f * d + RoyalSpecials.JetGap + 1e-5f, $"{d} cells out: the flame stays between the halves");
            }
            Assert.Greater(RoyalSpecials.JetGrow(0f, jetCells), 0f, "a lick of flame at the split");
            Assert.AreEqual(1f, RoyalSpecials.JetGrow(1f, jetCells), "full length once the halves are two cells apart");
        }

        [Test]
        public void RoyalDrawsInFrontOfTheBursts()
        {
            const int potaHighest = 9;                                              // ColorHuntFx: stars 9, flashes 8, shards 7
            foreach (int order in new[] { RoyalSpecials.PuffOrder, RoyalSpecials.JetOrder, RoyalSpecials.RingOrder, RoyalSpecials.FireOrder, RoyalSpecials.GlowOrder })
                Assert.Greater(order, potaHighest, "Royal's effects fly in front of the bursting blocks");
            Assert.Greater(RoyalSpecials.FireOrder, RoyalSpecials.PuffOrder, "the fireball over its smoke");
            Assert.Greater(RoyalSpecials.JetOrder, RoyalSpecials.PuffOrder, "a flame jet over its own trail smoke");
            Assert.Greater(RoyalSpecials.FireZ, PotaLook.FrontZ + PotaLook.PillowBulge, "the fireball in front of the pillows");
            Assert.Greater(RoyalSpecials.ShellZ, RoyalSpecials.FireZ, "the shell pieces fly out of the fireball, not behind it");
        }

        [Test]
        public void SynthClips_HaveTheirLengths_AndStayUnclipped()
        {
            var clips = new (AudioClip clip, float seconds, string name)[]
            {
                (RoyalSpecials.MakeRocket(), RoyalSpecials.RocketSoundTime, "roket"),
                (RoyalSpecials.MakeFuse(), RoyalSpecials.FuseSoundTime, "fitil"),
                (RoyalSpecials.MakeCoin(), RoyalSpecials.CoinSoundTime, "para"),
            };
            foreach (var (clip, seconds, name) in clips)
            {
                try
                {
                    Assert.AreEqual(name, clip.name);
                    Assert.AreEqual(Mathf.CeilToInt(seconds * 44100), clip.samples, name);
                    var data = new float[clip.samples];
                    clip.GetData(data, 0);
                    float most = 0f;
                    foreach (var x in data) most = Mathf.Max(most, Mathf.Abs(x));
                    Assert.That(most, Is.InRange(0.05f, 0.9f), name + ": audible and never at the clamp");
                }
                finally { Object.DestroyImmediate(clip); }
            }
            var a = RoyalSpecials.MakeRocket();
            var b = RoyalSpecials.MakeRocket();
            try
            {
                var da = new float[a.samples];
                var db = new float[b.samples];
                a.GetData(da, 0);
                b.GetData(db, 0);
                CollectionAssert.AreEqual(da, db, "hash noise: every recording sounds the same");
            }
            finally
            {
                Object.DestroyImmediate(a);
                Object.DestroyImmediate(b);
            }
        }
    }
}
