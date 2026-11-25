# VN TouchLink — how to use

Use your Android phone as a touchpad and drawing pad for your Windows PC, over a USB cable.

## Install (once)
1. **PC:** download `VNTouchLink-Setup-v1.exe` from
   [Releases](https://github.com/Vimmac-cell/vn-touchlink/releases/latest) and run it.
   If Windows says "Windows protected your PC", click **More info > Run anyway** (the installer isn't code-signed yet).
2. **Phone:** download `VNTouchLink-v1.apk` on the phone and open it to install
   (allow installing from your browser or file manager when Android asks).
3. **Phone:** turn on USB debugging:
   - Settings > About phone > tap **Build number** 7 times
     (Xiaomi: tap **OS version**; Samsung: Software information > Build number).
   - Go back to Settings > **Developer options** (Xiaomi: Additional settings > Developer options).
   - Turn on **USB debugging**.
4. Plug the phone into the PC with a data cable. On the phone, tick **Always allow from this computer** and tap **Allow**.

The PC app opens a **setup guide** the first time. It ticks each step as you finish it, and tells you if
Windows needs a USB driver for your phone. Open it again any time: double-click the tray icon.

## Every time
1. Plug the phone into the PC.
2. Start **VN TouchLink** on the PC (Start menu, or it starts with Windows if you chose that).
   A small blue phone icon appears in the tray (bottom-right, maybe under the **^** arrow).
3. Open **VN TouchLink** on the phone and hold it sideways. The dot turns green: "Connected to PC".

The first time, the PC sends a secret pairing code to the phone by itself.
Other apps on your phone can't control your PC without it.

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

### Xiaomi / Redmi / POCO phones
These phones use 3-finger swipes for screenshots and split screen. To stop that while VN TouchLink is open:
1. Phone: Developer options > turn on **USB debugging (Security settings)** (needs a Xiaomi account and a SIM card).
2. PC: right-click the tray icon > tick **Pause phone 3-finger gestures (Xiaomi)**.

The phone's own gestures come back as soon as you leave the app or unplug.
**Security note:** "USB debugging (Security settings)" lets any computer you have allowed change phone
settings over USB. Only plug into PCs you trust, and turn it off if you don't use this option.

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

The PC sees a real Windows pen with pressure (Windows Ink). A phone screen can't feel force, so pressure
comes from how much of your finger touches the glass. It gets better after your first few strokes.

Drawing apps:
- **Paint, OneNote, Whiteboard, Photoshop**: work as is.
- **Krita**: Settings > Configure Krita > Tablet settings > choose **Windows 8+ Pointer Input (Windows Ink)**, restart Krita.
- **Photoshop zoom**: set `"ArtZoomModifier": "Alt"` in settings.
- **Clip Studio**: File > Preferences > Tablet > use **TabletPC (Windows Ink)**.
- **Paint redo**: set `"art_redo": "Ctrl+Y"` and `"art_tap3": "Ctrl+Y"` in settings.

## Clipboard (copied text)
- **PC to phone:** copy on the PC. If VN TouchLink is open, the phone gets it straight away;
  otherwise it arrives the next time you open the app.
- **Phone to PC:** copy on the phone, then open VN TouchLink. Android only lets an app read the
  clipboard while it is on screen, so the text goes to the PC when you open the app.
- Text only (no pictures), up to 1 MB.

## Settings
Right-click the tray icon > **Edit settings**. This opens `%APPDATA%\PhoneLink\config.json` in Notepad.
Change a value, save, then tray icon > **Reload settings**.

| Setting | Default | What it does |
|---|---|---|
| `PointerSpeed` | 1.0 | Bigger = faster mouse |
| `ScrollSpeed` | 1.0 | Bigger = faster scrolling |
| `Gestures` | | Shortcut for each gesture, e.g. `"tap3": "Win+S"` |
| `ShareClipboard` | true | Share copied text |
| `PenMinPressure` | 0.08 | Raise if light touches draw nothing |
| `PenPressureCurve` | 1.0 | Lower (0.6) = thick lines come easier; higher (1.5) = need a flatter finger |
| `ArtPanMode` | "space" | `"space"` (Space + drag) or `"middle"` (middle-mouse drag) |
| `ArtZoomModifier` | "Ctrl" | Key held with the wheel for pinch-zoom (`"Alt"` for Photoshop) |
| `BlockPhoneGestures` | false | Xiaomi 3-finger pause (same as the tray menu tick) |

## If it doesn't connect
- Double-click the tray icon to open the setup guide: it shows which step is missing.
- "No phone seen": unplug and plug in again, unlock the phone, check USB debugging is on, try another cable.
- "Tap Allow": look at the phone screen.
- Tray > **Open phone app** opens the app on the phone and pairs again.
- Shortcuts don't reach programs running "as administrator" (Windows blocks that).
  Right-click VN TouchLink in the Start menu > **Run as administrator** to fix that.

## Uninstall
Windows Settings > Apps > Installed apps > **VN TouchLink** > Uninstall. On the phone, uninstall the app as usual.
