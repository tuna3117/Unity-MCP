# Özel top bekleme alternatifleri — rapor (2026-10-07)

Spec: `docs/superpowers/specs/2026-10-07-special-idle-alternatives.md`. Dal: `special-idle` (main'e birleştirilmedi). Pota'ya dokunulmadı.

## Ne yapıldı

Tahtada duran özel topun (çizgi, bomba, altın) beklemesi artık tek anahtarla seçiliyor: **Görünüm** (bugünkü, seçili özel görünümün
kendi beklemesi; kod yolu birebir aynı) ve beş sakin aday:

| ad | ne olur | nasıl |
|---|---|---|
| **A Sakin nefes** | yalnız ölçek nefesi | `BlockView.IdleScale` = 1 ± %2, 0,8 Hz, hücre başına faz |
| **B Parıltı süpürmesi** | 2,5 ± 0,4 sn'de bir, topun üstünden '/' eğik ince parlak şerit 0,35 sn'de geçer; yalnız çıkartmanın çizildiği yerde görünür | tek quad, `PotaGlint` shader çıkartmanın atlas hücresinin alfasıyla maskeler; Blender `glint_streak` |
| **C Kenar nabzı** | topun kenarında (0,36 hücre) ince yumuşak halka, 1 Hz'de belirip kaybolur, tepe opaklık 0,5; bomba turuncu, çizgi açık mavi, altın sarı | tek quad (0,98 hücre, hücre dışına çıkamaz), Blender `rim_ring` shader'da yarıçap/kalınlığa yeniden eşlenir |
| **D Tek yıldızcık** | 1,8 ± 0,5 sn'de bir topun sağ üst kenarında (20°–70°) TEK dört ışınlı yıldız belirir, büyür (0,35 → 1), söner (0,35 sn); aralarda hiçbir şey | tek quad, Blender `twinkle_star` |
| **E Hafif sallantı** | çıkartma ±5°, 0,5 Hz sallanır + %1 nefes; zıplama yok | `StickerSpin` + `IdleScale` |

Altın top: A, B, C (sarı), D; E'de kıpırdamaz (oyunun kendi altın nabzı her seçenekte sürer). Bütün sayılar Inspector'da:
`ColorHuntSettings` → **Special balls** → `Special Idle` ve `Idle` (A–E başlıkları).

Parçalar:
- `Scripts/ColorHunt/Specials/SpecialIdleFx.cs` — `IdleTuning`, saf eğriler `IdleCurves`, `SpecialIdleFx` (top başına havuzlu quad, boşken kapalı;
  B/C/D LateUpdate'te, görünümün son konumuna çizilir).
- `Shaders/PotaGlint.shader` (additive One One, üç kip: parıltı / halka / yıldız), şablon `Materials/Templates/PotaGlint.mat`
  (`ColorHuntSettings.GlintTemplate`, cihaz derlemesinde shader kalsın diye).
- `ColorHuntGame.SetSpecialIdle` / `SpecialIdle`: Görünüm seçiliyken yalnız eski `specials.Idle` çağrılır, `SpecialIdleFx` hiç kurulmaz.
  Değişince eski beklemenin kalıntıları temizlenir (Court'un zıplaması `Bob`, dönüş `StickerSpin`, Royal'ın nefesi `Scale`; `Pulse`
  zaten her kare altın nabzına döner).
- Specials vitrini: üçüncü düğme satırı **Görünüm · A · B · C · D · E** (görünüm satırının üstünde, aynı stil), etikette "bekleme …";
  yeni senaryo düğmesi **Bekleme**: bomba (3,2), çizgi dikey (3,4), altın (2,3, geçiş 3'ün üstünde), çizgi yatay (4,3) — dördü de
  ortadaki 24. hücreye değiyor, dokunuş yok.
- `ai_colorhunt_record`: `special_idle` (look | breath/a | glint/b | rim/c | twinkle/d | sway/e; kayıttan sonra eskisine döner),
  `scenario: "idle"` (Bekleme tahtası, 6 sn, odak 24), `closeup` olmadan verilen `zoom` artık normal hızda yaklaştırıyor.
- `Tools/frame_sheet.py --transpose` (seçenekler sütunda, anlar satırda).
- Blender atölyesi (`~/projects/Blender MCP`): `tools/bl/vfx.py`'ye üç tek kare tarif (`glint_streak`, `rim_ring`, `twinkle_star`; beyaz
  ışık, `_soft_light` ile yumuşatılmış), teslimler `assets/Pota/vfx-{glint-streak,rim-ring,twinkle-star}/export/` →
  `ai_import_asset` → `Games/Pota/Art/VFX/<id>/`, `StudioImportTool` kimlik listesine ve `WireIdleStills`'e eklendi. Blender
  çalıştı; koddan üretilen yedek doku gerekmedi.

## Tuna nasıl dener

1. Unity'de `Assets/_Project/Games/Pota/Scenes/Specials.unity` sahnesini aç, **Play**'e bas.
2. Alttaki senaryo satırından **Bekleme**'ye bas (dört özel top ortada durur). İstersen görünüm satırından **1** (Royal) seç: Görünüm
   bekleme o zaman Build 68'deki Royal beklemesi olur.
3. Üstteki bekleme satırında **Görünüm · A · B · C · D · E** arasında gez; etikette seçili bekleme yazar. Her seçenekte birkaç saniye
   bekle (B 2,5 sn'de bir, D 1,8 sn'de bir gelir). Diğer senaryolar (Doğuş, Zincir…) da seçili bekleme ile oynar.

Varsayılan (`ColorHuntSettings.SpecialIdle`) **Görünüm**: Tuna seçene kadar hiçbir şey değişmedi.

## Görseller

- Levha: `docs/screenshots/2026-10-07-idle-alternatives-sheet.jpg` — 6 sütun (Görünüm = bugünkü Royal, A–E) × 5 an, Bekleme tahtasına
  yakın plan (×2,4, dikey kayıt ortadan kare kırpıldı; bir hücre ~165 px, telefondaki boyuta yakın). Anlar 0,5 · 1,15 · 2,05 · 3,1 ·
  5,5 sn: B ve D kısa olaylar olduğu için anlar onların denk geldiği yerlerden seçildi (1,15 sn: B yatay çizgide, D bombada; 2,05 sn:
  B altında; 3,1 sn: D altın/bomba/yatay çizgide; 5,5 sn: D çizgilerde). Aralarda B ve D'de hiçbir şey yok (videoda görülür).
  5,5 sn satırında her sütunda bomba gri/parlak: bu oyunun kendi boşta ipucu (5 sn dokunulmayınca en iyi hamleyi parlatır), bekleme
  değil. A ve E hareket olduğu için durağan karede neredeyse görünmez, videoya bakılmalı.
- Video: `docs/screenshots/2026-10-07-idle-alternatives.mp4` — altı seçenek art arda, her biri başlık kartı + ~6 sn, sessiz (~43 sn).
- Levhayı tam boy kontrol ettim: B'nin şeridi yalnız topun üstünde, '/' eğik geçiyor; C'nin halkaları bomba (turuncu) ve çizgilerde
  (açık mavi-beyaz) net, hücre içinde; D'nin yıldızı topun sağ üst kenarında küçük ve tek; E'de çıkartmalar hafif dönük.

Kayıt komutu (Specials Play'deyken):
`execute_custom_tool {"tool_name":"ai_colorhunt_record","parameters":{"scene":"Specials","scenario":"idle","special_look":"royal","special_idle":"glint","zoom":2.4,"name":"idle-b"}}`

## Testler

- EditMode tamamı **530/530** (yeni `ColorHuntIdleTests` 14 test: varsayılan sayılar, faz yayılımı, nefes ±%2/0,8 Hz, sallantı ±5°/0,5 Hz,
  parıltı bandının konumu zamanla (toptan dışarıda başlayıp bitiyor), halka 0 → 0,5, aralıklar her zaman `every ± jitter` içinde,
  yıldız açısı 20°–70° ve hücre içinde, **Görünüm seçiliyken `SpecialIdleFx` hiçbir alanı yazmıyor**, A/E yalnız kendi alanlarını yazıyor,
  geçişler kalıntı bırakmıyor, kayıt aracı parametreleri, Bekleme senaryosu; `RuntimeShaderBuildTests` yeni shader'ı derlemede buluyor).
- PlayMode: yeni `SpecialIdlePlayTests` 7/7 (Specials sahnesi her bekleme ile 3 sn hatasız; B/C/D'nin her quad'ı topunun hücresi
  içinde; A ±%2, E ±5° ve altın kıpırdamıyor; Court'tan C'ye geçişte zıplama sıfırlanıyor; C açıkken Doğuş oynayınca yeni doğan dört
  top halkalanıyor). Tam PlayMode **194/194**.
- Blender: `tools/run.sh test test_vfx.py` 32/32 (yeni `IdleStillTests` 3 test).
- `Tools/test_frame_sheet.py` 7/7.

## Sapmalar ve dikkat edilecekler

1. **A/E `BlockView.Scale` yerine yeni `BlockView.IdleScale`'i yazıyor** (spec "yalnız Scale/StickerSpin" diyordu). Sebep: Royal'ın doğuş
   pop'u ve PotaFlow girişi `Scale`'i kendisi sürüyor; aynı alanı yazmak birinin diğerini ezmesi demekti. Görünüm seçiliyken
   `IdleScale` 1 kalır ve çarpım birebir aynıdır.
2. **E'de altın top kıpırdamaz** (spec "altın için A/B/D (C'de sarı)" diyor; C'yi de altına sarı olarak uyguladım).
3. **C'de altın üstündeki sarı halka çok silik** (sarı topa sarı ışık). Sakin olsun dendiği için bıraktım; istenirse altın için beyaz.
4. **D bombada** yıldız çıkartmanın kendi fitil alevinin hemen yanına düşüyor (ikisi de sağ üstte), birbirine karışıyor. İstenirse bomba
   için açı aralığı sol üste alınır.
5. **B parlak:** 0,8 additive, sarı/turuncu topta kısa bir an beyaza doyuyor; "vitrin camı" gibi okunuyor ama sakinlik için 0,5–0,6
   denenebilir (`GlintBrightness`).
6. Parıltı açısı yorumu: şerit dikeyden 35° yatık ('/'), şeride dik yönde sol üstten sağ alta, yumuşak hızlanıp yavaşlayarak geçiyor
   (ekrana göre sabit; tahta dönerken de aynı açı).
7. C'nin yarıçap ve kalınlığı shader'da Blender halkasından bağımsız ayarlanıyor (doku 0,40 / çekirdek 0,043; Inspector 0,36 / 0,05
   hücre). B'nin şerit kalınlığı da öyle (dokunun çekirdeği kare boyunun %10'u).
8. Halka ve yıldız, topun iniş yaylanmasında ve Royal'ın doğuş pop'unda (1,2) büyümüyor (en çok 1×): hücre dışına taşmasın diye.
   Parıltı çıkartmayla maskelendiği için zaten topun dışına çıkmıyor.
9. Kayıtlarda her sütun Royal özel görünümüyle çekildi (Görünüm sütunu Tuna'nın şikâyet ettiği Royal beklemesi); A–E görünümden
   bağımsız.
10. Telefon maliyeti ölçülmedi: en çok dört additive quad (top başına bir), MaterialPropertyBlock ile (SRP batcher dışı ama 4 çizim);
    sürekli parçacık yok.
11. Yeni senaryo `SpecialScenario.Idle` ("Bekleme") enum'un sonuna eklendi; `CourtPlayTests` dokunuşsuz senaryoda "Court çizdi" şartını
    Serbest gibi atlıyor.

## Sıradaki

Tuna levha + videoyla seçer ("hangisi?"); seçilen ayrı işte Pota'ya taşınır (`OZEL_AN.bekleme`, `gorselleriIlerlet`, gerekirse
`yapbozSahne` parıltı katmanı). Seçilen burada varsayılan da yapılabilir (`ColorHuntSettings.SpecialIdle`).

## Karar (2026-10-07)

Tuna levha ve videoya bakıp **A "Sakin nefes"** dedi ("a'yı yapalım"). Pota'ya taşındı (`~/projects/Pota` dal `guc-paketi`: `OZEL_AN.bekleme = { nefes: 0.02, nefesHz: 0.8 }`,
halka/kıvılcım/parlama kaldırıldı). B–E Unity'de referans olarak duruyor; dal `special-idle` main'e birleştirilmedi.
