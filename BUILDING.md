# Building VN TouchLink

All commands run from the project folder (the folder with this file). Open PowerShell there first.

## Tools
- **.NET 8 SDK** — `winget install Microsoft.DotNet.SDK.8`
- **Android Studio** (includes the JDK and Android SDK)
- **Inno Setup 6** (only for the installer) — `winget install JRSoftware.InnoSetup`

## PC app
```powershell
dotnet build pc -c Release -o build\pc
```
Run `build\pc\VNTouchLink.exe`. Self-test of the pen injection (opens its own window):
```powershell
build\pc\VNTouchLink.exe --pentest pentest.txt
```

## Phone app
Open the `android` folder in Android Studio and choose **Build > Build APK(s)**, or with Gradle 9.8:
```powershell
cd android; gradle assembleDebug; cd ..
```
The APK is `android\app\build\outputs\apk\debug\app-debug.apk`. Install it with:
```powershell
adb install -r android\app\build\outputs\apk\debug\app-debug.apk
```
Some phones (Xiaomi) block installs over USB. Then copy the APK to the phone and tap it there:
```powershell
adb push android\app\build\outputs\apk\debug\app-debug.apk "/sdcard/Download/VN TouchLink.apk"
```

## Windows installer
```powershell
dotnet publish pc -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o build\publish
```
```powershell
& "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe" installer\VNTouchLink.iss
```
Output: `build\installer\VNTouchLink-Setup.exe` (app + .NET + adb, about 67 MB).

## Release
Upload the installer and APK on GitHub: repo > **Releases** > **Draft a new release**.
The phone app's QR code points at `https://github.com/Vimmac-cell/vn-touchlink/releases/latest`
(change it in `android\app\src\main\java\com\vnk\phonelink\AppInfo.java`).

Before a public Play Store release:
- Build a signed **release** APK/AAB (the debug build is for testing only).
- The installer isn't code-signed, so Windows SmartScreen warns users. A code-signing certificate or the
  Microsoft Store removes that.
- `pc\adb\` is Google's adb from Android platform-tools (see `NOTICE.txt`). Check the Android SDK licence terms.

## Names
Shown name: **VN TouchLink**. Inside the code the working name stays (folder `PhoneLink`, package
`com.vnk.phonelink`, settings in `%APPDATA%\PhoneLink`) so updates install over the old app and pairing keeps working.

## Code map
- `pc\` — Windows tray app (C#): `Server.cs` reads phone commands, `Input.cs` mouse/keys, `Pen.cs` Windows Ink pen,
  `ClipboardSync.cs`, `Adb.cs` USB tunnel + pairing, `SetupForm.cs` setup guide, `Config.cs` settings.
- `android\app\src\main\java\com\vnk\phonelink\` — phone app (Java): `TouchpadView.java` gestures,
  `ArtPadView.java` drawing, `Link.java` USB connection, `MainActivity.java` screens + clipboard.
- `installer\VNTouchLink.iss` — Inno Setup script.

## Roadmap
Sinhala phonetic keyboard · file sharing (see PLAN.md)
