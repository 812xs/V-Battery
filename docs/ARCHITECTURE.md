# Mimari

```
IDeviceProvider (Core)  ──▶  DeviceMonitor (DeviceDetection)  ──▶  DeviceInfo (Core, INotifyPropertyChanged)
   ▲  Bluetooth GATT                    │ DeviceUpdated / DeviceListChanged
   ▲  HID                               ├──▶ TrayManager (Tray)         : cihaz başına NotifyIcon + menü, INotifier
   ▲  XInput                            ├──▶ BatteryNotificationService  : eşikler, histerezis, cihaz başına tercih
                                        └──▶ MainViewModel (App)         : WPF kartları / liste, masaüstü göstergesi
SettingsService (Settings): JSON, ertelenmiş atomik kayıt, Changed olayı; StartupManager: HKCU Run
```

- **Sağlayıcılar** `DeviceReading` (değişmez kayıt) üretir; `DeviceMonitor` kimliğe göre tekilleştirip `DeviceInfo`'yu günceller.
- Her sağlayıcı ve dinleyici çağrısı try/catch içindedir; hatalı cihaz yalnızca kendi durumunu bozar.
- Olay tabanlı: GATT Notify, bağlantı durumu olayı, `DeviceWatcher`. Sağlayıcıların seyrek yoklaması yalnızca olay olmayan kaynaklar içindir (HID, XInput).
- Tepsi simgeleri GDI tutamacı sızdırmaz (`GetHicon` → klon → `DestroyIcon`; eski simge `Dispose`).
- Tek örnek: adlandırılmış Mutex; ikinci başlatma çalışan örneğin penceresini açtırır.
- Yeni protokol: `IDeviceProvider` uygula, `AddProvider` ile ekle.
