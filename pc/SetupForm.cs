using System.Diagnostics;

namespace PhoneLinkPC;

/// <summary>
/// First-run guide. Checks each step live (every 1.5 s) and ticks it when done:
/// PC ready -> USB debugging on -> phone allowed this PC -> phone app connected.
/// </summary>
sealed class SetupForm : Form
{
    public const string PlayStoreUrl = "https://play.google.com/store/apps/details?id=com.vnk.phonelink";
    const string DriverUrl = "https://developer.android.com/studio/run/oem-usb";

    readonly Server server;
    readonly Config config;
    readonly Action openPhoneApp;
    readonly Label[] marks = new Label[4];
    readonly Label[] notes = new Label[4];
    readonly System.Windows.Forms.Timer timer = new() { Interval = 1500 };
    bool checking;

    static readonly string[] Titles =
    {
        "1. Get the phone app",
        "2. Turn on USB debugging on the phone",
        "3. Plug in the USB cable and tap \"Allow\" on the phone",
        "4. Open VN TouchLink on the phone",
    };

    static readonly string[] Help =
    {
        "Install \"VN TouchLink\" from the Play Store on your Android phone.",
        "a) Settings > About phone > tap \"Build number\" 7 times (Xiaomi: tap \"OS version\"; Samsung: Software information > Build number).\n" +
        "b) Go back to Settings > Developer options (Xiaomi: Additional settings > Developer options).\n" +
        "c) Turn on \"USB debugging\".",
        "Use a data cable (some cables only charge). On the phone, tick \"Always allow from this computer\" and tap Allow.",
        "Open the app. The dot turns green when it is connected.",
    };

    public SetupForm(Server server, Config config, Action openPhoneApp)
    {
        this.server = server; this.config = config; this.openPhoneApp = openPhoneApp;
        Text = "VN TouchLink setup";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 10f);
        ClientSize = new Size(640, 560);
        BackColor = Color.White;

        var head = new Label
        {
            Text = "Use your phone as a touchpad and drawing pad",
            Font = new Font("Segoe UI Semibold", 14f),
            Location = new Point(20, 16), AutoSize = true,
        };
        Controls.Add(head);

        // Stack the steps; each text label sizes itself to its wrapped text
        int y = 60;
        var wrap = new Size(560, 0);
        for (int i = 0; i < 4; i++)
        {
            marks[i] = new Label { Location = new Point(20, y), Size = new Size(30, 28), Font = new Font("Segoe UI Symbol", 14f), Text = "○", ForeColor = Color.Gray };
            var title = new Label { Location = new Point(55, y + 2), AutoSize = true, Text = Titles[i], Font = new Font("Segoe UI Semibold", 10.5f) };
            Controls.AddRange(new Control[] { marks[i], title });
            y += title.PreferredHeight + 6;
            var help = new Label { Location = new Point(55, y), AutoSize = true, MaximumSize = wrap, Text = Help[i], ForeColor = Color.FromArgb(70, 70, 80) };
            Controls.Add(help);
            y += help.PreferredSize.Height + 4;
            notes[i] = new Label { Location = new Point(55, y), AutoSize = true, MaximumSize = wrap, MinimumSize = new Size(0, 22), ForeColor = Color.FromArgb(180, 90, 0) };
            Controls.Add(notes[i]);
            y += 36;
        }

        var playBtn = new Button { Text = "Get the phone app", Location = new Point(20, y + 4), Size = new Size(170, 34) };
        playBtn.Click += (_, _) => Open(PlayStoreUrl);
        var openBtn = new Button { Text = "Open app on my phone", Location = new Point(200, y + 4), Size = new Size(190, 34) };
        openBtn.Click += (_, _) => Task.Run(openPhoneApp);
        var closeBtn = new Button { Text = "Close", Location = new Point(530, y + 4), Size = new Size(90, 34) };
        closeBtn.Click += (_, _) => Close();
        Controls.AddRange(new Control[] { playBtn, openBtn, closeBtn });
        ClientSize = new Size(640, y + 54);

        timer.Tick += (_, _) => Refresh_();
        Shown += (_, _) => { Refresh_(); timer.Start(); };
        FormClosed += (_, _) => timer.Dispose();
    }

    static void Open(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
    }

    void Mark(int i, bool? done, string note = "")
    {
        marks[i].Text = done == true ? "✔" : done == false ? "●" : "○";
        marks[i].ForeColor = done == true ? Color.FromArgb(30, 150, 70) : done == false ? Color.FromArgb(210, 130, 0) : Color.Gray;
        notes[i].ForeColor = Color.FromArgb(180, 90, 0);
        notes[i].Text = note;
    }

    async void Refresh_()
    {
        if (checking || IsDisposed) return;
        checking = true;
        var (state, serial) = await Task.Run(Adb.State);
        var missing = state == Adb.PhoneState.NoPhone ? await Task.Run(Adb.DevicesMissingDriver) : new List<string>();
        bool appInstalled = state == Adb.PhoneState.Ready && serial != null &&
            (await Task.Run(() => Adb.Run($"-s {serial} shell pm list packages {Adb.AppPackage}"))).Contains("package:" + Adb.AppPackage);
        checking = false;
        if (IsDisposed) return;

        bool connected = server.IsConnected;
        if (connected) { Mark(0, true); Mark(1, true); Mark(2, true); Mark(3, true, ""); notes[3].ForeColor = Color.FromArgb(30, 150, 70); notes[3].Text = "All set! You can close this window."; return; }

        switch (state)
        {
            case Adb.PhoneState.NoAdb:
                Mark(0, null); Mark(1, null); Mark(3, null);
                Mark(2, false, "adb.exe is missing next to VNTouchLink.exe - please reinstall the PC app.");
                break;
            case Adb.PhoneState.NoPhone:
                Mark(0, null); Mark(1, null); Mark(3, null);
                Mark(2, false, missing.Count > 0
                    ? $"Windows sees \"{missing[0]}\" but has no driver for it. Install your phone's USB driver (click here)."
                    : "No phone seen yet. Plug in the cable, unlock the phone, and check USB debugging is on.");
                notes[2].Cursor = missing.Count > 0 ? Cursors.Hand : Cursors.Default;
                notes[2].Click -= DriverClick;
                if (missing.Count > 0) notes[2].Click += DriverClick;
                break;
            case Adb.PhoneState.NeedsAllow:
                Mark(0, null); Mark(1, true);
                Mark(2, false, "Look at the phone screen and tap \"Allow\".");
                Mark(3, null);
                break;
            case Adb.PhoneState.Offline:
                Mark(0, null); Mark(1, true);
                Mark(2, false, "Phone is connecting... if it stays like this, unplug and plug in again.");
                Mark(3, null);
                break;
            case Adb.PhoneState.Ready:
                Mark(1, true); Mark(2, true);
                if (appInstalled)
                {
                    Mark(0, true);
                    Mark(3, false, "Open VN TouchLink on the phone, or click \"Open app on my phone\".");
                }
                else
                {
                    Mark(0, false, "The phone app isn't installed yet - click \"Get the phone app\".");
                    Mark(3, null);
                }
                break;
        }
    }

    void DriverClick(object? s, EventArgs e) => Open(DriverUrl);
}
