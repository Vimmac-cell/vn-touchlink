using System.Runtime.InteropServices;

namespace PhoneLinkPC;

/// <summary>
/// Injects a real Windows pen (Windows Ink) with pressure, so drawing apps
/// (Krita, Photoshop, Paint, OneNote, Clip Studio...) see a drawing tablet.
/// Uses CreateSyntheticPointerDevice + InjectSyntheticPointerInput (Windows 10 1809+).
/// Must be used from one thread (the server thread).
/// </summary>
sealed class Pen
{
    const int PT_PEN = 3;
    const int POINTER_FEEDBACK_DEFAULT = 1;

    const uint F_NEW = 0x1, F_INRANGE = 0x2, F_INCONTACT = 0x4, F_PRIMARY = 0x2000,
               F_DOWN = 0x10000, F_UPDATE = 0x20000, F_UP = 0x40000;
    const uint PEN_FLAG_ERASER = 0x4, PEN_FLAG_INVERTED = 0x2;
    const uint PEN_MASK_PRESSURE = 0x1;

    [StructLayout(LayoutKind.Sequential)]
    struct POINT { public int x, y; }

    [StructLayout(LayoutKind.Sequential)]
    struct POINTER_INFO
    {
        public int pointerType;
        public uint pointerId;
        public uint frameId;
        public uint pointerFlags;
        public IntPtr sourceDevice;
        public IntPtr hwndTarget;
        public POINT ptPixelLocation;
        public POINT ptHimetricLocation;
        public POINT ptPixelLocationRaw;
        public POINT ptHimetricLocationRaw;
        public uint dwTime;
        public uint historyCount;
        public int InputData;
        public uint dwKeyStates;
        public ulong PerformanceCount;
        public int ButtonChangeType;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct POINTER_PEN_INFO
    {
        public POINTER_INFO pointerInfo;
        public uint penFlags;
        public uint penMask;
        public uint pressure;
        public uint rotation;
        public int tiltX;
        public int tiltY;
    }

    // POINTER_TYPE_INFO: type + union { POINTER_TOUCH_INFO (144 bytes), POINTER_PEN_INFO (120 bytes) }
    [StructLayout(LayoutKind.Explicit, Size = 152)]
    struct POINTER_TYPE_INFO
    {
        [FieldOffset(0)] public int type;
        [FieldOffset(8)] public POINTER_PEN_INFO penInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    static extern IntPtr CreateSyntheticPointerDevice(int pointerType, uint maxCount, int mode);

    [DllImport("user32.dll", SetLastError = true)]
    static extern bool InjectSyntheticPointerInput(IntPtr device, [In] POINTER_TYPE_INFO[] info, uint count);

    [DllImport("user32.dll")]
    static extern void DestroySyntheticPointerDevice(IntPtr device);

    IntPtr device;
    bool inContact, inRange, eraser;
    int lastX, lastY;
    public string? Error { get; private set; }

    public bool Ensure()
    {
        if (device != IntPtr.Zero) return true;
        device = CreateSyntheticPointerDevice(PT_PEN, 1, POINTER_FEEDBACK_DEFAULT);
        if (device == IntPtr.Zero) Error = "Windows refused to create a pen (error " + Marshal.GetLastWin32Error() + ")";
        return device != IntPtr.Zero;
    }

    public void SetEraser(bool on)
    {
        if (eraser == on) return;
        // Leave range so apps notice the switch between pen tip and eraser
        if (inContact) Up(lastX, lastY);
        if (inRange) Leave();
        eraser = on;
    }

    bool Inject(uint flags, int x, int y, double pressure)
    {
        if (!Ensure()) return false;
        var info = new POINTER_TYPE_INFO
        {
            type = PT_PEN,
            penInfo = new POINTER_PEN_INFO
            {
                pointerInfo = new POINTER_INFO
                {
                    pointerType = PT_PEN,
                    pointerId = 0,
                    pointerFlags = flags,
                    ptPixelLocation = new POINT { x = x, y = y },
                },
                penFlags = eraser ? PEN_FLAG_ERASER | PEN_FLAG_INVERTED : 0,
                penMask = PEN_MASK_PRESSURE,
                pressure = (uint)Math.Clamp(pressure * 1024, 0, 1024),
            },
        };
        bool ok = InjectSyntheticPointerInput(device, new[] { info }, 1);
        if (!ok) Error = "Pen input failed (error " + Marshal.GetLastWin32Error() + ")";
        lastX = x; lastY = y;
        return ok;
    }

    /// <summary>Pen hovering above the surface (not touching).</summary>
    public void Hover(int x, int y)
    {
        uint f = F_INRANGE | F_PRIMARY | (inRange ? F_UPDATE : F_NEW | F_UPDATE);
        if (Inject(f, x, y, 0)) inRange = true;
    }

    public void Down(int x, int y, double p)
    {
        if (inContact) { Move(x, y, p); return; }
        if (!inRange) Hover(x, y);
        if (Inject(F_INRANGE | F_INCONTACT | F_PRIMARY | F_DOWN, x, y, p)) inContact = true;
    }

    public void Move(int x, int y, double p)
    {
        if (!inContact) { Down(x, y, p); return; }
        Inject(F_INRANGE | F_INCONTACT | F_PRIMARY | F_UPDATE, x, y, p);
    }

    public void Up(int x, int y)
    {
        if (!inContact) return;
        Inject(F_INRANGE | F_PRIMARY | F_UP, x, y, 0);
        inContact = false;
    }

    /// <summary>Pen leaves range (lifted away from the tablet).</summary>
    public void Leave()
    {
        if (inContact) Up(lastX, lastY);
        if (!inRange) return;
        Inject(F_PRIMARY | F_UPDATE, lastX, lastY, 0);
        inRange = false;
    }

    public void Dispose()
    {
        Leave();
        if (device != IntPtr.Zero) { DestroySyntheticPointerDevice(device); device = IntPtr.Zero; }
    }
}
