# Animasyonlu oyuncu karakteri: görselden Unity'ye tam hat (2026-09-25)

Amaç: Pota'nın menü kahramanı `kahraman.png`'den, kullanıcı elle hiçbir şey yapmadan, Unity'de şut/koşu/smaç
animasyonu oynayan bir 3B karakter üretmek. Pota'nın kendi dosyalarına dokunulmadı; her şey bu projede.

## Sonuç

- `Assets/_Project/Prefabs/PlayerCharacter.prefab`: Humanoid rig (52 kemik, Mixamo adları), Animator + `Assets/_Project/Anim/PlayerCharacter.controller`
  (durumlar: Idle, Run, Shoot, Dunk; parametreler: `Run` bool, `Shoot`/`Dunk` trigger).
- `BasketSettings.PlayerPrefab` bu prefab'a bağlandı; `PlayerActor` şutta Shoot klibini `PlayerShootClipStart` (0.9 s) noktasından başlatır, tur
  başında Idle'a döner. Animator yoksa eski squash/stretch davranışı korunur.
- Klipler `Assets/_Project/Art/Pota/Anim/player_*.fbx` (Y Bot iskeleti, Humanoid, kök hareketi yerinde, zıplama yüksekliği poza pişirilmiş).
- Toplam Replicate harcaması: ~$0.76 (13 çağrı; en pahalısı TRELLIS.2 $0.41, kullanılmadı).

## Adımlar ve görüntüler (`docs/screenshots/2026-09-25-character/`)

1. **A-pozu** — nano-banana-pro, kimlik kilitli, yeşil fon → `01-apose-nano-banana.jpg`. İlk deneme (3:4) ellerini kadraja değdirdi; kare oran + geniş boşluk istemi çözdü.
2. **Mesh** — `firtoz/trellis` (21.8k üçgen, 2048 doku, 30 s) → `02-trellis-turntable.jpg`. TRELLIS.2 (`02b`) daha temiz geometri verdi ama formadaki "7"yi bozdu ve 11 dk sürdü; kullanılmadı.
3. **Rig** — `uthana/create-character-v1` 30 s'de T-pozlu, ağırlıklı GLB verdi. glTFast bunu çarpık okuyor (Armature ölçeği); `Tools/character/fix_rig_glb.py` normalize etti → `03-rig-tpose-unity.jpg`.
4. **Animasyon** — `uthana/text-to-motion-diffusion-v2`, metinden Humanoid FBX. Kontakt sayfaları: `04-shoot-clip.jpg`, `05-run-clip.jpg`, `06-dunk-clip.jpg`.
5. **Oyun içi** — `07-ingame-idle.jpg` (nişan aşaması), `08-ingame-shot-frames.jpg` (şut: eller yukarı, top uçuyor, +1).

## Dürüst notlar

- Şut klibi iyi (eller 1.0–1.3 s'de başın üstüne çıkıyor). Koşu hafif bir jog, bacak hareketi zayıf. Smaç "tek kolla sert vuruş"tan çok iki kollu bir sıçrama; oyunda kullanılmıyor, sadece controller'da hazır.
- Oyunda şut klibi 0.9 s'den başladığı için çömelme görülmüyor; toplar zaten anında çıktığı için "kollar yukarı → iniş" yeterli. İstenirse `PlayerShootClipStart` düşürülüp top çıkışı geciktirilebilir.
- Parmak kemikleri var ama animasyon verisi parmaklara zayıf; eller düz.
- Yeni araçlar: `ai_character_setup`, `ai_anim_contact_sheet` (kemik yükseklik tablosu basar; klip kalitesini sayıyla ölçmek için).
- Hattı yeniden koşmak: `Tools/character/README.md`.
