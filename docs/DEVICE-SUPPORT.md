# Cihaz desteği ve sınırlar

V-Battery yalnızca cihazın standart yollarla bildirdiği pil bilgisini okur.

| Kaynak | Ne okunur | Şarj durumu | Güncelleme |
|---|---|---|---|
| Bluetooth LE GATT | Battery Service 0x180F / Battery Level 0x2A19 (0–100) | Bilinmiyor (bu karakteristik taşımaz) | Cihaz Notify destekliyorsa olay, yoksa "Şimdi yenile" / bağlantı olayı |
| HID | Usage Page 0x06 Usage 0x20 (Battery Strength); Usage Page 0x85 Usage 0x0D/0x0E (State of Charge) | Bilinmiyor (henüz okunmuyor) | 3 dakikada bir tarama + "Şimdi yenile" |
| XInput | `XInputGetBatteryInformation` → boş/düşük/orta/dolu | Bilinmiyor | 2 dakikada bir |

## Bilinen sınırlar

- Pil bilgisi sunmayan cihaz listede "Pil bilgisi sunulmuyor" ve `--` olarak görünür.
- Windows klavye/mouse HID koleksiyonlarına özel erişimi kısıtlar; pil raporu ayrı bir koleksiyonda değilse okunamayabilir.
- Üreticiye özel protokoller (Logitech HID++, Razer, SteelSeries, Corsair vb.) desteklenmez. Yeni protokol için `IDeviceProvider` uygulanıp `App.xaml.cs` içinde `DeviceMonitor.AddProvider` ile eklenir.
- Klasik Bluetooth (BR/EDR) kulaklıklarda pil bilgisi çoğunlukla HFP/AVRCP üzerinden gelir; bu yollar henüz yoktur.
- Aynı fiziksel cihaz hem GATT hem HID/XInput olarak görünürse iki kayıt olabilir; cihazı "Cihazlar" sekmesinden gizleyebilirsiniz.
- Bu sürüm gerçek donanımda henüz doğrulanmamıştır; sorun yaşarsanız tanılama raporunu issue'ya ekleyin.
