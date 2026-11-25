using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace PhoneLinkPC;

/// <summary>
/// Listens on 127.0.0.1 only. The phone reaches it through "adb reverse" over USB.
/// Protocol: one text command per line (see PROTOCOL.md).
/// </summary>
class Server
{
    readonly Func<Config> cfg;
    public event Action<string>? Status;
    public event Action? BadToken;
    public event Action? Connected;
    public event Action? Disconnected;
    public bool IsConnected { get; private set; }
    /// <summary>Text copied on the phone (already decoded).</summary>
    public event Action<string>? ClipboardFromPhone;
    readonly object sendLock = new();
    NetworkStream? stream;
    volatile bool running = true;

    /// <summary>Send one line to the phone. Safe from any thread. Returns false if no phone.</summary>
    public bool Send(string line)
    {
        lock (sendLock)
        {
            if (stream == null) return false;
            try
            {
                var bytes = Encoding.UTF8.GetBytes(line + "\n");
                stream.Write(bytes, 0, bytes.Length);
                return true;
            }
            catch { return false; }
        }
    }
    TcpListener? listener;
    bool altHeld;

    public Server(Func<Config> cfg) => this.cfg = cfg;

    public void Start() => new Thread(Loop) { IsBackground = true, Name = "PhoneLinkServer" }.Start();

    public void Stop() { running = false; try { listener?.Stop(); } catch { } }

    void Loop()
    {
        while (running)
        {
            try
            {
                listener = new TcpListener(IPAddress.Loopback, cfg().Port);
                listener.Start();
                while (running)
                {
                    using var client = listener.AcceptTcpClient();
                    client.NoDelay = true;
                    Handle(client);
                }
            }
            catch (Exception ex) when (running)
            {
                Status?.Invoke("Server error: " + ex.Message);
                Thread.Sleep(2000);
            }
            finally { try { listener?.Stop(); } catch { } }
        }
    }

    void Handle(TcpClient client)
    {
        using var reader = new StreamReader(client.GetStream(), Encoding.UTF8);
        var first = reader.ReadLine();
        if (first == null || first != "H " + cfg().Token)
        {
            Status?.Invoke("Phone app connected but is not paired yet. Pairing...");
            BadToken?.Invoke();
            return;
        }
        Status?.Invoke("Phone connected");
        lock (sendLock) stream = client.GetStream();
        // Tell the phone the screen size so the art pad keeps the right shape
        var b = ArtArea();
        Send($"SCR {b.Width} {b.Height}");
        IsConnected = true;
        Connected?.Invoke();
        try
        {
            string? line;
            while (running && (line = reader.ReadLine()) != null) Dispatch(line);
        }
        catch (IOException) { }
        finally
        {
            ReleaseAlt();
            PanEnd();
            pen.Leave();
            lock (sendLock) stream = null;
            IsConnected = false;
            Status?.Invoke("Phone disconnected");
            Disconnected?.Invoke();
        }
    }

    static double D(string s) => double.Parse(s, CultureInfo.InvariantCulture);

    readonly Pen pen = new();
    bool panning;

    /// <summary>Screen area the art pad draws on (main monitor, real pixels).</summary>
    static Rectangle ArtArea() => Screen.PrimaryScreen?.Bounds ?? new Rectangle(0, 0, 1920, 1080);

    (int x, int y) ToScreen(string nx, string ny)
    {
        var a = ArtArea();
        int x = a.Left + (int)Math.Round(Math.Clamp(D(nx), 0, 1) * (a.Width - 1));
        int y = a.Top + (int)Math.Round(Math.Clamp(D(ny), 0, 1) * (a.Height - 1));
        return (x, y);
    }

    void Art(string[] p, Config c)
    {
        switch (p[0])
        {
            case "PD": { var (x, y) = ToScreen(p[1], p[2]); pen.Down(x, y, Curve(D(p[3]), c)); break; }
            case "PM": { var (x, y) = ToScreen(p[1], p[2]); pen.Move(x, y, Curve(D(p[3]), c)); break; }
            case "PU": { var (x, y) = ToScreen(p[1], p[2]); pen.Up(x, y); pen.Hover(x, y); break; }
            case "PL": pen.Leave(); break;
            case "PE": pen.SetEraser(p[1] == "1"); break;
            case "PAN":
                if (p[1] == "s") PanStart();
                else if (p[1] == "e") PanEnd();
                else if (panning) Input.Move(D(p[2]), D(p[3]));
                break;
            case "ZA": Input.Zoom(D(p[1]), c.ArtZoomModifier); break;
        }
        if (pen.Error != null) Status?.Invoke(pen.Error);
    }

    static double Curve(double p, Config c) =>
        Math.Clamp(c.PenMinPressure + (1 - c.PenMinPressure) * Math.Pow(Math.Clamp(p, 0, 1), c.PenPressureCurve), 0, 1);

    void PanStart()
    {
        if (panning) return;
        pen.Leave();
        panning = true;
        if (cfg().ArtPanMode == "middle") Input.Button('M', true);
        else { Input.KeyDown(0x20); Thread.Sleep(15); Input.Button('L', true); }
    }

    void PanEnd()
    {
        if (!panning) return;
        panning = false;
        if (cfg().ArtPanMode == "middle") Input.Button('M', false);
        else { Input.Button('L', false); Input.KeyUp(0x20); }
    }

    void Dispatch(string line)
    {
        var p = line.Split(' ');
        if (p.Length == 0) return;
        var c = cfg();
        try
        {
            switch (p[0])
            {
                case "M": Input.Move(D(p[1]) * c.PointerSpeed, D(p[2]) * c.PointerSpeed); break;
                case "D": Input.Button(p[1][0], true); break;
                case "U": Input.Button(p[1][0], false); break;
                case "C": Input.Click(p[1][0]); break;
                case "W": Input.Scroll(D(p[1]) * c.ScrollSpeed, D(p[2]) * c.ScrollSpeed); break;
                case "Z": Input.Zoom(D(p[1])); break;
                case "G":
                    if (c.Gestures.TryGetValue(p[1], out var combo)) Input.Combo(combo);
                    break;
                case "K": Input.Combo(string.Join(' ', p.Skip(1))); break;
                case "A":
                    // Live app switcher: hold Alt, Tab/Shift+Tab per step, release on end
                    switch (p[1])
                    {
                        case "start": if (!altHeld) { Input.KeyDown(0x12); altHeld = true; Input.KeyDown(0x09); Input.KeyUp(0x09); } break;
                        case "next": Input.KeyDown(0x09); Input.KeyUp(0x09); break;
                        case "prev": Input.KeyDown(0x10); Input.KeyDown(0x09); Input.KeyUp(0x09); Input.KeyUp(0x10); break;
                        case "end": ReleaseAlt(); break;
                    }
                    break;
                case "PD": case "PM": case "PU": case "PL": case "PE": case "PAN": case "ZA":
                    Art(p, c); break;
                case "CB":
                    try { File.AppendAllText(Path.Combine(Config.Folder, "log.txt"), $"{DateTime.Now:HH:mm:ss} CB line received, {line.Length} chars\n"); } catch { }
                    if (p.Length >= 2)
                        ClipboardFromPhone?.Invoke(Encoding.UTF8.GetString(Convert.FromBase64String(p[1])));
                    break;
                case "P": break; // ping
            }
        }
        catch (Exception) { /* ignore a bad line */ }
    }

    void ReleaseAlt()
    {
        if (altHeld) { Input.KeyUp(0x12); altHeld = false; }
    }
}
