// =============================================================================
// VoltNative.dll - native bridge shaped like the recovered VoltLib:
// - no SendInput import
// - C# owns CPS/randomization and calls sendClick/sendRightClick
// - native side uses GetAsyncKeyState + SetWindowsHookExA + PostThreadMessageA
// =============================================================================
#include <windows.h>
#include <cstdlib>
#include <cmath>

#pragma comment(lib, "user32.lib")
#pragma comment(lib, "winmm.lib")

static const UINT WM_VOLT_MOUSE = WM_APP + 0x3A7;

static HWND    g_targetWindow = nullptr;
static DWORD   g_targetThreadId = 0;
static HHOOK   g_messageHook = nullptr;
static HMODULE g_selfModule = nullptr;
static bool    g_attached = false;

static volatile bool g_leftEnabled = false;
static volatile bool g_rightEnabled = false;
static volatile bool g_leftManualActive = false;
static volatile bool g_rightManualActive = false;

static volatile int g_currentSlot = 0;
static bool g_inGame = false;
static bool g_whitelist[9] = { false };

static WPARAM MakeMouseCommand(BYTE click, BYTE action) {
    return ((WPARAM)action << 8) | click;
}

static bool IsTargetUsable() {
    if (!g_targetWindow) return false;
    if (!IsWindow(g_targetWindow)) {
        g_targetWindow = nullptr;
        g_targetThreadId = 0;
        return false;
    }
    return true;
}

static LPARAM CursorLParam(HWND target) {
    POINT pt;
    GetCursorPos(&pt);
    ScreenToClient(target, &pt);
    return MAKELPARAM(pt.x, pt.y);
}

static void RewriteVoltMouseMessage(MSG* msg) {
    HWND target = (HWND)msg->lParam;
    BYTE click = (BYTE)(msg->wParam & 0xff);
    BYTE action = (BYTE)((msg->wParam >> 8) & 0xff);
    bool down = action == 0;

    msg->hwnd = target;
    msg->message = click == 0
        ? (down ? WM_LBUTTONDOWN : WM_LBUTTONUP)
        : (down ? WM_RBUTTONDOWN : WM_RBUTTONUP);
    msg->wParam = down ? (click == 0 ? MK_LBUTTON : MK_RBUTTON) : 0;
    msg->lParam = CursorLParam(target);
}

static LRESULT CALLBACK GetMessageHook(int code, WPARAM wParam, LPARAM lParam) {
    if (code >= 0 && lParam) {
        MSG* msg = (MSG*)lParam;
        if ((msg->message == WM_KEYDOWN || msg->message == WM_SYSKEYDOWN) && msg->wParam >= '1' && msg->wParam <= '9') {
            g_currentSlot = (int)(msg->wParam - '1');
        }
        if (msg->message == WM_MOUSEWHEEL) {
            const short delta = GET_WHEEL_DELTA_WPARAM(msg->wParam);
            if (delta > 0) {
                g_currentSlot = (g_currentSlot + 8) % 9;
            } else if (delta < 0) {
                g_currentSlot = (g_currentSlot + 1) % 9;
            }
        }
        if (msg->message == WM_VOLT_MOUSE) {
            RewriteVoltMouseMessage(msg);
        }
    }

    return CallNextHookEx(g_messageHook, code, wParam, lParam);
}

static bool PostMouseCommand(BYTE click, BYTE action) {
    if (!IsTargetUsable() || g_targetThreadId == 0) return false;
    return PostThreadMessageA(
        g_targetThreadId,
        WM_VOLT_MOUSE,
        MakeMouseCommand(click, action),
        (LPARAM)g_targetWindow) != FALSE;
}

static void PreciseSleep(int targetMs) {
    if (targetMs <= 0) return;

    LARGE_INTEGER freq;
    LARGE_INTEGER start;
    LARGE_INTEGER now;
    QueryPerformanceFrequency(&freq);
    QueryPerformanceCounter(&start);

    const LONGLONG targetTicks = (freq.QuadPart * targetMs) / 1000;
    for (;;) {
        QueryPerformanceCounter(&now);
        const LONGLONG elapsed = now.QuadPart - start.QuadPart;
        if (elapsed >= targetTicks) return;

        const double remainingMs = (double)(targetTicks - elapsed) * 1000.0 / (double)freq.QuadPart;
        if (remainingMs > 8.0) {
            Sleep(1);
        } else if (remainingMs > 2.0) {
            SwitchToThread();
        } else {
            YieldProcessor();
        }
    }
}

static void SendClickCommand(BYTE click, int holdMs) {
    if (!IsTargetUsable()) return;
    if (GetForegroundWindow() != g_targetWindow) return;

    PostMouseCommand(click, 0);
    PreciseSleep(holdMs);
    PostMouseCommand(click, 1);
}

extern "C" {

__declspec(dllexport) void __cdecl VN_Attach(HMODULE hModule, DWORD threadId) {
    if (g_attached && g_targetThreadId == threadId) return;

    if (g_messageHook) {
        UnhookWindowsHookEx(g_messageHook);
        g_messageHook = nullptr;
    }

    g_targetThreadId = threadId;
    g_attached = false;
    timeBeginPeriod(1);

    HMODULE module = g_selfModule ? g_selfModule : hModule;
    if (module && threadId != 0) {
        g_messageHook = SetWindowsHookExA(WH_GETMESSAGE, GetMessageHook, module, threadId);
        g_attached = g_messageHook != nullptr;
    }
}

__declspec(dllexport) void __cdecl VN_Detach() {
    if (g_messageHook) {
        UnhookWindowsHookEx(g_messageHook);
        g_messageHook = nullptr;
    }

    g_attached = false;
    g_leftManualActive = false;
    g_rightManualActive = false;
    timeEndPeriod(1);
}

__declspec(dllexport) void __cdecl VN_SetTargetWindow(HWND target) {
    g_targetWindow = target;
}

__declspec(dllexport) void __cdecl VN_SetLeftEnabled(bool enabled) {
    g_leftEnabled = enabled;
}

__declspec(dllexport) void __cdecl VN_SetRightEnabled(bool enabled) {
    g_rightEnabled = enabled;
}

__declspec(dllexport) void __cdecl VN_SetLeftCps(int cps) { }
__declspec(dllexport) void __cdecl VN_SetRightCps(int cps) { }

__declspec(dllexport) void __cdecl VN_SetLeftActive(bool active) {
    g_leftManualActive = active;
}

__declspec(dllexport) void __cdecl VN_SetRightActive(bool active) {
    g_rightManualActive = active;
}

__declspec(dllexport) void __cdecl VN_SendClick(int delayMs) {
    if (!g_leftEnabled) return;
    SendClickCommand(0, delayMs);
}

__declspec(dllexport) void __cdecl VN_SendRightClick(int delayMs) {
    if (!g_rightEnabled) return;
    SendClickCommand(1, delayMs);
}

__declspec(dllexport) void __cdecl VN_SendBreakBlockClick(bool firstMsg) {
    if (!g_leftEnabled) return;
    PostMouseCommand(0, firstMsg ? 0 : 1);
}

__declspec(dllexport) void __cdecl VN_OneClick(BYTE click, BYTE action) {
    PostMouseCommand(click, action);
}

__declspec(dllexport) BYTE __cdecl VN_IsClicking(BYTE click) {
    if (click == 0) {
        return ((GetAsyncKeyState(VK_LBUTTON) & 0x8000) || g_leftManualActive) ? 1 : 0;
    }

    return ((GetAsyncKeyState(VK_RBUTTON) & 0x8000) || g_rightManualActive) ? 1 : 0;
}

__declspec(dllexport) void __cdecl VN_SetInGameFlag(bool flag) { g_inGame = flag; }
__declspec(dllexport) bool __cdecl VN_GetInGame() { return g_inGame; }
__declspec(dllexport) int  __cdecl VN_GetCurrentSlot() { return g_currentSlot; }
__declspec(dllexport) void __cdecl VN_SetCurrentSlot(int i) { if (i >= 0 && i < 9) g_currentSlot = i; }
__declspec(dllexport) void __cdecl VN_SetSlotKeybind(int i, int k) { }
__declspec(dllexport) bool __cdecl VN_IsOnAWhitelistedSlot() { return (g_currentSlot >= 0 && g_currentSlot < 9) ? g_whitelist[g_currentSlot] : false; }
__declspec(dllexport) void __cdecl VN_SetWhitelist(int i, bool f) { if (i >= 0 && i < 9) g_whitelist[i] = f; }
__declspec(dllexport) bool __cdecl VN_GetWhitelist(int i) { return (i >= 0 && i < 9) ? g_whitelist[i] : false; }
__declspec(dllexport) void __cdecl VN_SelfDestruct() { VN_Detach(); }
__declspec(dllexport) double __cdecl VN_BoxMuller(double m, double s) {
    double u = ((double)rand() / RAND_MAX) * 2.0 - 1.0;
    double v = ((double)rand() / RAND_MAX) * 2.0 - 1.0;
    double r = u * u + v * v;
    if (r >= 1.0 || r == 0.0) return m;
    return m + s * u * sqrt(-2.0 * log(r) / r);
}

// Volt-shaped aliases. The recovered C# wrapper binds delegates by offsets
// with these semantic names rather than a clean VN_* import table.
__declspec(dllexport) bool __cdecl isOnAWhitelistedSlot() { return VN_IsOnAWhitelistedSlot(); }
__declspec(dllexport) void __cdecl setTargetWindow(HWND target) { VN_SetTargetWindow(target); }
__declspec(dllexport) int  __cdecl getCurrentSlot() { return VN_GetCurrentSlot(); }
__declspec(dllexport) void __cdecl dettach() { VN_Detach(); }
__declspec(dllexport) void __cdecl attach(HMODULE hModule, DWORD threadId) { VN_Attach(hModule, threadId); }
__declspec(dllexport) void __cdecl sendClick(int delayMs) { VN_SendClick(delayMs); }
__declspec(dllexport) void __cdecl setWhitelist(int index, bool flag) { VN_SetWhitelist(index, flag); }
__declspec(dllexport) bool __cdecl getInGame() { return VN_GetInGame(); }
__declspec(dllexport) bool __cdecl getWhitelist(int index) { return VN_GetWhitelist(index); }
__declspec(dllexport) void __cdecl setCurrentSlot(int index) { VN_SetCurrentSlot(index); }
__declspec(dllexport) void __cdecl setSlotKeybind(int index, int keycode) { VN_SetSlotKeybind(index, keycode); }
__declspec(dllexport) void __cdecl sendRightClick(int delayMs) { VN_SendRightClick(delayMs); }
__declspec(dllexport) void __cdecl setInGameFlag(bool flag) { VN_SetInGameFlag(flag); }
__declspec(dllexport) void __cdecl sendBreakBlockClick(bool firstMsg) { VN_SendBreakBlockClick(firstMsg); }
__declspec(dllexport) BYTE __cdecl isClicking(BYTE click) { return VN_IsClicking(click); }
__declspec(dllexport) void __cdecl oneClick(BYTE click, BYTE action) { VN_OneClick(click, action); }
__declspec(dllexport) void __cdecl selfDestruct() { VN_SelfDestruct(); }
__declspec(dllexport) double __cdecl boxMuller(double m, double s) { return VN_BoxMuller(m, s); }

} // extern "C"

BOOL APIENTRY DllMain(HMODULE hModule, DWORD reason, LPVOID) {
    if (reason == DLL_PROCESS_ATTACH) {
        DisableThreadLibraryCalls(hModule);
        g_selfModule = hModule;
    }
    if (reason == DLL_PROCESS_DETACH) {
        if (g_messageHook) {
            UnhookWindowsHookEx(g_messageHook);
            g_messageHook = nullptr;
        }
    }
    return TRUE;
}
