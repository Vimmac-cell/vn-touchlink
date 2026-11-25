using System.Security.Cryptography;
using System.Text.Json;

namespace PhoneLinkPC;

/// <summary>Settings saved in %APPDATA%\PhoneLink\config.json. Edit the file, then pick "Reload settings" in the tray menu.</summary>
class Config
{
    public int Port { get; set; } = 7333;
    /// <summary>Secret shared with the phone app so other phone apps can't control the PC.</summary>
    public string Token { get; set; } = "";
    public double PointerSpeed { get; set; } = 1.0;
    public double ScrollSpeed { get; set; } = 1.0;
    /// <summary>Share copied text between phone and PC.</summary>
    public bool ShareClipboard { get; set; } = true;
    /// <summary>Turn off the phone's own 3-finger gestures while PhoneLink is open.</summary>
    /// Off by default: it needs Xiaomi's "USB debugging (Security settings)" switch, which is risky to leave on.
    public bool BlockPhoneGestures { get; set; } = false;
    /// <summary>True after the phone has connected once; the setup guide then stops opening at start.</summary>
    public bool SetupDone { get; set; }
    /// <summary>The phone's original 3-finger gesture values while they are turned off (null = nothing to restore).</summary>
    public Dictionary<string, string>? SavedPhoneGestures { get; set; }

    /// <summary>Gesture name -> key combo. "AltTab" for left/right is handled live (hold Alt while you swipe).</summary>
    public Dictionary<string, string> Gestures { get; set; } = new()
    {
        ["swipe3_up"] = "Win+Tab",
        ["swipe3_down"] = "Win+D",
        ["tap3"] = "Win+S",
        ["swipe4_left"] = "Win+Ctrl+Right",
        ["swipe4_right"] = "Win+Ctrl+Left",
        ["swipe4_up"] = "Win+Tab",
        ["swipe4_down"] = "Win+D",
        ["tap4"] = "Win+A",
        ["art_tap2"] = "Ctrl+Z",
        ["art_tap3"] = "Ctrl+Shift+Z",
        ["art_undo"] = "Ctrl+Z",
        ["art_redo"] = "Ctrl+Shift+Z",
    };

    // ----- Art pad -----
    /// <summary>Lightest line (0..1). Raise it if light touches draw nothing.</summary>
    public double PenMinPressure { get; set; } = 0.08;
    /// <summary>1 = straight. Below 1 = thicker sooner, above 1 = needs a flatter finger for thick lines.</summary>
    public double PenPressureCurve { get; set; } = 1.0;
    /// <summary>2-finger drag on the art pad: "space" (Space + drag: Krita, Photoshop, Clip Studio) or "middle" (middle-mouse drag).</summary>
    public string ArtPanMode { get; set; } = "space";
    /// <summary>Key held with the wheel for pinch-zoom on the art pad: "Ctrl" (Krita, Paint) or "Alt" (Photoshop).</summary>
    public string ArtZoomModifier { get; set; } = "Ctrl";

    public static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PhoneLink");
    public static string FilePath => Path.Combine(Folder, "config.json");
    static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public static Config Load()
    {
        Config c;
        try { c = File.Exists(FilePath) ? JsonSerializer.Deserialize<Config>(File.ReadAllText(FilePath)) ?? new() : new(); }
        catch { c = new(); }
        // Add any gesture names that are new in this version
        foreach (var (k, v) in new Config().Gestures) c.Gestures.TryAdd(k, v);
        if (string.IsNullOrWhiteSpace(c.Token)) c.Token = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        c.Save();
        return c;
    }

    public void Save()
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, Json));
    }
}
