using System.Diagnostics;

namespace PhoneLinkPC;

static class Program
{
    [STAThread]
    static void Main()
    {
        using var single = new Mutex(true, "PhoneLinkPC_single", out bool first);
        var args = Environment.GetCommandLineArgs();
        if (args.Length >= 3 && args[1] == "--pentest") { ApplicationConfiguration.Initialize(); PenTest.Run(args[2]); return; }
        if (!first) { MessageBox.Show("VN TouchLink is already running (see the tray icon).", "VN TouchLink"); return; }
        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApp());
    }
}

class TrayApp : ApplicationContext
{
    Config config = Config.Load();
    readonly NotifyIcon tray;
    readonly ToolStripMenuItem statusItem = new("Starting...") { Enabled = false };
    readonly ToolStripMenuItem usbItem = new("USB: checking...") { Enabled = false };
    readonly Server server;
    readonly System.Threading.Timer usbTimer;
    readonly SynchronizationContext ui;
    DateTime lastPair = DateTime.MinValue;
    readonly ClipboardSync clipboard;
    readonly Control invoker;

    public TrayApp()
    {
        // A hidden control makes WinForms install its UI-thread context, so work posted
        // from other threads (clipboard, status text) really runs on this UI thread.
        invoker = new Control();
        invoker.CreateControl();
        ui = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
        var menu = new ContextMenuStrip();
        menu.Items.Add(statusItem);
        menu.Items.Add(usbItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Setup guide", null, (_, _) => ShowSetup());
        menu.Items.Add("Open phone app", null, (_, _) => Task.Run(Pair));
        var gestureItem = new ToolStripMenuItem("Pause phone 3-finger gestures (Xiaomi)") { CheckOnClick = true };
        gestureItem.Checked = config.BlockPhoneGestures;
        gestureItem.ToolTipText = "Needs Developer options > \"USB debugging (Security settings)\" ON. Only use with your own PC.";
        gestureItem.CheckedChanged += (_, _) =>
        {
            config.BlockPhoneGestures = gestureItem.Checked;
            config.Save();
            if (!gestureItem.Checked) Task.Run(RestorePhoneGestures);
        };
        menu.Items.Add(gestureItem);
        menu.Items.Add("Edit settings", null, (_, _) => Process.Start("notepad.exe", Config.FilePath));
        menu.Items.Add("Reload settings", null, (_, _) => { config = Config.Load(); gestureItem.Checked = config.BlockPhoneGestures; SetStatus("Settings reloaded"); });
        menu.Items.Add("Get the phone app", null, (_, _) => Process.Start(new ProcessStartInfo(SetupForm.PlayStoreUrl) { UseShellExecute = true }));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitThread());

        tray = new NotifyIcon
        {
            Icon = MakeIcon(),
            Text = "VN TouchLink",
            ContextMenuStrip = menu,
            Visible = true,
        };
        tray.DoubleClick += (_, _) => ShowSetup();

        server = new Server(() => config);
        server.Status += SetStatus;
        server.BadToken += () => Task.Run(Pair);
        server.Connected += () => Task.Run(() =>
        {
            if (!config.SetupDone) { config.SetupDone = true; config.Save(); }
            var dev = Adb.Device();
            if (dev == null || !config.BlockPhoneGestures) return;
            var err = PhoneGestures.Disable(dev, config);
            SetStatus(err ?? "Phone connected (phone 3-finger gestures paused)");
        });
        server.Disconnected += () => Task.Run(RestorePhoneGestures);
        clipboard = new ClipboardSync(server, () => config, ui);
        clipboard.Status += SetStatus;
        server.Start();

        usbTimer = new System.Threading.Timer(_ => CheckUsb(), null, 0, 3000);
        if (!config.SetupDone) ui.Post(_ => ShowSetup(), null);
    }

    SetupForm? setup;

    void ShowSetup()
    {
        if (setup == null || setup.IsDisposed)
        {
            setup = new SetupForm(server, config, Pair);
            setup.Show();
        }
        setup.Activate();
    }

    void Pair()
    {
        if ((DateTime.Now - lastPair).TotalSeconds < 5) return;
        lastPair = DateTime.Now;
        var dev = Adb.Device();
        if (dev == null) { SetStatus("No phone on USB"); return; }
        Adb.EnsureReverse(dev, config.Port);
        Adb.SendToken(dev, config.Token);
    }

    void RestorePhoneGestures()
    {
        if (config.SavedPhoneGestures == null || server.IsConnected) return;
        var dev = Adb.Device();
        if (dev == null) return; // unplugged: restore next time it is plugged in
        var err = PhoneGestures.Restore(dev, config);
        if (err != null) SetStatus(err);
    }

    void CheckUsb()
    {
        RestorePhoneGestures();
        string text;
        if (Adb.Path == null) text = "USB: adb.exe not found";
        else
        {
            var dev = Adb.Device();
            if (dev == null) text = "USB: no phone (plug in, allow USB debugging)";
            else text = Adb.EnsureReverse(dev, config.Port) ? $"USB: phone {dev} linked" : $"USB: phone {dev}, tunnel failed";
        }
        ui.Post(_ => usbItem.Text = text, null);
    }

    void SetStatus(string s) => ui.Post(_ =>
    {
        statusItem.Text = s;
        var t = "VN TouchLink - " + s;
        tray.Text = t.Length > 63 ? t[..63] : t;
    }, null);

    static Icon MakeIcon()
    {
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using var body = new SolidBrush(Color.FromArgb(40, 120, 220));
            g.FillRectangle(body, 9, 2, 14, 28);
            using var screen = new SolidBrush(Color.FromArgb(20, 20, 30));
            g.FillRectangle(screen, 11, 5, 10, 20);
            using var dot = new SolidBrush(Color.White);
            g.FillEllipse(dot, 14, 13, 4, 4);
        }
        return Icon.FromHandle(bmp.GetHicon());
    }

    protected override void ExitThreadCore()
    {
        usbTimer.Dispose();
        clipboard.Dispose();
        server.Stop();
        try { RestorePhoneGestures(); } catch { }
        tray.Visible = false;
        tray.Dispose();
        base.ExitThreadCore();
    }
}
