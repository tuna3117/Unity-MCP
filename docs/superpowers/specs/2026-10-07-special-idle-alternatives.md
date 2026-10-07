# Özel top bekleme alternatifleri (Pota geri bildirimi 2026-10-07)

Tuna, Pota Build 68'deki Royal bekleme hâline (bomba nefes + fitil kıvılcımı, çizgi oku parlaması, altında halka parıltısı) baktı:
**"çok dikkat dağıtıyor"**, çizgi topunun halkası **"çok kötü"**. İstek: alternatifleri **Unity'de** tasarla (Blender da kullan), o
Unity'de test etsin, beğendiğini Pota'ya taşıyalım. Pota'ya bu turda dokunulmaz.

## Amaç

Tahtada duran özel topun (çizgi, bomba; altın yalnız engel altında) **özel olduğunu** söyleyen ama **gözü çekmeyen** bekleme
hâlleri. Ölçüt: dikkat 1 sn içinde tahtaya döner; komşu hücrelere taşan hiçbir şey yok; sürekli parçacık yok (ya da çok seyrek).

## Beş aday (hepsi tek anahtarla seçilir, Specials sahnesinde düğme satırı)

| ad | ne olur | sayılar (başlangıç; Inspector'da ayarlanır) |
|---|---|---|
| **Görünüm** | bugünkü: seçili özel görünümün (Pota/Royal/Court) kendi beklemesi | — |
| **A Sakin nefes** | yalnız ölçek nefesi, başka hiçbir şey | ±%2, 0,8 Hz, hücre başına faz kayması |
| **B Parıltı süpürmesi** | topun üstünden ince parlak bir çizgi 2,5 sn'de bir çaprazdan geçer (vitrin camı parıltısı); yalnız çıkartmanın alfa alanında görünür | süre 0,35 sn, kalınlık 0,12 hücre, açı 35°, parlaklık 0,8 (additive), aralık 2,5 sn ± 0,4 |
| **C Kenar nabzı** | topun kenarında, topun yarıçapında ince yumuşak halka; görünüp kaybolur; hücre dışına taşmaz | yarıçap 0,36 hücre, kalınlık 0,05, tepe opaklık 0,5, 1 Hz, renk türe göre (bomba turuncu, çizgi açık mavi, altın sarı) |
| **D Tek yıldızcık** | 1,8 sn'de bir topun sağ üst kenarında TEK dört ışınlı yıldız belirir, büyür, söner; aralarda hiçbir şey | boy 0,22 hücre, ömür 0,35 sn, aralık 1,8 sn ± 0,5, konum kenarda rastgele açı 20°–70° |
| **E Hafif sallantı** | çıkartma yavaşça ±5° sallanır (top yerinde hafifçe yuvarlanır) + %1 nefes; zıplama yok | 0,5 Hz, ±5°, nefes ±%1 |

Bomba ve çizgi için hepsi geçerli; altın için A/B/D (C'de sarı). "Görünüm" seçiliyken bugünkü davranış birebir.

## Uygulama (Unity MCP, `Games/Pota`)

- `ColorHuntSettings`: `public enum SpecialIdle { Look, Breath, Glint, Rim, Twinkle, Sway }` + alan `SpecialIdle SpecialIdle = SpecialIdle.Look` + `IdleTuning Idle` (yukarıdaki sayılar, Header'lı).
- `ColorHuntGame.TickViews`: `S.SpecialIdle == Look` ise bugünkü `specials.Idle(v, dt)`; değilse yeni `SpecialIdleFx` (MonoBehaviour, `AddSpecialStyle` kalıbıyla oyunun altında) `Idle(v, dt, kind)`. Değiştirince (`SetSpecialIdle`) eski görünümün bekleme kalıntıları temizlenir (`v.Bob = 0`, `v.StickerSpin = 0`, `v.Pulse` yalnız altın nabzı, `v.Scale = 1`).
- `SpecialIdleFx`: hücre başına faz (`v.Cell`); A/E yalnız `BlockView.Scale`/`StickerSpin`; B/C/D topun çıkartma düzleminin (`LabelZ`) biraz önünde (`+0.02`) **tek quad** (hücre başına havuzlanmış; boşken `enabled=false`), additive `PotaAdditive` ya da yeni küçük bir shader:
  - **B** için shader `PotaGlint.shader`: çıkartmanın atlas UV'sini (`_UvRect`) okuyup alfasıyla çarpar, `_Sweep` (−1..2) ile hareket eden `smoothstep` bandı; böylece parıltı yalnız topun üstünde. `LabelAtlas.UvRect(LabelAtlas.SpecialCell(kind))`.
  - **C** için halka dokusu (Blender) ya da `PotaShockRing`/soft ring dokusu; **D** için dört ışınlı yıldız dokusu (Blender).
- **Blender** (`~/projects/Blender MCP`, `tools/run.sh vfx …`): `tools/bl/vfx.py`'ye üç STİLL (tek kare) üretici: `glint_streak` (yumuşak uçlu ince beyaz şerit, 256²), `rim_ring` (ince yumuşak halka, 256²), `twinkle_star` (dört ışınlı, uçları incelen yıldız, 256²). Teslim `export/<id>/` + `manifest.json` (`kind: vfx-flipbook`, 1 kare) → Unity `ai_import_asset` → `Games/Pota/Art/VFX/<id>/`. Blender çalışmazsa Unity'de kodla üretilmiş yedek doku (`PotaFx.PointSprite` kalıbı) — raporda söyle.
- `SpecialsDemo`: üçüncü düğme satırı "Bekleme: Görünüm · A · B · C · D · E" (look satırının altına, aynı stil), etiket metnine bekleme adı eklenir. `Serbest` senaryosunda tahtada en az bir bomba + bir çizgi + (engel altında değilse de) bir altın dursun ki bekleme izlenebilsin — gerekirse `SpecialScenarios`'a `Bekleme` senaryosu (3 özel, dokunuş yok).
- `ai_colorhunt_record`: `special_idle` parametresi (look|breath|glint|rim|twinkle|sway) → `Specials` sahnesinde `scenario: "idle"` benzeri 6 sn kayıt; `Tools/frame_sheet.py` ile 6 sütun × anlar levhası `docs/screenshots/2026-10-07-idle-alternatives-sheet.jpg` + `docs/screenshots/2026-10-07-idle-alternatives.mp4` (altı seçenek art arda ya da yan yana).
- Testler: EditMode `ColorHuntIdleTests` — saf eğriler (nefes genliği/frekansı, parıltı bandının konumu zamanla, yıldızcık aralığı rastgele ama sınır içinde, E açısı ±5°), `SpecialIdle.Look` seçiliyken `SpecialIdleFx` hiçbir alanı yazmıyor; PlayMode: Specials sahnesi her beklemeyle 3 sn hatasız.
- CLAUDE.md döngüsü: küçük adım → derle → konsol → ekran görüntüsü → commit. Pota'ya hiçbir şey yazılmaz.

## Teslim

Tuna Unity'de **Specials** sahnesini Play'e alıp düğmelerle gezer. Ben levha + video ile birlikte "hangisi?" diye sorarım; seçileni Pota'ya
taşıma ayrı iş (Pota tarafında `OZEL_AN.bekleme` + `gorselleriIlerlet` + gerekirse `yapbozSahne` parıltı katmanı yeniden).
