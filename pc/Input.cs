using System.Runtime.InteropServices;

namespace PhoneLinkPC;

/// <summary>Sends mouse and keyboard input to Windows with SendInput.</summary>
static class Input
{
    [StructLayout(LayoutKind.Sequential)]
    struct MOUSEINPUT { public int dx, dy; public int mouseData; public uint dwFlags, time; public IntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Sequential)]
    struct KEYBDINPUT { public ushort wVk, wScan; public uint dwFlags, time; public IntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Explicit)]
    struct InputUnion { [FieldOffset(0)] public MOUSEINPUT mi; [FieldOffset(0)] public KEYBDINPUT ki; }
    [StructLayout(LayoutKind.Sequential)]
    struct INPUT { public uint type; public InputUnion u; }

    [DllImport("user32.dll", SetLastError = true)]
    static extern uint SendInput(uint n, INPUT[] inputs, int size);

    const uint INPUT_MOUSE = 0, INPUT_KEYBOARD = 1;
    const uint MOVE = 0x0001, LDOWN = 0x0002, LUP = 0x0004, RDOWN = 0x0008, RUP = 0x0010,
               MDOWN = 0x0020, MUP = 0x0040, WHEEL = 0x0800, HWHEEL = 0x1000;
    const uint KEYUP = 0x0002, EXTENDED = 0x0001;

    static double fracX, fracY, fracWheel, fracHWheel, fracZoom;

    static void Send(params INPUT[] inputs) => SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());

    static INPUT Mouse(uint flags, int dx = 0, int dy = 0, int data = 0) =>
        new() { type = INPUT_MOUSE, u = new InputUnion { mi = new MOUSEINPUT { dx = dx, dy = dy, mouseData = data, dwFlags = flags } } };

    static readonly HashSet<ushort> ExtendedKeys = new() { 0x25, 0x26, 0x27, 0x28, 0x2D, 0x2E, 0x24, 0x23, 0x21, 0x22, 0x5B, 0x5C, 0xA3, 0xA5 };

    static INPUT Key(ushort vk, bool up) =>
        new() { type = INPUT_KEYBOARD, u = new InputUnion { ki = new KEYBDINPUT { wVk = vk, dwFlags = (up ? KEYUP : 0) | (ExtendedKeys.Contains(vk) ? EXTENDED : 0) } } };

    public static void Move(double dx, double dy)
    {
        fracX += dx; fracY += dy;
        int ix = (int)fracX, iy = (int)fracY;
        fracX -= ix; fracY -= iy;
        if (ix != 0 || iy != 0) Send(Mouse(MOVE, ix, iy));
    }

    public static void Button(char b, bool down)
    {
        uint f = b switch
        {
            'R' => down ? RDOWN : RUP,
            'M' => down ? MDOWN : MUP,
            _ => down ? LDOWN : LUP,
        };
        Send(Mouse(f));
    }

    public static void Click(char b) { Button(b, true); Button(b, false); }

    /// <summary>Scroll. Units: 120 = one wheel notch. Positive dy = scroll up, positive dx = scroll right.</summary>
    public static void Scroll(double dy, double dx)
    {
        fracWheel += dy; fracHWheel += dx;
        int iy = (int)fracWheel, ix = (int)fracHWheel;
        fracWheel -= iy; fracHWheel -= ix;
        if (iy != 0) Send(Mouse(WHEEL, data: iy));
        if (ix != 0) Send(Mouse(HWHEEL, data: ix));
    }

    /// <summary>Zoom with modifier (default Ctrl) + wheel. Positive = zoom in.</summary>
    public static void Zoom(double delta, string modifier = "Ctrl")
    {
        fracZoom += delta;
        int notches = (int)(fracZoom / 120);
        if (notches == 0) return;
        fracZoom -= notches * 120;
        ushort mod = Vk(modifier);
        var list = new List<INPUT>();
        if (mod != 0) list.Add(Key(mod, false));
        for (int i = 0; i < Math.Abs(notches); i++) list.Add(Mouse(WHEEL, data: Math.Sign(notches) * 120));
        if (mod != 0) list.Add(Key(mod, true));
        Send(list.ToArray());
    }

    public static void KeyDown(ushort vk) => Send(Key(vk, false));
    public static void KeyUp(ushort vk) => Send(Key(vk, true));

    /// <summary>Press a combo like "Win+Tab" or "Ctrl+Shift+Z": hold modifiers, tap the last key, release.</summary>
    public static void Combo(string combo)
    {
        var keys = combo.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                        .Select(Vk).Where(v => v != 0).ToList();
        if (keys.Count == 0) return;
        var list = new List<INPUT>();
        foreach (var k in keys) list.Add(Key(k, false));
        for (int i = keys.Count - 1; i >= 0; i--) list.Add(Key(keys[i], true));
        Send(list.ToArray());
    }

    public static ushort Vk(string name)
    {
        string n = name.Trim().ToLowerInvariant();
        switch (n)
        {
            case "ctrl": case "control": return 0x11;
            case "shift": return 0x10;
            case "alt": return 0x12;
            case "win": case "windows": return 0x5B;
            case "tab": return 0x09;
            case "enter": case "return": return 0x0D;
            case "esc": case "escape": return 0x1B;
            case "space": return 0x20;
            case "backspace": return 0x08;
            case "delete": case "del": return 0x2E;
            case "left": return 0x25;
            case "up": return 0x26;
            case "right": return 0x27;
            case "down": return 0x28;
            case "home": return 0x24;
            case "end": return 0x23;
            case "pageup": return 0x21;
            case "pagedown": return 0x22;
            case "printscreen": return 0x2C;
            case "volumeup": return 0xAF;
            case "volumedown": return 0xAE;
            case "mute": return 0xAD;
            case "playpause": return 0xB3;
            case "next": return 0xB0;
            case "prev": return 0xB1;
        }
        if (n.Length == 1)
        {
            char c = char.ToUpperInvariant(n[0]);
            if (c is >= 'A' and <= 'Z' or >= '0' and <= '9') return c;
        }
        if (n.StartsWith('f') && int.TryParse(n[1..], out int f) && f is >= 1 and <= 24) return (ushort)(0x70 + f - 1);
        return 0;
    }
}
