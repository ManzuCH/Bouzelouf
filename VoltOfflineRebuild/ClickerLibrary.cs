using System.Runtime.InteropServices;

namespace volt_design.Clicker.Library;

public sealed class ClickerLibrary
{
    private const string DLL = "VoltNative.dll";

    // Core
    [DllImport(DLL, EntryPoint = "attach", CallingConvention = CallingConvention.Cdecl)]
    private static extern void VN_Attach(nint hModule, uint threadId);
    [DllImport(DLL, EntryPoint = "dettach", CallingConvention = CallingConvention.Cdecl)]
    private static extern void VN_Detach();
    [DllImport(DLL, EntryPoint = "setTargetWindow", CallingConvention = CallingConvention.Cdecl)]
    private static extern void VN_SetTargetWindow(nint target);

    // Clicker control/config. The repeated CPS loop lives in managed Clicker,
    // matching the recovered delegate shape more closely.
    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
    private static extern void VN_SetLeftEnabled([MarshalAs(UnmanagedType.I1)] bool enabled);
    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
    private static extern void VN_SetRightEnabled([MarshalAs(UnmanagedType.I1)] bool enabled);
    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
    private static extern void VN_SetLeftCps(int cps);
    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
    private static extern void VN_SetRightCps(int cps);
    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
    private static extern void VN_SetLeftActive([MarshalAs(UnmanagedType.I1)] bool active);
    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
    private static extern void VN_SetRightActive([MarshalAs(UnmanagedType.I1)] bool active);

    // Legacy
    [DllImport(DLL, EntryPoint = "sendClick", CallingConvention = CallingConvention.Cdecl)]
    private static extern void VN_SendClick(int delayMs);
    [DllImport(DLL, EntryPoint = "sendRightClick", CallingConvention = CallingConvention.Cdecl)]
    private static extern void VN_SendRightClick(int delayMs);
    [DllImport(DLL, EntryPoint = "isClicking", CallingConvention = CallingConvention.Cdecl)]
    private static extern byte VN_IsClicking(byte click);
    [DllImport(DLL, EntryPoint = "setInGameFlag", CallingConvention = CallingConvention.Cdecl)]
    private static extern void VN_SetInGameFlag([MarshalAs(UnmanagedType.I1)] bool flag);
    [DllImport(DLL, EntryPoint = "getInGame", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool VN_GetInGame();
    [DllImport(DLL, EntryPoint = "getCurrentSlot", CallingConvention = CallingConvention.Cdecl)]
    private static extern int VN_GetCurrentSlot();
    [DllImport(DLL, EntryPoint = "selfDestruct", CallingConvention = CallingConvention.Cdecl)]
    private static extern void VN_SelfDestruct();
    [DllImport(DLL, EntryPoint = "sendBreakBlockClick", CallingConvention = CallingConvention.Cdecl)]
    private static extern void VN_SendBreakBlockClick([MarshalAs(UnmanagedType.I1)] bool firstMsg);
    [DllImport(DLL, EntryPoint = "oneClick", CallingConvention = CallingConvention.Cdecl)]
    private static extern void VN_OneClick(byte click, byte action);
    [DllImport(DLL, EntryPoint = "isOnAWhitelistedSlot", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool VN_IsOnAWhitelistedSlot();
    [DllImport(DLL, EntryPoint = "setCurrentSlot", CallingConvention = CallingConvention.Cdecl)]
    private static extern void VN_SetCurrentSlot(int index);
    [DllImport(DLL, EntryPoint = "setWhitelist", CallingConvention = CallingConvention.Cdecl)]
    private static extern void VN_SetWhitelist(int index, [MarshalAs(UnmanagedType.I1)] bool flag);

    // Win32 for window detection
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, nint lParam);
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int GetWindowText(nint hWnd, System.Text.StringBuilder lpString, int nMaxCount);
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int GetClassName(nint hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(nint hWnd);
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint hWnd, out uint processId);
    private delegate bool EnumWindowsProc(nint hWnd, nint lParam);

    private readonly List<string> _log = new();
    private bool _nativeLoaded;
    private ClickerLibrary() { }

    public IReadOnlyList<string> Log => _log;
    public bool IsAttached { get; private set; }
    public nint TargetWindow { get; private set; }
    public uint TargetThreadId { get; private set; }

    public static ClickerLibrary CreateLive()
    {
        var lib = new ClickerLibrary();
        try
        {
            VN_GetCurrentSlot(); // test load
            lib._nativeLoaded = true;
            lib._log.Add("[OK] VoltNative.dll loaded.");
        }
        catch (Exception ex)
        {
            lib._nativeLoaded = false;
            lib._log.Add($"[WARN] VoltNative.dll: {ex.Message}");
        }
        return lib;
    }

    public static ClickerLibrary CreateOfflineNoOp()
    {
        var lib = new ClickerLibrary();
        lib._log.Add("No-op mode.");
        return lib;
    }

    public nint FindGameWindow(string searchTerm = "Minecraft")
    {
        nint minecraft = nint.Zero;
        nint lwjgl = nint.Zero;
        nint az = nint.Zero;
        string foundLabel = "";

        EnumWindows((hWnd, _) =>
        {
            if (!IsWindowVisible(hWnd)) return true;

            var title = GetWindowTitle(hWnd);
            var className = GetWindowClass(hWnd);
            if (title.Length == 0) return true;

            if (minecraft == nint.Zero && title.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
            {
                minecraft = hWnd;
                foundLabel = $"{title} ({className})";
            }

            if (lwjgl == nint.Zero &&
                (className.Contains("LWJGL", StringComparison.OrdinalIgnoreCase) ||
                 className.Contains("GLFW", StringComparison.OrdinalIgnoreCase)))
            {
                lwjgl = hWnd;
                if (foundLabel.Length == 0) foundLabel = $"{title} ({className})";
            }

            if (az == nint.Zero && title.Contains("AZ", StringComparison.OrdinalIgnoreCase))
            {
                az = hWnd;
                if (foundLabel.Length == 0) foundLabel = $"{title} ({className})";
            }

            return minecraft == nint.Zero;
        }, nint.Zero);

        var found = minecraft != nint.Zero ? minecraft : (lwjgl != nint.Zero ? lwjgl : az);
        _log.Add(found != nint.Zero ? $"[OK] Window: 0x{found:X} {foundLabel}" : "[WARN] Window not found.");
        return found;
    }

    private static string GetWindowTitle(nint hWnd)
    {
        var sb = new System.Text.StringBuilder(256);
        GetWindowText(hWnd, sb, sb.Capacity);
        return sb.ToString();
    }

    private static string GetWindowClass(nint hWnd)
    {
        var sb = new System.Text.StringBuilder(128);
        GetClassName(hWnd, sb, sb.Capacity);
        return sb.ToString();
    }

    public void Attach(nint hModule, uint threadId)
    {
        if (!_nativeLoaded) { _log.Add("Attach blocked (no DLL)."); return; }
        if (TargetWindow == nint.Zero) TargetWindow = FindGameWindow();
        if (TargetWindow != nint.Zero)
        {
            VN_SetTargetWindow(TargetWindow);
            TargetThreadId = threadId != 0 ? threadId : GetWindowThreadProcessId(TargetWindow, out _);
        }
        else
        {
            TargetThreadId = threadId;
        }

        VN_Attach(hModule, TargetThreadId);
        IsAttached = true;
        _log.Add($"[OK] Attached! Target: 0x{TargetWindow:X}, thread: {TargetThreadId}");
    }

    public void Detach()
    {
        if (_nativeLoaded) VN_Detach();
        IsAttached = false;
        _log.Add("Detached.");
    }

    // NEW: Direct clicker control (threads are in the DLL)
    public void SetLeftEnabled(bool enabled)
    {
        if (_nativeLoaded) VN_SetLeftEnabled(enabled);
        _log.Add($"Left clicker: {(enabled ? "ON" : "OFF")}");
    }

    public void SetRightEnabled(bool enabled)
    {
        if (_nativeLoaded) VN_SetRightEnabled(enabled);
        _log.Add($"Right clicker: {(enabled ? "ON" : "OFF")}");
    }

    public void SetLeftActive(bool active)
    {
        if (_nativeLoaded) VN_SetLeftActive(active);
    }

    public void SetRightActive(bool active)
    {
        if (_nativeLoaded) VN_SetRightActive(active);
    }

    public void SetLeftCps(int cps)
    {
        if (_nativeLoaded) VN_SetLeftCps(cps);
    }

    public void SetRightCps(int cps)
    {
        if (_nativeLoaded) VN_SetRightCps(cps);
    }

    // Legacy API
    public void SetTargetWindow(nint t)
    {
        TargetWindow = t;
        TargetThreadId = t == nint.Zero ? 0 : GetWindowThreadProcessId(t, out _);
        if (_nativeLoaded)
        {
            VN_SetTargetWindow(t);
            VN_Attach(nint.Zero, TargetThreadId);
        }
        IsAttached = TargetThreadId != 0;
        _log.Add($"Target set: 0x{TargetWindow:X}, thread: {TargetThreadId}");
    }
    public void SendClick(int d) { if (_nativeLoaded) VN_SendClick(d); }
    public void SendRightClick(int d) { if (_nativeLoaded) VN_SendRightClick(d); }
    public int GetCurrentSlot() => _nativeLoaded ? VN_GetCurrentSlot() : 0;
    public bool GetInGame() => _nativeLoaded && VN_GetInGame();
    public void SetInGameFlag(bool f) { if (_nativeLoaded) VN_SetInGameFlag(f); }
    public byte IsClicking(byte c) => _nativeLoaded ? VN_IsClicking(c) : (byte)0;
    public void SelfDestruct() { if (_nativeLoaded) VN_SelfDestruct(); }
    public void ConfigureOffsets(volt_design.Api.Entities.LibOffsets o) { }
    public bool IsOnAWhitelistedSlot() => _nativeLoaded && VN_IsOnAWhitelistedSlot();
    public void SetCurrentSlot(int i) { if (_nativeLoaded) VN_SetCurrentSlot(i); }
    public void SetWhitelist(int i, bool flag) { if (_nativeLoaded) VN_SetWhitelist(i, flag); }
    public void SendBreakBlockClick(bool f) { if (_nativeLoaded) VN_SendBreakBlockClick(f); }
    public void OneClick(byte c, byte a) { if (_nativeLoaded) VN_OneClick(c, a); }
}
