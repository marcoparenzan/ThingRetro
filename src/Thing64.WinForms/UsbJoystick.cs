using System.Runtime.InteropServices;
using Thing64.C64;

namespace Thing64.WinForms;

public sealed record UsbJoystickInfo(int Id, string Name);

// Simulator: a USB joystick or gamepad moves a Joystick connected to a control port.
// It uses the Windows joystick API (winmm), which sees almost every HID device, Xbox controllers included.
// Directions from the main stick (with a dead zone) or from the directional pad; fire from buttons 1-4
public sealed class UsbJoystick : IDisposable
{
    const int PollMilliseconds = 10;
    const double DeadZone = 0.35; // fraction of half travel

    readonly int id;
    readonly Joystick joystick;
    readonly System.Windows.Forms.Timer timer = new() { Interval = PollMilliseconds };
    readonly AxisRange xRange, yRange;

    public UsbJoystick(UsbJoystickInfo device, Joystick joystick)
    {
        id = device.Id;
        Name = device.Name;
        this.joystick = joystick;
        (xRange, yRange) = ReadRanges(id);
        timer.Tick += (_, _) => Poll();
        timer.Start();
    }

    public string Name { get; }

    // Devices connected now (those that answer a read)
    public static IReadOnlyList<UsbJoystickInfo> Connected()
    {
        var devices = new List<UsbJoystickInfo>();
        try
        {
            int count = joyGetNumDevs();
            for (int i = 0; i < count; i++)
            {
                var info = NewInfo();
                if (joyGetPosEx(i, ref info) != 0) continue;
                var caps = new JOYCAPSW();
                string name = joyGetDevCapsW(i, ref caps, Marshal.SizeOf<JOYCAPSW>()) == 0 && !string.IsNullOrWhiteSpace(caps.szPname)
                    ? caps.szPname.Trim()
                    : $"Joystick {i + 1}";
                devices.Add(new UsbJoystickInfo(i, name));
            }
        }
        catch (DllNotFoundException) { }
        catch (EntryPointNotFoundException) { }
        return devices;
    }

    void Poll()
    {
        var info = NewInfo();
        if (joyGetPosEx(id, ref info) != 0)
        {
            joystick.ReleaseAll(); // disconnected
            return;
        }
        var state = Map(info.dwXpos, info.dwYpos, xRange, yRange, info.dwPOV, info.dwButtons);
        joystick.SetState(state.Up, state.Down, state.Left, state.Right, state.Fire);
    }

    internal readonly record struct AxisRange(int Min, int Max);

    internal readonly record struct State(bool Up, bool Down, bool Left, bool Right, bool Fire);

    // Raw reading -> switches: stick beyond the dead zone or directional pad (in hundredths of a degree,
    // 0 = up, 9000 = right, $FFFF = centered); fire is any of buttons 1-4
    internal static State Map(int x, int y, AxisRange xRange, AxisRange yRange, int pov, int buttons)
    {
        double ax = Normalize(x, xRange), ay = Normalize(y, yRange);
        bool left = ax < -DeadZone, right = ax > DeadZone, up = ay < -DeadZone, down = ay > DeadZone;

        if (pov is >= 0 and < 36000)
        {
            up |= pov is >= 31500 or <= 4500;
            right |= pov is >= 4500 and <= 13500;
            down |= pov is >= 13500 and <= 22500;
            left |= pov is >= 22500 and <= 31500;
        }

        return new State(up, down, left, right, (buttons & 0x0F) != 0);
    }

    static double Normalize(int value, AxisRange range)
    {
        double half = (range.Max - range.Min) / 2.0;
        return half <= 0 ? 0 : (value - range.Min - half) / half;
    }

    static (AxisRange X, AxisRange Y) ReadRanges(int id)
    {
        var caps = new JOYCAPSW();
        try
        {
            if (joyGetDevCapsW(id, ref caps, Marshal.SizeOf<JOYCAPSW>()) == 0 && caps.wXmax > caps.wXmin && caps.wYmax > caps.wYmin)
                return (new AxisRange((int)caps.wXmin, (int)caps.wXmax), new AxisRange((int)caps.wYmin, (int)caps.wYmax));
        }
        catch (DllNotFoundException) { }
        return (new AxisRange(0, 65535), new AxisRange(0, 65535));
    }

    public void Dispose()
    {
        timer.Dispose();
        joystick.ReleaseAll();
    }

    // winmm
    const int JOY_RETURNALL = 0xFF;

    static JOYINFOEX NewInfo() => new() { dwSize = Marshal.SizeOf<JOYINFOEX>(), dwFlags = JOY_RETURNALL };

    [StructLayout(LayoutKind.Sequential)]
    struct JOYINFOEX
    {
        public int dwSize, dwFlags, dwXpos, dwYpos, dwZpos, dwRpos, dwUpos, dwVpos, dwButtons, dwButtonNumber, dwPOV, dwReserved1, dwReserved2;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct JOYCAPSW
    {
        public ushort wMid, wPid;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string szPname;
        public uint wXmin, wXmax, wYmin, wYmax, wZmin, wZmax, wNumButtons, wPeriodMin, wPeriodMax;
        public uint wRmin, wRmax, wUmin, wUmax, wVmin, wVmax, wCaps, wMaxAxes, wNumAxes, wMaxButtons;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string szRegKey;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szOEMVxD;
    }

    [DllImport("winmm.dll")] static extern int joyGetNumDevs();
    [DllImport("winmm.dll")] static extern int joyGetPosEx(int uJoyID, ref JOYINFOEX pji);
    [DllImport("winmm.dll", CharSet = CharSet.Unicode)] static extern int joyGetDevCapsW(int uJoyID, ref JOYCAPSW pjc, int cbjc);
}
