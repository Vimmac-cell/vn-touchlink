# VN TouchLink

Use your Android phone as a **touchpad** and **drawing pad** for your Windows PC, over a USB cable.

- **Touchpad:** move, tap to click, 2-finger scroll / right-click / zoom, 3- and 4-finger Windows gestures.
- **Art pad:** draws as a real Windows pen (Windows Ink) with pressure from how flat your finger presses. Works in Krita, Photoshop, Paint, OneNote, Clip Studio. Undo/redo, eraser, canvas move and zoom.
- **Clipboard:** copy on the PC, paste on the phone (and back).

## Download
- **PC app:** `VNTouchLink-Setup.exe` from [Releases](https://github.com/Vimmac-cell/vn-touchlink/releases/latest).
- **Phone app:** the APK from Releases (Play Store coming later).

Then turn on USB debugging on the phone, plug in the cable and tap **Allow**. The PC app's setup guide walks you through it.

## Project layout
- `android/` — phone app (Java, no extra libraries except ZXing for the QR code)
- `pc/` — Windows tray app (C# .NET 8)
- `installer/` — Inno Setup script for `VNTouchLink-Setup.exe`
- `PLAN.md` — features and roadmap · `HOW-TO-USE.md` — usage, settings and build commands

`pc/adb/` contains Google's adb from Android SDK platform-tools (see `pc/adb/NOTICE.txt`).
