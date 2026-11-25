using System.Runtime.InteropServices;
using System.Text;

namespace PhoneLinkPC;

/// <summary>
/// Shares copied text both ways.
/// PC -> phone: as soon as you copy on the PC (if the phone app is open), or when the app next opens.
/// Phone -> PC: when you copy inside the app, or when you open the app after copying elsewhere
/// (Android only lets an app read the clipboard while it is on screen).
/// "Only if changed since last sync" on both sides stops the two clipboards fighting.
/// Lives on the UI thread (the Windows clipboard needs it).
/// </summary>
sealed class ClipboardSync : NativeWindow, IDisposable
{
    const int WM_CLIPBOARDUPDATE = 0x031D;
    const int MaxBytes = 1_000_000;

    [DllImport("user32.dll", SetLastError = true)] static extern bool AddClipboardFormatListener(IntPtr hwnd);
    [DllImport("user32.dll", SetLastError = true)] static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

    readonly Server server;
    readonly Func<Config> cfg;
    readonly SynchronizationContext ui;
    string? lastSynced;
    public event Action<string>? Status;

    public ClipboardSync(Server server, Func<Config> cfg, SynchronizationContext ui)
    {
        this.server = server; this.cfg = cfg; this.ui = ui;
        CreateHandle(new CreateParams { Parent = new IntPtr(-3) }); // message-only window
        AddClipboardFormatListener(Handle);
        server.Connected += () => ui.Post(_ => PushPcClipboard(), null);
        server.ClipboardFromPhone += text => ui.Post(_ => SetFromPhone(text), null);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_CLIPBOARDUPDATE) PushPcClipboard();
        base.WndProc(ref m);
    }

    static string? ReadText()
    {
        for (int i = 0; i < 5; i++)
        {
            try { return Clipboard.ContainsText() ? Clipboard.GetText() : null; }
            catch (ExternalException) { Thread.Sleep(30); } // another app has the clipboard open
        }
        return null;
    }

    void PushPcClipboard()
    {
        if (!cfg().ShareClipboard || !server.IsConnected) return;
        var text = ReadText();
        if (string.IsNullOrEmpty(text) || text == lastSynced) return;
        var bytes = Encoding.UTF8.GetBytes(text);
        if (bytes.Length > MaxBytes) { Status?.Invoke("Copied text too big to send (over 1 MB)"); return; }
        if (server.Send("CB " + Convert.ToBase64String(bytes))) lastSynced = text;
    }

    static void Log(string s)
    {
        try { File.AppendAllText(Path.Combine(Config.Folder, "log.txt"), $"{DateTime.Now:HH:mm:ss} {s}\n"); } catch { }
    }

    void SetFromPhone(string text)
    {
        Log($"clip from phone: {text.Length} chars");
        if (!cfg().ShareClipboard) return;
        lastSynced = text; // set first so our own clipboard event doesn't send it back
        for (int i = 0; i < 5; i++)
        {
            try { Clipboard.SetText(text); Status?.Invoke("Got copied text from phone"); Log("clip set on PC"); return; }
            catch (ExternalException ex) { Log("clip set failed: " + ex.Message); Thread.Sleep(30); }
            catch (Exception ex) { Log("clip set error: " + ex.GetType().Name + " " + ex.Message); return; }
        }
    }

    public void Dispose()
    {
        RemoveClipboardFormatListener(Handle);
        DestroyHandle();
    }
}
