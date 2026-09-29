# Renk Avı: Pota'nın güncel bulmaca görünümü ve efektleri Unity'ye birebir — tasarım (2026-09-30)

Tuna'nın isteği: "Pota projesinde tasarımı baya değiştirdim, artık çok daha güzel efektler, tasarımlar var; onları birebir kullan,
sonra üstüne inşa et. Patlama efekti çok farklı; puzzle bölümü aktif oyunda çok daha iyi. Assetleri alabiliyorsan Pota klasöründen
al kullan; oradaki puzzle'da yer alan kare tasarımı ve efektler daha iyi." Basket turu farklı kalabilir (Unity'deki daha fizikli ve güzel).
Sorulara "önerilerini yap" dedi: 1A 2A 3A ve varsayılanlar (§0).

## 0. Kararlar (Tuna 2026-09-30)

| # | Karar |
|---|---|
| 1 | **Kapsam A:** Pota'nın güncel bulmacasının **görünümü, efektleri, zamanlamaları ve arayüz stili** birebir; oyun kuralları bugünkü Unity kuralları (normal bloklar). Özel toplar (bomba, çizgi, altın), engeller (file, buz, hakem), güçlendiriciler, öğretici vurgusu ve smaç ayrı bir sonraki iş. |
| 2 | **Önce birebir, sonra üstüne:** bu spec yalnız birebir aşamasını kapsar ve Pota ile yan yana video ve kare levhasıyla biter. Ardından Unity/Blender'ın katabildiğiyle iki "daha iyi" aday ayrı bir spec'le gelir, Tuna seçer. |
| 3 | **Sesler:** Pota'nın efekt sesleri (mp3) kopyalanır ve bulmacada kullanılır; Pota'da sentezle üretilenler (`kombo`, `carpma`) Core `SynthSfx`'e parametreleriyle taşınır. |

Varsayılanlar (itirazsız):
- Yeni görünüm hem `ColorHunt` sahnesine hem `PotaFlow`'a gelir.
- Tahta her iki sahnede de Pota'daki gibi dev potanın içinde durur, çerçevesiyle ekranı genişlikte doldurur.
- Pota salt okunur. Dosya varlıkları Pota'dan kopyalanır; kodla üretilen dokular Pota'nın **scratch kopyasında** çalıştırılıp PNG olarak dışa aktarılır (kopyada değişiklik serbest). Her sayının Pota'daki kaynağı not edilir.
- Yazılar Türkçe kalır (CEPHANE, HEDEF).
- HUD rakam fontu Lilita One (OFL, Google Fonts'tan TTF).
- Ücretli çağrı yok, push yok.

## 1. Pota'da bugün (okundu 2026-09-30, `~/projects/Pota` HEAD c742a6a; bulmaca dosyaları `main` ile aynı)

Kaynak: araştırma raporu (bu oturum) ve `onizleme/renkavi-wk-04-acilis.png`, `balon-dokunus.png`, `balon-patlama-008/016.png`, `renkavi-wk-05-salvo.png`,
`donus-orta.png`, `karsilastirma-balon.png`; spec `docs/superpowers/specs/2026-09-27-balon-gorunum-design.md`.

- **Blok ("balon A", `src/yastik.ts:11-37`, `yapbozSahne.ts:596-605`)**
  - Şekil: `RoundedBox(0.9, 0.9, 0.9, segment 5, yarıçap 0.22)`. Ön yarı yastık gibi şişer: `z += 0.1·(1−u²)(1−w²)·z/h`, normaller eğimden düzeltilir.
  - Malzeme: `MeshStandard` beyaz, roughness 0.16, metalness 0.02, kendi oda yansıması (RoomEnvironment PMREM, yoğunluk 0.45). Doku yok.
  - Renk örnek başına. Şeker tonları: kırmızı #e8352e, mavi #2a82e8, yeşil #46bf36, sarı #f8c22a, mor #a246db.
  - Izgara panoya sığar: hücre `s = (8.8 − 0.4)/n` (7×7'de 1.2).
- **Etiketler (`yapbozSahne.ts:137-213, 272-412`)**
  - Tek 2048² kanvas atlası: 11×11 hücre × 186 px; 1–99 rakam, yıldız, 5 eşya, 3 özel, 3 engel.
  - Eşya: bloğun 0.72'si, çıkartma gölgesiyle, merkezde y +0.06.
  - Rakam: Lilita One, beyaz, lacivert #0b1a31 kontur (0.18 × punto), gölgeli; 0.44 ölçekte **sağ alt köşede** (+0.26, −0.28).
  - Bütün etiketler tek instanced quad katmanı.
- **Durumlar**
  - Beyaz flaş: `lerp(ton, beyaz, f)·(1 + 1.4f)`.
  - Cephane yetmez: ton `lerp(#8792a6, 0.5)·0.62`, rakam #ff7a72.
  - Seçim: beyaz halka (0.46 / 0.055, opaklık 0.9), 0.35 ezilme.
- **Çerçeve ve sahne**
  - Dev pota: buzlu cam pano 8.8² (#bfe3ff, opaklık 0.32, `pano-a.png`), turuncu #ff8a2a çubuklar 0.35 kalın / 0.9 derin, çember R 1.6, 6 halkalı file, gri direk.
  - Kamera 38°, çerçevenin dış genişliği (9.5) ekran genişliğinin %98.5'i.
  - Işık: gökyüzü #8ec9f2, sis #9ed3f5 30–130, yarıküre #dcefff/#8a7a66 0.8, güneş #fff4e0 2.1 (−24, 26, 24) gölgeli, ortam yansıması 0.55.
  - Son işlem: Neutral tonemapping ×1.12 pozlama, bloom (güç 0.28, yarıçap 0.45, eşik 0.86, yarım çözünürlük), vinyet 0.32 / 0.55.
- **Efektler (`renkAviFaz.ts:46-115` `RENK_AVI_HIS`, `patlamaEfekt.ts:261-360`, `efekt.ts:318-392`)**
  - **Dokunuş:** blok 0.11 s'de 1 → 1.18 şişer; **zirvede** (0.0605 s) sarsıntı 0.08, anlık yavaşlama ×0.12 × 0.05 s, `kirildi` sesi, puf, 14 nokta parçacık, patlama katmanları, 4 komşu 0.5 ezilir.
  - **Salvo:** 0.5 s; `min(24, bedel)` top, 0.275 s'e yayılır, her biri 0.21 s uçar.
    - Kameraya doğru `sin(πo)·0.9` kavis çizer.
    - Top r 0.19, kanvas basketbol dokusu, 14 rad/s üç eksende döner.
    - İz: 10 noktalı şerit, genişlik 0.34, #ffa040, toplamalı karışım.
  - **Zincir adımı:** 0.15 s; ölçek %55'te 1.24'e çıkar, sonra `1.24·(1−u)^1.5`. Zirvede zincir derinliğine göre büyüyen sarsıntı, yavaşlama, `kombo` sesi (pentatonik), "×d" yazısı, puf ve parçacıklar.
  - **Patlama katmanları:**
    - Beyaz flaş 0.16 s, renkli flaş 0.22 s, halka 0.38 s.
    - Yassı parlak parçalar: `min(34, 14 + 5d)` adet, 56° koni, yerçekimi 12, ömür 0.7–1.2 s.
    - Dört köşeli yıldız kıvılcımlar (`10 + 2d`, pırıltılı).
    - Tek nokta ışık (yoğunluk `6·(1 + 0.25d)`, 0.18 s), puf küresi (0.36 s), GL nokta parçacıklar.
  - **Düşüş:** sabit ivme, 1 hücre 0.25 s. İnişte düzgün ezilme 0.12 (6/s söner), puf, `dustu` sesi.
  - **Dönüş:** 0.42 s smoothstep. Döner tahta çerçeveye sığmak için küçülür (45°'de ~0.8); etiketler ilk %70 tahtaya yapışık, sonra dikelir.
  - **Eşya HUD'a uçar:** 0.55 s eğri; hedef sayısı **her varışta birer düşer**.
  - **Kazanma:** bloklar şişip parçacık verir, sarsıntı 0.14, kâğıt konfeti (220 yassı kutu).
  - **Sarsıntı:** toplamalı, tavan 0.5, doğrusal 2.2/s söner.
- **HUD (`index.html`):** lacivert gradyan #24407a → #142850, altın kenar #ffc83d 3 px, köşe 18; Lilita One rakamlar; oklar sağda üst üste 64 px daireler.
- **Varlıklar**
  - Kopyalanabilir: `public/assets/esya-*.png`, `engel-*.png`, `ozel-*-a.png` (256² yeni takım), `ok-a.png`, `pano-a.png`, `ses/*.mp3`.
  - Kodla üretilen: etiket atlası, basketbol dokusu, iz, yumuşak disk, halka, yıldız, oda yansıması.

## 2. Hedef ve "birebir"in ölçüsü

1. **Aynı anlar yan yana:** Pota (kopyadan, aynı telefon oranında) ve Unity kayıtları şu anlarda eşleşir:
   - boş tahta, seçim, dokunuşta şişme zirvesi, patlama +0.08 s ve +0.16 s;
   - salvo ortası, düşüş inişi, dönüş ortası, eşyanın HUD'a varışı, kazanma.
2. **Aynı sayılar:** bütün boyut, renk, süre ve eğri Pota'daki değerle aynı. Her sayı ayarda kaynağıyla not edilir. Farklı çıkan tek şey motorların doğası gereği farklı olanlar: BRDF, bloom algoritması, gölge filtresi.
3. **Aynı dokular:** kodla üretilen dokular Pota'nın kendi kodundan dışa aktarılır, yeniden çizilmez.
4. **Oynanış bozulmaz:** mevcut Renk Avı ve PotaFlow testleri yeşil kalır; kurallar değişmez.
5. **Tahta dizilimi:** Pota'nın kuralları 09-22'den sonra çok genişledi (özel toplar, engeller). Karşılaştırmada aynı tohumla aynı dizilim beklenmez, **anlar** eşlenir. Pota'da engelsiz, özelliksiz bir bölüm/tohum seçilir (`__pota.faz2.kur(bölüm, cephane, tohum, false, [])`).

## 3. Yaklaşım

- **Kopyadan dışa aktarma:** Pota'nın scratch kopyasına küçük bir dışa aktarma sayfası/kancası eklenir. Sayfa, Pota'nın kendi fonksiyonlarıyla şunları PNG olarak yazar:
  - etiket atlası (2048², 11×11 düzeniyle);
  - efekt dokuları (yumuşak disk, halka, yıldız, iz), basketbol dokusu;
  - oda yansıması (RoomEnvironment küp yüzleri).

  Dosyalar `Games/Pota/Art/PotaLook/` altına gelir.
- **Dosya varlıkları:** yeni 256² eşya ikonları (eskilerin yerine), `pano-a.png`, `ok-a.png` ve sesler `Games/Pota/Art/`, `Games/Pota/Audio/` altına kopyalanır.
- **Sayılar:** mevcut `ColorHuntSettings` asset'inde durur (eski görünüm değerleri Pota değerleriyle değişir, yeni alanlar eklenir; eski görünüme dönüş anahtarı tutulmaz). Kod varsayılanları Pota değerleridir, her birinin yanında kaynağı yazar.
- **Koddaki değişiklik bölgeleri:**
  - blok mesh'i (yastık şişkinliği), blok malzemesi (yansıma probu);
  - etiket katmanı (atlas + instanced quad, UV dikdörtgeni gölgelendiricisi);
  - `ColorHuntFx` (patlama katmanlarının tamamı), `BallSalvo` (kavis, iz şeridi);
  - `ColorHuntGame` animasyon kuyruğu (şişme-zirve-patlama, zincir eğrisi, düşüş, dönüş);
  - `ColorHuntJuice` (toplamalı sarsıntı, ×0.12/×0.06 yavaşlama), `ColorHuntHud` (stil, düzen, sayının varışta düşmesi);
  - sahne araçları (kamera 38°, ışık, sis, son işlem Volume'u, dev pota çerçevesi).

  Kurallar (`ColorHuntRules`) değişmez.

## 4. Bileşenler

| Birim | Sorumluluk |
|---|---|
| `PillowBlockMesh` (saf) | `RoundedBoxMesh` + ön şişkinlik ve normal düzeltmesi (`yastik.ts` formülü). |
| `BlockLook` | Blok malzemesi (URP Lit, smoothness ≈ 1 − 0.16, metallic 0.02), şeker tonları, durum renkleri (flaş, yetmez), `MaterialPropertyBlock` ile örnek başına renk. |
| `LabelAtlas` + `LabelLayer` | Dışa aktarılmış atlas; rakam ve eşya quad'ları (atlas dikdörtgeni başına UV). Dönüşte %70 yapışık → dikelme. URP HLSL unlit, alfa. |
| `PotaBurst` (`ColorHuntFx` yerine) | Beyaz/renkli flaş, halka, yassı parçalar (ezik ikosfer mesh parçacık), yıldız kıvılcımlar, puf, nokta parçacıklar, havuzlu nokta ışık. Hepsi oyun zamanında (anlık yavaşlama onları da dondurur). |
| `BallSalvo` (yenilenir) | Kavis (kameraya doğru), üç eksende dönüş, iz şeridi (`TrailRenderer` genişlik eğrisi (1−t)^0.6 + dışa aktarılmış doku), çarpma flaşı/ezilmesi. |
| `ColorHuntGame` kuyruğu | Dokunuşta şişme → zirvede patlama; zincir adımı 0.15 s eğrisi; komşu ezilmesi; sabit ivmeli düşüş, düzgün ezilme; dönüşte sığdırma ölçeği. |
| `ColorHuntJuice` / `PotaShake` | Toplamalı sarsıntı (tavan 0.5, 2.2/s), yavaşlama ×0.12 (dokunuş) / ×0.06 (zincir), Pota sesleri ve `kombo` merdiveni. |
| `ColorHuntHud` (stil) | Lacivert gradyan panel, altın kenar, Lilita One rakamlar, oklar sağda üst üste, hedef sayısı her eşya varışında düşer. |
| Sahne araçları (`ColorHuntSceneTool`, `PotaFlowSceneTool`) | Kamera 38° + genişlik sığdırma, ışık/sis/gökyüzü, bulmaca Volume'u (Neutral, bloom, vinyet); `ColorHunt` sahnesinde de dev pota çerçevesi ve saha. PotaFlow'da iki Volume: geçişte ağırlık basket → bulmaca karışır. |
| `ai_colorhunt_record` | Renk Avı'nı betikle oynatıp kare kare kaydeder: tohum, dokunuş listesi, dönüş. |
| Pota kopyası kayıt betiği | Aynı anları Pota'da kaydeder: `?test=renkavi`, `__pota.faz2.kur/kir/dondur/yavas`. |

## 5. Test ve doğrulama

- **EditMode:**
  - yastık mesh'i (ön yüzün tepesi +0.1, arka yüz düz, normaller birim);
  - atlas UV eşlemesi (rakam 1–99, eşya hücreleri);
  - şişme/zincir/dönüş eğrileri (zirve anı, değer);
  - sarsıntı modeli (toplama, tavan, doğrusal sönme);
  - dönüş sığdırma katsayısı (45°'de ≈ 0.8).
- **PlayMode:**
  - dokunuşta blok ≈ 0.06 s'de zirveye çıkar ve patlama o anda başlar;
  - hedef sayısı eşya varışında düşer;
  - dönüşte etiketler %70'ten sonra diker;
  - mevcut Renk Avı ve PotaFlow testleri yeşil kalır.
- **Görsel:** Pota | Unity yan yana video ve levha (§2.1 anları), `docs/screenshots/2026-09-30-potalook-*`. Tuna bakar.

## 6. Kapsam dışı (sonraki işler)

- Özel toplar, engeller, güçlendirici çipleri, altın top ve potaya inişi, geçiş halkaları, öğretici vurgusu, smaç, kazanma sonrası akış.
- "Üstüne inşa et" adayları (ayrı spec).
- Telefon derlemesi.

## 7. Riskler

- **Farklı fizik tabanlı modeller:** three.js Standard ile URP Lit BRDF'leri ve bloom algoritmaları aynı değil. Değerler önce birebir girilir, sonra gözle yan yana ayarlanır; her ayar gerekçesiyle not edilir.
- **Cam sıralaması:** buzlu cam pano saydam; sıralama ve derinlik sorunları olabilir.
- **Nokta ışık sınırı:** URP Forward+'ta nokta ışık sınırları var; havuz tek ışıkla yetinir.
- **Font gecikmesi:** atlas dışa aktarılırken Lilita One yüklenmemiş olabilir (Pota'da `font-display: swap`). Aktarma, yazı tipi hazır olunca yapılır ve görsel olarak kontrol edilir.
- **Mobil:** parçacık sayıları Pota'nınkiyle aynı; mobil performans ayrıca ölçülür.
