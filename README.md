# BLE Proximity

**BLE Proximity** adalah aplikasi desktop Windows yang memantau perangkat Bluetooth Low Energy (BLE) dan secara otomatis menjalankan perintah kustom ketika perangkat tepercaya keluar dari jangkauan. Aplikasi ini dirancang untuk meningkatkan keamanan dengan mengunci workstation secara otomatis ketika smartphone atau perangkat BLE lainnya tidak terdeteksi dalam jarak tertentu.

## Kenapa tidak menggunakan Dynamic Lock bawaan Windows?
Karena harus pairing dulu baru bisa di tambahkan dan hanya bisa ke HP saja, karena aku menggunakan Smart Ring jadilah aplikasi ini~

![BLE Proximity](https://img.shields.io/badge/Platform-Windows-blue) ![.NET 8](https://img.shields.io/badge/.NET-8.0-purple) ![WPF](https://img.shields.io/badge/UI-WPF-orange) ![License](https://img.shields.io/badge/License-MIT-green)

## 🚀 Fitur Utama

### 🔍 **Pemindaian BLE Pasif**
- Memindai iklan BLE secara pasif tanpa memerlukan pairing
- Mendeteksi perangkat berdasarkan alamat MAC dan kekuatan sinyal (RSSI)
- Menampilkan daftar perangkat yang terdeteksi dengan informasi real-time

### 📊 **Smoothing RSSI dengan EMA**
- Menggunakan Exponential Moving Average (EMA) untuk menghaluskan fluktuasi sinyal
- Parameter alpha yang dapat dikonfigurasi (0.1 - 0.5)
- Mengurangi false positive akibat gangguan sinyal sementara

### ⚙️ **Threshold Hysteresis**
- Threshold terpisah untuk "in-range" dan "out-of-range"
- Buffer hysteresis minimum 5 dBm untuk mencegah osilasi
- Konfigurasi threshold yang fleksibel (-50 hingga -95 dBm)

### 🔄 **State Machine Proximity**
- State machine yang jelas: InRange → OutOfRangePending → Countdown → Executing
- Timeout yang dapat dikonfigurasi sebelum memicu countdown
- Grace period saat startup untuk menghindari eksekusi yang tidak diinginkan

### 📱 **Notifikasi Toast dengan Countdown**
- Notifikasi Windows Toast dengan countdown 3 detik
- Dapat dibatalkan jika perangkat kembali dalam jangkauan
- Informasi perangkat dan perintah yang akan dijalankan

### 🛠️ **Eksekusi Perintah Kustom**
- Preset perintah: Lock Workstation, Mute Volume
- Dukungan script kustom dengan placeholder dinamis
- Timeout eksekusi 30 detik dengan penanganan error

### 👥 **Dukungan Multi-Device** (Belum di test dan belum stabil)
- Memantau hingga 10 perangkat tepercaya secara bersamaan
- Mode single-device atau multi-device
- Logika "semua perangkat out-of-range" untuk memicu aksi
- **Rename perangkat**: Klik kanan pada trusted device untuk mengubah nama

### 🎯 **System Tray Integration**
- Berjalan di background dengan ikon system tray
- Context menu dengan informasi status real-time
- Indikator visual berdasarkan status proximity

### 🔧 **Konfigurasi Persisten**
- Penyimpanan konfigurasi dalam format JSON
- Auto-start dengan Windows (opsional)
- Tema gelap/terang

### 🔒 **Single Instance Enforcement**
- Hanya satu instance aplikasi yang dapat berjalan
- IPC untuk mengembalikan window yang sudah ada
- Penanganan error mutex yang robust

## 📋 Persyaratan Sistem

- **OS**: Windows 10 version 19041 atau lebih baru
- **Framework**: .NET 8 Runtime
- **Hardware**: Adapter Bluetooth dengan dukungan BLE
- **Permissions**: Akses Bluetooth dan Registry (untuk auto-start)

## 🛠️ Instalasi

### Download Release
1. Download file executable terbaru dari [Releases](../../releases)
2. Simpan aplikasi di tempat permanen, karena ini portable apps.
3. Jalankan `BLE Proximity.exe`
4. Aplikasi akan membuat shortcut di Start Menu secara otomatis

### Build dari Source
```bash
# Clone repository
git clone https://github.com/yourusername/BLEwinLock.git
cd BLEwinLock

# Build project
dotnet build BLEProximity/BLEProximity.csproj -c Release

# Run aplikasi
dotnet run --project BLEProximity/BLEProximity.csproj
```

## 🎯 Cara Penggunaan

### 1. **Setup Perangkat Tepercaya**
   - Jalankan aplikasi dan klik "Scan Devices"
   - Double-click perangkat BLE yang ingin dipantau atau klik kanan dan tambahkan
   - Perangkat akan ditambahkan ke daftar trusted devices
   - **Rename perangkat**: Klik kanan pada perangkat di daftar trusted devices → pilih "Rename Device"

### 2. **Konfigurasi Threshold**
   - Atur **In-Range Threshold** (default: -70 dBm)
   - Atur **Out-of-Range Threshold** (default: -75 dBm)
   - Pastikan buffer minimum 5 dBm antara kedua threshold

### 3. **Pilih Perintah**
   - Pilih preset: Lock Workstation, Mute Volume
   - Atau buat custom script

### 4. **Konfigurasi Timeout**
   - **Out-of-Range Timeout**: Durasi tunggu sebelum countdown (5-60 detik)
   - **Grace Period**: Delay setelah startup sebelum monitoring aktif (0-30 detik)
   - **Missing Beacon Grace**: Toleransi hilangnya beacon sebelum dianggap out-of-range (1-30 detik)

### 5. **Mode Multi-Device** (Belum di test dan belum stabil)
   - Toggle "Use Multi-Device Mode" untuk memantau beberapa perangkat
   - Perintah hanya dijalankan jika **semua** perangkat out-of-range
   - Dibatalkan jika **salah satu** perangkat kembali in-range

## 🎨 Interface Pengguna

### Main Window
- **Device Scanner**: Daftar perangkat BLE yang terdeteksi dengan RSSI real-time
- **Trusted Devices**: Perangkat yang dipantau untuk proximity
  - Klik kanan untuk context menu: "Rename Device" dan "Remove Device"
- **Configuration Panel**: Pengaturan threshold, timeout, dan perintah
- **Activity Log**: Log aktivitas dan status monitoring

### System Tray
- **Icon Status**: 
  - 🟢 Hijau: In Range
  - 🟡 Kuning: Out of Range Pending
  - 🔴 Merah: Countdown/Executing
  - ⚫ Abu-abu: No Device/Cancelled
- **Context Menu**: Status perangkat, pengaturan, dan kontrol eksekusi
- **Tooltip**: Informasi perangkat dan RSSI terkini

## 🔧 Konfigurasi Lanjutan

### File Konfigurasi
Lokasi: `%APPDATA%\BLEProximity\config.json`

```json
{
  "startWithWindows": false,
  "useMultiDevice": false,
  "darkMode": false,
  "trustedDevices": [
    {
      "name": "Smart Ring",
      "macAddress": "AABBCCDDEEFF"
    }
  ],
  "inRangeThreshold": -70,
  "outOfRangeThreshold": -75,
  "outOfRangeTimeoutSec": 10,
  "rssiAlpha": 0.3,
  "commandPreset": "LockWorkstation",
  "gracePeriodSec": 5,
  "missingBeaconGraceSec": 3
}
```

### Command Line Arguments
```bash
# Enable debug console
"BLE Proximity.exe" --debug-console

# Set environment variable untuk debug
set BLELOCK_DEBUG=1
"BLE Proximity.exe"
```

### Registry Auto-Start
Aplikasi menggunakan registry key berikut untuk auto-start:
```
HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run
```

## 🏗️ Arsitektur Teknis

### Komponen Utama
- **BleScanner**: Pemindaian BLE menggunakan WinRT APIs
- **RssiSmoother**: Smoothing sinyal dengan EMA
- **ProximityMonitor**: State machine dengan Stateless library
- **ToastNotifier**: Notifikasi Windows dengan countdown
- **CommandExecutor**: Eksekusi perintah dengan timeout
- **ConfigManager**: Persistensi konfigurasi JSON
- **TrayManager**: Integrasi system tray

### Dependencies
- **Microsoft.Windows.SDK.NET.Ref**: WinRT BLE APIs
- **Hardcodet.NotifyIcon.Wpf**: System tray integration
- **Microsoft.Toolkit.Uwp.Notifications**: Toast notifications
- **CommunityToolkit.Mvvm**: MVVM framework
- **Stateless**: State machine library
- **System.Text.Json**: JSON serialization

### Design Patterns
- **MVVM**: Separation of concerns untuk UI
- **State Machine**: Explicit state management
- **Observer Pattern**: Event-driven architecture
- **Dependency Injection**: Loose coupling antar komponen

## 🧪 Testing

### Menjalankan Tests
```bash
# Unit tests
dotnet test BLEProximity.Tests/BLEProximity.Tests.csproj

# Property-based tests dengan FsCheck
dotnet test --filter "Category=Property"

# Integration tests
dotnet test --filter "Category=Integration"
```

### Test Coverage
- **Property-Based Testing**: 16 properties dengan FsCheck
- **Unit Testing**: Komponen individual dan edge cases
- **Integration Testing**: Lifecycle aplikasi dan persistensi

## 🐛 Troubleshooting

### Bluetooth Tidak Terdeteksi
- Pastikan adapter Bluetooth mendukung BLE
- Periksa driver Bluetooth terbaru
- Restart Bluetooth service di Windows

### Aplikasi Tidak Auto-Start
- Jalankan sebagai Administrator untuk akses registry
- Periksa Windows Startup settings
- Disable antivirus yang memblokir registry access

### Toast Notification Tidak Muncul
- Pastikan Windows Notifications enabled
- Periksa shortcut di Start Menu
- Restart aplikasi untuk regenerate shortcut

### False Positive Out-of-Range
- Turunkan nilai alpha EMA (0.1-0.2)
- Naikkan out-of-range timeout
- Periksa interferensi Bluetooth di sekitar

### Performance Issues
- Batasi jumlah trusted devices (max 10)
- Tutup aplikasi BLE lain yang tidak perlu
- Periksa CPU usage di Task Manager

## 📝 Changelog

### v1.0.1 (Current)
- ✅ Fitur rename trusted devices via context menu
- ✅ Implementasi lengkap semua fitur core
- ✅ UI dengan tema gelap/terang
- ✅ Property-based testing dengan FsCheck
- ✅ Dokumentasi lengkap
- ✅ Single instance enforcement
- ✅ Robust error handling

### v1.0.0
- ✅ Release pertama dengan fitur dasar BLE proximity monitoring

### Planned Features (Ntah kapan akan di realisasikan)
- 🔄 Buat aplikasi lebih stabil
- 🔄 Custom notification sounds
- 🔄 Proximity zones dengan multiple thresholds dan multiple devices


## 🤝 Contributing

Kontribusi sangat diterima! Silakan:

1. Fork repository ini
2. Buat feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit perubahan (`git commit -m 'Add some AmazingFeature'`)
4. Push ke branch (`git push origin feature/AmazingFeature`)
5. Buka Pull Request

### Development Guidelines
- Ikuti C# coding conventions
- Tambahkan unit tests untuk fitur baru
- Update dokumentasi sesuai perubahan
- Gunakan property-based testing untuk invariants

## 👨‍💻 Author

**FoxLost**
- GitHub: [@FoxLost](https://github.com/FoxLost)
- Email: admin@foxlost.my.id

## 🙏 Acknowledgments

- [Hardcodet.NotifyIcon.Wpf](https://github.com/hardcodet/wpf-notifyicon) untuk system tray integration
- [Microsoft Toolkit](https://github.com/CommunityToolkit/WindowsCommunityToolkit) untuk toast notifications
- [Stateless](https://github.com/dotnet-state-machine/stateless) untuk state machine implementation
- [FsCheck](https://github.com/fscheck/FsCheck) untuk property-based testing
- Windows BLE API documentation dan community

---

**⚠️ Disclaimer**: Aplikasi ini dirancang untuk meningkatkan keamanan, namun tidak boleh menjadi satu-satunya lapisan keamanan. Selalu gunakan password yang kuat dan praktik keamanan yang baik.