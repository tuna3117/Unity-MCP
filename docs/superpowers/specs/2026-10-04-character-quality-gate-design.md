# Karakter kalite kapısı ve kalite turu — tasarım (2026-10-04)

Tuna (2026-10-04, animasyon kanıtına bakınca): "baya kötü … adamın elleri bozuk, ayaklar saçma … bunun kontrolünü de sağlamak lazım
düzgünce". Hat çalışıyor ama çıktı kalitesini yakalayan bir kontrol yok. Bu belge kök nedenleri ve kapıyı tanımlar.

## 1. Kök nedenler (kanıt: yakın plan render'ları, ölçümler)

| # | Kusur | Kök neden | Kanıt |
|---|---|---|---|
| R1 | Eller köşeli, sivri "parmak" dikenleri, şutta buruşuk | Model hattı (`character_model`): QuadriFlow 4 000 dörtgen hedefi yüzeye **eşit** dağılıyor; eller (yüzeyin küçük bir payı) birkaç düzine yüze iniyor. Kaynak Rodin elleri (95 bin üçgen) düzgün, yuvarlak parmaklı. Parmak kemikleri sıfırlansa da dikenler kalıyor: kusur mesh'te. | `docs/screenshots/2026-10-04-qc-evidence-hands-{source-vs-model,fingers}.jpg` |
| R2 | Ayakkabı tabanı ten rengi (ayak kalkınca çıplak ayak gibi) | Doku pişirme/palet: referans görünümlerin (ön/yan/arka) hiç görmediği yüzeyler (taban, koltuk altı) kaynağın rastgele rengini ve paletin en yakın rengini alıyor. | `docs/screenshots/2026-10-04-qc-evidence-sole-face.jpg` (alttan görünüm) |
| R3 | Ayaklar saçma: koşuda topuk kalçaya, ayak ucu aşağı; büyük ayakkabılar palet gibi savruluyor; darbe almada ayaklar öne kayıyor | Gerçekçi hareket kaydının bacak mekaniği kısa bacaklı / büyük ayakkabılı karaktere olduğu gibi aktarılıyor (kaynak Y Bot koşusunda da ayak ucu ≈170° geriye dönüyor: aktarım sadık, üslup yanlış). | ayak yönü ölçümü: kaynak ve ham koşuda ±170°; `docs/screenshots/2026-10-04-qc-evidence-legs.jpg` |
| R4 | Kontrol yok | Model kapıları yalnız tüm vücut (yüzey sapması p95 ≤ %1: eller ortalamayı oynatmıyor); animasyon metriklerinde el/ayak duruşu, eklem sınırı, yakın plan yok; teslim levhaları 256 px tam boy (el ~10 px). | — |

Ek: yüz dokusunda göz ve diş kenarları lekeli (palet gürültüsü), yakında görünür.

## 2. Kalite kapısı (her karakter ve animasyon teslimi)

### 2.1 Model (`qc_model`, `character_model`'in son adımı ve tek başına araç)
- **Bölge bazlı sapma:** iskelet ya da işaret noktalarından bölgeler (eller, ayaklar, kafa/yüz, gövde); her bölgede kaynaktan modele yüzey sapması p95, bölgenin kendi boyuna oranla (el: p95 ≤ el boyunun %4'ü; ayak ve kafa ≤ %3; gövde mevcut kapı).
- **Görülmeyen yüzey:** referans görünümlerden hiç görünmeyen yüz payı ve bunların rengi komşu görünür yüzeylerden doldurulmuş mu (taban rengi = ayakkabının yan rengi ailesi; ten rengi taban = başarısız).
- **Yakın plan levhası** `steps/qc-closeups.png`: iki el (üstten, avuç), iki ayak (yan, alt), yüz (ön, ¾), kaynakla yan yana. Teslim edilmeden bakılır.

### 2.2 Animasyon (`qc_anim`, `character_animated`'in parçası ve tek başına araç)
Her klip, her kare:
- **Eklem sınırları** (kahraman oranına göre): diz geri bükülmesi yok, diz bükümü ≤ 120°, topuğun kalçaya yaklaşması sınırlı (geri tekme ≤ bacak boyunun %35'i); ayak bileği: ayak ucu aşağı (plantar) ≤ 35°, yukarı ≤ 25° dinlenmeye göre; bilek bükümü ≤ 60°, bilek burulması ≤ 45°; parmaklar yalnız bükülme ekseninde, ≤ 50° (yana açılma ≤ 10°); dirsek geri bükülmesi yok.
- **İç içe geçme:** el–kafa, el–gövde, bacak–bacak, ayakkabı–ayakkabı (kemik kapsülleriyle hızlı yaklaşık).
- **Zemin:** basan ayak tabanı yerde (± 1 cm), hiçbir kare zeminin altında; basan ayak kayması (mevcut metrik) eşiği.
- **Yakın plan levhası** `steps/qc-<klip>.png`: anahtar karelerde (hazırlık, tepe, iniş, son) eller ve ayaklar yakın, tam boy yan ve ön görünüm.
- Kapı sonucu manifest'te `checks.anim` (klip başına ihlal sayıları); ihlal varsa `checks.passed = false`.

### 2.3 Süreç
Teslim Tuna'ya gösterilmeden önce yakın plan levhalarına tam çözünürlükte bakılır; "kod çalıştı / metrik geçti" yetmez. Unity kanıtında ham | stilize videonun yanında el/ayak yakın plan levhaları da teslim edilir.

## 3. Düzeltmeler (kalite turu)

1. **Model (R1, R2, yüz):** `character_model`'e bölgeye göre yoğunluk: eller, ayaklar ve yüz daha sık örülür (kaynak Rodin aynı; yeni ücretli çağrı yok). Yöntem atölyenin seçimi (ör. QuadriFlow sonrası bölge yerine kaynaktan uyarlamalı inceltme, ya da tüm hedefin yükseltilip el bölgesinin korunması); toplam üçgen bütçesi kahraman için ≤ 15 bin (Brawl Stars / Squad Busters kahramanları 5–15 bin). Görülmeyen yüzey rengi komşu görünür yüzeyden. Kahraman `pota-hero` sürüm 3; eski iskelet korunur, ağırlıklar yeni mesh'e aktarılır (`rig_transfer`, ücretsiz), `rig_check` ile puanlanır.
2. **Animasyon (R3):** `anim_stylize`'a `limits` adımı (§2.2 sınırları yumuşak kısıtlama olarak; ayak bileği, diz, bilek, parmak) ve kısa bacaklı karakter için koşu/zıplama bacak mekaniği (kısa, hızlı adım; ayak çoğunlukla yere paralel; havada ayak ucu ≤ 35° aşağı). Parmaklar: kaba el mesh'inde parmak animasyonu yalnız hafif kıvrılma (gevşek el pozu), ani parmak açılması yok.
3. **Yeniden teslim:** 8 klip yeni modelde, kapıdan geçerek; Unity kanıtı + yakın plan levhaları.

## 4. Ölçüt
- Kapı her iki teslimde de geçer; geçmeyen kare sayısı 0.
- Yakın plan levhalarında el parmakları yuvarlak ve ayrık, taban ayakkabı renginde, yüz kenarları temiz.
- Ham | stilize videoda ayaklar kısa bacaklı karaktere uygun, eller bozulmuyor.
- Tuna'nın gözü.
