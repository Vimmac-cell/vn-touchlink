namespace PhoneLinkPC;

/// <summary>
/// Turns HyperOS 3-finger gestures (screenshot, split screen, partial screenshot) OFF while the
/// PhoneLink app is connected, and puts the phone's own values back when it disconnects.
/// The original values are saved in config.json, so they are restored even after a crash or unplug
/// (the next time the phone is plugged in without the app open).
/// Needs Developer options > "USB debugging (Security settings)" ON.
/// </summary>
static class PhoneGestures
{
    static readonly Dictionary<string, string> Off = new()
    {
        ["enable_three_gesture"] = "0",
        ["three_gesture_down"] = "none",
        ["three_gesture_long_press"] = "none",
        ["three_gesture_horizontal_ltr"] = "none",
        ["three_gesture_horizontal_rtl"] = "none",
    };

    static readonly object gate = new();

    /// <summary>Returns null on success, or an error message.</summary>
    public static string? Disable(string serial, Config cfg)
    {
        lock (gate)
        {
            if (cfg.SavedPhoneGestures == null)
            {
                var saved = new Dictionary<string, string>();
                foreach (var key in Off.Keys)
                    saved[key] = Adb.Run($"-s {serial} shell settings get system {key}").Trim();
                cfg.SavedPhoneGestures = saved;
                cfg.Save();
            }
            foreach (var (key, val) in Off)
            {
                var r = Adb.Run($"-s {serial} shell settings put system {key} {val}");
                if (r.Contains("SecurityException"))
                    return "Phone blocked it: turn on 'USB debugging (Security settings)'";
            }
            return null;
        }
    }

    public static string? Restore(string serial, Config cfg)
    {
        lock (gate)
        {
            if (cfg.SavedPhoneGestures == null) return null;
            foreach (var (key, val) in cfg.SavedPhoneGestures)
            {
                var r = val is "" or "null"
                    ? Adb.Run($"-s {serial} shell settings delete system {key}")
                    : Adb.Run($"-s {serial} shell settings put system {key} {val}");
                if (r.Contains("SecurityException"))
                    return "Phone blocked restoring 3-finger gestures: turn on 'USB debugging (Security settings)'";
            }
            cfg.SavedPhoneGestures = null;
            cfg.Save();
            return null;
        }
    }
}
