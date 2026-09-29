# Pota akışı: sepetten tahtaya geçiş — tasarım (2026-09-29)

Tuna'nın isteği: "atış yapıldıktan sonra kamera geri puzzle'a geçiyor, senden komple bir görsel animasyon tasarımı yapmanı
istiyorum, bakalım daha iyisini yapabilecek misin. Potadaki kodlara müdahale etme, sadece okuyabilirsin, gerekirse kopyalarsın."
Soruların hepsine "önerilerini yap" dedi: 1A 2A 3A 4A 5A 6A 7A ve varsayılanlar (§0).

Unity burada atölye: his burada kurulur, beğenilirse Pota'ya taşınması ayrı iştir (bu spec'in dışında).

## 0. Kararlar (Tuna 2026-09-29, hepsi onaylı)

| # | Karar |
|---|---|
| 1 | **Tek sahne** `Games/Pota/Scenes/PotaFlow.unity`: kule, çatı sahası, dev pota + tahta aynı dünyada; kamera kesintisiz uçar. |
| 2 | **Toplar gerçekten uçar**: sepetten fışkırır, kavis çizip dev potanın filesine (cephane haznesi) akar; sayaç her topla artar. Çok toplu bölümde en çok 25 top görünür, geri kalanı sayıyla gelir. |
| 3 | **~2,5 s**, dokununca hızlanıp biter. |
| 4 | **Tahta belirir**: bloklar merkezden dışa zıplayarak "pat pat", çerçeve parlar, HUD kayarak girer. |
| 5 | Tahta görünümü **Unity'deki mevcut Renk Avı**; balon görünümü bu turda yok. |
| 6 | **Kahraman** geçişte görünür, topların dev potaya uçuşuna döner; eldeki klipler (ücretsiz). |
| 7 | **İki koreografi adayı** (A "Akan toplar", B "Sade") + Pota'daki geçişle yan yana kare levhası ve video; Tuna seçer. |

Varsayılanlar (itirazsız): mevcut Basket ve ColorHunt sahneleri olduğu gibi çalışır; Pota yalnız okunur, kopyalanan her sayının
kaynağı not edilir; video için ffmpeg (Homebrew) kurulur; sesler Core `SynthSfx`; zaman çizelgesi saf kod + EditMode testi, akış
PlayMode duman testi; Pota'ya taşıma yok; ücretli çağrı yok; push yok.

## 1. Pota'da bugün (okundu, `~/projects/Pota`, main 62f5462)

- Akış (`src/akis.ts` `fazABitti` → `fazBBasla`): sepet sayısı cephane olur; `ucanYazi("N top")` ekranın ortasında 1,3 s (CSS);
  kamera yönetmeni (`src/kameraYonetmeni.ts`) sütun kamerasının pozundan tahta kamerasının pozuna **1,4 s** geçer: konum ve bakış
  `smoothstep` ile doğrusal karışır, ortada `sin(πu)·3,5 m` yükselir, FOV karışır. Tahta geçişten ÖNCE kurulur, varışta hazır bekler.
- Dünya (`src/sahaYerlesim.ts`): kahraman (0,0,3), sütun sağda (14,3; 0; 0), dev pota (0; 8,5; −10). Dev pota (`src/pota.ts`): buzlu cam
  pano 8,8 × 8,8, turuncu çerçeve 0,35, çember R 1,6 panonun altında y −5,6 ve öne R+0,6, 6 halkalı file (derinlik 1,8), çatıya direk.
- Kareler: `onizleme/saha-pota-0…5.png` (23 Eylül; tahta o gün blok görünümündeydi).
- Eksikler (bu tasarımın düzelttiği): toplar ile cephane arasında görsel bağ yok (yalnız yazı); kamera düz bir kayma, hedefi
  gösteren bir "an" yok; tahta hazır bekliyor, gelişi yok; kahraman o anda rol almıyor; atlama yok (1,4 s her seferinde).

## 2. Hedef ve "daha iyi"nin ölçüsü

1. **Süreklilik:** sepetteki toplar gözle görülür biçimde dev potaya akar; HUD cephanesi tam olarak sepet sayısına ulaşır.
2. **Okunurluk:** sayı her an okunur (sepette vurgu → akışla artan sayaç → HUD).
3. **Tempo:** dokunulabilir tahtaya ≤ 2,6 s; dokununca kalan süre ≤ 0,35 s; ölü kare yok (her an bir şey hareket eder).
4. **Akıcılık:** 60 fps hedefi; geçişte kare başına bellek tahsisi yok (havuz), ilk karede takılma yok (ön ısıtma).
5. **Yargı:** Pota'daki geçişle aynı kadraj boyunda yan yana video ve kare levhası; son söz Tuna'nın.

## 3. Dünya yerleşimi (tek sahne)

Taramadan çıkan kısıt: Basket dünyası orijine sabit (kule z = 0 düzleminde, kamera +z'den −z'ye bakar; top fiziği, düşüş düzlemi,
kafes ve nişan dünya koordinatı kullanır). Renk Avı tahtası ters yöne bakar (kamerası −z'den +z'ye) ve **yalnız ötelenirse**
(döndürülmeden) birkaç küçük değişiklikle çalışır (§6). Buna göre:

- **Kule + kafes + teras + kahraman:** Basket sahnesinde olduğu gibi orijinde (dokunulmaz).
- **Çatı sahası:** terasın arkasına (z 6 → 36) turuncu, beyaz çizgili saha zemini (Pota `saha.ts` `zeminDokusu` deseni, kodla
  üretilen doku), y = 0. Basket nişan kamerası −z'ye baktığı için basketi etkilemez.
- **Dev pota + tahta:** kök `BoardAnchor` = (0; 8,5; 30), dönüş yok. Tahta panonun içinde, yüzü −z'ye (kuleye ve kahramana) bakar.
  Dev pota ölçüleri Pota'dan (hücre = 1 birim): cam pano (tahta kasası + pay), turuncu çerçeve, çember (R 1,6) panonun altında öne
  çıkık, file (6 halka) = **cephane haznesi** (içi toplarla dolar, alt halkalar şişer), çatıya direk. Tahta kamerası
  `BoardAnchor + (0, merkezY, −uzaklık)`: kahramanın üstünde, kuleye sırtı dönük.
- **Sis:** Basket'in doğrusal sisi (26–115 m) tahtayı ve arka planını boğar; yönetmen sis aralığını geçişte tahta değerlerine
  (60–220 m) karıştırır, dönüşte geri alır.

## 4. Koreografi

Zaman gerçek zaman (ölçeksiz) saniye; t = 0 Basket turunun bitişi (`BasketRound.RoundEnded(N)`). Sayılar `PotaFlowSettings`te.

### Aday A — "Akan toplar" (önerilen)

| t (s) | Olay |
|---|---|
| 0,00–0,30 | **Vurgu.** Kamera kafesin üstünde (Basket'in bitiş pozu); kafes ağzında "N TOP" yazısı zıplar (0 → 1,25 → 1), kafes kenarı parlar, `swish` sesi, FOV 3° içeri vuruş. Basket sonuç paneli akışta açılmaz. |
| 0,25–1,10 | **Fışkırma.** V = min(N, 25) görsel top, kafes ağzından 0,035 s arayla çıkar; dönerek, kısa izli. |
| 0,30–1,90 | **Kamera uçuşu.** P0 (bitiş pozu, −z'ye bakış) → P1 yan manzara (≈ (13; 7; 17), akışın ortasına bakar: kule solda, kahraman ortada, dev pota sağda, toplar profilden kavis çizer) → P2 tahta kamerası (+z'ye bakış). Yol Catmull-Rom, hız yumuşak başla-bitir; bakış noktası kafes → akışın başı → tahta merkezi; FOV 60 → 52 → tahtanınki (34). Yaw 180° tek yönde döner. Sonda 0,12 s hafif ileri taşma ve geri oturma. |
| 0,40–0,90 | **Kahraman.** Terasta görünür (atıştan beri gizliydi), dev potaya doğru döner, `Idle`. |
| 1,15–2,20 | **Varış.** Her top ~1,15 s'lik kavisle (tepe, iki uçtan en yükseğin 9 m üstü) çembere girer, fileye düşer (`land` sesi, perde hafif artar); file içinde en çok 10 top birikir, alt halkalar şişer. Her iniş HUD cephanesine payını ekler (paylar toplamı tam N). |
| 1,75–2,35 | **Tahta belirir.** Bloklar merkezden halka halka (7×7'de 4 halka, halka arası 0,07 s) 0 → 1,12 → 1 ölçekle zıplar (0,28 s), çerçeve 0,4 s parlar. |
| 1,90–2,25 | **HUD girer.** Hedef paneli üstten, cephane paneli soldan, oklar alttan kayar (0,3 s). |
| 2,50 | **Hazır.** Girdi açılır; sayaç N'de durur, küçük bir zıplama. |

### Aday B — "Sade"

Kamera yolu, kahraman, tahtanın belirişi ve HUD aynı. Top akışı yok: "N TOP" rozeti kamerayla birlikte yol alır (ekran uzayında
yukarıdan aşağı büyüyüp küçülerek), varışta cephane paneline uçar ve patlar; cephane 0'dan N'ye uçuş boyunca sayar.

### Atlama ve dönüş

- Geçiş sırasında ekrana dokunuş: kalan zaman 6× hızlı oynar (≤ 0,35 s), olaylar atlanmaz, yalnız hızlanır.
- Bulmaca bitince (kazanma/kaybetme) sonuç panelinde "Tekrar at": ters uçuş P2 → P1 → Basket nişan pozu (1,2 s, top akışı yok),
  file boşalır, Basket turu aynı bölümle yeniden başlar. (Deneme döngüsü için; bu turun odağı ileri geçiş.)

## 5. Bileşenler (`Games/Pota/Scripts/Flow/`, derleme `Pota.Runtime`)

| Birim | Sorumluluk | Bağımlılık |
|---|---|---|
| `FlowTimeline` (saf C#) | Evreler ve süreler; `t`'de: kamera ilerlemesi, top i'nin çıkış/iniş zamanı, iniş paylarının kümülatifi, blok gecikmesi (halka no → s), HUD ilerlemesi, hazır anı; atlama hızı. | yok |
| `CameraFlight` (saf C# + ince MonoBehaviour) | P0/P1/P2 pozları arası yol (konum, bakış, FOV), taşma/oturma; P2 tahta kamerasının hesabından. | `FlowTimeline` |
| `BallStream` | Havuzlu görsel toplar (fiziksiz, iz): çıkış/kavis/iniş, file yığını; iniş olayı. | `FlowTimeline`, `GiantHoop` |
| `GiantHoop` | Dev pota görseli (pano, çerçeve, çember, file, direk) ve file haznesi (yığın yuvaları, şişme, boşalma); çerçeve parlaması. | — |
| `BoardReveal` | Renk Avı blok görünümlerinin halkalı zıplayarak belirmesi. | `ColorHuntGame` blok görünümleri |
| `PotaFlowDirector` | Durum makinesi Basket → Geçiş → Bulmaca → Sonuç → (Dönüş) → Basket; faz başına girdi, HUD, kamera bileşeni, sis; cephaneyi aktarır; atlama. | hepsi |
| `PotaFlowSettings` (ScriptableObject) | Bütün sayılar (süreler, pozlar, top sınırı, halka aralığı, sis), aday seçimi (A/B), Basket bölümü (20). | — |
| `PotaFlowSceneTool` (Editor, `ai_potaflow_build_scene`) | Sahneyi kodla kurar: Basket kurulumu + çatı sahası + dev pota + Renk Avı (öteli) + tek kamera/ışık/UI/EventSystem/Juice + yönetmen. | mevcut sahne araçları |
| `PotaFlowRecordTool` (Editor, `ai_potaflow_record`) | Play'de programatik atış → tur sonu → geçiş kare kare PNG (sabit 1/30 s), ffmpeg ile mp4 + kare levhası. | `BasketRecordTool` yöntemi |

Bölüm eşleşmesi: Basket bölüm 20 (ortanca sepet 158) ↔ Renk Avı tahtası (Pota bölüm 20, cephane 158): Pota'daki gibi cephane =
sepet sayısı.

## 6. Mevcut koda değişiklikler (en az, geriye uyumlu; mevcut iki sahne aynı çalışır)

- `ColorHuntCamera`: tahta çapası (`Anchor`, varsayılan orijin) — konum çapaya göre.
- `ColorHuntGame`: konfeti noktası tahta köküne göre; `AutoStart` (varsayılan açık); `NewRound(int ammo)`; turu kurup oyunu
  sonra başlatma (`Prepare` / `Begin`); gösterilen cephaneyi dışarıdan sayma (geçişte).
- `BasketHud`: sonuç panelini göstermeme seçeneği (akışta yönetmen gösterir).
- `RoundCamera`: yeniden etkinleşince durumu sıfırlama (dönüşte eski pozdan kaymasın).
- Tek `TimeScaleController`: iki Juice aynı örneği kullanır (iki tane birbirini ezer).
- Sahne araçlarının kurulum adımları, yeni araç da çağırabilsin diye yardımcı metotlara ayrılır (davranış aynı); `BasketSceneTool`
  Build Settings listesini ezmez, ekler.

## 7. Girdi, ses, performans

- Girdi: yönetmen `ShotInput.InputEnabled` ve `ColorHuntInput.enabled`i faza göre açar; geçişte ikisi kapalı, yalnız atlama dokunuşu.
- Ses (Core `SynthSfx`): vurgu `swish`, fışkırma `shot` (hafif), iniş `land` (perde +%1/top, tavanlı), belirme `multiply`, hazır `add`.
- Performans: görsel toplar ve izler havuzdan, sahne kurulumunda ön ısıtılır; en çok 25 top + 10 yığın topu; kare başına tahsis yok.

## 8. Test ve doğrulama

- EditMode: `FlowTimeline` (evre sınırları, iniş payları toplamı = N her N için 0…999, halka gecikmeleri, atlama ≤ 0,35 s),
  `CameraFlight` (uçlarda poz tam eşit, yaw tek yönde, FOV sınırda).
- PlayMode: `PotaFlow` sahnesi açılır; tur sonu N ile tetiklenir → yönetmen ≤ 3 s içinde Bulmaca'da, Renk Avı `Playing`, cephane = N,
  Renk Avı girdisi açık, Basket girdisi kapalı; atlamayla ≤ 1 s. Sahne açılış testi (Build Settings'teki her sahne) PotaFlow'u da kapsar.
- Görsel: `ai_potaflow_record` ile iki adayın kare dizisi, mp4 ve levhası. Pota karşılaştırması: Pota **kopyası** (scratch klasöründe)
  çalıştırılır, aynı geçiş aynı telefon boyunda kaydedilir; Pota klasörüne hiçbir şey yazılmaz.

## 9. Teslim

A ve B kayıtları + Pota kaydı yan yana (levha + video), `docs/screenshots/2026-09-29-potaflow-*`. Tuna seçer; seçilen aday
varsayılan olur, diğeri ayarda kalır. Çalıştırma: `PotaFlow` sahnesi → Play → aşağıdan yukarı sürükle; tur bitince geçiş.

## 10. Kapsam dışı

Pota'ya taşıma; Renk Avı'nın balon görünümü; yeni animasyon klibi; salvo toplarının fileden çıkması; telefon derlemesi.

## 11. Riskler

- İki oyunun zaman ölçeği ve efektleri aynı sahnede çakışabilir: tek `TimeScaleController`, faz başına Juice.
- 8 sıralı bölüm 20 kulesi uzun: kayıtta tur bitişi programatik tetiklenebilir (kayıt aracı "tur sonu N" kipi).
- Tahta kamerası portrede ~25 m uzakta: sis ve ışık ayarı gözle doğrulanır.
