# VN TouchLink (working name PhoneLink) — Redmi Note 12 + Windows PC over USB

Working name. Two programs talk over the USB cable:
- **Phone app** (Android, Kotlin, built in Android Studio): touchpad screen, art pad screen, Sinhala keyboard, clipboard, files.
- **PC app** (Windows tray app, C# .NET 8): receives the phone's input and acts on Windows.

## How the USB link works
`adb reverse tcp:7333 tcp:7333` makes the phone's `localhost:7333` reach the PC through the cable.
- No Wi-Fi, no internet, no account. Lag is about 5–15 ms.
- Needs plain **USB debugging** ON (already on). The Xiaomi "Security settings" switch is NOT needed.
- The PC app runs `adb reverse` by itself when the phone is plugged in.

## Your choices (2026-11-25)
- Phone screen: **blank touchpad** (no screen mirroring).
- Sinhala: **phonetic (Singlish)**, e.g. `amma` -> අම්මා.
- Art pad pressure: **faked from finger contact size**.
- First milestone: **touchpad + gestures**.

## Milestones
1. **Touchpad + gestures** (first)
   - 1 finger move = mouse move (with acceleration, adjustable speed)
   - 1-finger tap = left click, tap-and-drag = drag
   - 2-finger tap = right click, 2-finger drag = scroll (up/down/sideways), pinch = zoom (Ctrl+wheel)
   - 3-finger swipe up = Task View (Win+Tab), down = Desktop (Win+D), left/right = switch app (Alt+Tab)
   - 3-finger tap = Search (Win+S)
   - 4-finger swipe left/right = switch virtual desktop (Win+Ctrl+Left/Right), 4-finger tap = Action Center (Win+A)
   - All gestures editable in a settings file
   - Note: Windows' own "precision touchpad" needs a signed driver, so the PC app turns gestures into the same shortcuts Windows uses. Result feels the same.
2. **Art pad**
   - Phone sends pen events; PC app uses Windows pen injection (`InjectSyntheticPointerInput`, PT_PEN).
   - Works with Windows Ink apps: Krita, Photoshop (Windows Ink on), Paint, OneNote, Clip Studio.
   - Pressure from finger contact size; palm rejection; map phone area to whole screen or one window.
   - 2-finger tap = undo (Ctrl+Z), 3-finger tap = redo (Ctrl+Y), 2-finger pinch/drag = canvas zoom/pan.
3. **Clipboard sharing** (text first, then images)
   - Android 10+ only lets an app read the clipboard when it is in front OR is the active keyboard.
     The Sinhala keyboard (milestone 4) solves this: it can sync the clipboard in the background.
4. **Sinhala phonetic keyboard**
   - A phone keyboard (IME). Types into phone apps normally.
   - "PC mode" button: everything typed goes to the PC as Unicode. No Sinhala layout needed on Windows.
5. **File share**
   - Send files phone -> PC (Share menu "Send to PC") and PC -> phone (drag onto tray icon).
   - Saved in `Downloads\PhoneLink` on both sides.

## Tools needed on the PC
- Android Studio + JDK 17: already installed.
- Android SDK build-tools 36 / platform 37: already installed.
- **.NET 8 SDK: missing** (only the runtime is on the PC). Install with:
  `winget install Microsoft.DotNet.SDK.8`

## Safety
- The PC app accepts only connections that come through the USB tunnel (localhost).
- Nothing is sent to the internet.
