# Renk Avı: "Pota'dan daha iyi" patlama adayları ve stüdyo cilası — tasarım (2026-09-30)

Tuna'nın isteği: Pota'nın bulmacası birebir alındıktan sonra "üstüne inşa et"; Unity ve Blender'ın katabildiğiyle iki "daha iyi" aday,
Tuna seçer (2026-09-30 spec'inin 2. kararı). Bu oturumda verdiği kararlar:
- **Odak:** patlama ve zincir (önerilen seçenek). Bloklar, sahne ve HUD düzeni Pota'daki gibi kalır.
- **Yol:** iki zıt yön. A "Şeker patlaması" (stilize) ve B "Gerçek kırılma" (3B).
- **Ek istek:** "kırılan eşyanın üstteki hedefe gidip sayıyı azaltma animasyonuna da bir el at; genel olarak eksik gördüğün yerleri de
  iyileştir, stüdyo kalitesine yaklaşmamız lazım". §3'teki ortak cila listesi bu istekle onaylandı.
- Bölüm 1 (ne göreceksin), ortak cila listesi ve bölüm 2 (nasıl yapılacak) onaylandı.

## 1. Amaç ve ölçüt

Bulmacanın anlık hissini (dokunuş, patlama, zincir, eşyanın hedefe varışı) stüdyo oyunlarının (Royal Match, Candy Crush) seviyesine
yaklaştırmak. İki aday aynı tahtada ve aynı hamlelerle Pota'nın yanında gösterilir; Tuna birini seçer, kazanan ayrı bir cila turunda
varsayılan olur.

İş bittiğinde:
1. `ColorHuntSettings.Look` = **Pota / Candy (A) / Shatter (B)** seçilebilir. Pota seçeneği bugünkü birebir görünümdür, değişmez.
2. **Pota | A | B** üç sütunlu yan yana video; üç anın kare levhası (dokunuş patlaması, 3–4'lük zincir, kazanma); her adayın tek
   patlamasının yavaş çekim yakın planı. Hepsi `docs/screenshots/` altında.
3. Oyun ritmi üç görünümde aynı: dokunuşta 0,11 sn şişme, zirvede (0,0605 sn) patlama, zincir adımları 0,15 sn, düşüş, dönüş.
   Sayılar ve kurallar değişmez.
4. Tüm testler yeşil. "Her varışta hedef sayısı bir düşer, kuraldakinin altına inmez" kuralı üç görünümde de test edilir.
5. Canlı parçacık ve parça sayısı en çok Pota'nın 1,5 katı. Pota havuzları: 160 parça, 72 yıldız, 160 nokta, 12 flaş, 12 halka,
   16 puf, 240 konfeti.
6. Ücretli çağrı yok, Pota salt okunur, push yok.

## 2. Adaylar

Her iki aday da patlamayı ve zincir hissini değiştirir. §3'teki ortak cilayı ikisi de taşır.

### A "Şeker patlaması" (`Candy`)

Stilize, Royal Match çizgisi. Dış hatlı, iri, okunaklı şekiller.
- **Puf:** blok, çizgi film duman bulutuna döner. Blender'da çizilmiş kare kare doku (4×4 = 16 kare, 256 px), beyaz ve hafif blok
  rengine boyalı, koyu dış hatlı, ~0,35 sn.
- **Parlama:** ışınlı yıldız şeklinde beyaz flaş (8 ışın), 0,12 sn içinde büyüyüp söner. Bloom ile hafif parlar.
- **Parçalar:** 6–10 iri, dış hatlı şeker parçası (Core `Project/Toon` + dış hat, blok rengi). Kavis çizerek, dönerek saçılır,
  yerçekimiyle düşer, 0,6–0,9 sn'de küçülerek kaybolur.
- **Yıldızcıklar:** 4–8 küçük yıldız çevrede yanıp söner.
- **Zincir:**
  - Her adım: stilize halka dalgası (kare kare doku). Ekran kenarında zincirin renginde parlama; güç derinlikle artar, tavanı var.
  - Kamera vuruşu derinlikle biraz güçlenir.
  - Yazılar Lilita One, zıplayarak girer, 0,6 sn'de söner:

    | Derinlik | Yazı |
    |---|---|
    | 2 | İyi! |
    | 3 | Süper! |
    | 4 | Harika! |
    | 5+ | Muhteşem! |

  - 5+ zincirde konfeti patlaması ve 0,25 sn yavaş çekim (×0,5, `TimeScaleController.SlowMo`).

### B "Gerçek kırılma" (`Shatter`)

Unity'nin 3B gücü.
- **Kırılma:** blok, Blender'da önceden kırılmış ~10 parçaya ayrılır (yastık bloğun aynısı, dışbükey parçalar).
  - Parçalar blok rengiyle URP Lit'te, sahnenin ışığı ve oda yansımasıyla çizilir.
  - Ön yüzdeki çıkartma (eşya + rakam) parçaların ön yüzlerinde kalır: parçalar ön yüz için düzlemsel UV taşır ve atlastaki hücreyi okur.
- **Hareket:** parçalar patlama merkezinden dışa ve kameraya doğru fırlar, döner, yerçekimiyle düşer. Çerçevenin iç kenarlarına ve
  arkadaki cama çarpıp seker (kendi basit ve belirlenimci hareketimiz; fizik motoru yok). 0,8–1,2 sn'de küçülerek kaybolur.
- **Duman:** Blender'da ışıklı duman simülasyonundan kare kare doku (4×4, 256 px). Blok rengine hafif boyanır; patlama ışığıyla
  aydınlanmış görünür.
- **Kıvılcımlar:** uzayan, iz bırakan kıvılcımlar (8–14). Patlama anındaki nokta ışık Pota'dakinden güçlüdür, parçaları ve dumanı
  aydınlatır.
- **Şok dalgası:** havayı büken halka. Kameranın opak dokusunu kırar; yalnız bulmaca kamerasında ve bu görünümde açılır.
- **Zincir:**
  - Her adımın parçaları bir öncekinin üstüne dökülür.
  - 4+ zincirde 0,3 sn yavaş çekim (×0,4) ve kamera zincirin ağırlık merkezine doğru hafifçe yaklaşıp geri döner.

## 3. Ortak stüdyo cilası (A ve B'de açık, Pota'da kapalı)

1. **Hedefe uçuş ve HUD** (Tuna'nın isteği; bugün: 0,55 sn düz kavis, varışta ikon 1,25 büyür, hepsi aynı anda kalkar):
   - **Kalkış:** eşya patlamadan fırlar; 0,12 sn'de 1,4'e taşarak büyür, ±12° sallanır, 0,06 sn asılı kalır.
   - **Yol:** kavisli (ikinci dereceden Bezier; kontrol noktası yana açılır, zincirde sırayla sol/sağ yelpaze). Hedefe doğru hızlanır
     (ease-in), yol yönüne hafif döner, arkasında parıltı izi bırakır.
   - **Akış:** aynı adımın eşyaları 0,05 sn arayla kalkar.
   - **Varış:**
     - hedef ikonu ezilip uzar (squash & stretch, 0,22 sn);
     - küçük yıldız patlaması ve halka;
     - sayı eski rakamı yukarı kaydırıp yenisini getirir, beyazdan altına parlar;
     - Core ses sentezleyicisinde "tık"; aynı akış içinde her varışta perde biraz yükselir, tavanı var.
   - **Hedef 0:** ✓ damga gibi basılır (1,6 → 1 taşarak), yıldızlar saçılır, kutu kenarı yeşil parlar, "tamam" sesi.
   - Varış anı ve sayı düşüşü bugünkü kuraldadır: her varışta bir düşer (`LowerShown`); cila yalnız görüntüyü değiştirir.
2. **Dokunuş tepkisi:** parmak bloğa değince blok 0,08 sn'de 0,92'ye basılır ve hafif parlar; parmak kalkınca geri yaylanır. Bloğun
   bedeli cephane sayacının yanında hayalet "−12" olarak belirir (bedel yetmiyorsa kırmızı). Not: bu kurallarda dokunuş tek bloğu kırar,
   zincir düşüşlerden doğar; dokunmadan önce parlatılacak "aynı renkli grup" yoktur (onay sırasındaki taslak bu maddede düzeltildi).
3. **Tahtanın açılışı:** tur başında ve "Yeni tahta"da bloklar alttan üste dalga halinde (satır başına 0,03 sn gecikme) 0'dan zıplayarak
   (1,15 taşma) gelir; toplam ≤ 0,6 sn. PotaFlow kendi halka açılışını korur.
4. **Boşta ipucu:** oynarken 5 sn dokunulmazsa, parası yeten bloklar içinde en uzun zinciri veren (kuralların `Break` simülasyonuyla)
   blok hafifçe kıpırdar (±4°, 2 kez) ve parlar. Dokununca
   hemen durur, 5 sn sonra tekrar eder. Kazanma/kaybetme ve salvo sırasında kapalı.
5. **Cephane sayacı:** salvo sırasında rakamlar tıkır tıkır döner (her düşüşte kısa bir kayma). Hiçbir bloğa yetmeyince sayaç kırmızı
   nabız atar (1 Hz).

Bu işe girmeyenler: kazanma sonrası akış (banner, yıldızlar, smaç), özel toplar/engeller/güçlendiriciler, Pota'nın yeni alt barı (U28),
dönüş ve düşüş hissi (Pota'daki gibi kalır).

## 4. Yapı

### Unity (Pota oyunu, `Games/Pota/`)

- **Seçim:** `ColorHuntSettings.Look` (`PuzzleLook { Pota, Candy, Shatter }`); PotaFlow aynı asset'i kullanır.
- **Patlama arayüzü:** `IBurstStyle`. `ColorHuntGame` patlamanın zirvesinde, zincir adımında ve kazanmada seçili stili çağırır.
  - Bugünkü `ColorHuntFx` "Pota" stilidir, davranışı değişmez.
  - `CandyBurst` ve `ShatterBurst` yeni sınıflardır. Hepsi havuzlu ve oyun zamanında çalışır (anlık yavaşlama onları da dondurur).
  - Stilin varlığı eksik ya da bozuksa (flipbook/mesh yok) uyarı loglanır, o tur için Pota stiline düşülür.
- **Ortak cila:** her madde ayrı ve küçük bir bileşendir: `CollectFlight` (HUD uçuşu, varış, sayaç kayması, ✓ damgası), `PressFeedback`,
  `BoardIntro`, `IdleHint`, `AmmoCounter`.
  - Eğriler saf statik `StudioCurves` sınıfındadır (EditMode ile test edilir).
  - Pota seçiliyken bileşenler kapalıdır; bugünkü kod yolu çalışır.
- **Sesler:** "tık" ve "tamam" Core `SynthSfx` benzeri sentezle (`PotaSounds`'a eklenir); Pota'nın sesleri aynen kalır.
- **Kamera ve zaman:** yavaş çekim `TimeScaleController.SlowMo`, kamera vuruşu ve yaklaşması `CameraShake`/`ColorHuntCamera` üzerinden,
  toplamalı ve kendiliğinden geri döner.
- **Şok dalgası (B):** kameranın opak dokusunu okuyan halka shader'ı. Bulmaca kamerasında `requiresColorTexture` yalnız Shatter seçiliyken
  açılır. Telefon maliyeti editörde ölçülüp not edilir.
- **Derleme güvenliği:** yeni shader'lar ve materyaller şablon yolundan gider (`RuntimeMaterial.From`, `PotaMaterialTemplates`).
  `RuntimeShaderBuildTests` onları da kapsar.
- **Kayıt:** `ai_colorhunt_record`'a `look` ve yakın plan (`closeup`: kamera zoom + zaman ölçeği) seçenekleri. `compare.py` üç satırlı
  levha (Pota, A, B) ve üç sütunlu video üretir.

### Blender atölyesi (`~/projects/Blender MCP`, stüdyo haritası madde 4 "efekt hattı"nın ilk adımı)

- **`vfx_flipbook`:** tarif adı → kare kare doku (sprite sheet, alfa kanallı PNG) + teslim manifest'i (`kind: vfx-flipbook`, kare sayısı,
  satır/sütun, süre). Tarifler:

  | Tarif | Kullanıldığı aday | Ne |
  |---|---|---|
  | `puff_toon` | A | çizgi film bulutu, dış hatlı |
  | `ring_toon` | A | stilize halka |
  | `smoke_lit` | B | ışıklı duman |
  | `shock_ring` | B | şok dalgası için normal/kırılma dokusu |

  Önizleme levhasıyla göz kontrolü yapılır.
- **`vfx_fracture`:** yastık blok (Pota ölçüleri: 0,9 kutu, 0,22 köşe yarıçapı, ön yüz 0,1 şişme) → ~10 dışbükey parça. Ön yüz için
  düzlemsel UV korunur, parçaların merkezleri ve hacimleri manifest'e yazılır. GLB olarak `kind: vfx-mesh` teslim edilir.
- Unity'ye `ai_import_asset` ile `Games/Pota/Art/VFX/<id>/` altına gelir; manifest ile Unity'nin ölçtükleri karşılaştırılır.

## 5. Doğrulama

- **EditMode:**
  - `StudioCurves`: kalkış taşması, Bezier yolu, varış esnemesi, damga, açılış dalgası, ipucu kıpırtısı.
  - Stil seçimi ve Pota'ya düşme.
  - Havuz tavanları.
  - Blender teslimlerinin içe aktarma karşılaştırması (`matches`).
- **PlayMode:**
  - Her görünüm için dokunuş, zincir ve kazanma senaryosu hatasız koşar.
  - Hedef sayısı her varışta bir düşer, sonunda kuraldakine eşit olur.
  - Zincirde eşyalar 0,05 sn arayla kalkar.
  - Boşta ipucu 5 sn'de başlar, dokununca durur.
  - Canlı parçacık ve parça sayısı tavanın altında.
  - PotaFlow seçili görünümle geçişi tamamlar.
- **Blender:** araç testleri (`tools/run.sh test`): kare sayısı ve alfa, parça sayısı, kapalılık, ön yüz UV aralığı.
- **Göz kontrolü:** Pota | A | B levhaları ve videosu, yakın planlar. Kusur görülürse "bitti" denmez (kalite ilkesi).
- **Telefon (isteğe bağlı, sonda):** Look seçmeli iPhone geliştirme derlemesi (`ai_build`), Tuna elinde dener.

## 6. İş sırası

1. Blender: `vfx_flipbook` ve `vfx_fracture` araçları ve testleri; dört doku + kırık blok; Unity'ye içe aktarma.
2. Unity: `PuzzleLook` seçimi, `IBurstStyle` ve Pota stilinin arkasına alınması (davranış aynı; mevcut testler yeşil kalır).
3. Ortak cila: hedefe uçuş ve HUD, dokunuş tepkisi, tahta açılışı, boşta ipucu, cephane sayacı.
4. A "Şeker patlaması".
5. B "Gerçek kırılma".
6. Kayıt aracı ve karşılaştırma: `look`, yakın plan, üçlü levha ve video.
7. Belgeler (README, CLAUDE.md §12, backlog, hafıza).

## 7. Riskler

- **Flipbook kalitesi:** Blender'da duman simülasyonu yavaş ve ayara duyarlı. Tarifler önce düşük çözünürlükte denenir, önizleme levhasıyla
  bakılır.
- **Stil tutarlılığı:** A'nın dış hatlı parçaları ile Pota'nın parlak blokları birbirine uymayabilir. Yakın planda bakılır, dış hat
  kalınlığı ayarlanır.
- **Şok dalgası maliyeti:** opak doku kopyası telefonda ek bir tam ekran okuma. Ölçülür; pahalıysa halka ekranın küçük bir bölgesine
  sınırlanır ya da bozulma yerine parlak halkaya düşülür.
- **Kırık parçaların çıkartması:** ön yüz UV'si parçalar arasında kesintisiz kalmalı. Blender testi aralığı denetler, Unity'de yakın
  planla bakılır.
- **Kapsam:** iş büyük (iki depo, beş cila maddesi, iki aday). Sıra korunur; her adımda testler yeşil kalır ve commit edilir.
