# Pota'ya teslim: menü ve ödül katmanı (M1–M11)

_2026-10-03. Kaynak: Unity MCP atölyesi (`~/projects/Unity MCP`, dal `meta-feel`). Okuyan: Pota projesinde (`~/projects/Pota`, three.js) çalışan Claude Code oturumu ve Tuna. Oyun içi anların (F1–F13) belgesi: `2026-10-03-pota-oyun-ici-his.md`; iki belge **birlikte** teslim edilir._

Pota'nın menü katmanı (ana ekran, bölüm penceresi, kazanma/kaybetme, harita, lig/terfi, bonus, market, öğretici) Unity'de
Pota'nın görselleri ve ölçüleriyle birebir kuruldu (`Scenes/Meta.unity`), üstüne on bir "his" anı eklendi ve her an için
**Pota | Önce | Sonra** videosu alındı. Ses, müzik, titreşim bu turda yok (Tuna). Kurallar ve ekonomi değişmez: ödül 15 + ilk deneme 10 +
kalan top parası (`min(20, floor(kalan × 0,1))`), madalya ve lig eşikleri, bonusun ağırlık ve eşikleri Pota'nınkiler (`akis.ts`, `skor.ts`,
`bonus.ts`, `katalog.ts`).

## 1. Önce bunlara bak

| Ne | Yol |
|---|---|
| Her an Pota \| Önce \| Sonra, arka arkaya | `~/projects/Unity MCP/docs/screenshots/2026-10-03-meta-all.mp4` |
| Tek tek anlar | `…/2026-10-03-meta-<an>-side.mp4` ve `…-sheet.jpg`; `<an>` = menu, level, win, lose, map, promotion, bonus, market, tutorial |
| Tasarım | `~/projects/Unity MCP/docs/superpowers/specs/2026-10-03-pota-meta-feel-design.md`; Pota UI rehberi `docs/reference/2026-10-03-pota-meta-ui-style.md` |
| Unity'de canlı | `Scenes/Meta.unity`, Play'e bas; üstteki geliştirici şeridi: Kazan / Kaybet / Terfi / Bonus / Öğretici / His aç-kapa. Anahtar `MetaSettings.Feel.Enabled` (`Settings/MetaSettings.asset`) |

Pota sütunu `~/projects/Pota/onizleme/` ekran görüntülerinden (hareketsiz; bazıları İngilizce dilde çekilmiş, düzen aynı; öğretici için Pota'nın oyun içi öğretici karesi — Pota'da menüde öğretici yok). "Önce" = his
katmanı kapalı: Pota'nın bugünkü davranışının Unity'deki kopyası — Pota'nın **kendi** hareketleri (skor sayacının 0,8 sn sayması ve sondaki pop, market
deneme zıplaması / "yetmez" sallanması / satın alma kutlaması, bonus şeritlerinin dönüşü) kapalı hâlde de oynar; "Sonra" = açık.

## 2. Anlar ve sayılar

Süreler gerçek saniye. Yazı tipleri Lilita One (başlık, sayılar) ve Fredoka 600/700 (metin); renkler rehberin paleti (`#142850`, `#ff7a1a`,
`#ffc83d`, `#2fb36b`, `#fff4e0`).

| # | An | Ne oluyor | Sayılar | Unity kodu (`Games/Pota/Scripts/Meta/`) |
|---|---|---|---|---|
| M1 | Pencere ve düğme kiti | Örtü kararır, kutu 0,85 → 1,05 → 1 yaylanır, şerit başlık 0,06 sn sonra yukarıdan düşer; kapanış 0,9'a küçülüp söner. Her düğme basınca 0,94 + 3B gölge 2 px'e iner, bırakınca 1,04 taşıp oturur. Toast alttan yükselir, durur, süzülür. | örtü 0,18 (0 → 0,62); kutu 0,28; şerit −30 css → 0, 1,1 taşma, 0,2; kapanış 0,15; basma 0,06, bırakma 0,18; toast 0,12 / 1,2 / 0,25 | `Ui/MetaWindow`, `Ui/MetaButton`, `Ui/Toast`, eğriler `Ui/MetaStyle` |
| M2 | Ekran geçişleri | Harita / Market sayfaları alttan kayar + örtü; sekme ikonu 14 css yükselip 1,25 büyür, altın kart 1,08 taşarak gelir; "Oyna": kutu küçülür, ekran 0,3 sn siyaha karışıp döner (vitrinde oyun yok). | kayma 0,25; sekme 0,15; karartma 0,3 + 0,25 bekleme | `Ui/ScreenSlide`, `Screens/TabBar`, `MetaDirector.StartGame` |
| M3 | Para uçuşu ve dönen sayaçlar | Kazanım: 8–12 para kaynaktan yelpaze gibi saçılır, üst şeritteki rozete kavisle uçar, her varışta sayı eski rakamı yukarı kaydırıp yenisini getirir. Harcama: rozetten ürüne. Uçuş sayısı miktardan bağımsız (`clamp(miktar/5, 8, 12)`). | saçılma 0,15; uçuş 0,45; aralık 0,04; yuvarlanma 0,14 | `Ui/CoinFlight` (`CoinPlan.Count`), `Ui/RollingNumber`, `MetaDirector.GainCoins/SpendCoins` |
| M4 | Kazanma penceresi | 0,20 "Harika!" damga (0 → 1,3 → 1, −3°); 0,45 SKOR 0'dan toplama kübik yavaşlamayla sayar, her 1/12'de 1,06 zıplar; kalemler 0,9'da belirir. **Madalya yükselir:** sayaçla bronz belirir, gümüş/altın eşiği geçilince 0,15 sn'de flaşla döner (geri dönmez). 1,35 "Kupa +N" sayar, lig çubuğu eski değerden yeniye 0,6 sn dolar, "Sonraki lige N kupa" azalır; terfi varsa çubuk 1,0'da parlar ve Devam'dan sonra terfi penceresi gelir. 1,7 "+N" para `zipla` + M3 uçuşu; 1,9 ödül kartı. 2,3 Devam nabız (1,8 sn, 1,035). Konfeti Pota'nın (70 parça). | `MetaFeelTuning`: StampAt 0,2 · ScoreAt 0,45 · ScoreTime 0,8 · TrophyAt 1,35 · BarTime 0,6 · CoinsAt 1,7 · ContinueAt 2,3; eşik anı = `ScoreAt + ScoreTime × (1 − ∛(1 − eşik/skor))` | saf `Screens/WinSequence` (`Stage(t)`, `MedalFlipAt`), `Screens/WinWindow`, kurallar `MetaRules` |
| M5 | Kaybetme penceresi | Kutu açılırken 0,25 sn ±4 px sallanır (`salla`), kalan sayı 52 px 0 → 1,3 → 1 basılır; kalan ≤ 3 ise sayının yanında kırmızı "Kıl payı!" (−6°); önerilen turuncu düğme 0,8 sn sonra nabız. | LoseShake 0,25; LosePulseAt 0,8; damga 0,3 | `Screens/LoseWindow` (`Shake(u)`, `NearMiss`) |
| M6 | Harita ilerlemesi | Devam → harita kayar, 0,5 sn'de önceki bölümü ortalar; 0,3 sn sonra düğüm yeşile döner (0,2, 1,15 taşma) + madalya damgalanır (1,6 → 1, 0,3); yol noktaları sırayla altın yanar (0,04/nokta); "Sen" pini 3 zıplamayla gider (0,7), inişte 0,9 ezilir; yeni düğümün kilidi 0,15'te büzülür, rakam 0 → 1,3 → 1 (0,3), halka + 8 yıldız; kaydırma yeni düğümü ortalar (0,4). His kapalı: Pota gibi ana ekran. | toplam 2,1 sn; düğüm formülü `x = W/2 + sin(n·0,85)·W·0,3`, `y = 90 + (50−n)·118`; "Sen" yeri `haritaYer.ts senYeri` portu | saf `Screens/MapLayout`, `Screens/MapProgress`; `Screens/MapScreen` (`Play(from, to, medal)`) |
| M7 | Ana ekran | Ana görsel nefes alır (3,2 sn, 1 → 1,02, hafif salınım; pivot kahramanda), Oyna düğmesinde 2,2 sn'de bir soldan sağa parıltı (0,35) + Pota'nın nabzı, üst şerit rozetleri açılışta sırayla iner (0,05 arayla), iki yumuşak bulut sprite'ı (Blender `cloud_still`) sağa kayar (6 ve 10 css/sn). | Breath 3,2; ShineEvery 2,2; ShineTime 0,35 | `Screens/MenuScreen`, `MetaCurves.Breath/Shine` |
| M8 | Lig terfisi | Eski rozet 0,25 sn'de küçülüp dönerek kaybolur, yeni rozet 0 → 1,25 → 1 (0,4) ışın flaşı + konfetiyle gelir, adı damgalanır. Lig penceresi terfiden sonra açılınca "SEN" hapı ve vurgu rengi eski basamaktan yeniye 0,5 sn'de kayar. | OldOut 0,25; NewAt 0,2; NewIn 0,4; ışınlar 0,7; SEN 0,5 | `Screens/PromotionWindow`, `Screens/LeagueWindow` |
| M9 | Bonus | Şeritler Pota'nın eğrisiyle döner (1,4 sn + 0,12/sütun, `cubic-bezier(.25,.9,.3,1)` ≈ `1 − (1−u)^3,5`); her şerit dururken 0,12 sn ezilme (y 0,92) ve hücreler 1,1 taşma; kazanan hücreler altın + 2 nabız, diğerleri 0,4 alfa; kazanç paraları hücreden sonuç kartına uçar (bekleyen kazanç, şeride yazılmaz); "Bu sefer olmadı" kart ±4 px sallanır. | SquashTime 0,12; PopTime 0,2; eşikler para ≥ 3 → ×10, saat ≥ 4 → 30 dk, hak ≥ 4 → +3 | saf `Screens/ReelSpin`, `Screens/BonusRules` (`bonus.ts` portu, tohumlu); `Screens/BonusWindow` |
| M10 | Market | Satın alma: paralar rozetten çipe uçar (M3 harcama), sayaç düşer, çip 1,2 taşarak "Giyili/Senin" damgası alır, halka + 12 yıldız, kahraman 0,5 sn zıplar (`kutla`). Deneme: çipe dokununca kahraman 0,28 hop + yeni parça 0,15 sn beyaz flaş. Para yetmiyorsa büyük düğme ±4 px sallanır, para rozeti 2 kez kırmızı yanıp söner, toast "Para yetmiyor". Kahramanın forması `kahraman-maske.png`'nin forma seviyesi (gri 36) ile renklenir. | Hop 0,28; Celebrate 0,5; Flash 0,15; sallanma 0,3; blink 0,6 | `Screens/MarketScreen`, katalog `Screens/MarketCatalog` (forma 0/120/120/140/160, top 0/200/250/300/500) |
| M11 | Öğretici eli | Blender'dan stilize **eldiven** sprite'ı (`hand_pointer`, 512², mürekkepli): hedefe 0,3 sn'de süzülür, her 1 sn'de dokunur (0,9 ezilme + beyaz halka dalgası 0,4 sn), sonraki hedefe yay boyunca kayar (0,6); krem balon metni Pota'nın. His kapalı: Pota'nın yanıp sönen halkası, el yok. Vitrin akışı: Oyna ×3 → Harita sekmesi ×2 → çıkar. | Glide 0,3; TapEvery 1; ArcTime 0,6; halka 46 css, kenar 4 | `Screens/TutorialHand` |

## 3. Varlıklar

| Varlık | Nereden | Teslim |
|---|---|---|
| Pota UI görselleri (41) + Fredoka 600/700 | `ai_potameta_import` (Pota `public/assets` okunur, Fredoka değişken TTF'den iki statik örnek) | `Games/Pota/Art/Meta/`, `Art/Fonts/` |
| El (`hand_pointer`, 512²) | Blender `vfx_flipbook` tarifi (`tools/bl/vfx.py`), 1 kare | `~/projects/Blender MCP/assets/Pota/vfx-hand-pointer/export/` → `Art/VFX/vfx-hand-pointer/` |
| Bulut (`cloud_still`, 512×256) | aynı, mürekkepsiz yumuşak toon | `assets/Pota/vfx-cloud-still/export/` → `Art/VFX/vfx-cloud-still/` |

Pota'da bulut ve el için PNG'ler doğrudan `<img>`/CSS olarak kullanılır; maske/renk için `kahraman-maske.png` seviyeleri: forma 36, şort 72,
(ayakkabı 108), saç 144, (180), top 216.

## 4. Pota'da bulunanlar (okuma sırasında)

- Kazanma penceresindeki lig çubuğu (`ligCubukYaz`) genişliği pencere gizliyken yazılıyor; CSS geçişi (0,6 sn) muhtemelen hiç oynamıyor (F belgesinde de var).
- `kazandi`/`kaybetti` ses + titreşim iki kez (F belgesi §4).
- Harita "Sen" pini: `senYeri` adayları bazı düğümlerde (ör. 10, 20, 21) varsayılan yeri (78 px yan, −8 px) seçemiyor; Unity portu aynı sonucu veriyor, tasarım gereği.

## 5. Tuna'nın seçimi

Teslim: `docs/screenshots/2026-10-03-meta-*`. Her an için **Evet / Hayır / Değişiklik** bekleniyor; F1–F13 ile birlikte toplu aktarılacak.

| # | An | Karar |
|---|---|---|
| M1 | Pencere ve düğme kiti | |
| M2 | Ekran geçişleri | |
| M3 | Para uçuşu ve sayaçlar | |
| M4 | Kazanma penceresi | |
| M5 | Kaybetme penceresi | |
| M6 | Harita ilerlemesi | |
| M7 | Ana ekran | |
| M8 | Lig terfisi | |
| M9 | Bonus | |
| M10 | Market | |
| M11 | Öğretici eli | |

## 6. Bu turda yapılmayanlar / vitrin sınırları

- Ses, müzik, titreşim (Tuna: bu turda yok).
- Vitrinde gerçek bölüm yok: "Oyna" karartır ve geri döner, sonuç şeritten verilir. Kupa ve can sayaçları yuvarlanmıyor ("11,8K" metni), yalnız para yuvarlanıyor.
- Market: forma ve top satılır; güç ve para paketleri kart olarak durur (satın alma yok); kahramanda yalnız forma renklenir (şort/ayakkabı/saç ve top değişmez).
- Bonus "saat" hücresi kalp simgesiyle (Pota'nın saat görseli içe aktarma listesinde yok); vitrin tohumu ilk çevirişte para ödeyen ilk tohum.
- Pota sütunu hareketsiz ekran görüntüsü (Pota kaydı Chromium'u öne alır, Tuna aynı Mac'te); bazıları İngilizce dilde.
- Harita: geçilen bölümlerin madalyası örnek desen (`(n·7+1) % 3`), gerçek geçmiş yok; yürünen yolun noktaları altın kalır.
