# Pota: menü ve ödül katmanı hissi — tasarım (2026-10-03, 2. alt proje)

1. alt proje (oyun içi his F1–F13) bitti, Tuna hepsini beğendi; Pota'ya aktarım iki alt proje bitince toplu yapılacak. Bu belge 2. alt
projeyi tanımlar: Pota'nın **menü, harita, pencere ve ödül** katmanının hissi. Pota'da bu katman DOM/CSS'tir ve Unity'de karşılığı yoktur;
bu yüzden önce Pota'nın görünümü birebir kurulur (`docs/reference/2026-10-03-pota-meta-ui-style.md`), his üstüne eklenir. Ses, müzik ve
titreşim yine yok (Tuna). Kurallar, ekonomi, metinler Pota'nınki; hiçbir yeni özellik (sandık, günlük ödül) icat edilmez.

## 1. Amaç ve ölçüt

Türün ölçütü Royal Match / Toon Blast: her pencere yaylanarak açılır, her düğme basınca ezilir, para sayaca uçar ve sayaç döner, kazanma
penceresinde madalya ve lig çubuğu "kazanır", harita bölümden bölüme ilerler, ana ekran canlıdır, öğretici bir elle gösterir.

İş bittiğinde:
1. `Scenes/Meta.unity` ("Pota menü vitrini"): Pota'nın ekranları uGUI ile 390 css px ölçeğinde birebir (renk, yazı tipi, ölçü, görsel);
   `MetaSettings.Feel.Enabled` kapalıyken Pota'nın bugünkü ani davranışı (geçişsiz), açıkken M1–M11.
2. Vitrin akışı tek başına oynanır: Ana ekran → Bölüm penceresi → (geliştirici şeridinden "Kazan" / "Kaybet" ile sonuç) → Kazanma
   penceresi → Harita ilerlemesi → Ana ekran; Kaybetme penceresi; Lig/terfi; Bonus; Market; Öğretici eli; Ayarlar; toast. Gerçek oyun
   bölümü oynanmaz (1. alt projenin sahneleri ayrı); sayılar `MetaSettings`'teki örnek kayıttan gelir (bölüm 20, 145 para, 3 can, Çaylak 11 800 kupa).
3. Teslim: `docs/screenshots/2026-10-03-meta-<an>-{side.mp4,sheet.jpg}` — sütunlar **Pota (ekran görüntüsü) | Unity Önce | Unity Sonra**
   (Pota sütunu `~/projects/Pota/onizleme/` kayıtlı görüntüleri; yeni Pota kaydı alınmaz) ve `…-meta-all.mp4`.
4. Tüm testler yeşil; Pota salt okunur; push yok; ücretli çağrı yok; Unity öne çağrılmaz.
5. Göz kontrolü: her anın kaydı izlenir, kusur "bitti" denmeden düzeltilir.

## 2. Anlar (ne göreceksin)

**M1. Pencere ve düğme kiti.** Pencereler şu an bir anda beliriyor, yalnız büyük düğmeler çöküyor.
- Örtü 0,18 sn'de kararır (0 → 0,62). Kutu 0,85'ten 1,05'e taşıp 1'e oturur (0,28 sn, geri yaylanma); şerit başlık kutudan 0,06 sn
  sonra yukarıdan düşer (−30 css → 0, 1,1 taşma). Kapanış: 1 → 0,9 küçülüp 0,15 sn'de söner.
- Her düğme (büyük, küçük, çip, sekme, düğüm, kapat): basınca 0,06 sn'de 0,94 ölçek + 3B gölgenin 2 px'e inmesi (Pota'nın
  `translateY(4)`'ü); bırakınca 1,04'e taşıp oturur (0,18 sn). Pasif düğme tepki vermez.
- Toast: hap altta 0,12 sn'de yükselerek belirir, 1,2 sn durur, aşağı süzülür (Pota `bildir`).

**M2. Ekran geçişleri.** Ana ekran ↔ Harita / Market: sayfa 0,25 sn'de alttan kayar + örtü (Pota: anında). Sekme ikonu seçilince
Pota'daki gibi 0,15 sn'de yükselir, altın kart 1,08 taşarak gelir. Bölüm penceresinden "Oyna": kutu küçülür, ekran 0,3 sn'de
siyaha karışır ("oyun başlar" — vitrinde geliştirici şeridi).

**M3. Para uçuşu ve dönen sayaçlar.** Para, kupa ve can sayıları şu an anında değişiyor.
- Para kazanımı: kaynaktan (kazanma penceresinin "+15"i, bonus, finale rozeti, market iadesi) 8–12 para `para.png` yelpaze gibi
  saçılır (0,15 sn), üst şeritteki para rozetine kavisle uçar (0,45 sn, 0,04 sn arayla), varışta rozet 1,25 ezilir, sayı eski
  rakamı yukarı kaydırıp yenisini getirir (0,14 sn), son parada altın parıltı. Harcama: paralar rozetten ürüne uçar, rozet düşerken döner.
- Kupa ve can sayıları aynı yuvarlanmayla değişir. Toplam uçuş sayısı para miktarından bağımsız (tavan 12).

**M4. Kazanma penceresi.** Sıra (gerçek saniye, pencere açılışından):
- 0,00 kutu + şerit "Bölüm N bitti"; 0,20 "Harika!" damga (0 → 1,3 → 1, −3°, altın parıltı); 0,45 SKOR 0'dan toplama sayar
  (0,8 sn, kübik yavaşlama; her 1/12'de sayı 1,06 zıplar); kalemler satırı 0,9'da soluk belirir.
- Madalya **yükselir**: 0,5'te bronz madalya belirir; skor sayacı gümüş eşiğini geçerken (sayaç oranına göre) madalya 0,15 sn'de
  gümüşe döner (flaş + 1,2 taşma), altın eşiğini geçerken altına; eşiğe ulaşmadıysa olduğu yerde kalır. Adı da değişir.
- 1,35 "Kupa +N" kartı sayısı yuvarlanır; lig çubuğu eski değerden yeniye 0,6 sn dolar (altın→turuncu), dolarken "Sonraki lige N kupa"
  azalır; çubuk taşarsa (terfi) 1,0 taşarak parlar ve Devam'dan sonra terfi penceresi gelir.
- 1,7 "+15" para: `para.png` `zipla` + M3 uçuşu üst şeride; "N top arttı · +N ödül · ilk denemede!" kartı 1,9'da belirir.
- 2,3 "Devam" düğmesi `nabizDugme` (1,8 sn, 1,035) ile nefes alır. Konfeti Pota'nın (70 parça, 1,6–2,8 sn).
- Sayılar, kalemler, eşikler `MetaSettings`'teki örnekten; ne gösterileceği Pota'nın `kazandiSkorYaz` düzeninin aynısı.

**M5. Kaybetme penceresi.** Kutu açılırken 0,25 sn ±4 px sallanır (`salla`), kalan sayı 52 px 0 → 1,3 → 1 taşarak basılır; toplam
kalan ≤ 3 ise sayının üstünde "Kıl payı!" etiketi (kırmızı, −6°); önerilen (turuncu) düğme 0,8 sn sonra nabız; diğerleri sabit.

**M6. Harita ilerlemesi.** Pota'da "Devam" ana ekrana dönüyor, harita hiç kıpırdamıyor. Yeni: Devam → harita (M2 geçişi), kaydırma
0,5 sn'de **bir önceki** bölümü ortalar; 0,3 sn sonra:
- geçilen düğüm yeşile döner (0,2 sn renk + 1,15 taşma), madalya sağ altına damgalanır (1,6 → 1, 0,3 sn);
- yol noktaları önceki düğümden sonrakine sırayla yanar (her nokta 0,04 sn, beyaz → altın parıltı); "Sen" pini noktaların üstünden
  3 zıplamayla gider (0,7 sn), varışta yere basar (0,9 ezilme);
- yeni düğüm kilidi açılır: kilit ikonu 0,15 sn'de büzülür, rakam 0 → 1,3 → 1 patlar, halka dalgası + 8 yıldızcık, sonra `nabiz`;
- kaydırma yeni düğümü ortalar (0,4 sn). Harita açık kalır; "Ana" sekmesiyle ana ekrana dönülür. Feel kapalı: Pota'daki gibi ana ekran.

**M7. Ana ekran.** Kahraman şu an zemine basılmış resim. Yeni: `kahraman.png` kesiği (Pota'nın vitrin kesiği, maske ile) zeminin
üstüne aynı yere bindirilir ve **nefes alır** (3,2 sn, 1 → 1,02, hafif sallanma), 6–9 sn'de bir topu 0,3 sn zıplatır (`zipla`); Oyna
düğmesinde 2,2 sn'de bir soldan sağa parıltı süpürmesi (beyaz şerit, 0,35 sn, maske içinde) + var olan nabız; üst şerit rozetleri ekran
açılışında sırayla aşağıdan iner (0,05 sn arayla); zeminde bulutlar 1. alt projenin bulutlarıyla yavaş kayar (2B: `cloud-set` render'ı
yerine zeminin kendi bulutlu gökyüzü üstünde yarı saydam beyaz bulut sprite'ı — Blender `vfx_flipbook` tek kare `cloud_still`).
(3B toon kahraman yerine kesik: Tuna'nın "Pota görünümü kalıyor" kararı; 3B kahraman görünümü değiştirirdi.)

**M8. Lig terfisi.** Terfi penceresinde eski rozet 0,25 sn'de küçülüp dönerek kaybolur, yeni rozet 0 → 1,25 → 1 (0,4 sn) ışınlı
flaş + konfeti ile gelir, adı "Yeni lig!" şeridinin altında damgalanır. Lig penceresinde "SEN" satırı eski basamaktan yeniye 0,5 sn'de
kayar (vurgu rengi onunla gider).

**M9. Bonus penceresi.** Şu an geçişsiz açılıyor, kazanç yazıyla veriliyor. Yeni: kutu M1; şeritler dönerken hızlanma/yavaşlama Pota'nın
eğrisi + her şerit dururken 0,12 sn ezilme (y 0,92) ve hücreler 1,1 taşma; kazanan hücreler altın parıltı (Pota) + kazanç paraları
sonuç kartına M3 uçuşu; "Bu sefer olmadı" ise kart ±4 px sallanır.

**M10. Market.** Satın alma: paralar rozetten çipe uçar (M3 harcama), çip 1,2 taşarak "Senin"/"Giyili" damgası alır, 12 yıldızcık +
halka, vitrindeki kahraman 0,5 sn zıplar (Pota `kutla`). Deneme: çipe dokununca kahraman 0,28 sn hop (Pota) + yeni parça 0,15 sn parlar.
Para yetmiyorsa büyük düğme sallanır (Pota) + para rozeti kırmızı yanıp söner (2 kez).

**M11. Öğretici eli.** Pota'da yanıp sönen halka var. Yeni: Blender'dan stilize **el** sprite'ı (`hand_pointer`, tek kare, işaret parmağı,
dış hatlı, 512²); hedefe 0,3 sn'de süzülür, her 1 sn'de "dokunur" (0,9 ezilme + halka dalgası), döndürme adımında yay boyunca
kaydırır; balon metni Pota'nın. Halka kalkar.

Kapsam dışı: açılış ve yükleme ekranı (Pota'nınki zaten animasyonlu), sandık/günlük ödül (Pota'da yok), reklam, satın alma akışı,
gerçek oyun bölümü.

## 3. Yapı

- **Varlıklar:** `ai_potameta_import` (`Games/Pota/Editor/`): `~/projects/Pota/public/assets/` içinden rehberin §12 listesini
  `Games/Pota/Art/Meta/` altına kopyalar (okuma; Pota'ya yazılmaz), içe aktarma ayarlarını yapar (sprite, mip yok, sıkıştırma mobil),
  `Fredoka` TTF'yi (OFL, Google Fonts) `Art/Fonts/` altına indirir ve `MetaSettings`'e bağlar. Kesik kahraman `kahraman.png`+maske.
- **Ayarlar:** `MetaSettings` (ScriptableObject, `Games/Pota/Settings/MetaSettings.asset`): `Feel` bloğu (`Enabled` + M1–M11 süreleri),
  örnek kayıt (bölüm, para, can, kupa, lig, bölüm skoru, madalya eşikleri, lig eşikleri 0/12400/44700/119100/154200), görsel ve font
  referansları, renk paleti (rehber §0).
- **Arayüz kiti** (`Scripts/Meta/Ui/`, ad alanı `Project.Meta`): `MetaStyle` (sabitler, sprite üreticileri: panel/şerit/düğme/rozet
  `ColorHuntHud.PanelSprite`'ın genelleştirilmesi), `MetaButton` (basma/bırakma eğrisi, 3B gölge katmanı), `MetaWindow` (örtü + kutu +
  şerit + kapat; `Open/Close` M1), `RollingNumber` (RollWindow genellemesi), `CoinFlight` (M3; kaynak/hedef canvas noktası, havuz),
  `Toast`, `ScreenSlide` (M2). Her biri tek işli, `Feel.Enabled` kapalıyken anında.
- **Ekranlar** (`Scripts/Meta/Screens/`): `MenuScreen` (M7), `MapScreen` + `MapProgress` (M6; düğüm yerleşimi saf `MapLayout`),
  `LevelWindow`, `WinWindow` (M4; sıra saf `WinSequence`: t → her parçanın durumu, madalya eşik geçişleri), `LoseWindow` (M5),
  `LeagueWindow` + `PromotionWindow` (M8), `BonusWindow` (M9; dönüş saf `ReelSpin`), `MarketScreen` (M10; vitrin + çipler + kartlar,
  katalog Pota `katalog.ts` fiyatları örnek), `SettingsWindow`, `TutorialHand` (M11), `DevStrip` (vitrin düğmeleri: Kazan, Kaybet,
  Terfi, Bonus, Öğretici, Feel aç/kapa).
- **Yönetmen:** `MetaDirector` (akış durumu, örnek kayıt üzerinde değişiklik: kazanınca para/kupa/bölüm artar, kaybedince can düşer;
  Pota `akis.ts` kurallarının vitrin için gereken kadarı: ödül 15 + ilk deneme 10 + kalan top paraya (F3 formülü), madalya/lig eşiği).
- **Sahne aracı:** `ai_meta_build_scene` (`Scenes/Meta.unity`, Build Settings + `PotaProfile`'a ekler; Game view 1170×2532).
- **Kayıt:** `ai_meta_record` `{"scenario": menu|level|win|lose|map|promotion|bonus|market|tutorial, "feel": on|off}`: senaryo
  betiği (yönetmen adımları + bekleme), kare kare `Temp/record/<ad>/` + `events.json` işaretleri; `Tools/meta_record.sh` tüm anları iki
  kez (off/on) kaydeder, Pota sütununu `onizleme/` görüntülerinden alır, `side_video.py groups` + `frame_sheet.py --anchor` ile teslim üretir.
- **Blender:** `vfx_flipbook` tarifleri `hand_pointer` (tek kare el, inkli) ve `cloud_still` (tek kare yumuşak bulut) → `Art/VFX/`.
- **Saf ve test edilebilir:** `WinSequence`, `MapLayout`, `MapProgress` zaman çizelgesi, `ReelSpin`, `CoinFlight` planı, `MetaStyle`
  eğrileri (EditMode); ekranlar PlayMode'da senaryo ile.

## 4. Doğrulama

- EditMode: kazanma sırası (madalya eşik geçiş anları, sayaç, lig çubuğu taşması → terfi), harita yerleşimi (Pota formülü), ilerleme
  zaman çizelgesi (nokta sırası, pin zıplamaları, kilit açılış anı), makara dönüşü (süre 1400 + 120·sütun, durma sırası), para uçuşu
  planı (8–12, tavan 12), düğme eğrisi (0,94 / 1,04 / 1), Feel kapalıyken tüm süreler 0.
- PlayMode: vitrin akışı baştan sona hatasız (menü → bölüm → kazan → harita → menü; kaybet; terfi; bonus; market; öğretici); her
  pencere açılıp kapanır; para rozeti uçuş sonunda doğru sayıyı gösterir; Feel aç/kapa pencere açıkken yarım durum bırakmaz; Pota
  görsellerinin tamamı içe aktarılmış ve ayarlara bağlı (`StudioAssetsTests` benzeri).
- Göz: `Pota | Önce | Sonra` levhaları ve videolar.

## 5. İş sırası

1. Varlık içe aktarma (`ai_potameta_import`, Fredoka) + `MetaSettings` + `MetaStyle` sprite'ları + arayüz kiti (M1, M3 çekirdek) — testler.
2. Sahne aracı + Ana ekran (M7) + sekme çubuğu + ekran geçişleri (M2) + ayarlar + toast.
3. Bölüm penceresi + Kazanma penceresi (M4) + Kaybetme (M5) + geliştirici şeridi + yönetmen.
4. Harita + ilerleme (M6).
5. Lig/terfi (M8), Bonus (M9), Market (M10).
6. Blender el + bulut; öğretici eli (M11), ana ekran bulutları.
7. Kayıt aracı, teslim videoları/levhaları, göz kontrolü ve düzeltme turu.
8. Belgeler (README, CLAUDE.md, backlog, hafıza) + Pota teslim belgesi (1. alt projeninkiyle birlikte toplu).

## 6. Riskler

- **Yazı tipi:** Fredoka değişken TTF'yi Unity legacy `Text` kullanır (TMP yok); ağırlık 600/800 için `FontStyle.Bold` yetmeyebilir;
  gerekirse Fredoka Bold statik TTF ayrıca indirilir. Lilita One'da ğ/ş/İ yok: Pota'da da yok, aynı davranış (başlıklar Türkçe karakter içermiyor).
- **Kesik kahraman:** `kahraman.png` zeminde çizili kahramanla hizalanmalı; vitrin ölçüsü Pota'nın `anaEkran` yerleşiminden alınır, yan yana
  levhada doğrulanır.
- **Kapsam:** 11 madde ve 10 ekran; her adım ayrı commit, testler yeşil; önce görünüm birebir, sonra his.
