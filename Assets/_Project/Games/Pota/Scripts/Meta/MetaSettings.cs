using System;
using UnityEngine;

namespace Project.Meta
{
    /// <summary>The menu/reward layer's feel switch and timings (spec 2026-10-03 meta feel §2). Off = Pota's instant behaviour.</summary>
    [Serializable]
    public class MetaFeelTuning
    {
        [Tooltip("Master switch: M1–M11. Off = every window, button and counter changes instantly, like Pota today.")]
        public bool Enabled = true;

        [Header("M1 windows and buttons")]
        public float WindowOpen = 0.28f;
        public float WindowClose = 0.15f;
        public float Backdrop = 0.18f;
        public float RibbonDrop = 0.2f;
        public float RibbonDelay = 0.06f;
        public float Press = 0.06f;
        public float Release = 0.18f;
        public float ToastIn = 0.12f, ToastHold = 1.2f, ToastOut = 0.25f;

        [Header("M2 screen slides")]
        public float Slide = 0.25f;
        public float TabLift = 0.15f;
        public float PlayFade = 0.3f;

        [Header("M3 coin flight and rolling counters")]
        public float CoinBurst = 0.15f;
        public float CoinFlight = 0.45f;
        public float CoinStagger = 0.04f;
        public float Roll = 0.14f;

        [Header("M4 win window")]
        public float StampAt = 0.2f, ScoreAt = 0.45f, ScoreTime = 0.8f, TrophyAt = 1.35f, BarTime = 0.6f, CoinsAt = 1.7f, ContinueAt = 2.3f;

        [Header("M5 lose window")]
        public float LoseShake = 0.25f, LosePulseAt = 0.8f;

        [Header("M6 map progression")]
        public float MapScroll = 0.5f, NodeGreen = 0.2f, MedalStamp = 0.3f, DotStep = 0.04f, PinHop = 0.7f, UnlockPop = 0.3f;

        [Header("M7 main screen")]
        public float Breath = 3.2f, BallHopEvery = 7f, ShineEvery = 2.2f, ShineTime = 0.35f;

        /// <summary>A duration under the switch: itself when on, 0 when off.</summary>
        public float T(float seconds) => Enabled ? seconds : 0f;
    }

    /// <summary>The showcase's sample save (Pota kayit): what the screens show and what a win or a loss changes.</summary>
    [Serializable]
    public class SampleSave
    {
        public int Level = 20;
        public int Coins = 145;
        public int Lives = 3, MaxLives = 5;
        public int Trophies = 11800;
        public int BestScore = 0;
        public bool FirstTry = true;
        [Tooltip("The showcase win's score lines (Pota skorHesapla): blok, zincir, özel, altın, kalan, engel; carpan 1.2 on a first try.")]
        public int ScoreBlocks = 120, ScoreChain = 30, ScoreSpecial = 40, ScoreGold = 100, ScoreLeft = 9, ScoreObstacle = 0;
        public int LeftoverBalls = 137;
        public int RemainingOnLoss = 3;
        [Tooltip("Level 20's medal thresholds (Pota renkAviBolum madalya): silver, gold.")]
        public int MedalSilver = 300, MedalGold = 420;
    }

    /// <summary>Every number, image and font of the menu/reward showcase (Scenes/Meta.unity). Edit in the Inspector.</summary>
    [CreateAssetMenu(menuName = "Project/Pota/Meta Settings", fileName = "MetaSettings")]
    public class MetaSettings : ScriptableObject
    {
        public const string AssetPath = "Assets/_Project/Games/Pota/Settings/MetaSettings.asset";

        public MetaFeelTuning Feel = new MetaFeelTuning();
        public SampleSave Save = new SampleSave();

        [Header("Rules (Pota akis.ts ODUL, skor.ts ESIK)")]
        public int LevelReward = 15;
        public int FirstTryReward = 10;
        [Tooltip("Çaylak, Amatör, Profesyonel, All-Star, Şampiyonlar: the trophy totals each league starts at.")]
        public int[] LeagueThresholds = { 0, 12400, 44700, 119100, 154200 };
        public string[] LeagueNames = { "Çaylak", "Amatör", "Profesyonel", "All-Star", "Şampiyonlar Ligi" };
        public int MapLevels = 50;

        [Header("Fonts (ai_potameta_import)")]
        public Font Lilita;
        public Font FredokaSemiBold;
        public Font FredokaBold;

        [Header("Images (ai_potameta_import, Art/Meta)")]
        [Tooltip("anaekran-1-gunduz, 2-gunbatimi, 3-gece, 4-sokak (levels 1, 15, 30, 45).")]
        public Sprite[] MenuBackgrounds = new Sprite[4];
        public Sprite MapBackground, LoadingBackground;
        public Sprite Hero, HeroMask;
        public Sprite Coin, Heart, Trophy, Gear, Basket, Ball, CoinPile;
        [Tooltip("lig-caylak, amator, profesyonel, allstar, sampiyonlar")]
        public Sprite[] LeagueBadges = new Sprite[5];
        [Tooltip("madalya-bronz, gumus, altin")]
        public Sprite[] Medals = new Sprite[3];
        [Tooltip("esya-forma, ayakkabi, sapka, corap, bileklik")]
        public Sprite[] Items = new Sprite[5];
        [Tooltip("market, ev-ikon, harita-ikon")]
        public Sprite[] TabIcons = new Sprite[3];
        [Tooltip("kat-giysi, kat-top, kat-guc, para-yigini")]
        public Sprite[] CategoryIcons = new Sprite[4];
        [Tooltip("guc-altin, guc-cephane, guc-devam")]
        public Sprite[] Boosters = new Sprite[3];
        [Tooltip("giysi-forma, sort, ayakkabi, sac (grey, tinted at runtime)")]
        public Sprite[] Clothes = new Sprite[4];
        [Tooltip("M11 / M7 Blender stills: hand_pointer, cloud_still (ai_studio_import).")]
        public Sprite HandSprite, CloudSprite;

        /// <summary>The menu background for a level (Pota anaEkranGorseli: 1, 15, 30, 45).</summary>
        public Sprite MenuBackgroundFor(int level)
        {
            int[] firsts = { 1, 15, 30, 45 };
            Sprite chosen = MenuBackgrounds.Length > 0 ? MenuBackgrounds[0] : null;
            for (int i = 0; i < firsts.Length && i < MenuBackgrounds.Length; i++) if (level >= firsts[i] && MenuBackgrounds[i] != null) chosen = MenuBackgrounds[i];
            return chosen;
        }

        /// <summary>League index for a trophy total (Pota skor.ts lig).</summary>
        public int LeagueOf(int trophies)
        {
            int l = 0;
            for (int i = 0; i < LeagueThresholds.Length; i++) if (trophies >= LeagueThresholds[i]) l = i;
            return l;
        }
    }
}
