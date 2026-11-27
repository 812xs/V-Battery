# V-Battery

Windows 10/11 için hafif, yerel (C# / .NET 8 / WPF) pil izleyici. USB ve Bluetooth ile bağlı
cihazların (mouse, klavye, kulaklık, gamepad…) pil seviyesini görev çubuğundaki bildirim alanında
(saatin solunda) cihaz başına ayrı bir simge olarak gösterir.

> **Dürüstlük ilkesi:** Yalnızca cihazın gerçekten bildirdiği değerler gösterilir. Pil bilgisi
> alınamıyorsa `--` yazar; tahmini veya sahte yüzde üretilmez.

## Özellikler

- Cihaz başına dinamik tepsi simgesi: tür simgesi + yüzde, düşük pilde kırmızı, şarjda yeşil + şimşek, bilgi yoksa `--`
- Bluetooth LE: standart GATT Battery Service (0x180F) / Battery Level (0x2A19); cihaz bildirim destekliyorsa olay tabanlı
- USB/Bluetooth HID: HID pil raporları (Generic Device Controls *Battery Strength*, Battery System *State of Charge*)
- Xbox/XInput gamepad: dört kademeli seviye (Windows yüzde vermez; uygulama da yüzde göstermez)
- Koyu temalı ana pencere: Durum, Cihazlar, Bildirimler, Ayarlar
- Düşük pil bildirimi (%10/15/20/30), %5'te kritik uyarı, isteğe bağlı şarj tamamlandı bildirimi, cihaz başına açma/kapama
- Sağ tık menüsü: cihazlar, görev çubuğunda göster, masaüstünde serbest göster, simge boyutu, saat/tarih, bildirim ayarları, test, Windows ile başlat, yenile, tanılama raporu, hakkında, çıkış
- Sürüklenebilir masaüstü göstergesi (saydam/koyu arka plan, ölçek, her zaman üstte, konum kaydı)
- Windows ile otomatik başlatma (HKCU Run, yönetici izni gerekmez), tepside başlatma
- Ayarlar `%APPDATA%\V-Battery\settings.json` içinde saklanır

## Kurulum

1. [Releases](../../releases) sayfasından `V-Battery-Setup-x.y.z.exe` dosyasını indirip çalıştırın (yönetici izni gerekmez), **veya**
2. Tek dosyalı `V-Battery.exe` dosyasını istediğiniz klasörden çalıştırın (.NET kurulumu gerekmez).

İlk açılışta uygulama Windows ile başlayacak şekilde kaydolur; **Ayarlar** sekmesinden veya tepsi menüsünden kapatılabilir.

### Simgeleri saatin yanına sabitleme

Windows 11/10 yeni tepsi simgelerini taşma menüsüne koyabilir. **Ayarlar → Kişiselleştirme → Görev çubuğu → Diğer sistem tepsisi simgeleri**
bölümünden V-Battery simgelerini açın. Simge sırasını ve gizlemeyi Windows yönetir; V-Battery destek olmayan görev çubuğu müdahaleleri yapmaz.

## Kullanım

- Simgeye **sol tık**: ana pencere. **Sağ tık**: menü.
- Pencereyi kapatmak uygulamayı kapatmaz, tepsiye gizler. Kapatmak için menüden **Çıkış**.
- **Cihazlar** sekmesinden bir cihazı göstergelerden gizleyebilir veya bildirimlerini kapatabilirsiniz.
- **Tanılama raporunu aç**: algılanan cihazlar, durumları ve son günlük kayıtları `%LOCALAPPDATA%\V-Battery\diagnostics.txt` dosyasına yazılıp açılır. Bir cihaz görünmüyorsa önce buna bakın.

## Desteklenen / desteklenmeyen cihazlar

Ayrıntı: [docs/DEVICE-SUPPORT.md](docs/DEVICE-SUPPORT.md). Kısaca: standart GATT pil servisi sunan Bluetooth LE cihazlar,
standart HID pil raporu sunan cihazlar ve XInput gamepad'ler desteklenir. Üreticiye özel protokoller
(ör. Logitech HID++, Razer/SteelSeries özel kanalları) şu an desteklenmez.

## Derleme

Gereksinimler: Windows 10/11, .NET 8 SDK.

```powershell
dotnet build VBattery.sln -c Release
dotnet test tests/VBattery.Tests/VBattery.Tests.csproj -c Release
./scripts/publish.ps1            # artifacts/publish/V-Battery.exe (self-contained, tek dosya)
iscc installer/V-Battery.iss     # artifacts/installer/V-Battery-Setup-*.exe (Inno Setup 6)
```

GitHub Actions (`.github/workflows/build.yml`) her push'ta derler, test eder, yayınlar ve kurulum paketini üretir;
`v*` etiketinde GitHub Release oluşturur.

## Mimari

Ayrıntı: [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).

```
src/
  VBattery.Core            modeller, IDeviceProvider, günlük
  VBattery.DeviceDetection DeviceMonitor (sağlayıcıları toplar, tekilleştirir)
  VBattery.Bluetooth       GATT Battery Service sağlayıcısı
  VBattery.HID             HID pil raporu + XInput sağlayıcıları
  VBattery.Notifications   eşik/histerezis mantığı
  VBattery.Settings        JSON ayarlar, Windows ile başlatma
  VBattery.Tray            dinamik tepsi simgeleri ve menü
  VBattery.App             WPF arayüz, masaüstü göstergesi, başlatma
tests/VBattery.Tests
```
