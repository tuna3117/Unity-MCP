# Pota: oyun içi his katmanı — tasarım (2026-10-03)

Tuna'nın isteği (2026-10-03): Pota'nın okunmasından çıkan eksik his listesindeki her şeyi (ses, müzik ve titreşim hariç) Unity'de
yap; "premium stüdyo" seviyesi hedef; Blender ve Unity kullanımda. Tuna videolardan bakacak, beğendikleri Pota oturumuna teslim
belgesiyle gidecek. Çerçeve (iki alt proje: 1 oyun içi his, 2 menü ve ödül katmanı; sıra 1 → 2) onaylandı, "yok, başla yapmaya".

Bu belge **1. alt projeyi** tanımlar. 2. alt proje (`Scenes/Meta.unity`: kazanma/kaybetme pencereleri, harita, ana ekran, market,
öğretici eli) ayrı bir belgedir.

## 1. Amaç ve ölçüt

Tahtadaki anlık his (vuruş, patlama, zincir, özel toplar, eşya uçuşu) Pota'da zaten türün seviyesinde. Eksik olan anların **etrafı**:
bölümün başı ve sonu, az top gerilimi, kaybetme anı, basket'te nişan ve sonuç, sahnenin canlılığı. Bu iş onları Pota görünümünü
değiştirmeden ekler (Tuna'nın 2026-09-30 kararı: yeni sanat yönü yok, var olan iyileştirilir).

İş bittiğinde:
1. `ColorHuntSettings.Feel.Enabled` ve `BasketSettings.Feel.Enabled` açıkken aşağıdaki 13 madde oynanır; kapalıyken bugünkü davranış
   birebir kalır (yan yana karşılaştırma için).
2. Her an için teslim: `docs/screenshots/2026-10-03-feel-<an>-{sheet.jpg,side.mp4}` — sütunlar **Pota | Unity (önce) | Unity (sonra)**;
   Pota'da karşılığı olmayan anlarda iki sütun. Pota sütunu yalnız scratch kopyasından kaydedilir.
3. Oyun kuralları ve sayılar değişmez; his katmanı yalnız görüntü, zaman ve kamera. Tek istisna: kalan topların paraya dönüşmesi
   Pota'nın kendi formülüyle gösterilir (`min(20, floor(kalan × 0,1))`, akis.ts ODUL) — sayı kuralı Pota'nınki.
4. Tüm testler yeşil (EditMode + PlayMode), Pota salt okunur, push yok, ücretli çağrı yok.
5. Göz kontrolü: her anın kaydı izlenir; görülen kusur "bitti" denmeden düzeltilir (kalite ilkesi).

## 2. Anlar (ne göreceksin)

Süreler gerçek saniye. Yazı tipi Lilita One (HUD'un fontu), renkler HUD paleti (lacivert panel, altın kenar).

### Renk Avı

**F1. Bölüm başı (LevelIntro).** Tahta şu an yerinde hazır beliriyor.
- 0,00: üstten lacivert-altın **şerit** iner ("BÖLÜM 20"; 1,15 taşarak oturur), hedef eşyaları şeridin altında büyük (ikon + sayı)
  yan yana belirir (0,06 sn arayla, zıplayarak).
- 0,70: eşyalar şeritten hedef paneline **uçar** (`CollectFlight` eğrileri: kavis, hızlanma, parıltı izi); varışta panel ezilir ve
  sayı yerine oturur. Şerit yukarı çıkar.
- 0,95: bloklar alttan üste **dalga** halinde zıplayarak gelir (var olan stüdyo açılışı: satır başına 0,03 sn, 1,15 taşma).
- 1,60: hazır; dokunuş açılış boyunca bekler, ekrana dokunmak kalan kısmı ≤ 0,3 sn'de bitirir.
- PotaFlow'da: şerit damga anında (2,15) iner; eşyalar hedef paneli girerken (2,45) uçar; blok yağmuru adayın kendisinin. "Yeni tahta"
  düğmesi tam sırayı oynatır.

**F2. Az top gerilimi (AmmoTension).** Cephane en ucuz bloğun 2 katının altına inince:
- sayaç kırmızıya döner ve 1 Hz nabız atar (var olan `AmmoCounter.Pulsing`, Pota görünümüne açılır);
- ekran kenarında kırmızı **nefes** (0,18 alfa, 1 Hz, kenar parlaması dokusu);
- parası yeten tek blok kaldıysa o blok hemen ipucu kıpırtısıyla parlar (bekleme yok).
Cephane bitince veya tur bitince söner.

**F3. Kalan topların finali (LeftoverFinale).** Kazanınca kalan toplar şu an yazıya dönüşüyor. Yeni sıra, "Hedef tamam!"dan sonra:
- 0,00: cephane paneli zıplar, üst ortada altın **para rozeti** "+0" belirir.
- 0,15'ten: kalan toplar panelden tahtaya **salvo** gibi fırlar (var olan salvo topu ve izi), 6–14 uçuş (kalan ≤ 6 → her top bir uçuş;
  fazlası uçuş başına eşit paylaşılır, en çok 14 uçuş, 0,08 sn arayla), her biri rastgele bir bloğa çarpar: blok Pota patlamasıyla
  patlar ve içinden **altın para** (Blender `vfx-coin`, var) fırlayıp rozete uçar; rozet her varışta yuvarlanarak artar, panel sayacı
  her kalkışta düşer (RollWindow).
- Para toplamı Pota formülüyle `min(20, floor(kalan × 0,1))`; para 0 ise (kalan < 10) toplar yine tahtaya gider, bloklar patlar, rozet
  çıkmaz.
- Son varışta rozet 1,3 taşarak parlar, ardından var olan dağılma (`Disperse`) ve konfeti.
- Süre ≤ 2,0 sn; dokunuş atlamaz (kısa).

**F4. Zincir kelimeleri.** Pota'da yalnız "×d" var. Derinlik 3 "Süper!", 4 "Harika!", 5+ "Muhteşem!" (var olan `CandyHud.Word`,
Pota görünümüne derinlik ≥ 3'ten açılır; "×d" rakamı kalır). 5+ zincirde 0,25 sn ×0,5 yavaş çekim ve konfeti (A'daki kural).

**F5. Boşta ipucu.** 5 sn dokunulmazsa en iyi blok kıpırdar ve parlar (var olan `IdleHint`, Pota görünümüne açılır). U40 kararı:
puan = zincir adımı + o zincirin topladığı **hedef** eşya sayısı; hedef renkli zincir eşit adımda öne geçer.

**F6. İniş yaylanması.** Blok düşünce şu an tekdüze 0,88'e küçülüyor. Yeni: yönlü ezilme (y 0,86 / x 1,08), 0,18 sn'de bir taşma
(y 1,04) ve oturma; alt komşuya küçük ezilme dalgası sürüyor (var).

**F7. Dolum blokları çerçevenin üstünde görünmesin (U47).** Yeni bloklar şu an çerçevenin üstünde gökyüzünde düşerken görünüyor
(Pota'da da). Çerçeve içi **stencil maskesi**: tahta katmanındaki bloklar ve etiketler yalnız çerçevenin iç dikdörtgeninde çizilir.
Çerçevenin üst kenarından "çıkıyormuş" gibi okunur.

**F8. Canlı arka plan (SkyLife).** Gökyüzü ve çatı şu an tamamen durgun. Blender'dan:
- **bulut seti** (3 düşük poligon stilize bulut, toon): uzakta yavaş kayar (0,3–0,6 m/sn), hafif salınır; 5–7 örnek, kameranın
  arkasından dolaşır;
- **kuş sürüsü** (5 kuş, kanat çırpma animasyonlu tek klip): 18–30 sn'de bir uzaktan geçer;
- **şehir bandı** (U27): çatının ardında düşük poligon bina silueti, cam yüzeylerde hafif gün ışığı parlaması.
Renk paleti Pota'nın gökyüzü/sis renklerinden; hiçbiri tahtanın önünden geçmez, parlaklığı tahtadan düşük.

**F9. Kaybetme anı (LoseMoment).** "Cephane bitti" yazısıyla birlikte:
- tahta 0,4 sn'de soluklaşır (bloklar grileşir, %60 parlaklık), kamera 0,3 m geri çekilir;
- hedef panelinde **kalan hedef** satırları kırmızı nabız atar, her satırın yanında "2 kaldı" etiketi;
- toplam kalan ≤ 3 ise ortada "Kıl payı!" yazısı (büyük, sarsılarak).
Sonuç kartı var olan beklemeden sonra gelir (kartın kendisi 2. alt proje).

### Basket

**F10. Sapan hissi (SlingshotFeel).** Çekerken kahraman yalnız nefes alıyor.
- Kahraman elinde **top** tutar (BasketSettings top materyali; nişan boyunca sağ el kemiğine bağlı, atışta ilk top çıkarken gizlenir).
- Çekme miktarına göre kahraman **geri yaslanır** (omurga −12°'ye kadar), kalça 6 cm çöker, baş potaya bakar (Humanoid kemikleri
  LateUpdate'te, klibin üstüne).
- Yörünge çizgisi güçle **kalınlaşır ve parlar**; tam güçte iniş halkası 1 Hz nabız atar; aşırı güçte kırmızı (var).
- Nişanın düştüğü **çember parlar** (çemberin rengi 0,2 sn'de altına yükselir, bırakınca söner).
- Kamera çekme ile 0,4 m geri ve 2° aşağı (kavisli), bırakınca yerine (var olan nişan kamerasının üstüne ofset).

**F11. Iska (MissFeel).** Yalnız "Kaçtı" yazısı var.
- "KAÇTI!" yazı levhası (Blender `lettering`, yeni stil `miss`: kırmızı, eğik, lacivert dış hat), 0,12 sn'de 1,2 taşarak oturur,
  sallanır, 1,0 sn'de söner;
- üst çemberler 0,3 sn kırmızı yanıp söner;
- kahraman omuz düşürür (omurga +8° öne, baş aşağı, 0,4 sn; kahraman o anda görünürse);
- kamera 0,15 sn'de 0,2 m çöker, 0,5 sn'de döner.

**F12. Temiz giriş "SWISH!".** Top çembere değmeden geçerse (top başına çember teması izlenir) çemberin üstünde "SWISH!" levhası
(Blender `vfx-letter-swish`, var) ve altın halka; çembere değip girerse bugünkü geri bildirim. İlk üst geçişin yavaş çekimi ve flaşı
aynen kalır.

**F13. Sayım ve son damga.** Sepet sayacı her inişte 1,25 zıplıyor. Yeni: rakam **yuvarlanarak** değişir (RollWindow), iniş hızlandıkça
zıplama küçülür (titremesin); tur bitince sepet kafesi 0,3 sn altın parlar ve "N TOP!" rozeti kafesin üstünde 1 → 2,2 → 1,8 damgalanır
(PotaFlow'un `FlowBadge`/damga eğrileri; PotaFlow'da zaten var, Basket sahnesinde eklenir).

## 3. Yapı

### Unity (`Games/Pota/`)

- **Ayarlar:** `ColorHuntSettings.Feel` (`FeelTuning`: `Enabled`, F1–F9 süreleri ve ölçüleri) ve `BasketSettings.Feel`
  (`BasketFeelTuning`: F10–F13). Varsayılan açık. Kod içinde `Game.Feel` / `Round.Feel` kısayolu.
- **Var olan stüdyo cilası:** `ColorHuntGame.Studio` A/B'yi seçiyordu; açılış, ipucu, cephane nabzı ve zincir kelimeleri artık
  `Studio || Feel.Enabled` ile Pota görünümünde de çalışır (dokunuş çökmesi ve bedel çipi **kapalı kalır**: Pota'nın seçim halkası ve
  ezilmesi zaten var). `StudioFlight` olduğu gibi.
- **Yeni bileşenler** (`Scripts/ColorHunt/Feel/`, ad alanı `Project.ColorHunt`): `LevelIntro`, `AmmoTension`, `LeftoverFinale`,
  `LoseMoment`, `BoardClip` (maske quad'ı + katman), `SkyLife` (`Scripts/Flow/`'da; ColorHunt ve PotaFlow sahneleri). Saf sayılar ve
  eğriler `FeelCurves` (statik; EditMode testleri). Her bileşen tek işli, `ColorHuntGame` olaylarına abone (`RoundStarted`,
  `RoundEnded`, `AmmoChanged`, `CellBurst`); `Bind` içinde kurulur, `[SerializeField]` referanslar.
- **F3 kurgusu:** `ColorHuntGame.End(true)` dağılmayı hemen değil `LeftoverFinale` bitince çağırır (`FinaleThenDisperse`); Feel kapalıyken
  eski yol. Para sayısı `FeelCurves.LeftoverCoins(kalan)` (Pota formülü), uçuş planı `FeelCurves.FinalePlan(kalan)` (saf, test edilir).
  Para mesh'i `ColorHuntSettings.RoyalCoin` (var), rozet uGUI.
- **F7 kurgusu:** yeni katman `PotaBoard` (TagManager'a yazılır, PotaRoom gibi); blok gövdeleri ve etiketleri bu katmanda. `BoardClip`
  çerçeve içine stencil yazan görünmez bir quad koyar (`Project/PotaStencilMask`, ColorMask 0, Stencil Replace 1). PC ve Mobile
  renderer asset'lerine iki `RenderObjects` özelliği eklenir (`AI Tools/Rendering/Install Board Clip`, idempotent): `PotaBoard`
  katmanı varsayılan opak/saydam geçişlerden çıkar, özellik onları `Stencil Equal 1` ile çizer. Render Graph'ta `RenderObjects`
  destekli (URP 17). Tahta döndüğünde maske çerçeveyle döner (çerçeve karedir, kare içi aynı kalır). Geri dönüş: özellik kurulamazsa
  `BoardClip` uyarı loglar ve bloklar bugünkü gibi çizilir.
- **F8 kurgusu:** `SkyLife` sahne aracında kurulur (`ai_colorhunt_build_scene`, `ai_potaflow_build_scene` → `SceneParts`), prefab
  referansları `ColorHuntSettings.Sky` bloğunda (`Clouds[]`, `Birds`, `CityBand`); hareket saf eğrilerle (`FeelCurves.CloudDrift`,
  `BirdPass`), `Time.time` ile (yavaş çekimden etkilenmesin diye `unscaled`). Kuş klibi `Animator` yerine `Animation` (legacy) ya da
  `PlayableGraph` ile oynar; glTF klibi `ai_import_asset` ile gelir.
- **F10–F13 kurgusu:** `Scripts/Basket/Feel/`: `SlingshotFeel` (`ShotInput`'tan `Pull(amount, aim)` olayı; kemik eğimi
  `FeelCurves.Lean(pull)`; eldeki top; çember parlaması `Hoop.SetGlow`), `MissFeel`, `SwishFeel` (`Ball.RimContact` → `Ball.TouchedRim`
  bayrağı; `BasketRound.HoopPassed` imzası değişmez, `Ball.TouchedRim` okunur), `BasketCountFeel` (`BasketHud` içinde RollWindow;
  `FlowBadge` Basket sahnesine). Kamera ofsetleri `RoundCamera.Offset` (toplamalı, kendiliğinden döner), zaman `TimeScaleController`.
- **Materyal/derleme güvenliği:** yeni shader (`PotaStencilMask`) ve levha materyalleri şablon yolundan (`PotaMaterialTemplates`,
  `RuntimeShaderBuildTests` kapsar).
- **Kayıt:** `ai_colorhunt_record` yeni parametreler: `feel` (on | off), `scenario` ColorHunt sahnesinde de: `intro`, `lowammo`
  (cephane 9'a çekilir), `finale` (kazanma, kalan 140), `lose`, `chain` (tohum 7, 4'lük zincir veren dokunuş), `idle` (7 sn bekler).
  `ai_basket_record` yeni: `feel`, `pull` (1,2 sn'de 0 → 0,8 çekme, 0,3 sn bekleme, atış), `miss` (ıska nişanı), `swish` (temiz giriş
  nişanı; tarama ile bulunur). Levha ve yan yana video `Tools/potalook/compare.py` ve `side_video.py` ile; Pota sütunu `record-pota.mjs`.

### Blender atölyesi (`~/projects/Blender MCP`)

| Tarif | Tür | Ne | Kullanım |
|---|---|---|---|
| `lettering` style `miss` | vfx-flipbook (tek kare) | "KAÇTI!" kırmızı, eğik, lacivert dış hat (var olan `_lettering`'e stil) | F11 |
| `prop:cloud_set` | prop | 3 stilize bulut (metaball → mesh, toon iki ton, ≤ 400 üçgen/bulut) | F8 |
| `prop:bird_flock` | animation | 5 kuş, basit iskelet, 0,5 sn döngülü kanat klibi (`flap`), ≤ 120 üçgen/kuş | F8 |
| `prop:city_band` | prop | 180° düşük poligon bina şeridi (yükseklik 6–40 m, pencere UV'leri), ≤ 6 000 üçgen | F8 |

Hepsi `recipes/` altında tarif, `make` ile üretilir, `preview` PNG'sine bakılır, `ai_import_asset` ile `Games/Pota/Art/Props/<id>/`
ve `Art/VFX/<id>/`e gelir. Kuş kliplerinin glTF'te dışa aktığı `inspect`/manifest `clips` ile doğrulanır.

## 4. Doğrulama

- **EditMode:** `FeelCurves` (şerit ve eşya zamanları, finale planı: uçuş sayısı 6–14, toplar toplamı = kalan, para = Pota formülü;
  iniş yaylanması tepe ve oturma; eğim eğrisi; bulut/kuş yolu), `ColorHuntHint` hedef puanı (U40), `Ball.TouchedRim` kuralı
  (temas → bayrak, yeni atışta sıfır), Feel kapalıyken bugünkü eşikler (geriye dönük). `RuntimeShaderBuildTests` yeni shader'ı görür.
- **PlayMode:** F1 açılış boyunca dokunuş beklenir ve ≤ 1,8 sn'de biter (atlama ≤ 0,3); F2 cephane 9'da nabız ve kenar açık, 0'da
  kapalı; F3 kazanmada rozet para sayısı formüle eşit, finale ≤ 2,0 sn, ardından dağılma ve `RoundEnded`; F7 özellik kurulu ve maske
  quad'ı çerçeve içinde; F8 nesneler sahnede ve hareket ediyor; F9 kalan etiketleri doğru sayıyı yazıyor; F10 çekmede eğim ve top
  görünür, atışta top gizli; F12 temas etmeyen topta SWISH levhası, edende yok; F13 tur sonunda rozet. Feel kapalı → mevcut testler
  olduğu gibi geçer. PotaFlow üç adayla hâlâ tahtaya varır.
- **Blender:** `tools/run.sh test` yeni tarifleri kapsar (üçgen tavanı, kapalılık, klip varlığı).
- **Göz:** her anın kaydı izlenir; kusur varsa düzeltilir. Telefon maliyeti: stencil geçişi ve bulutlar editörde Mobile RP asset'iyle
  ölçülür, nota yazılır.

## 5. İş sırası

1. Ayar blokları, `FeelCurves`, stüdyo cilasının Pota görünümüne açılması (F4, F5, F2'nin nabız kısmı) — testler.
2. F1 bölüm başı; F6 iniş yaylanması.
3. F3 kalan topların finali; F9 kaybetme anı.
4. F7 stencil maskesi (renderer özelliği + katman + shader).
5. Blender: `miss` levhası, bulutlar, kuşlar, şehir bandı → içe aktarma; F8 SkyLife.
6. Basket: F10 sapan, F11 ıska, F12 swish, F13 sayım.
7. Kayıt araçları (`feel`, senaryolar), Pota kayıtları, levhalar ve videolar; göz kontrolü ve düzeltme turu.
8. Belgeler: README, CLAUDE.md §12, backlog (U21/U27/U40/U47 kapanır), hafıza; Pota için teslim belgesi taslağı (`docs/handoff/`).

## 6. Riskler

- **Stencil ve Render Graph:** `RenderObjects` katman filtresi Forward+ ile çalışır ama tahta katmanının varsayılan geçişlerden
  çıkması gölge çizimini etkilemez (gölge geçişi katman maskesini kullanmaz). Çalışmazsa geri dönüş yukarıda.
- **Kemik eğimi klibin üstüne:** Humanoid'de `LateUpdate` dönüşleri Animator'dan sonra uygulanır; `Shoot` klibi sırasında eğim 0'a
  karışmalı (bırakınca 0,15 sn'de). Deri bayatlığı yok (Play Mode).
- **Blender animasyon teslimi:** glTF'te iskeletli kuş klibi; `ai_import_asset` klip sayısını ölçer. Sorun çıkarsa kanatlar vertex
  animasyonlu shader ile (geri dönüş).
- **Kapsam:** 13 madde; her adım ayrı commit, testler yeşil. Tuna aralarda bakıp yön değiştirebilir.
