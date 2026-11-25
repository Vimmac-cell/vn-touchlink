# VN TouchLink — how to use (milestone 1: touchpad + gestures)

## Every time
1. Plug the phone into the PC with USB.
2. On the PC, start `D:\APP DEV\Test Projects\PhoneLink\build\pc\VNTouchLink.exe`. A small blue phone icon appears in the tray (bottom-right, maybe under the ^ arrow).
3. On the phone, open **VN TouchLink**. The dot turns green and says "Connected to PC".

Hold the phone sideways (the touchpad is landscape).

While VN TouchLink is open, the PC turns off the phone's own 3-finger gestures (screenshot, partial screenshot,
split screen) so they don't fire by mistake. They come back as soon as you leave the app, close the PC app,
or (if you unplug while the app is open) the next time you plug the phone in.
This needs Developer options > **USB debugging (Security settings)** to stay ON. To stop this feature, set
`"BlockPhoneGestures": false` in the settings file.

The first time, the PC sends a secret pairing code to the phone by itself. Other phone apps can't control your PC without it.

## Touchpad gestures
| Fingers | Do this | PC does |
|---|---|---|
| 1 | Move | Move mouse (faster finger = goes further) |
| 1 | Tap | Left click |
| 1 | Double tap | Double click |
| 1 | Tap, then touch again and drag | Drag (select text, move windows) |
| 2 | Tap | Right click |
| 2 | Drag up/down/sideways | Scroll |
| 2 | Pinch / spread | Zoom out / in (Ctrl + wheel) |
| 3 | Swipe up | Task View (Win+Tab) |
| 3 | Swipe down | Show desktop (Win+D) |
| 3 | Swipe left/right and keep fingers down | App switcher: move through apps, lift to pick |
| 3 | Tap | Search (Win+S) |
| 4 | Swipe left/right | Next / previous virtual desktop |
| 4 | Swipe up/down | Task View / desktop |
| 4 | Tap | Quick settings (Win+A) |

## Art pad
Tap **Art** (top-left of the phone). Tap **Mouse** to go back to the touchpad.
The blue box on the phone = your whole main PC screen (same shape), so where you touch is where the pen lands.

| Do this | PC does |
|---|---|
| 1 finger | Draw. Fingertip = thin, finger pressed flat = thick |
| Quick tap | A dot |
| 2-finger tap / **Undo** button | Undo (Ctrl+Z) |
| 3-finger tap / **Redo** button | Redo (Ctrl+Shift+Z) |
| 2-finger drag | Move the canvas (Space + drag) |
| Pinch / spread | Zoom the canvas (Ctrl + wheel) |
| **Erase** button | Pen becomes an eraser (Krita, Photoshop, OneNote switch to eraser by themselves) |
| Palm or very big touch | Ignored |

The PC sees a real Windows pen with pressure (Windows Ink). Pressure is learned from your finger: it
gets better after the first few strokes. A phone screen can't feel force, so this uses how much of your
finger touches the glass.

Drawing apps:
- **Paint, OneNote, Whiteboard, Photoshop**: work as is.
- **Krita**: Settings > Configure Krita > Tablet settings > choose **Windows 8+ Pointer Input (Windows Ink)**, restart Krita.
- **Photoshop zoom**: set `"ArtZoomModifier": "Alt"` in settings.
- **Clip Studio**: File > Preferences > Tablet > use **TabletPC (Windows Ink)**.

Art pad settings (in the settings file):
- `PenMinPressure` (0.08): raise if light touches draw nothing.
- `PenPressureCurve` (1.0): lower (e.g. 0.6) = thick lines come easier; higher (1.5) = need a flatter finger.
- `ArtPanMode`: `"space"` or `"middle"` (middle-mouse drag).
- Gestures `art_tap2`, `art_tap3`, `art_undo`, `art_redo`: change the shortcuts (e.g. redo `Ctrl+Y` for Paint).

## Clipboard (copied text)
- **PC to phone** (tested, works): copy on the PC. If VN TouchLink is open, the phone gets it straight away;
  otherwise it arrives the next time you open the app. Then paste on the phone.
- **Phone to PC** (fixed, not re-tested yet): copy on the phone, then open VN TouchLink. Android only lets
  an app read the clipboard while it is on screen, so the text goes to the PC when you open the app.
- Text only for now (no pictures), up to 1 MB.
- Turn off: `"ShareClipboard": false` in settings.

## Settings
Right-click the tray icon > **Edit settings**. This opens `%APPDATA%\PhoneLink\config.json`:
- `PointerSpeed` (default 1.0) — bigger = faster mouse.
- `ScrollSpeed` (default 1.0).
- `Gestures` — change any shortcut, e.g. `"tap3": "Win+S"` to `"tap3": "Ctrl+C"`.
Save, then tray icon > **Reload settings**.

## If it doesn't connect
- Tray icon > right-click: the 2nd line shows the USB state.
- "no phone": unplug/replug, unlock the phone, accept "Allow USB debugging".
- Tray > **Open phone app** opens the app and re-pairs.
- Gestures don't work over apps run "as administrator" (Windows blocks that). Run VNTouchLink.exe as admin to fix that.

## Rebuild after code changes
PC app:
```powershell
cd "D:\APP DEV\Test Projects\PhoneLink\pc"; dotnet build -c Release -o "..\build\pc"
```
Phone app (then install by tapping "VN TouchLink.apk" in File Manager > Download, because Xiaomi blocks USB installs):
```powershell
$env:JAVA_HOME="C:\Program Files\Android\Android Studio\jbr"; cd "D:\APP DEV\Test Projects\PhoneLink\android"; & "$env:USERPROFILE\.gradle\wrapper\dists\gradle-9.8.0-bin\3m7h6ceboy5k31n8kzwzuxssm\gradle-9.8.0\bin\gradle.bat" assembleDebug
```
```powershell
& "$env:LOCALAPPDATA\Android\Sdk\platform-tools\adb.exe" push "D:\APP DEV\Test Projects\PhoneLink\android\app\build\outputs\apk\debug\app-debug.apk" "/sdcard/Download/VN TouchLink.apk"
```

## Sharing with other people
**Phone app** goes on the Play Store (package `com.vnk.phonelink`).
While not connected it shows setup steps plus a QR code / link to the PC app download:
`https://github.com/Vimmac-cell/vn-touchlink/releases/latest` (change it in `android\...\AppInfo.java`).
Code backup and releases: https://github.com/Vimmac-cell/vn-touchlink

**PC app** = one installer: `build\installer\VNTouchLink-Setup.exe` (66 MB).
It contains the app, .NET and its own adb, so users need nothing else. It adds a Start menu entry,
optional "start with Windows", an uninstaller, and a Play Store link at the end.
Upload it to GitHub > your repo > Releases > "Draft a new release" > attach the file.

Build the installer after changing the PC app:
```powershell
cd "D:\APP DEV\Test Projects\PhoneLink\pc"; dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o "..\build\publish"
```
```powershell
& "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe" "D:\APP DEV\Test Projects\PhoneLink\installer\VNTouchLink.iss"
```

**First run on a new PC:** the PC app opens a setup guide (tray icon > Setup guide, or double-click the
tray icon, to open it again). It checks each step live: phone app installed, USB debugging, "Allow"
tapped, app connected. If Windows sees the phone but has no driver, it says so and links to the driver page.

**Xiaomi 3-finger pause** is now OFF by default for new users (tray menu > "Pause phone 3-finger gestures
(Xiaomi)"). Your own PC keeps it ON.

Before publishing, still to do:
- The PC installer isn't code-signed, so Windows SmartScreen will warn users ("More info > Run anyway").
  A code-signing certificate, or the Microsoft Store, removes that.
- adb is Google's file from Android platform-tools. Many tools (e.g. scrcpy) ship it, but read the
  Android SDK licence terms before public release. `NOTICE.txt` sits next to it.
- The Play Store build must be a signed **release** build (not the debug build used for testing).

## Names
Shown name: **VN TouchLink**. Inside the code the old working name stays (folder `PhoneLink`, package
`com.vnk.phonelink`, settings in `%APPDATA%\PhoneLink`) so updates install over the old app and pairing keeps working.

## Code map
- `pc\` — Windows tray app (C#): `Server.cs` reads phone commands, `Input.cs` moves mouse / presses keys, `Adb.cs` USB tunnel + pairing, `Config.cs` settings.
- `android\app\src\main\java\com\vnk\phonelink\` — phone app (Java): `TouchpadView.java` gesture logic, `Link.java` USB connection.

## Next milestones
2 Art pad · 3 Clipboard · 4 Sinhala phonetic keyboard · 5 File share (see PLAN.md)
