// RA3Mouse 1.0 - Red Alert 3 style mouse (and optional hotkeys) for
// Red Alert 2, Yuri's Revenge, Mental Omega and other YR mods, incl. CnCNet.
//
// RA2: left click selects AND gives orders, right click deselects.
// RA3: left click selects (empty ground = deselect), right click gives orders.
//
// This is an input translator only. It never touches game files or game memory,
// so it cannot desync online games and CnCNet sees an unmodified game.
//
// While a game window is active:
//   right click            -> left click (order). Held modifiers carry over:
//                             Ctrl = force fire, Alt (or your waypoint key) held = waypoint
//   right drag             -> right drag (scroll, unchanged)
//   left click             -> right + left click (deselect, then select what is under the cursor)
// Left click stays vanilla for Shift/Ctrl/Alt clicks, double clicks, the sidebar/top bar,
// while the waypoint key is held, and in cursor modes (sell, repair, power, beacon, and the
// first click after a sidebar click, e.g. placing a building or firing a superweapon).
// In cursor modes right click is a real right click, so it cancels the mode like RA3.
// Mode keys are read from the game's own Keyboard.ini / KeyboardMD.ini.
//
// Build: csc /target:winexe /platform:x64 /out:RA3Mouse.exe /r:System.Windows.Forms.dll /r:System.Drawing.dll RA3Mouse.cs

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

static class RA3Mouse
{
    const string Version = "1.0";

    // ======================= Win32 =======================
    const int WH_MOUSE_LL = 14, WH_KEYBOARD_LL = 13;
    const int WM_MOUSEMOVE = 0x200, WM_LBUTTONDOWN = 0x201, WM_RBUTTONDOWN = 0x204, WM_RBUTTONUP = 0x205;
    const int WM_KEYDOWN = 0x100, WM_KEYUP = 0x101, WM_SYSKEYDOWN = 0x104, WM_SYSKEYUP = 0x105;
    const uint MOUSEEVENTF_LEFTDOWN = 0x2, MOUSEEVENTF_LEFTUP = 0x4,
               MOUSEEVENTF_RIGHTDOWN = 0x8, MOUSEEVENTF_RIGHTUP = 0x10;
    const int VK_SHIFT = 0x10, VK_CONTROL = 0x11, VK_MENU = 0x12, VK_ESCAPE = 0x1B, VK_RETURN = 0x0D;

    delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)] struct POINT { public int x, y; }
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int left, top, right, bottom; }
    [StructLayout(LayoutKind.Sequential)]
    struct MSLLHOOKSTRUCT { public POINT pt; public uint mouseData, flags, time; public IntPtr extra; }
    [StructLayout(LayoutKind.Sequential)]
    struct KBDLLHOOKSTRUCT { public uint vkCode, scanCode, flags, time; public IntPtr extra; }
    [StructLayout(LayoutKind.Sequential)]
    struct MOUSEINPUT { public int dx, dy; public uint mouseData, dwFlags, time; public IntPtr extra; }
    [StructLayout(LayoutKind.Sequential)]
    struct INPUT { public uint type; public MOUSEINPUT mi; } // MOUSEINPUT is the largest union member

    [DllImport("user32.dll", SetLastError = true)] static extern IntPtr SetWindowsHookEx(int id, HookProc fn, IntPtr mod, uint thread);
    [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr h);
    [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr h, int n, IntPtr w, IntPtr l);
    [DllImport("kernel32.dll")] static extern IntPtr GetModuleHandle(string name);
    [DllImport("user32.dll")] static extern uint SendInput(uint n, INPUT[] inputs, int size);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] static extern bool GetClientRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] static extern bool ClientToScreen(IntPtr h, ref POINT p);
    [DllImport("user32.dll")] static extern short GetAsyncKeyState(int vk);
    [DllImport("user32.dll")] static extern uint GetDoubleClickTime();
    [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr h);
    [DllImport("kernel32.dll")] static extern IntPtr OpenProcess(uint access, bool inherit, uint pid);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    static extern bool QueryFullProcessImageName(IntPtr h, int flags, StringBuilder name, ref int size);
    [DllImport("advapi32.dll", SetLastError = true)] static extern bool OpenProcessToken(IntPtr p, uint access, out IntPtr token);
    [DllImport("advapi32.dll", SetLastError = true)]
    static extern bool GetTokenInformation(IntPtr t, int cls, out int info, int len, out int retLen);

    // ======================= settings =======================
    static readonly IntPtr Marker = (IntPtr)0x52413301; // tags clicks this program sends
    static string exeDir = Path.GetDirectoryName(Application.ExecutablePath);
    static string settingsPath, logPath;
    static int toggleVk = 0x7B;                // F12
    static bool leftDeselects = true;
    static int sidebarWidth = 168, topBarHeight = 16;
    const int DragThreshold = 6;               // screen pixels before a right press counts as a drag
    static readonly List<string> gameExes = new List<string> { "game.exe", "gamemd.exe", "gamemd-spawn.exe" };

    const string DefaultSettings =
@"; RA3Mouse settings. Restart RA3Mouse after editing.
[General]
; Key that turns RA3Mouse on/off (Windows virtual-key code). 123 = F12
ToggleKey=123
; yes = left click on empty ground deselects (RA3). no = left click keeps vanilla RA2 behaviour
LeftClickDeselects=yes
; Extra game executable names, comma separated (for mods with renamed exes)
ExtraGameExes=
; Size of the sidebar and top bar in game pixels
SidebarWidth=168
TopBarHeight=16
";

    static void LoadSettings()
    {
        settingsPath = Path.Combine(exeDir, "RA3Mouse.ini");
        try { if (!File.Exists(settingsPath)) File.WriteAllText(settingsPath, DefaultSettings); } catch { }
        var s = ReadIni(settingsPath, "General");
        int v; string t;
        if (s.TryGetValue("togglekey", out t) && int.TryParse(t, out v) && v > 0 && v < 256) toggleVk = v;
        if (s.TryGetValue("leftclickdeselects", out t)) leftDeselects = !Regex.IsMatch(t, "^(no|false|0)$", RegexOptions.IgnoreCase);
        if (s.TryGetValue("sidebarwidth", out t) && int.TryParse(t, out v) && v >= 0) sidebarWidth = v;
        if (s.TryGetValue("topbarheight", out t) && int.TryParse(t, out v) && v >= 0) topBarHeight = v;
        if (s.TryGetValue("extragameexes", out t))
            foreach (string e in t.Split(','))
            {
                string n = e.Trim().ToLowerInvariant();
                if (n.Length == 0) continue;
                if (!n.EndsWith(".exe")) n += ".exe";
                if (!gameExes.Contains(n)) gameExes.Add(n);
            }
    }

    // Reads one section of an INI file into a lower-case key dictionary.
    static Dictionary<string, string> ReadIni(string path, string section)
    {
        var d = new Dictionary<string, string>();
        if (!File.Exists(path)) return d;
        bool inSec = false;
        try
        {
            foreach (string raw in File.ReadAllLines(path))
            {
                string l = raw.Trim();
                if (l.StartsWith("[")) { inSec = string.Equals(l.Trim('[', ']'), section, StringComparison.OrdinalIgnoreCase); continue; }
                if (!inSec || l.StartsWith(";")) continue;
                int eq = l.IndexOf('=');
                if (eq > 0) d[l.Substring(0, eq).Trim().ToLowerInvariant()] = l.Substring(eq + 1).Trim();
            }
        }
        catch { }
        return d;
    }

    static void Log(string s) { if (logPath != null) try { File.AppendAllText(logPath, DateTime.Now.ToString("HH:mm:ss.fff ") + s + "\r\n"); } catch { } }

    // ======================= per-game info =======================
    struct Binding
    {
        public int Vk; public bool Shift, Ctrl, Alt;
        public static Binding From(string v, int fallback)
        {
            int n; if (v == null || !int.TryParse(v, out n)) n = fallback;
            return new Binding { Vk = n & 0xFF, Shift = (n & 0x100) != 0, Ctrl = (n & 0x200) != 0, Alt = (n & 0x400) != 0 };
        }
        public bool Matches(uint vk) { return Vk != 0 && vk == Vk && Shift == Held(VK_SHIFT) && Ctrl == Held(VK_CONTROL) && Alt == Held(VK_MENU); }
    }

    class GameInfo
    {
        public bool IsGame, Blocked, Warned;
        public int W, H; public bool Stretch;
        public Binding Sell, Repair, Power, Beacon, Planning;
    }
    static readonly Dictionary<uint, GameInfo> games = new Dictionary<uint, GameInfo>();

    static GameInfo ForegroundGame(out IntPtr hwnd)
    {
        hwnd = GetForegroundWindow();
        uint pid; GetWindowThreadProcessId(hwnd, out pid);
        GameInfo g;
        if (games.TryGetValue(pid, out g)) return g;
        g = new GameInfo();
        string path = ProcessPath(pid);
        string exe = path == null ? "" : Path.GetFileName(path).ToLowerInvariant();
        if (path != null && gameExes.Contains(exe))
        {
            string dir = Path.GetDirectoryName(path);
            bool known = exe != "game.exe" || Directory.GetFiles(dir, "*.mix").Length > 0 ||
                         path.IndexOf("Red Alert", StringComparison.OrdinalIgnoreCase) >= 0;
            if (known)
            {
                g.IsGame = true;
                LoadGameInfo(dir, exe, g);
                g.Blocked = !SelfElevated && IsElevated(pid);
                Log("game " + path + " res=" + g.W + "x" + g.H + " stretch=" + g.Stretch + " blocked=" + g.Blocked +
                    " planningVk=" + g.Planning.Vk + " sellVk=" + g.Sell.Vk);
            }
        }
        if (games.Count > 64) games.Clear();
        games[pid] = g;
        return g;
    }

    static void LoadGameInfo(string dir, string exe, GameInfo g)
    {
        bool ra2 = exe == "game.exe";
        // resolution
        foreach (string name in ra2 ? new[] { "RA2.INI" } : new[] { "RA2MO.ini", "RA2MD.INI" })
        {
            var v = ReadIni(Path.Combine(dir, name), "Video");
            string w, h; int wi, hi;
            if (v.TryGetValue("screenwidth", out w) && v.TryGetValue("screenheight", out h) &&
                int.TryParse(w, out wi) && int.TryParse(h, out hi)) { g.W = wi; g.H = hi; break; }
        }
        // cnc-ddraw scaling: maintas/boxing keep the aspect ratio, otherwise the picture is stretched
        string ddraw = Path.Combine(dir, "ddraw.ini");
        var dd = ReadIni(ddraw, "ddraw");
        if (dd.ContainsKey("maintas"))
        {
            foreach (var kv in ReadIni(ddraw, Path.GetFileNameWithoutExtension(exe))) dd[kv.Key] = kv.Value;
            string m, b;
            bool keepAspect = (dd.TryGetValue("maintas", out m) && IsTrue(m)) || (dd.TryGetValue("boxing", out b) && IsTrue(b));
            g.Stretch = !keepAspect;
        }
        // the player's own mode keys
        var k = ReadIni(Path.Combine(dir, ra2 ? "Keyboard.ini" : "KeyboardMD.ini"), "Hotkey");
        string t;
        g.Sell = Binding.From(k.TryGetValue("togglesell", out t) ? t : null, 76);       // L
        g.Repair = Binding.From(k.TryGetValue("togglerepair", out t) ? t : null, 75);   // K
        g.Power = Binding.From(k.TryGetValue("togglepower", out t) ? t : null, 80);     // P
        g.Beacon = Binding.From(k.TryGetValue("placebeacon", out t) ? t : null, 66);    // B
        g.Planning = Binding.From(k.TryGetValue("planningmode", out t) ? t : null, 90); // Z
    }

    static bool IsTrue(string s) { return Regex.IsMatch(s ?? "", "^(true|yes|1)$", RegexOptions.IgnoreCase); }

    static string ProcessPath(uint pid)
    {
        IntPtr h = OpenProcess(0x1000 /* QUERY_LIMITED_INFORMATION */, false, pid);
        if (h == IntPtr.Zero) return null;
        try { var sb = new StringBuilder(1024); int n = sb.Capacity; return QueryFullProcessImageName(h, 0, sb, ref n) ? sb.ToString() : null; }
        finally { CloseHandle(h); }
    }

    static readonly bool SelfElevated = new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);

    // Windows will not let a normal program send clicks to a game running as administrator.
    static bool IsElevated(uint pid)
    {
        IntPtr h = OpenProcess(0x1000, false, pid);
        if (h == IntPtr.Zero) return true;
        try
        {
            IntPtr tok;
            if (!OpenProcessToken(h, 0x0008 /* TOKEN_QUERY */, out tok)) return true;
            try { int elev, len; return GetTokenInformation(tok, 20 /* TokenElevation */, out elev, 4, out len) && elev != 0; }
            finally { CloseHandle(tok); }
        }
        finally { CloseHandle(h); }
    }

    // Is this screen point on the battlefield (not sidebar, top bar or black borders)?
    static bool OnBattlefield(IntPtr hwnd, GameInfo g, POINT pt)
    {
        RECT rc; if (!GetClientRect(hwnd, out rc)) return false;
        var origin = new POINT(); ClientToScreen(hwnd, ref origin);
        double cw = rc.right, ch = rc.bottom;
        if (cw <= 0 || ch <= 0) return false;
        double W = g.W > 0 ? g.W : cw, H = g.H > 0 ? g.H : ch;
        double sx = cw / W, sy = ch / H;
        if (!g.Stretch) sx = sy = Math.Min(sx, sy);
        double gx = (pt.x - origin.x - (cw - W * sx) / 2) / sx;
        double gy = (pt.y - origin.y - (ch - H * sy) / 2) / sy;
        return gx >= 0 && gy >= topBarHeight && gx < W - sidebarWidth && gy < H;
    }

    // ======================= state =======================
    static IntPtr mouseHook, keyHook;
    static HookProc mouseProc = MouseProc, keyProc = KeyProc; // kept alive for the hooks
    static bool enabled = true;
    static NotifyIcon tray;
    static SynchronizationContext ui;

    enum Mode { None, Sell, Repair, Power, Beacon }
    static Mode mode = Mode.None;
    static bool chatting;
    static int sidebarArmed;               // vanilla left clicks left after a sidebar click
    static bool rPending, rVanilla;        // right press swallowed / passed through
    static POINT rStart;
    static uint lastLeftTime; static POINT lastLeftPt;
    static readonly HashSet<uint> keysDown = new HashSet<uint>();

    static bool Held(int vk) { return (GetAsyncKeyState(vk) & 0x8000) != 0; }
    static bool PlanningHeld(GameInfo g) { return g.Planning.Vk != 0 && Held(g.Planning.Vk); }
    static void ResetState() { mode = Mode.None; chatting = false; sidebarArmed = 0; rPending = false; }

    // Clicks are sent from the message loop just after the hook returns, never from inside it.
    static void Send(params uint[] flags)
    {
        Log("send " + string.Join(",", Array.ConvertAll(flags, f => f.ToString("X"))));
        ui.Post(_ =>
        {
            var inputs = new INPUT[flags.Length];
            for (int i = 0; i < flags.Length; i++) { inputs[i].type = 0; inputs[i].mi.dwFlags = flags[i]; inputs[i].mi.extra = Marker; }
            if (SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT))) == 0) Log("SendInput failed " + Marshal.GetLastWin32Error());
        }, null);
    }

    // ======================= mouse =======================
    static IntPtr MouseProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode < 0) return CallNextHookEx(mouseHook, nCode, wParam, lParam);
        var m = (MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(MSLLHOOKSTRUCT));
        if (m.extra == Marker) return CallNextHookEx(mouseHook, nCode, wParam, lParam); // our own clicks
        int msg = (int)wParam;
        try
        {
            // a swallowed right press finishes the same way whatever happens to the window
            if (rPending)
            {
                if (msg == WM_MOUSEMOVE)
                {
                    if (Math.Abs(m.pt.x - rStart.x) > DragThreshold || Math.Abs(m.pt.y - rStart.y) > DragThreshold)
                    { rPending = false; rVanilla = true; Send(MOUSEEVENTF_RIGHTDOWN); } // it is a scroll drag
                }
                else if (msg == WM_RBUTTONUP)
                {
                    rPending = false; sidebarArmed = 0;
                    Send(MOUSEEVENTF_LEFTDOWN, MOUSEEVENTF_LEFTUP); // order (or waypoint while the waypoint key is held)
                    return (IntPtr)1;
                }
                return CallNextHookEx(mouseHook, nCode, wParam, lParam);
            }
            if (msg == WM_RBUTTONUP && rVanilla) { rVanilla = false; return CallNextHookEx(mouseHook, nCode, wParam, lParam); }
            if (!enabled || (msg != WM_LBUTTONDOWN && msg != WM_RBUTTONDOWN)) return CallNextHookEx(mouseHook, nCode, wParam, lParam);

            IntPtr hwnd; GameInfo g = ForegroundGame(out hwnd);
            if (!g.IsGame) return CallNextHookEx(mouseHook, nCode, wParam, lParam);
            if (g.Blocked) { WarnBlocked(g); return CallNextHookEx(mouseHook, nCode, wParam, lParam); }
            bool field = OnBattlefield(hwnd, g, m.pt);
            bool planning = PlanningHeld(g);
            Log((msg == WM_LBUTTONDOWN ? "L" : "R") + " at " + m.pt.x + "," + m.pt.y + " field=" + field +
                " mode=" + mode + " planning=" + planning + " sidebarArmed=" + sidebarArmed);

            if (msg == WM_RBUTTONDOWN)
            {
                if (!field) return CallNextHookEx(mouseHook, nCode, wParam, lParam); // sidebar right click = cancel/hold build
                if (mode != Mode.None)
                {
                    // real right click: cancels the cursor mode, as in RA3
                    mode = Mode.None; sidebarArmed = 0; rVanilla = true;
                    return CallNextHookEx(mouseHook, nCode, wParam, lParam);
                }
                rPending = true; rStart = m.pt;
                return (IntPtr)1;
            }

            // left button down
            bool dbl = m.time - lastLeftTime <= GetDoubleClickTime() &&
                       Math.Abs(m.pt.x - lastLeftPt.x) <= 4 && Math.Abs(m.pt.y - lastLeftPt.y) <= 4;
            lastLeftTime = m.time; lastLeftPt = m.pt;
            if (!field)
            {
                sidebarArmed = 1; // next battlefield click may be placing a building / firing a superweapon
                return CallNextHookEx(mouseHook, nCode, wParam, lParam);
            }
            bool vanilla = !leftDeselects || dbl || planning || mode != Mode.None || sidebarArmed > 0 ||
                           Held(VK_SHIFT) || Held(VK_CONTROL) || Held(VK_MENU);
            if (vanilla)
            {
                if (mode == Mode.Beacon) mode = Mode.None;
                if (sidebarArmed > 0) sidebarArmed--;
                return CallNextHookEx(mouseHook, nCode, wParam, lParam);
            }
            Send(MOUSEEVENTF_RIGHTDOWN, MOUSEEVENTF_RIGHTUP, MOUSEEVENTF_LEFTDOWN); // deselect, then select
            return (IntPtr)1; // the real left-up passes through and completes the click
        }
        catch (Exception e)
        {
            Log("mouse error " + e.Message);
            return CallNextHookEx(mouseHook, nCode, wParam, lParam);
        }
    }

    static void WarnBlocked(GameInfo g)
    {
        if (g.Warned) return;
        g.Warned = true;
        ui.Post(_ => tray.ShowBalloonTip(8000, "RA3Mouse",
            "The game runs as administrator, so RA3Mouse can't send clicks to it. Right-click the RA3Mouse tray icon and choose \"Restart as administrator\".",
            ToolTipIcon.Warning), null);
    }

    // ======================= keyboard: follow the game's cursor modes =======================
    static IntPtr KeyProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode < 0) return CallNextHookEx(keyHook, nCode, wParam, lParam);
        var k = (KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(KBDLLHOOKSTRUCT));
        int msg = (int)wParam;
        try
        {
            if (msg == WM_KEYUP || msg == WM_SYSKEYUP) { keysDown.Remove(k.vkCode); return CallNextHookEx(keyHook, nCode, wParam, lParam); }
            if (msg != WM_KEYDOWN && msg != WM_SYSKEYDOWN) return CallNextHookEx(keyHook, nCode, wParam, lParam);
            bool repeat = !keysDown.Add(k.vkCode);

            IntPtr hwnd; GameInfo g = ForegroundGame(out hwnd);
            if (!g.IsGame) return CallNextHookEx(keyHook, nCode, wParam, lParam);

            if (k.vkCode == toggleVk)
            {
                if (!repeat) ui.Post(_ => SetEnabled(!enabled), null);
                return (IntPtr)1;
            }
            if (repeat) return CallNextHookEx(keyHook, nCode, wParam, lParam);

            if (k.vkCode == VK_ESCAPE) ResetState();
            else if (k.vkCode == VK_RETURN) chatting = !chatting;   // Enter opens / sends chat
            else if (!chatting)
            {
                Mode before = mode;
                if (g.Sell.Matches(k.vkCode)) mode = mode == Mode.Sell ? Mode.None : Mode.Sell;
                else if (g.Repair.Matches(k.vkCode)) mode = mode == Mode.Repair ? Mode.None : Mode.Repair;
                else if (g.Power.Matches(k.vkCode)) mode = mode == Mode.Power ? Mode.None : Mode.Power;
                else if (g.Beacon.Matches(k.vkCode)) mode = Mode.Beacon;
                if (mode != before) Log("mode " + mode);
            }
        }
        catch (Exception e) { Log("key error " + e.Message); }
        return CallNextHookEx(keyHook, nCode, wParam, lParam);
    }

    // ======================= RA3 hotkey layout =======================
    // Key value = Windows virtual-key code + Shift 256 / Ctrl 512 / Alt 1024.
    static Dictionary<string, int> Ra3Layout(bool phobos)
    {
        const int S = 256, C = 512, A = 1024;
        var d = new Dictionary<string, int>
        {
            { "CombatantSelect", 81 },    // Q  select all
            { "TypeSelect", 87 },         // W  select matching
            { "StructureTab", 69 },       // E
            { "DefenseTab", 82 },         // R
            { "InfantryTab", 84 },        // T
            { "UnitTab", 89 },            // Y
            { "GuardObject", 65 },        // A  (closest RA2 order to attack-move)
            { "StopObject", 83 },         // S
            { "DeployObject", 70 },       // F  unit ability
            { "Follow", 70 + A },         // Alt+F
            { "ScatterObject", 88 },      // X
            { "ToggleRepair", 67 },       // C
            { "ToggleSell", 90 },         // Z
            { "PlanningMode", 18 },       // hold Alt = waypoint mode, like RA3
            { "PlaceBeacon", 66 },        // B
            { "CenterBase", 72 },         // H
            { "CenterOnRadarEvent", 32 }, // Space
            { "TogglePower", 80 },        // P
            { "PreviousObject", 77 },     // M
            { "NextObject", 78 + S },     // Shift+N
            { "View1", 74 }, { "View2", 75 }, { "View3", 76 }, { "View4", 186 },                 // J K L ;
            { "SetView1", 74 + C }, { "SetView2", 75 + C }, { "SetView3", 76 + C }, { "SetView4", 186 + C },
            { "ScreenCapture", 107 },     // Numpad +
            { "CenterView", 12 },         // Numpad 5
            { "Options", 27 },            // Esc
            { "Delete", 46 },             // Del
            { "ToggleAlliance", 0 }, { "AllToCheer", 0 }, { "VeterancyNav", 0 }, { "HealthNav", 0 },
            { "PageUser", 85 },           // U
        };
        for (int i = 1; i <= 7; i++) d["Taunt_" + i] = 115 + i; // F5..F11
        for (int i = 1; i <= 10; i++)
        {
            int vk = i == 10 ? 48 : 48 + i;
            d["TeamSelect_" + i] = vk; d["TeamCreate_" + i] = vk + C; d["TeamAddSelect_" + i] = vk + S; d["TeamCenter_" + i] = vk + A;
        }
        if (phobos) d["Next Idle Harvester"] = 78; // N
        return d;
    }

    static string ApplyHotkeys(string dir)
    {
        var targets = new List<string>();
        if (File.Exists(Path.Combine(dir, "gamemd.exe")) || File.Exists(Path.Combine(dir, "gamemd-spawn.exe"))) targets.Add("KeyboardMD.ini");
        if (File.Exists(Path.Combine(dir, "game.exe")) && File.Exists(Path.Combine(dir, "ra2.mix"))) targets.Add("Keyboard.ini");
        if (targets.Count == 0) return "No Red Alert 2 / Yuri's Revenge game found in:\n" + dir;

        bool phobos = File.Exists(Path.Combine(dir, "Phobos.dll"));
        string backup = Path.Combine(dir, "RA3Mouse-backup", DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        var report = new StringBuilder();
        foreach (string file in targets)
        {
            string path = Path.Combine(dir, file);
            var layout = Ra3Layout(phobos && file == "KeyboardMD.ini");
            var lines = new List<string> { "[Hotkey]" };
            var written = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in layout) { lines.Add(kv.Key + "=" + kv.Value); written.Add(kv.Key); }
            if (File.Exists(path))
            {
                Directory.CreateDirectory(backup);
                File.Copy(path, Path.Combine(backup, file), true);
                bool inSec = false;
                foreach (string raw in File.ReadAllLines(path))
                {
                    string l = raw.Trim();
                    if (l.StartsWith("[")) { inSec = l.Equals("[Hotkey]", StringComparison.OrdinalIgnoreCase); continue; }
                    int eq = l.IndexOf('=');
                    if (!inSec || eq <= 0) continue;
                    string key = l.Substring(0, eq).Trim();
                    if (written.Add(key)) lines.Add(key + "=" + l.Substring(eq + 1).Trim()); // keep extra commands
                }
            }
            File.WriteAllText(path, string.Join("\r\n", lines) + "\r\n", Encoding.ASCII);
            report.AppendLine("Updated " + file + (phobos && file == "KeyboardMD.ini" ? " (with Phobos idle-harvester key)" : ""));
        }
        if (Directory.Exists(backup)) report.AppendLine("\nOld files backed up to:\n" + backup);
        games.Clear(); // re-read mode keys
        return report.ToString();
    }

    // ======================= UI =======================
    static void SetEnabled(bool on)
    {
        if (enabled == on) return;
        enabled = on;
        ResetState();
        tray.Text = "RA3Mouse " + Version + ": " + (on ? "on" : "off");
        tray.Icon = MakeIcon(on);
        ThreadPool.QueueUserWorkItem(_ => { try { Console.Beep(on ? 1200 : 500, 120); } catch { } });
    }

    static Icon MakeIcon(bool on)
    {
        using (var bmp = new Bitmap(32, 32))
        using (var gr = Graphics.FromImage(bmp))
        {
            gr.SmoothingMode = SmoothingMode.AntiAlias;
            gr.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            using (var b = new SolidBrush(on ? Color.FromArgb(200, 30, 30) : Color.Gray)) gr.FillEllipse(b, 1, 1, 30, 30);
            using (var f = new Font("Arial", 15, FontStyle.Bold))
            {
                var sz = gr.MeasureString("3", f);
                gr.DrawString("3", f, Brushes.White, (32 - sz.Width) / 2, (32 - sz.Height) / 2);
            }
            IntPtr h = bmp.GetHicon();
            var icon = (Icon)Icon.FromHandle(h).Clone();
            DestroyIcon(h);
            return icon;
        }
    }

    static void ApplyHotkeysDialog()
    {
        using (var dlg = new FolderBrowserDialog
        {
            Description = "Choose the game folder (the one with gamemd.exe or game.exe). Your current key files are backed up first.",
            ShowNewFolderButton = false
        })
        {
            if (dlg.ShowDialog() != DialogResult.OK) return;
            try { MessageBox.Show(ApplyHotkeys(dlg.SelectedPath), "RA3Mouse"); }
            catch (UnauthorizedAccessException) { MessageBox.Show("Windows blocked writing to that folder. Restart RA3Mouse as administrator and try again.", "RA3Mouse"); }
            catch (Exception e) { MessageBox.Show("Could not apply hotkeys: " + e.Message, "RA3Mouse"); }
        }
    }

    [STAThread]
    static void Main(string[] args)
    {
        // RA3Mouse.exe --apply-hotkeys "<game folder>" [--report "<file>"]
        if (args.Length >= 2 && args[0] == "--apply-hotkeys")
        {
            string result;
            try { result = ApplyHotkeys(args[1]); } catch (Exception e) { result = "Could not apply hotkeys: " + e.Message; }
            if (args.Length == 4 && args[2] == "--report") File.WriteAllText(args[3], result);
            else MessageBox.Show(result, "RA3Mouse");
            return;
        }
        if (args.Length == 2 && args[0] == "--log") logPath = args[1];

        bool first;
        using (var mutex = new Mutex(true, "RA3Mouse_single_instance", out first))
        {
            if (!first) { MessageBox.Show("RA3Mouse is already running (red \"3\" icon in the tray).", "RA3Mouse"); return; }
            SetProcessDPIAware();
            Application.EnableVisualStyles();
            SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
            ui = SynchronizationContext.Current;
            LoadSettings();

            IntPtr mod = GetModuleHandle(null);
            mouseHook = SetWindowsHookEx(WH_MOUSE_LL, mouseProc, mod, 0);
            keyHook = SetWindowsHookEx(WH_KEYBOARD_LL, keyProc, mod, 0);
            if (mouseHook == IntPtr.Zero || keyHook == IntPtr.Zero) { MessageBox.Show("Could not install the mouse hook.", "RA3Mouse"); return; }

            var menu = new ContextMenuStrip();
            var toggle = new ToolStripMenuItem("Enabled (" + (Keys)toggleVk + ")") { Checked = true };
            toggle.Click += (s, e) => SetEnabled(!enabled);
            menu.Items.Add(toggle);
            menu.Items.Add("Apply RA3 hotkeys to a game folder...", null, (s, e) => ApplyHotkeysDialog());
            if (!SelfElevated) menu.Items.Add("Restart as administrator", null, (s, e) =>
            {
                try
                {
                    mutex.ReleaseMutex();
                    Process.Start(new ProcessStartInfo(Application.ExecutablePath) { Verb = "runas", UseShellExecute = true });
                    Application.Exit();
                }
                catch { } // user said no to the UAC prompt
            });
            menu.Items.Add("Settings file", null, (s, e) => { try { Process.Start("notepad.exe", "\"" + settingsPath + "\""); } catch { } });
            string readme = Path.Combine(exeDir, "README.txt");
            if (File.Exists(readme)) menu.Items.Add("Help", null, (s, e) => { try { Process.Start("notepad.exe", "\"" + readme + "\""); } catch { } });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Exit", null, (s, e) => Application.Exit());
            menu.Opening += (s, e) => toggle.Checked = enabled;

            tray = new NotifyIcon { Icon = MakeIcon(true), Visible = true, ContextMenuStrip = menu, Text = "RA3Mouse " + Version + ": on" };
            Application.Run();

            tray.Visible = false;
            UnhookWindowsHookEx(mouseHook);
            UnhookWindowsHookEx(keyHook);
        }
    }
}
