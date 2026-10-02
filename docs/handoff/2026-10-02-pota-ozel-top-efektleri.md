# Pota'ya teslim: özel top efektleri (Royal + Basketbol katalogu)

_2026-10-02. Kaynak: Unity MCP atölyesi. Okuyan: Pota projesinde çalışan Claude Code oturumu ve Tuna._

Unity atölyesinde Pota'nın özel topları (çizgi, bomba, altın top) için iki efekt adayı yapıldı ve Tuna'ya video olarak gösterildi:
**1 · Royal Match** ve **2 · Basketbol**. Tuna ikisinden karışık bir sürüm istiyor; hangi anda hangisinin alınacağını aşağıdaki
"Karışım" tablosuna yazacak. Bu belge o karışımı Pota'da (three.js) kurmak için gereken her şeyi tek yerde toplar: her anın tarifi,
zamanlaması, Blender varlıkları, sesler ve Unity'deki çalışan referans kodu.

**Kurallar değişmez.** Pota'nın `src/renkAvi.ts`'i zaten özel topları, geçişleri ve altın topu içeriyor. Unity'deki port bunun birebir
kopyası (Pota'nın kendi koduyla üretilen fikstürlerle doğrulandı). Pota'da yapılacak iş yalnız **görünüm**: `renkAviFaz.ts` ve
`yapbozSahne.ts` / `patlamaEfekt.ts` tarafı.

## 1. Önce bunlara bak

| Ne | Yol |
|---|---|
| Sesli sıralı video (her an: Pota → 1 → 2) | `~/projects/Unity MCP/docs/screenshots/2026-10-01-specials-voiced.mp4` |
| Sessiz yan yana video (Pota \| 1 \| 2) | `~/projects/Unity MCP/docs/screenshots/2026-10-01-specials-side.mp4` |
| An levhaları (satırlar Pota / 1 / 2, sütunlar zaman) | `~/projects/Unity MCP/docs/screenshots/2026-10-01-specials-sheet-{birth,line,bomb,chain,golden}.jpg` |
| Yavaş çekim yakın planlar | `~/projects/Unity MCP/docs/screenshots/2026-10-01-specials-closeup-{royal,court}-{bomb,line}.mp4` |
| Tek kareler | `~/projects/Unity MCP/docs/screenshots/2026-10-01-specials-{royal,court}-{line,bomb,birth,golden}.jpg`, `…-court-combo.jpg` |
| Tasarım (spec) | `~/projects/Unity MCP/docs/superpowers/specs/2026-10-01-colorhunt-special-balls-fx-design.md` §2 |
| Unity'de canlı deneme | Unity MCP projesi, `Assets/_Project/Games/Pota/Scenes/Specials.unity` → Play; üst sıra görünüm (Pota \| 1 \| 2), alt sıra an |

## 2. An katalogu

Her satırın bir kimliği var (`R-…` = 1 Royal, `C-…` = 2 Basketbol, `P-…` = Pota'nın bugünkü hali). Tuna karışımı bu kimliklerle
yazar ("bombada C-Bomba, ama ateş topu R'den" gibi). Sayıların tamamı ve varsayılanları Unity'de ayar sınıflarında durur:
`RoyalTuning` (`Scripts/ColorHunt/Specials/RoyalSpecials.cs` başı) ve `CourtTuning` (`CourtSpecials.cs` başı); Unity yolu
`~/projects/Unity MCP/Assets/_Project/Games/Pota/`. Süreler oyun zamanı, uzunluklar hücre (blok aralığı) cinsinden.

### Doğuş (4'lü / L-T / 5'li eşleşmeden özel top doğar)

| Kimlik | Ne olur | Önemli sayılar | Varlık |
|---|---|---|---|
| P-Doğuş | Düşüş başında hücre özel topa döner; flaş 1,0, çarpma 0,6; ışık flaşı (altın sarı); "ÇİZGİ TOPU!/BOMBA!/ALTIN TOP!" yazısı | Pota `dususleriBaslat` | — |
| R-Doğuş | Gruptaki bloklar doğum hücresine çekilir (küçülerek 0,35'e, ±40° dönerek); özel top 0 → 1,2 → 1 "pıt" diye çıkar; beyaz çekirdek flaşı, 8 ışınlı yıldız, 7 yıldızcık | GatherTime 0,18 sn; PopTime 0,25 sn; BirthFlash 2,2 hücre, Star 2,8 | yıldız dokusu (kodla) |
| C-Doğuş | Bloklar top gibi kavisle sekerek hücreye zıplar (kameraya doğru hafif yükselerek); özel top "yere iner" (ezilip toparlanır), altından beyaz saha çizgisi halkası açılır, hafif toz | GatherTime 0,22 sn; Hop 0,5 / sekme 0,18; halka 0,9 → 2,4 hücre / 0,35 sn | `vfx-dust-ring` (küçük) |

### Bekleme (özel top tahtada dururken)

| Kimlik | Ne olur | Sayılar |
|---|---|---|
| P-Bekleme | Altın top nabız atar | genlik 0,08, 5 rad/s |
| R-Bekleme | Bombanın fitilinden kıvılcım, bomba nefes alır; çizgi topun oku parlar; altında parıltı | kıvılcım 3/sn; nefes ±%3, 1,1 Hz; ok 1,5 Hz; altın yıldızcık 1/sn |
| C-Bekleme | Özel toplar dripling gibi zıplar (altta ezilir); altın yavaşça döner | 2 Hz, zıplama 0,06 hücre; altın 40°/sn |

### Çizgi topu (sütun / satır patlatan)

| Kimlik | Ne olur | Sayılar | Varlık |
|---|---|---|---|
| P-Çizgi | Alan birlikte şişer; satır/sütun boyunca üç flaş (uçlar + orta); sarsıntı 0,12; `cizgi` sesi | Pota `adimBaslat` | — |
| R-Çizgi | Top 0,06 sn ezilip parlar, **ikiye ayrılır**; iki yarım top alev püskürterek iki uca roket gibi uçar; bloklar roket üstünden geçerken **sırayla** patlar; arkada gri duman pufları ve kıvılcım; kenarda flaşla çıkar; eksen boyunca sarsıntı | 6 hücre 0,32 sn'de (≈ 18,75 hücre/sn); puf her 0,5 hücrede; alev jeti 1,8 hücre, yarımların arası açıldıkça uzar | `vfx-ball-halves` (GLB), `vfx-flame-jet` (döngü), `vfx-puff-toon` (gri boyanır) |
| C-Çizgi | Top bir kez sert sektirilir (0,08 sn), **alev almış** iki basketbol topu dönerek iki uca fırlar; arkada alev kuyruğu + hız çizgileri; geçtiği bloklar zıplayıp **sırayla** patlar; uçta beyaz "swish" halkası | 6 hücre 0,3 sn'de; top 0,72 hücre, 720°/sn; alev 1,8 hücre, sarıya çekilmiş (×1, ×1,35, ×0,7); swish halkası 0,5 → 0,9 hücre | `vfx-basketball` (GLB), `vfx-flame-jet`, `vfx-speed-lines` |

### Bomba (3×3)

| Kimlik | Ne olur | Sayılar | Varlık |
|---|---|---|---|
| P-Bomba | Alan birlikte şişer; büyük turuncu flaş (2,6 / 6,5 / 8 / 0,25 sn); sarsıntı 0,2; `bomba` sesi; 0,06 sn anlık yavaşlama | Pota `adimBaslat` | — |
| R-Bomba | Hazırlık 0,18 sn: şişer (1,25), iki kez kırmızı-beyaz yanar, titrer, fitil kıvılcımı; sonra beyaz çekirdek flaşı (0,08 sn), mürekkep hatlı **ateş topu** (~3,2 hücre, 0,45 sn), şok halkası (3,6), 8 kabuk parçası dönerek saçılır, 5 duman, 12 kıvılcım, güçlü ışık; 3×3 merkezden dışa **dalga** gibi patlar (halka başına 0,04 sn); sarsıntı 0,2 | BombLead 0,18; BombRing 0,04 | `vfx-fire-toon`, `vfx-ring-toon`, `vfx-bomb-shell` (8 parça GLB), `vfx-puff-toon` |
| C-Bomba | **Smaç**: top kameraya doğru zıplar (0,14 sn, 1 → 1,6), havada asılı kalır (0,08 sn), çakılır (0,07 sn); çarpmada zemin çatlağı (0,6 sn'de söner), mürekkep hatlı toz halkası, 3×3 bloklar havaya fırlayıp patlar, **"SLAM!"** (0,5 sn), çerçeve kenarında 6–10 fotoğraf flaşı, sarsıntı 0,24 + kamera vuruşu | çarpma = 0,29 sn; BombRing 0,03 | `vfx-floor-crack`, `vfx-dust-ring`, `vfx-letter-slam`, `vfx-basketball` |

### Zincir (bir özel diğerini tetikler)

| Kimlik | Ne olur |
|---|---|
| P-Zincir | Hepsi aynı adımda birlikte şişer, ilk zirvede her tetiklenen için yukarıdaki flaşlar |
| R-Zincir | Tetiklenen özel, ilkinin etkisi kendi hücresine **vardığı an** kendi efektini başlatır (roket bombaya çarpınca bomba patlar). Bir adımda 2+ özel: 0,2 sn ×0,5 yavaş çekim + 0,1 sn beyaz ekran flaşı |
| C-Zincir | Aynı varış mantığı + **"COMBO!"** yazısı + kısa seyirci sesi |

### Altın top sepete iniş

| Kimlik | Ne olur | Varlık |
|---|---|---|
| P-Altın | Hücreden geçiş halkasına 0,32 sn (ivmeli), halkada sarı flaş, potaya 0,5 sn kavis | — |
| R-Altın | Parıltı izi bırakarak iner, halkada altın flaş, potaya kavis; çemberde 10 dönen **altın para** + 10 yıldız + altın parlama | `vfx-coin` (GLB) |
| C-Altın | Dönerek yuvarlanır, geçitten potaya atılır, **file dalgalanır** (altı halka sırayla esner), **"SWISH!"**, 28 altın konfeti | `vfx-letter-swish` |

## 3. Zamanlama modeli: "sırayla patlama"

Pota bugün tetik adımındaki bütün blokları **aynı anda** şişirip patlatıyor. R ve C'de bloklar roket/top geçerken, bomba dalgası
ulaşırken **sırayla** patlar ve zincirdeki özel, etki kendisine varınca ateşlenir. Unity'de bunu saf bir fonksiyon hesaplıyor:

- `Scripts/ColorHunt/Specials/SpecialSchedule.cs` → `Plan(n, step, origin, timing, swellTime)`: her hücrenin şişme başlangıcı,
  her özelin ateşlenme anı, adımın bitişi. En erken varış (Dijkstra benzeri); karşılıklı zincirde her özel bir kez ateşlenir.
  Toplam adım **≤ 0,6 sn**; aşarsa bütün zamanlar orantılı sıkıştırılır (`Pace`). Testleri `Tests/EditMode/SpecialScheduleTests.cs`.
- TypeScript'e doğrudan taşınabilir (bağımlılığı yok). Pota'da `adimBaslat` bu plana göre hücre başına gecikmeli şişme başlatmalı;
  çizilen roket/top konumu aynı formülle hesaplanmalı ki blok tam altından geçerken patlasın (`RoyalSpecials.RocketReach`,
  `CourtSpecials.ReachTime`).
- Bomba hazırlığı (R) ve smaç (C) bombanın **kendi** patlamasından sonra bir yedek görselle oynar; 3×3'ü `ateşlenme + BombLead·Pace`
  anında patlar (R 0,18, C 0,29).

## 4. Blender varlıkları

Hepsi `~/projects/Blender MCP/assets/Pota/<id>/export/` altında, `manifest.json` ile (kare sayısı, satır/sütun, süre, döngü).
Pota'ya kopyalarken `public/assets/` altına koy (Pota'nın düzenine göre).

| id | Tür | Ayrıntı |
|---|---|---|
| `vfx-fire-toon` | flipbook 4×4, 256 px, 0,45 sn | çizgi film ateş topu, sonu gri duman |
| `vfx-flame-jet` | flipbook 4×2, 256 px, **döngü 0,3 sn** | ağız karede (0,5; 0,9), alev karenin −y'sine bakar |
| `vfx-dust-ring` | flipbook 4×4, 256 px, 0,4 sn | yüzü kameraya dönük toz halkası, ortası boş |
| `vfx-speed-lines` | flipbook 4×2, 256 px, 0,25 sn | yatay çizgiler, top karenin sağ kenarında (u ≈ 0,95) |
| `vfx-puff-toon`, `vfx-ring-toon` | flipbook 4×4 | duman pufu (gri boyanır), stilize halka |
| `vfx-floor-crack` | tek kare 512² | alfa ile 0,6 sn'de söndür |
| `vfx-letter-slam/-swish/-combo` | tek kare 512×256 | 2:1 düzlem gerekir |
| `vfx-ball-halves` | GLB, `HalfA` / `HalfB` | yarıçap 0,225 m; kesik yüz x = 0'da, orijin kesik yüzün ortası. **three.js'te kubbe −X'e bakar** (Unity glTFast x'i çevirdiği için orada +X). Bütün top = HalfA ve y'de 180° dönmüş HalfB |
| `vfx-bomb-shell` | GLB, `piece_00`…`piece_07` | 0,45 m bomba topunun kabuğu, çocuk konumu = parçanın merkezi |
| `vfx-coin` | GLB | 0,30 m, kalınlık z yönünde |
| `vfx-basketball` | GLB | 0,45 m, 960 üçgen |

- Flipbook'lar: düz alfa, sRGB, kareler sol üstten satır satır. three.js'te `texture.colorSpace = SRGBColorSpace`; `flipY`
  varsayılan olduğu için satır ofseti `v = 1 − (satır + 1) / satırSayısı`.
- GLB'lerin dokusu içinde gömülü (512×256, para 256×128). Unity'de dış hatlı toon materyalle çizildi (`Project/Toon`); Pota'da
  `MeshToonMaterial` + ters kabuk dış hat ya da Pota'nın blok materyali, Pota'nın görünümüne göre seçilsin.
- Hepsi Blender atölyesinde tariften yeniden üretilebilir (`tools/bl/vfx.py`: `vfx_flipbook`, `vfx_mesh`, `vfx_fracture`).
  Boyut/renk değişikliği gerekirse orada değiştirip yeniden üret (`~/projects/Blender MCP/CLAUDE.md`).

## 5. Sesler

- Pota'nın kendi örnekleri: `bomba`, `cizgi`, `altin-dogum`, `altin-inis`, `smac-vurus`, `file-swish`, `kalabalik`
  (`public/assets/ses/`).
- Unity'de sentezlenenler (Pota'nın `ses.ts` sentezine taşınacak; formüller Unity kodunda):
  `RoyalSpecials.MakeRocket()` ("roket", çizgi vınn), `MakeFuse()` ("fitil", bomba hazırlığı), `MakeCoin()` ("para", altın inişi),
  `CourtSpecials.MakePass()` ("courtPas", pas vıııt), `ShortCrowd` ("kalabalik" 1,2 sn'de kesilip 0,35 sn sönen kısa seyirci).
- Sesli videoda (`…-voiced.mp4`) her sesin hangi anda çaldığı duyulur.

## 6. Unity'den çıkan dersler (Pota'da da işe yarar)

- Efektler Pota'nın blok patlama flaşlarının **üstünde** çizilmeli; altta kalınca ateş topu ve alev soluyordu (Unity'de çizim
  sırası 5–15).
- Tahta düzlemine konan parçalar (kabuk parçaları, çatlak) komşu blokların içinde kayboluyordu: kameraya doğru biraz öne al.
- HUD'a uçan eşya çıkartmaları SLAM!/COMBO!/ateş topunun üstünden geçiyor ve altın doğuşunu örtüyor. **Tuna'nın gözüne bırakıldı.**
- Uzun düşüşlerde yeni bloklar çerçevenin üstünde görünüyor; Pota'da da aynı (kırpma düzlemi yok). İyileştirme fikri.
- Telefon maliyeti kabaca ölçüldü (Mac kilitliyken): ikisi de Pota'ya göre GPU'da milisaniyenin altında ek (R +0,24 ms, C +0,36 ms).
- Pota'nın kuralı: görsel işte Tuna'ya önce iki alternatif ve ekran görüntüsü, derleme yalnız istenince.

## 7. Karışım (Tuna yazacak)

| An | Seçim (P / R / C / karışık) | Not |
|---|---|---|
| Doğuş | | |
| Bekleme | | |
| Çizgi | | |
| Bomba | | |
| Zincir | | |
| Altın | | |
| Sesler | | |
