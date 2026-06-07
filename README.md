<div align="center">

<img src="Assets/Icons/Logo.png" alt="Traffic Monitor Logo" width="120" height="120"/>

# 🌐 Traffic Monitor

**A modern, feature-rich Windows network traffic monitoring application — rebuilt from the ground up.**

[![Platform](https://img.shields.io/badge/Platform-Windows-0078D4?style=for-the-badge&logo=windows&logoColor=white)](https://github.com/Diagoo1)
[![.NET](https://img.shields.io/badge/.NET-8.0--windows-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![WPF](https://img.shields.io/badge/UI-WPF-68217A?style=for-the-badge&logo=microsoft&logoColor=white)](https://docs.microsoft.com/en-us/dotnet/desktop/wpf/)
[![License](https://img.shields.io/badge/License-MIT-22C55E?style=for-the-badge)](LICENSE)
[![Made By](https://img.shields.io/badge/Made%20by-Tarek%20Sadek-0EA5E9?style=for-the-badge)](https://github.com/Diagoo1)



</div>

---

## 📖 What is Traffic Monitor?

**Traffic Monitor** is a lightweight yet powerful Windows application that monitors your internet and network traffic in real time. It displays live upload/download speeds directly on your desktop and inside the Windows taskbar — designed with a modern, clean UI that adapts to your system's dark or light theme.

Built with **WPF on .NET 8**, Traffic Monitor is fully portable, supports multiple languages including Arabic (RTL), and comes packed with advanced features like a built-in speed test, process-level network tracking, and detailed historical traffic statistics.

---

## ✨ Features at a Glance

| Category | Features |
|---|---|
| 📊 **Live Monitoring** | Real-time upload/download speed, today's total usage, active connection type |
| 🪟 **Taskbar Window** | Embedded taskbar widget that auto-detects light/dark theme |
| 🔍 **Speed Test** | Built-in speed test with animated gauge, ping, jitter, packet loss, ISP info |
| 🧩 **Process Monitor** | Per-process network usage with icons, live bars, and sortable list |
| 📅 **Traffic History** | Daily/monthly/yearly stats with list view and interactive calendar view |
| ⚙️ **Options** | Full settings panel: theme, language, opacity, startup, intervals, notifications |
| 🌍 **Multi-Language** | 5 languages: English, Arabic (RTL), Spanish, French, Russian |
| 🔔 **Notifications** | Custom animated toast notifications (success, warning, error, info) |
| 🎨 **Theming** | Light / Dark / Auto theme with smooth transitions and DPI-aware rendering |
| 🪟 **Transparency** | Global window opacity control (30–100%) |
| 📋 **System Tray** | Minimize to tray, tooltip with live stats, context menu |
| 🌐 **Connection Details** | Full adapter info: IP, MAC, gateway, DNS, speed, bytes sent/received |

---

## 🖼️ Screenshots

<div align="center">
  <img src="https://raw.githubusercontent.com/Diagoo1/TrafficMonitor/refs/heads/main/Screenshots/Img%20(1).png" width="45%" alt="Screenshot 1"/>
  <img src="https://raw.githubusercontent.com/Diagoo1/TrafficMonitor/refs/heads/main/Screenshots/Img%20(2).png" width="45%" alt="Screenshot 2"/>
  <br/>
  <img src="https://raw.githubusercontent.com/Diagoo1/TrafficMonitor/refs/heads/main/Screenshots/Img%20(3).png" width="45%" alt="Screenshot 3"/>
  <img src="https://raw.githubusercontent.com/Diagoo1/TrafficMonitor/refs/heads/main/Screenshots/Img%20(4).png" width="45%" alt="Screenshot 4"/>
  <br/>
  <img src="https://raw.githubusercontent.com/Diagoo1/TrafficMonitor/refs/heads/main/Screenshots/Img%20(5).png" width="45%" alt="Screenshot 5"/>
  <img src="https://raw.githubusercontent.com/Diagoo1/TrafficMonitor/refs/heads/main/Screenshots/Img%20(6).png" width="45%" alt="Screenshot 6"/>
  <br/>
  <img src="https://raw.githubusercontent.com/Diagoo1/TrafficMonitor/refs/heads/main/Screenshots/Img%20(7).png" width="45%" alt="Screenshot 7"/>
  <img src="https://raw.githubusercontent.com/Diagoo1/TrafficMonitor/refs/heads/main/Screenshots/Img%20(8).png" width="45%" alt="Screenshot 8"/>
  <br/>
  <img src="https://raw.githubusercontent.com/Diagoo1/TrafficMonitor/refs/heads/main/Screenshots/Img%20(9).png" width="45%" alt="Screenshot 9"/>
  <img src="https://raw.githubusercontent.com/Diagoo1/TrafficMonitor/refs/heads/main/Screenshots/Img%20(10).png" width="45%" alt="Screenshot 10"/>
  <br/>
  <img src="https://raw.githubusercontent.com/Diagoo1/TrafficMonitor/refs/heads/main/Screenshots/Img%20(11).png" width="45%" alt="Screenshot 11"/>
  <img src="https://raw.githubusercontent.com/Diagoo1/TrafficMonitor/refs/heads/main/Screenshots/Img%20(12).png" width="45%" alt="Screenshot 12"/>
  <br/>
  <img src="https://raw.githubusercontent.com/Diagoo1/TrafficMonitor/refs/heads/main/Screenshots/Img%20(13).png" width="45%" alt="Screenshot 13"/>
  <img src="https://raw.githubusercontent.com/Diagoo1/TrafficMonitor/refs/heads/main/Screenshots/Img%20(14).png" width="45%" alt="Screenshot 14"/>
</div>


---

## 🚀 Quick Start

### 📥 Download

Download the latest release from the [**Releases page**](https://github.com/Diagoo1/TrafficMonitor/releases).

### 💻 System Requirements

| Requirement | Value |
|---|---|
| OS | Windows 10 / 11 (Windows 7/8 supported) |
| Framework | .NET 8.0-windows |
| Architecture | x64 / x86 (AnyCPU) |
| RAM | ~20 MB |
| Disk | ~15 MB |

### 🛠️ Installation

```
1. Download TrafficMonitor.exe from Releases
2. Run the executable — no installation required (fully portable)
3. Settings are stored in: HKEY_CURRENT_USER\SOFTWARE\TrafficMonitor
4. Traffic history is saved to: %AppData%\TrafficMonitor\history.json
```

---

## 🏗️ Architecture Overview

### 🪟 Application Windows

| Window | Purpose |
|---|---|
| `MainWindow` | Desktop overlay: live upload/download/today stats, connection badge |
| `TaskbarWindow` | Embedded taskbar widget with auto theme detection |
| `SpeedTestWindow` | Full speed test with animated gauge, results, and connection ratings |
| `ProcessNetworkWindow` | Per-process network usage with real-time bars |
| `HistoryWindow` | Traffic history (list + calendar views) with month summary |
| `NetworkInfoWindow` | Detailed adapter information for all connections |
| `OptionsWindow` | Settings panel (Main Window, Taskbar, General tabs) |
| `AboutWindow` | Credits, links, animated heart |

### ⚙️ Core Services

| Service | Description |
|---|---|
| `NetworkService` | Polls `NetworkInterface` every N ms, calculates speeds, manages daily history flush |
| `HistoryService` | Persists daily traffic to `history.json` with atomic file writes |
| `RegistrySettingsService` | Loads/saves all settings to Windows Registry |
| `SpeedTestService` | Orchestrates full speed test via provider chain |
| `SpeedTestManager` | Selects best provider: Cloudflare → Multi-CDN → Local Fallback |
| `CloudflareProvider` | Primary speed test via `speed.cloudflare.com` with geo-server selection |
| `ProcessNetworkService` | Reads per-process I/O counters via Win32 + TCP/UDP table PIDs |
| `ToastManager` | Floating overlay notification system with animations and RTL support |

### 🎨 Theming System

| Component | Description |
|---|---|
| `ThemeManager` | Light/Dark/Auto mode, listens to Windows `WM_SETTINGCHANGE`, saves to registry |
| `App.xaml` | Dynamic resource dictionary with full light/dark color token sets |
| `GlobalFontFamily` | Per-language font switching (Satoshi, 29LT Bukra, Stem-Regular) |

---

## 🌍 Language Support

| Language | Code | RTL |
|---|---|---|
| English | `en` | ❌ |
| العربية (Arabic) | `ar` | ✅ |
| Español (Spanish) | `es` | ❌ |
| Français (French) | `fr` | ❌ |
| Русский (Russian) | `ru` | ❌ |

Language is stored in `HKEY_CURRENT_USER\SOFTWARE\TrafficMonitor\Language` and can be changed live from Options without restarting.

---

## 🔧 Registry Keys

All settings are stored under: `HKEY_CURRENT_USER\SOFTWARE\TrafficMonitor`

| Key | Type | Description |
|---|---|---|
| `Language` | String | `en`, `ar`, `es`, `fr`, `ru` |
| `ThemeMode` | Integer | `0` = Light, `1` = Dark, `2` = Auto |
| `Opacity` | Double | Window transparency (30–100) |
| `UpdateInterval` | Integer | Polling interval in milliseconds |
| `ShowTaskbarWindow` | Integer | `1` = show embedded taskbar widget |
| `AutoSelect` | Integer | `1` = auto-select best network adapter |
| `ShowTrayIcon` | Integer | `1` = show system tray icon |
| `TrafficTipEnable` | Integer | `1` = enable daily traffic limit alert |
| `TrafficTipValue` | Integer | Limit value (in MB or GB) |
| `StartWithWindows` | Integer | `1` = add to startup registry |

---

## 📁 Project Structure

```
TrafficMonitor/
│
├── App.xaml / App.xaml.cs           ← Application entry, tray, context menu, language engine
├── MainWindow.xaml / .cs            ← Desktop overlay window
├── TaskbarWindow.xaml / .cs         ← Taskbar-embedded widget (Win32 SetParent)
│
├── Views/
│   ├── SpeedTestWindow              ← Speed test with animated gauge
│   ├── ProcessNetworkWindow         ← Per-process traffic monitor
│   ├── HistoryWindow                ← Traffic history list + calendar
│   ├── NetworkInfoWindow            ← Network adapter details
│   ├── OptionsWindow                ← Full settings panel
│   └── AboutWindow                  ← About / credits
│
├── Services/
│   ├── NetworkService               ← Core traffic polling engine
│   ├── HistoryService               ← JSON-based history persistence
│   ├── RegistrySettingsService      ← Settings load/save (Registry)
│   ├── SpeedTestService             ← Speed test orchestrator
│   ├── SpeedTestManager             ← Provider selection
│   ├── CloudflareProvider           ← Cloudflare speed test + geo-server selector
│   ├── MultiCdnProvider             ← CDN fallback (OVH, Hetzner, etc.)
│   ├── LocalFallbackProvider        ← Offline ping-based estimation
│   ├── GeoServerSelector            ← Nearest server selection by GPS distance
│   ├── ProcessNetworkService        ← Win32 TCP/UDP table + I/O counters
│   └── ToastManager                 ← Animated overlay notifications
│
├── Models/
│   ├── NetworkData                  ← Live speed/today data model
│   ├── HistoryData                  ← Daily history entry
│   └── SettingsData                 ← All app settings (with Clone())
│
├── Themes/
│   └── ThemeManager                 ← Light/Dark/Auto engine
│
├── Lang/
│   ├── Lang-EN.xaml
│   ├── Lang-AR.xaml
│   ├── Lang-FR.xaml
│   ├── Lang-ES.xaml
│   └── Lang-RU.xaml
│
└── Assets/
    ├── Icons/
    ├── Avatars/
    └── Fonts/
```

---

## 🔬 Technical Highlights

- **Taskbar embedding** via `SetParent()` Win32 API — the widget is a real child window of `Shell_TrayWnd` with automatic DPI-aware positioning
- **Auto theme detection** by hooking `WM_SETTINGCHANGE` from a hidden message-only window
- **Named pipe IPC** for single-instance enforcement
- **Atomic history writes** using `.tmp` → rename to prevent data corruption
- **Per-process I/O tracking** using `GetExtendedTcpTable`, `GetExtendedUdpTable`, and `GetProcessIoCounters` Win32 APIs
- **Geo-aware speed test server selection** using Haversine distance calculation against 30+ global Cloudflare edge locations
- **RTL-aware toast notifications** with slide-in/out animations that flip direction for Arabic

---

## ☕ Support the Project

If Traffic Monitor has been useful to you, consider supporting continued development:

<div align="center">

[![PayPal](https://img.shields.io/badge/PayPal-Donate-00457C?style=for-the-badge&logo=paypal&logoColor=white)](https://paypal.me/Diagoo1)
[![Ko-fi](https://img.shields.io/badge/Ko--fi-Support-FF5E5B?style=for-the-badge&logo=ko-fi&logoColor=white)](https://ko-fi.com/Diagoo1)

</div>

---

## 📜 Credits

| Role | Developer |
|---|---|
| 🚀 Lead Developer & Designer | **Tarek Sadek** ([Diagoo1](https://github.com/Diagoo1)) |
| 📷 Icon and some ideas | **zhongyang219** ([zhongyang219](https://github.com/zhongyang219/TrafficMonitor)) |

---

## 📬 Contact

| Channel | Link |
|---|---|
| GitHub | [@Diagoo1](https://github.com/Diagoo1) |
| Email | tarek.sadek44@gmail.com |
| PayPal | [paypal.me/Diagoo1](https://paypal.me/Diagoo1) |

---

## ⚖️ License

```
MIT License

Copyright © 2026–2027 Tarek Sadek

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
```

Full license terms → [LICENSE](LICENSE)

---

<div align="center">

**⭐ If you find Traffic Monitor useful, please give it a star on GitHub!**

<br/>

Made with ❤️ by **Tarek Sadek**

</div>
