# Renk Avı: özel toplar ve Royal Match tarzı efekt adayları — tasarım (2026-10-01)

Tuna'nın isteği: "Pota oyununda özel kareler oluyor, dikey patlatan, bomba, altın top vb. Buna Royal Match tarzı güzel efektler yapmak
istiyorum, Blender ve Unity'yi kullanarak. Bana 2 farklı efekt örneği oluştur, beğendiğimi seçeyim."

Bu oturumda verdiği kararlar:
- **Yön:** 1 "Royal Match birebir" (roket, ateş topu, para) ve 2 "Basketbol" (alev almış pas, smaç, swish). Önerilen seçenekti.
- **Bölüm 1 (ne göreceksin)** ve teknik yol (özel top kuralları Pota'dan birebir alınır, oyunun bugünkü bölümünde kapalı kalır, efektler
  ayrı bir vitrin sahnesinde denenir) onaylandı: "evet devam edebilirsin".

## 1. Amaç ve ölçüt

Pota'nın özel toplarının (çizgi, bomba, altın) doğuşunu, tahtada duruşunu, tetiklenmesini, birbirini tetiklemesini ve altın topun sepete
inişini Royal Match / Toon Blast seviyesine çıkarmak. Bugün Pota'da bomba yalnız bir flaş, sarsıntı ve ses; çizgi satır/sütun boyunca
üç flaş. Unity'de özel top hiç yok (backlog U25).

İş bittiğinde:
1. Unity Renk Avı'nda Pota'nın özel top kuralları vardır, Pota'nın kendi kodundan üretilen fikstürlerle birebir doğrulanmıştır. Bugünkü
   bölümde (bölüm 20) kapalıdır: oyun bugünkü gibi oynar, mevcut testler yeşil kalır.
2. `ColorHuntSettings.SpecialLook` = **Pota / Royal (1) / Court (2)** seçilebilir. Pota seçeneği Pota'nın bugünkü özel top
   geri bildiriminin birebir karşılığıdır.
3. Vitrin sahnesi `Scenes/Specials.unity`: Pota görünümlü tahta, hazır senaryolar (doğuş, çizgi, bomba, zincir, altın, serbest) ve
   ekranda üç görünüm arasında geçiş düğmesi. Tuna Play'e basıp kendisi dener.
4. Teslim videoları (`docs/screenshots/2026-10-01-specials-*`):
   - Pota | 1 | 2 üç sütunlu, sessiz yan yana video ve anların kare levhası.
   - **Sesli** sıralı video: her senaryo önce Pota, sonra 1, sonra 2. Sesler oyunun çaldığı anlara olay günlüğünden yerleştirilir.
   - Her adayın bomba ve çizgi anının yavaş çekim yakın planı.
5. Normal blok patlamaları üç görünümde de bugünkü Pota görünümündedir; yalnız özel toplar değişir.
6. Canlı parçacık ve parça sayısı en çok Pota görünümünün 1,5 katı (`BurstBudget`). Ücretli çağrı yok, Pota salt okunur, push yok.

## 2. Ne göreceksin

Ortak kurallar:
- Özel topların çıkartmaları Pota'nın çizimleri (`ozel-cizgi-a.png`, `ozel-bomba-a.png`, `ozel-altin-a.png`): çizgi turuncu, bomba füme,
  altın altın tonunda yastık bloğun üstünde, rakamsız. Yatay çizgi topunun oku 90° döner.
- Doğuş yazıları Pota'daki gibi ("ÇİZGİ!", "BOMBA!", "ALTIN TOP!").
- Kurallar üç görünümde aynıdır: hangi bloğun patlayacağı, doğum hücresi, zincir, düşüş ve dolum değişmez. Görünümler yalnız **zamanlamayı
  ve çizimi** belirler (aşağıdaki "sırayla patlama" dahil).
- Her tetik adımı ≤ 0,6 sn sürer; oyunun ritmi ağırlaşmaz.

### Pota (bugün, referans)

Birebir Pota `renkAviFaz.ts`:
- **Doğuş:** düşüş başında hücre özel topa döner, flaş 1,0 + çarpma 0,6; ışık flaşı (altın için sarı, diğerleri beyaz; 1,6 / 3,4 / 5 /
  0,2 sn), ses (`ozelDogdu` altında altın, diğerlerinde `kombo`), doğuş yazısı.
- **Bekleme:** altın top nabız atar (flaş genliği 0,08, 5 rad/s).
- **Tetik:** adımdaki bütün bloklar birlikte şişer, ilk zirvede: bomba için 2,6 / 6,5 / 8 / 0,25 sn turuncu flaş + 0,2 sarsıntı +
  `bomba` sesi; çizgi için satır/sütun boyunca üç flaş (uçlar ve orta) + 0,12 sarsıntı + `cizgi` sesi; 0,06 sn anlık yavaşlama.
- **Altın iniş:** hücreden geçiş halkasına 0,32 sn (ivmeli), halkada sarı flaş, potaya 0,5 sn kavis (1,4), 0,12 sn arayla; `altinInis` sesi.

### 1 · Royal Match (`Royal`)

Parlak, şişkin, mürekkep hatlı çizgi film dili; her şey "pıt" diye taşarak girer.

| An | Ne olur |
|---|---|
| **Doğuş** | Gruptaki bloklar 0,18 sn'de doğum hücresine çekilir (küçülerek, hafif dönerek); çıkartmaları oradan HUD'a uçar. Özel top 0 → 1,2 → 1 taşarak (0,25 sn) belirir; beyaz çekirdek flaşı, 8 ışınlı yıldız, 6–8 yıldızcık. |
| **Bekleme** | Bombanın fitil ucundan seyrek kıvılcım (saniyede ~3), bomba hafifçe nefes alır (±%3). Çizgi topun oku 1,5 Hz parlar. Altın top Pota nabzına ek olarak parıltı yıldızcıkları (saniyede ~1). |
| **Çizgi** | Top 0,06 sn ezilip parlar, **ikiye ayrılır**: iki yarım top (Blender) alev püskürterek (Blender alev jeti, döngülü) satırın/sütunun iki ucuna roket gibi uçar (0,32 sn'de en uzak kenara, ivmeli). Arkada gri çizgi film duman pufları (yarım hücrede bir) ve kıvılcımlar. Bloklar roket **üstünden geçerken sırayla** patlar. Kenarda roket küçük bir flaşla çerçeveden çıkar. Eksen boyunca sarsıntı (0,12). |
| **Bomba** | Hazırlık 0,18 sn: 1,0 → 1,25 şişer, iki kez kırmızı-beyaz yanıp söner, titrer, fitil kıvılcım saçar. Patlama: beyaz çekirdek flaşı (0,08 sn), mürekkep hatlı ateş topu (Blender, ~3,2 hücre, 0,45 sn), stilize şok halkası, 8 kabuk parçası (Blender'da kırılmış bomba, siyah + turuncu dikiş) dönerek saçılır, 4–6 duman pufu, 12 kıvılcım, güçlü nokta ışık. 3×3 içindeki bloklar 0,1 hücre dışa itilir ve merkezden dışa **dalga** gibi patlar (halka başına 0,04 sn). Sarsıntı 0,2, 0,06 sn anlık yavaşlama. |
| **Zincir** | Tetiklenen her özel, ilkinin etkisi kendi hücresine **vardığı an** kendi efektini başlatır (roket bombaya çarpınca bomba patlar). Bir adımda 2+ özel: 0,2 sn ×0,5 yavaş çekim ve 0,1 sn beyaz ekran flaşı. |
| **Altın iniş** | Sütundan geçiş halkasına parıltı izi bırakarak iner, halkada altın flaş, potaya kavis; çemberde 8–12 dönen altın para (Blender), yıldızlar ve altın parlama saçılır. |

### 2 · Basketbol (`Court`)

Pota'nın kendi dünyası: hız çizgileri, saha, smaç, file. Yazılar kalın, mürekkep hatlı 3B harfler (Blender).

| An | Ne olur |
|---|---|
| **Doğuş** | Gruptaki bloklar top gibi kavis çizip sekerek (0,22 sn) doğum hücresine zıplar; çıkartmaları oradan HUD'a uçar. Özel top hücreye "yere iner" gibi düşer: ezilip toparlanır (çarpma 0,6), altından saha çizgisi gibi beyaz bir halka açılır, hafif toz. |
| **Bekleme** | Özel toplar 2 Hz hafifçe zıplar (dripling; altta hafif ezilme). Altın top yavaşça döner ve parlar. |
| **Çizgi** | Top bir kez sert sektirilir (0,08 sn ezilme), **"alev almış"** iki basketbol topu (Blender topu, dönerek) hız çizgileri (Blender) ve alev kuyruğuyla iki uca fırlar (0,3 sn). Geçtiği bloklar zıplayıp **sırayla** patlar. Top çerçeveye çarpınca beyaz "swish" halkası. |
| **Bomba** | **Smaç:** top kameraya doğru zıplar (0,14 sn, 1 → 1,6 büyür), havada asılı kalır (0,08 sn, yavaş döner), aşağı çakılır (0,07 sn). Çarpmada zemin çatlar (Blender çatlak dokusu, 0,6 sn'de söner), mürekkep hatlı toz halkası (Blender), 3×3 bloklar havaya fırlayıp (0,25 hücre) patlar, "SLAM!" yazısı (0,5 sn), çerçeve kenarlarında 6–10 fotoğraf flaşı, sarsıntı 0,24 + kamera vuruşu. |
| **Zincir** | 1'deki gibi varış anında tetik. Bir adımda 2+ özel: aynı yavaş çekim + "COMBO!" yazısı + kısa seyirci sesi. |
| **Altın iniş** | Sütundan dönerek yuvarlanır, geçitten potaya kavisle atılır, **file dalgalanır** (dev potanın altı halkası sırayla esner), "SWISH!" yazısı, altın konfeti. |

### Sesler

- **Pota:** Pota'nın örnekleri: `bomba`, `cizgi`, `altin-dogum`, `altin-inis`, `kombo` (Pota'nın kopyasından `run_export.sh` ile).
- **Royal:** Pota'nın örnekleri + sentezlenmiş roket "vınn"ı (çizgi), para şıngırtısı (altın), hazırlık fitil cızırtısı (bomba).
- **Court:** `smac-vurus` (bomba çarpması), `file-swish` (çizgi uçları ve altın), `kalabalik` (2+ özel), sentezlenmiş pas "vıııt"ı.
- Sentez sesleri `PotaSounds.Synth` yolundan; örnek sesler Pota'nın seviyesinde.

## 3. Kurallar (Pota `src/renkAvi.ts`'ten birebir)

Unity'nin `ColorHuntRules`'ı Pota'nın eski sürümünün portu; özel toplar sonradan geldi. Bu işte **yalnız özel toplar ve geçişler** alınır.
Engeller (file, buz, hakem), güçlendiriciler, bonus ve özel hedef türleri (bomba/çizgi/altın hedefi, HUD altın sayacı) U25'te kalır.

- **Veri:** `Block.Special` (`None, LineV, LineH, Bomb, Golden`; Pota `cizgiDikey` = sütunu patlatan = `LineV`). Özel blokta renk −1, can 0.
  `Board.Features` (`Line, Bomb, Golden`), `Board.Gates` (geçiş açık sütunlar), `Board.Golden` (sepete inen sayısı).
  `Step`'e `Born` (hücre, tür, **grup hücreleri**), `Triggered` (hücre, tür), `ToBasket` (hücre, sütun), `Opened` (açılan sütunlar).
  Grup hücreleri Pota'da yok; yalnız görünüm için eklenir (doğuşta "çekilen" bloklar), kuralı değiştirmez.
- **Eşleşme:** renk −1 koşuya girmez (`eslesmeleriBul`).
- **Doğum** (`eslesmeleriCoz`): bağlı gruplar; grupta ≥5 → altın (doğum yerinde), yoksa ≥2 koşu (kesişim) → bomba (en küçük indeksli
  kesişim), yoksa her tam 4'lük koşu → çizgi (yatay koşudan `LineV`, dikeyden `LineH`). Doğum hücresi patlamaz. Kapalı özellik o adımı atlar.
- **Tetik** (`patlamaAlani`, `kir`): çizgi/bomba dokununca **bedava** tetiklenir; alan zincirle genişler (alandaki özel de tetiklenir),
  altın alandan çıkar. Altına dokunmak bir şey yapmaz ("altın sepete iner" titremesi).
- **Geçiş ve sepet** (`patlatVeDoldur`, `sepeteDusur`): doğan altın serbest altın sayısı açık geçişi aşarsa rastgele geçişsiz bir sütun
  açar (rng çeker); alt satırda, açık geçişin üstündeki altın sepete iner, sayılır; o sütunda başka serbest altın yoksa geçiş kapanır.
  `zinciriCoz` önce sepete indirir, sonra eşleşmeleri çözer.
- **Döndürme** (`dondurVeOtur`): döndürme eşleşme yaratmaz ama altını geçişin üstüne getirebilir; dönüşten sonra tahta oturtulur.
  Geçişler çerçevededir, tahtayla dönmez.
- **Hamle:** `Cheapest` özel blokları saymaz; tetiklenebilir özel varsa hamle vardır.
- **Doğrulama:** Pota'nın kopyasında (scratch, `tsx`) Pota'nın kendi `renkAvi.ts`'iyle fikstür üretilir: özellikler açık birkaç tohum ×
  rastgele dokunuş/döndürme dizileri → her hamlenin tahtası ve adımları (JSON). Unity aynı diziyi oynar, tahta ve adımlar birebir eşit
  olmalı. Aynı üretici özellikler kapalı fikstürler de yazar (bölüm 20 tasarımı); Unity'nin bugünkü davranışı onlarla da eşit kalmalı
  (rng tüketimi değişmez). Bugün Unity'de fikstür testi yok; mevcut `ColorHuntRulesTests` elle yazılmış durumları denetliyor.

## 4. Yapı

### Unity (`Games/Pota/`)

- **Kurallar:** `ColorHuntRules` genişler (§3). `LevelDesign.Features` ve hazır tahta (`Preset`: renk ızgarası + özel toplar + geçişler)
  vitrin senaryoları için.
- **Oyun:** `ColorHuntGame`:
  - Özel dokunuş: salvo yok, ilk adım derinlik 1'den oynar (Pota `adimlariSiraya(.., false)`).
  - Doğuş, sepete iniş (uçan altın varken tahta kilitli, Pota `kilitliMi`), geçiş halkaları (Pota `gecisGoster` ölçüleriyle).
  - Tetik adımında hücre başına gecikme: seçili görünümün **zaman çizelgesi** (`SpecialSchedule`, saf, EditMode testli) her hücrenin ve
    her tetiklenen özelin patlama anını verir (roket hızı, bomba dalgası, zincirde varış anı). Pota görünümünde hepsi 0'dır (bugünkü gibi
    birlikte şişer). Adımın düşüşü son patlamadan sonra başlar.
- **Görünüm arayüzü:** `ISpecialStyle` (`IBurstStyle`'ın kardeşi): `Birth`, `IdleTick`, `Trigger` (zaman çizelgesiyle), `Combo`,
  `GoldenFlight`, `Clear`, `Ready`. Uygulamalar `Specials/PotaSpecials`, `Specials/RoyalSpecials`, `Specials/CourtSpecials`. Varlığı eksik
  görünüm uyarı loglar ve Pota'ya düşer (bugünkü `Resolve` kalıbı). Hepsi havuzlu, oyun zamanında çalışır.
- **Blok görünümü:** `BlockView` özel tonu (Pota `OZEL_RENK_HEX` + `koyuDoygun`) ve çıkartmayı çizer; etiket atlasının 105–107 hücreleri
  Pota'nın kendi atlas koduyla dışa aktarılır (`run_export.sh` + `ozelleriGiydir`). Rakam yok; altın nabzı.
- **Ayar:** `ColorHuntSettings.SpecialLook` (`Pota, Royal, Court`) ve görünüm başına sayılar (hızlar, süreler, sayılar) ayar asset'inde.
- **Vitrin:** `ai_specials_build_scene` → `Scenes/Specials.unity` (Renk Avı sahne yardımcılarıyla; Build Settings'e ve `PotaProfile`'a
  eklenir, `SceneBootTests` kapsar). `SpecialsDemo` bileşeni: senaryo düğmeleri, görünüm düğmeleri (Pota | 1 | 2), "Tekrar". Düğmeler
  çalışma anında bağlanır (genel ders: Edit Mode'da eklenen dinleyici kaydedilmez).
- **Kayıt:** `ai_colorhunt_record`'a `scenario` ve `special_look`; `PotaSounds` kayıt süresince çaldığı her sesi oyun zamanıyla
  `events.json`'a yazar. `Tools/potalook/side_video.py` sessiz üç sütun; yeni `seq_video.py` sıralı video + ses karışımı (ffmpeg
  `adelay` + `amix`, `/opt/homebrew/bin/ffmpeg`).
- **Derleme güvenliği:** yeni shader/materyaller şablondan (`RuntimeMaterial.From`, `PotaMaterialTemplates`); `RuntimeShaderBuildTests`.

### Blender atölyesi (`~/projects/Blender MCP`, efekt hattının ikinci adımı)

`tools/bl/vfx.py`'ye yeni tarifler ve mesh araçları; teslim `manifest.json` ile, Unity'ye `ai_import_asset` / `ai_studio_import`.

| Varlık | Tür | Aday | Ne |
|---|---|---|---|
| `fire_toon` | flipbook 4×4, 256 px | 1 | mürekkep hatlı çizgi film ateş topu (sarı çekirdek, turuncu, koyu kenar) |
| `flame_jet` | flipbook döngü 4×2 | 1, 2 | roket/alev kuyruğu |
| `puff_toon` (var) | flipbook | 1 | gri boyanarak duman pufu |
| `ring_toon` (var) | flipbook | 1 | şok halkası |
| `dust_ring` | flipbook 4×4 | 2 | mürekkep hatlı toz halkası (saha toprağı tonu) |
| `speed_lines` | flipbook 4×2 | 2 | hız çizgileri |
| `floor_crack` | tek doku | 2 | yıldız biçimli zemin çatlağı |
| `lettering` | tek doku ×3 | 2 | "SLAM!", "SWISH!", "COMBO!" kalın 3B harf, dış hatlı (oyunun yazı tipi Lilita One) |
| `ball_halves` | mesh (GLB) | 1 | çizgi topun iki yarısı (turuncu basketbol, dikişler), kesik yüzü kapalı |
| `basketball` | mesh (GLB) | 2 | düşük poligon basketbol, toon + dış hat |
| `bomb_shell` | mesh (GLB, `vfx_fracture` küre kabuk) | 1 | bomba topunun 8 kabuk parçası |
| `coin` | mesh (GLB) | 1 | top kabartmalı altın para |

Her varlık önizleme levhasıyla göz kontrolünden geçer; atölye testleri kare sayısı, alfa, poligon ve kapalılığı denetler.

## 5. Doğrulama

- **EditMode:** kurallar (Pota fikstürleriyle birebir; özellikler açık ve kapalı), `SpecialSchedule` (roket sırası, bomba
  dalgası, zincirde varış anı, kenarda kırpma), görünüm seçimi ve Pota'ya düşme, havuz tavanları, Blender teslimlerinin `matches`'i.
- **PlayMode:** her görünüm × her senaryo hatasız koşar; patlayan hücreler kuralın listesiyle aynı; hedef sayısı kuralla eşit biter;
  altın uçarken tahta kilitli; canlı parçacık tavanın altında; bugünkü Renk Avı ve PotaFlow testleri yeşil.
- **Göz kontrolü:** üç sütunlu levha ve video, yakın planlar, sesli sıralı video. Kusur görülürse "bitti" denmez.
- **Telefon maliyeti:** Mobile RP asset'iyle editörde kare süresi ölçülür (en ağır an: bomba + 2 çizgi zinciri), not edilir.

## 6. İş sırası

1. Kurallar: Pota fikstür üreticisi (scratch) + `ColorHuntRules` özel topları; özellikler kapalıyken eski davranış.
2. Oyun: özel dokunuş, doğuş, geçiş halkaları, altın uçuşu, `ISpecialStyle` + Pota görünümü (birebir), çıkartmalar ve sesler.
3. Vitrin sahnesi ve senaryolar (Pota görünümüyle oynanır hâlde).
4. Blender varlıkları ve içe aktarma.
5. `SpecialSchedule` + 1 Royal.
6. 2 Court.
7. Kayıt: senaryo/görünüm, ses günlüğü, levha, sessiz yan yana ve sesli sıralı video, yakın planlar.
8. Belgeler (README, CLAUDE.md §12, backlog, hafıza).

## 7. Riskler

- **Kural portu:** Pota'nın `patlatVeDoldur`'u engeller ve hakemle iç içe; yalnız özel top yolunu almak rng tüketimini kaydırabilir.
  Fikstürler engelsiz/hakemsiz bölümlerden üretilir; tutmazsa sebebi bulunmadan devam edilmez.
- **Sırayla patlama ritmi:** roket ve dalga zamanlaması adımı uzatır. Tavan 0,6 sn; zincirde toplam süre ölçülür, gerekirse hız artar.
- **Stil uyumu:** mürekkep hatlı ateş/para ile Pota'nın parlak blokları uyuşmayabilir (önceki A adayının dersi). Yakın planda bakılır;
  dış hat kalınlığı ve doygunluk ayarlanır.
- **Mesh çıkartma:** yarım topların ve basketbolun dokusu Pota'nın topuyla aynı tonda olmalı; önizlemede yan yana bakılır.
- **Kapsam:** iki depo, kural portu ve iki aday. Sıra korunur; her adımda testler yeşil, commit atılır.
