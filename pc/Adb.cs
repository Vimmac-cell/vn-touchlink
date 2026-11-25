using System.Diagnostics;

namespace PhoneLinkPC;

/// <summary>Finds adb.exe and keeps the USB tunnel (adb reverse) set up.</summary>
static class Adb
{
    public const string AppPackage = "com.vnk.phonelink";
    public const string AppActivity = "com.vnk.phonelink/.MainActivity";

    public static string? Path { get; } = Find();

    static string? Find()
    {
        // 1. Our own copy next to VNTouchLink.exe (what normal users have)
        var own = System.IO.Path.Combine(AppContext.BaseDirectory, "adb", "adb.exe");
        if (File.Exists(own)) return own;
        // 2. Android SDK / PATH (developers)
        var sdk = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Android\Sdk\platform-tools\adb.exe");
        if (File.Exists(sdk)) return sdk;
        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';'))
        {
            try { var p = System.IO.Path.Combine(dir.Trim(), "adb.exe"); if (File.Exists(p)) return p; } catch { }
        }
        return null;
    }

    public static string Run(string args, int timeoutMs = 8000)
    {
        if (Path == null) return "";
        try
        {
            var psi = new ProcessStartInfo(Path, args)
            {
                RedirectStandardOutput = true, RedirectStandardError = true,
                UseShellExecute = false, CreateNoWindow = true,
            };
            using var p = Process.Start(psi)!;
            var outTask = p.StandardOutput.ReadToEndAsync();
            var errTask = p.StandardError.ReadToEndAsync();
            if (!p.WaitForExit(timeoutMs)) { try { p.Kill(); } catch { } return ""; }
            return outTask.Result + errTask.Result;
        }
        catch { return ""; }
    }

    /// <summary>Returns the serial of the first authorized USB phone, or null.</summary>
    public static string? Device()
    {
        foreach (var line in Run("devices").Split('\n'))
        {
            var parts = line.Trim().Split('\t');
            if (parts.Length == 2 && parts[1] == "device" && !parts[0].Contains(':')) return parts[0];
        }
        return null;
    }

    public enum PhoneState { NoAdb, NoPhone, NeedsAllow, Offline, Ready }

    /// <summary>What the PC sees on USB right now (for the setup guide).</summary>
    public static (PhoneState state, string? serial) State()
    {
        if (Path == null) return (PhoneState.NoAdb, null);
        PhoneState best = PhoneState.NoPhone; string? serial = null;
        foreach (var line in Run("devices").Split('\n'))
        {
            var parts = line.Trim().Split('\t');
            if (parts.Length != 2 || parts[0].Contains(':')) continue;
            switch (parts[1])
            {
                case "device": return (PhoneState.Ready, parts[0]);
                case "unauthorized": best = PhoneState.NeedsAllow; serial = parts[0]; break;
                case "offline": if (best == PhoneState.NoPhone) { best = PhoneState.Offline; serial = parts[0]; } break;
            }
        }
        return (best, serial);
    }

    /// <summary>USB devices Windows found but has no driver for (often a phone needing its USB driver).</summary>
    public static List<string> DevicesMissingDriver()
    {
        var list = new List<string>();
        try
        {
            using var q = new System.Management.ManagementObjectSearcher(
                "SELECT Name, DeviceID FROM Win32_PnPEntity WHERE ConfigManagerErrorCode = 28");
            foreach (var o in q.Get())
            {
                var id = o["DeviceID"]?.ToString() ?? "";
                if (id.StartsWith("USB", StringComparison.OrdinalIgnoreCase))
                    list.Add(o["Name"]?.ToString() ?? id);
            }
        }
        catch { }
        return list;
    }

    public static bool EnsureReverse(string serial, int port)
    {
        var list = Run($"-s {serial} reverse --list");
        if (list.Contains($"tcp:{port} tcp:{port}")) return true;
        Run($"-s {serial} reverse tcp:{port} tcp:{port}");
        return Run($"-s {serial} reverse --list").Contains($"tcp:{port} tcp:{port}");
    }

    /// <summary>Gives the phone app the secret token by starting it through adb (only the USB-connected PC can do this).</summary>
    public static void SendToken(string serial, string token) =>
        Run($"-s {serial} shell am start -n {AppActivity} --es token {token}");
}
