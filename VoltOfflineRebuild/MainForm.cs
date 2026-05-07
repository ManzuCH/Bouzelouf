using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using volt_design.Clicker.Library;
using volt_design.Models;

namespace volt_design.Forms;

public sealed class MainForm : Form
{
    private enum Page
    {
        Left,
        Right,
        Misc,
        Settings,
        Log
    }

    private static readonly Color AppBg = Color.FromArgb(14, 15, 18);
    private static readonly Color NavBg = Color.FromArgb(8, 10, 13);
    private static readonly Color Surface = Color.FromArgb(24, 26, 31);
    private static readonly Color SurfaceAlt = Color.FromArgb(30, 33, 39);
    private static readonly Color SurfaceSoft = Color.FromArgb(19, 21, 26);
    private static readonly Color Border = Color.FromArgb(54, 58, 66);
    private static readonly Color BorderSoft = Color.FromArgb(38, 42, 50);
    private static readonly Color TextMain = Color.FromArgb(245, 247, 250);
    private static readonly Color TextMuted = Color.FromArgb(151, 159, 171);
    private static readonly Color Accent = Color.FromArgb(255, 198, 74);
    private static readonly Color AccentSoft = Color.FromArgb(55, 43, 22);
    private static readonly Color Good = Color.FromArgb(52, 199, 89);
    private static readonly Color Danger = Color.FromArgb(255, 95, 86);
    private const string BrandName = "Bouzelouf";

    private readonly ClickerLibrary _library = ClickerLibrary.CreateLive();
    private readonly UserConfig _config = UserConfig.CreateOfflineDefault();
    private readonly Panel _content = new() { Dock = DockStyle.Fill, BackColor = AppBg };
    private readonly Label _status = new();
    private readonly Label _pageTitle = new();
    private readonly Label _leftState = new();
    private readonly Label _rightState = new();
    private readonly Dictionary<Page, Button> _navButtons = new();

    private Page _activePage = Page.Left;
    private volatile bool _leftEnabled;
    private volatile bool _rightEnabled;
    private bool _lastLeftBindDown;
    private bool _lastRightBindDown;
    private volatile int _leftCps = 11;
    private volatile int _rightCps = 14;
    private readonly RuntimeClickerSettings _leftRuntime = RuntimeClickerSettings.LeftDefaults();
    private readonly RuntimeClickerSettings _rightRuntime = RuntimeClickerSettings.RightDefaults();
    private volatile bool _clickWorkersRunning;
    private Thread? _leftWorker;
    private Thread? _rightWorker;

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);
    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")]
    private static extern bool GetCursorInfo(ref CursorInfo cursorInfo);

    [StructLayout(LayoutKind.Sequential)]
    private struct CursorInfo
    {
        public int cbSize;
        public int flags;
        public nint hCursor;
        public Point ptScreenPos;
    }

    private sealed class RuntimeClickerSettings
    {
        private int _lastRandomMode = -1;
        private int _blatantRetargetAt;
        private double _blatantCps;
        private int _butterflyStep;

        public bool Randomization = true;
        public bool Jitter;
        public float JitterPower = 0.1f;
        public bool IgnoreMenus = true;
        public bool ClickSound;
        public bool BreakBlock;
        public bool OnlyFocused = true;
        public bool SimulateExhaust;
        public bool SlotsWhitelist;
        public bool IgnoreWhileShifting;
        public bool AllowInMenusOnShift;
        public bool IgnoreLeftClicker;
        public bool Spikes;
        public int SpikeDurationMs = 75;
        public int SpikeCps = 4;
        public int BreakBlockHoldMs = 260;
        public float RandomizationCps = 2f;
        public int RandomMode = 1;
        public int ContinuousTicks;
        public readonly bool[] Slots = new bool[9];

        public readonly record struct ClickPlan(int IntervalMs, int HoldMs, int RepeatCount);

        private RuntimeClickerSettings()
        {
            Slots[0] = true;
        }

        public static RuntimeClickerSettings LeftDefaults() => new()
        {
            Randomization = true,
            RandomMode = 1,
            SpikeCps = 4,
            SpikeDurationMs = 75,
            RandomizationCps = 2f
        };

        public static RuntimeClickerSettings RightDefaults() => new()
        {
            Randomization = true,
            RandomMode = 1,
            SpikeCps = 4,
            SpikeDurationMs = 75,
            RandomizationCps = 2f
        };

        public ClickPlan NextClickPlan(int baseCps, bool spikeBurst)
        {
            var now = Environment.TickCount;
            var cps = Math.Clamp((double)baseCps, 1.0, 300.0);
            ResetModeStateIfNeeded();

            if (SimulateExhaust)
            {
                var fatigue = Math.Min(0.28, ContinuousTicks / 900.0);
                cps *= 1.0 - fatigue;
            }

            var interval = RandomMode switch
            {
                0 => NextNormalInterval(cps),
                2 => NextBlatantInterval(cps, now),
                _ => NextButterflyInterval(cps)
            };

            var intervalMs = Math.Clamp((int)Math.Abs(Math.Round(interval)), 1, 500);
            var holdMs = intervalMs > 60 ? Math.Max(1, (int)Math.Floor(intervalMs * 0.3)) : 1;
            var repeatCount = 1;

            if (spikeBurst)
            {
                repeatCount = Math.Clamp(SpikeCps, 1, 200);
                intervalMs = Math.Max(1, (int)((float)Math.Max(1, SpikeDurationMs) / repeatCount));

                if (intervalMs < 2)
                {
                    intervalMs = 1;
                    holdMs = 1;
                }
            }

            return new ClickPlan(intervalMs, holdMs, repeatCount);
        }

        public void MarkIdle()
        {
            ContinuousTicks = 0;
            _butterflyStep = 0;
        }

        public Point NextJitterMove()
        {
            var power = Math.Clamp(JitterPower, 0.01f, 1.0f);
            var horizontalMax = Math.Clamp((int)Math.Ceiling(power * 22.0f), 1, 22);
            var verticalMax = Math.Clamp((int)Math.Ceiling(power * 10.0f), 1, 10);

            var dx = Random.Shared.Next(-horizontalMax, horizontalMax + 1);
            var dy = Random.Shared.Next(-verticalMax, verticalMax + 1);

            if (dx == 0 && dy == 0)
            {
                dx = Random.Shared.Next(0, 2) == 0 ? -1 : 1;
            }

            return new Point(dx, dy);
        }

        private void ResetModeStateIfNeeded()
        {
            if (_lastRandomMode == RandomMode)
            {
                return;
            }

            _lastRandomMode = RandomMode;
            _blatantRetargetAt = 0;
            _blatantCps = 0;
            _butterflyStep = 0;
        }

        private double NextNormalInterval(double cps)
        {
            var baseInterval = 1000.0 / Math.Clamp(cps, 1.0, 300.0);
            if (!Randomization)
            {
                return Math.Floor(baseInterval);
            }

            var spread = Math.Clamp(RandomizationCps / 2.0, 0.10, 1.15);
            var interval = NextGaussian(baseInterval, baseInterval * spread);

            if (Random.Shared.NextDouble() < 0.08)
            {
                interval *= NextUniform(1.45, 2.75);
            }

            return Math.Abs(interval);
        }

        private double NextBlatantInterval(double cps, int now)
        {
            if (!Randomization)
            {
                return Math.Floor(1000.0 / Math.Clamp(cps, 1.0, 300.0));
            }

            if (_blatantRetargetAt == 0 || now >= _blatantRetargetAt)
            {
                _blatantCps = Math.Clamp(cps + NextUniform(-RandomizationCps, RandomizationCps), 1.0, 300.0);
                _blatantRetargetAt = now + Random.Shared.Next(900, 2200);
            }

            return Math.Floor(1000.0 / _blatantCps);
        }

        private double NextButterflyInterval(double cps)
        {
            var baseInterval = 1000.0 / Math.Clamp(cps, 1.0, 300.0);
            var cycle = _butterflyStep++ % 4;

            if (cycle == 1)
            {
                var shortMax = cps <= 10 ? 10.0 : 14.0;
                return NextUniform(2.0, shortMax);
            }

            var compensation = cycle == 2 ? NextUniform(1.45, 2.25) : NextUniform(1.05, 1.55);
            var interval = baseInterval * compensation;

            if (Randomization)
            {
                interval += NextGaussian(0, baseInterval * Math.Clamp(RandomizationCps / 5.0, 0.08, 0.55));
            }

            return Math.Abs(interval);
        }

        private static double NextUniform(double min, double max)
        {
            return min + Random.Shared.NextDouble() * (max - min);
        }

        private static double NextGaussian(double mean, double stdDev)
        {
            var u1 = 1.0 - Random.Shared.NextDouble();
            var u2 = 1.0 - Random.Shared.NextDouble();
            var randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
            return mean + stdDev * randStdNormal;
        }
    }

    public MainForm()
    {
        Text = BrandName;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1060, 700);
        ClientSize = new Size(1120, 720);
        BackColor = AppBg;
        Font = new Font("Segoe UI", 9.5f);

        Controls.Add(BuildShell());

        _library.Attach(nint.Zero, 0);
        _library.SetLeftCps(_leftCps);
        _library.SetRightCps(_rightCps);

        SelectPage(Page.Left);
        StartTimers();
        StartClickWorkers();

        FormClosing += (_, _) =>
        {
            _clickWorkersRunning = false;
            _library.Detach();
        };
    }

    private Control BuildShell()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = AppBg,
            ColumnCount = 2,
            RowCount = 1
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 230));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        root.Controls.Add(BuildNav(), 0, 0);

        var main = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = AppBg,
            ColumnCount = 1,
            RowCount = 3
        };
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
        main.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        main.Controls.Add(BuildTopBar(), 0, 0);
        main.Controls.Add(_content, 0, 1);
        main.Controls.Add(BuildStatusBar(), 0, 2);
        root.Controls.Add(main, 1, 0);

        return root;
    }

    private Control BuildNav()
    {
        var nav = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = NavBg,
            Padding = new Padding(18, 18, 18, 16),
            ColumnCount = 1,
            RowCount = 9
        };
        nav.RowStyles.Add(new RowStyle(SizeType.Absolute, 86));
        nav.RowStyles.Add(new RowStyle(SizeType.Absolute, 18));
        for (var i = 0; i < 5; i++) nav.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        nav.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        nav.RowStyles.Add(new RowStyle(SizeType.Absolute, 88));

        var brand = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = NavBg,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0)
        };
        brand.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 64));
        brand.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        brand.Controls.Add(LogoBox(), 0, 0);
        brand.Controls.Add(new Label
        {
            Text = BrandName,
            Dock = DockStyle.Fill,
            ForeColor = Accent,
            Font = new Font("Segoe UI Semibold", 17f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        }, 1, 0);
        nav.Controls.Add(brand, 0, 0);

        AddNavButton(nav, Page.Left, "Left clicker", 2);
        AddNavButton(nav, Page.Right, "Right clicker", 3);
        AddNavButton(nav, Page.Misc, "Macros", 4);
        AddNavButton(nav, Page.Settings, "Settings", 5);
        AddNavButton(nav, Page.Log, "Logs", 6);

        nav.Controls.Add(new Label
        {
            Text = _library.TargetWindow == nint.Zero ? "No game target" : $"Game 0x{_library.TargetWindow:X}",
            Dock = DockStyle.Fill,
            ForeColor = TextMuted,
            BackColor = SurfaceSoft,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold),
            Margin = new Padding(0, 14, 0, 0)
        }, 0, 8);
        return nav;
    }

    private static Control LogoBox()
    {
        var logo = LoadLogo();
        if (logo is not null)
        {
            return new PictureBox
            {
                Dock = DockStyle.Fill,
                Image = logo,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = NavBg,
                Margin = new Padding(0, 0, 10, 0)
            };
        }

        return new Label
        {
            Text = "B",
            Dock = DockStyle.Fill,
            ForeColor = Accent,
            Font = new Font("Segoe UI Semibold", 20f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter,
            Margin = new Padding(0, 0, 10, 0)
        };
    }

    private static Image? LoadLogo()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "bouzelouf_logo.png");
        if (!File.Exists(path))
        {
            return null;
        }

        using var stream = File.OpenRead(path);
        using var image = Image.FromStream(stream);
        return new Bitmap(image);
    }

    private Control BuildTopBar()
    {
        var bar = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = AppBg,
            Padding = new Padding(24, 14, 24, 6),
            ColumnCount = 3,
            RowCount = 1
        };
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 124));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 124));

        _pageTitle.Text = ActivePageTitle();
        _pageTitle.Dock = DockStyle.Fill;
        _pageTitle.ForeColor = TextMain;
        _pageTitle.Font = new Font("Segoe UI Semibold", 16f, FontStyle.Bold);
        _pageTitle.TextAlign = ContentAlignment.MiddleLeft;
        bar.Controls.Add(_pageTitle, 0, 0);

        StylePill(_leftState, _leftEnabled ? "LEFT ON" : "LEFT OFF", _leftEnabled);
        StylePill(_rightState, _rightEnabled ? "RIGHT ON" : "RIGHT OFF", _rightEnabled);
        bar.Controls.Add(_leftState, 1, 0);
        bar.Controls.Add(_rightState, 2, 0);
        return bar;
    }

    private string ActivePageTitle() => _activePage switch
    {
        Page.Left => "Left autoclicker",
        Page.Right => "Right autoclicker",
        Page.Misc => "Macros",
        Page.Settings => "Settings",
        _ => "Activity"
    };

    private void UpdateTopBar()
    {
        _pageTitle.Text = ActivePageTitle();
        StylePill(_leftState, _leftEnabled ? "LEFT ON" : "LEFT OFF", _leftEnabled);
        StylePill(_rightState, _rightEnabled ? "RIGHT ON" : "RIGHT OFF", _rightEnabled);
    }

    private void AddNavButton(TableLayoutPanel nav, Page page, string text, int row)
    {
        var button = new Button
        {
            Text = text,
            Tag = page,
            Dock = DockStyle.Fill,
            FlatStyle = FlatStyle.Flat,
            BackColor = NavBg,
            ForeColor = TextMuted,
            Font = new Font("Segoe UI Semibold", 10.5f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(16, 0, 0, 0),
            Margin = new Padding(0, 4, 0, 4)
        };
        button.FlatAppearance.BorderColor = NavBg;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.MouseOverBackColor = Surface;
        button.FlatAppearance.MouseDownBackColor = SurfaceAlt;
        button.Click += (_, _) => SelectPage(page);
        _navButtons[page] = button;
        nav.Controls.Add(button, 0, row);
    }

    private Control BuildStatusBar()
    {
        var bar = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = NavBg,
            ColumnCount = 3,
            Padding = new Padding(12, 0, 12, 0)
        };
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));

        _status.Text = "Bouzelouf ready - connect Minecraft, enable a clicker, hold mouse in game";
        _status.Dock = DockStyle.Fill;
        _status.ForeColor = TextMuted;
        _status.TextAlign = ContentAlignment.MiddleLeft;

        bar.Controls.Add(_status, 0, 0);
        bar.Controls.Add(StatusText("F4 Left / F7 Right"), 1, 0);
        bar.Controls.Add(StatusText("Bouzelouf local"), 2, 0);
        return bar;
    }

    private static Label StatusText(string text)
    {
        return new Label
        {
            Text = text,
            Dock = DockStyle.Fill,
            ForeColor = TextMuted,
            TextAlign = ContentAlignment.MiddleRight
        };
    }

    private void SelectPage(Page page)
    {
        _activePage = page;
        foreach (var (buttonPage, button) in _navButtons)
        {
            var selected = buttonPage == page;
            button.ForeColor = selected ? Accent : TextMuted;
            button.BackColor = selected ? Surface : NavBg;
            button.FlatAppearance.BorderColor = selected ? Accent : NavBg;
        }

        _content.SuspendLayout();
        _content.Controls.Clear();
        _content.Controls.Add(page switch
        {
            Page.Left => BuildClickerPage(left: true),
            Page.Right => BuildClickerPage(left: false),
            Page.Misc => BuildMiscPage(),
            Page.Settings => BuildSettingsPage(),
            _ => BuildLogPage()
        });
        _content.ResumeLayout();
        UpdateTopBar();
    }

    private void StartTimers()
    {
        var keybindTimer = new System.Windows.Forms.Timer { Interval = 15 };
        keybindTimer.Tick += (_, _) =>
        {
            PollToggleKey(0x73, ref _lastLeftBindDown, () => SetClickerEnabled(left: true, !_leftEnabled, refresh: true));
            PollToggleKey(0x76, ref _lastRightBindDown, () => SetClickerEnabled(left: false, !_rightEnabled, refresh: true));
            PollCurrentSlot();
        };
        keybindTimer.Start();

        var detectTimer = new System.Windows.Forms.Timer { Interval = 1200 };
        detectTimer.Tick += (_, _) =>
        {
            if (_library.TargetWindow != nint.Zero) return;
            var hwnd = _library.FindGameWindow();
            if (hwnd == nint.Zero) return;
            _library.SetTargetWindow(hwnd);
            SetStatus($"Connected to game window 0x{hwnd:X}", good: true);
        };
        detectTimer.Start();
    }

    private void PollCurrentSlot()
    {
        if (_library.TargetWindow == nint.Zero || GetForegroundWindow() != _library.TargetWindow)
        {
            return;
        }

        RefreshCurrentSlotFromKeyboard();
    }

    private bool RefreshCurrentSlotFromKeyboard()
    {
        for (var i = 0; i < 9; i++)
        {
            if ((GetAsyncKeyState(0x31 + i) & 0x8000) != 0)
            {
                _library.SetCurrentSlot(i);
                return true;
            }
        }

        return false;
    }

    private void StartClickWorkers()
    {
        _clickWorkersRunning = true;
        _leftWorker = new Thread(() => ClickWorker(left: true))
        {
            IsBackground = true,
            Name = "Volt left click worker"
        };
        _rightWorker = new Thread(() => ClickWorker(left: false))
        {
            IsBackground = true,
            Name = "Volt right click worker"
        };
        _leftWorker.Start();
        _rightWorker.Start();
    }

    private void ClickWorker(bool left)
    {
        var click = left ? (byte)0 : (byte)1;
        var runtime = left ? _leftRuntime : _rightRuntime;
        var physicalDownSince = 0;
        var breakBlockHolding = false;
        var wasClicking = false;

        while (_clickWorkersRunning)
        {
            var enabled = left ? _leftEnabled : _rightEnabled;
            var isClicking = _library.IsClicking(click) != 0;

            if (!enabled || _library.TargetWindow == nint.Zero || !isClicking || ShouldSkipClick(runtime, left))
            {
                if (breakBlockHolding)
                {
                    _library.SendBreakBlockClick(false);
                    breakBlockHolding = false;
                }

                physicalDownSince = 0;
                wasClicking = isClicking;
                runtime.MarkIdle();
                Thread.Sleep(4);
                continue;
            }

            var spikeBurst = left && runtime.Spikes && !wasClicking;
            wasClicking = true;

            if (left && runtime.BreakBlock)
            {
                physicalDownSince = physicalDownSince == 0 ? Environment.TickCount : physicalDownSince;
                var heldMs = unchecked(Environment.TickCount - physicalDownSince);

                if (heldMs >= runtime.BreakBlockHoldMs)
                {
                    if (!breakBlockHolding)
                    {
                        _library.SendBreakBlockClick(true);
                        breakBlockHolding = true;
                    }
                }
            }
            else
            {
                if (breakBlockHolding)
                {
                    _library.SendBreakBlockClick(false);
                    breakBlockHolding = false;
                }

                physicalDownSince = 0;
            }

            var cps = Math.Clamp(left ? _leftCps : _rightCps, 1, left ? 200 : 300);
            runtime.ContinuousTicks++;
            var plan = runtime.NextClickPlan(cps, spikeBurst);

            for (var i = 0; i < plan.RepeatCount && _clickWorkersRunning; i++)
            {
                if (i > 0)
                {
                    var stillEnabled = left ? _leftEnabled : _rightEnabled;
                    if (!stillEnabled || _library.IsClicking(click) == 0 || ShouldSkipClick(runtime, left))
                    {
                        runtime.MarkIdle();
                        break;
                    }
                }

                if (left)
                {
                    if (breakBlockHolding)
                    {
                        ApplyJitter(runtime);
                        _library.OneClick(0, 0);
                        if (!SleepWithGuards(plan.IntervalMs, runtime, left, click))
                        {
                            runtime.MarkIdle();
                            break;
                        }
                        continue;
                    }

                    ApplyJitter(runtime);
                    _library.SendClick(plan.HoldMs);
                }
                else
                {
                    ApplyJitter(runtime);
                    _library.SendRightClick(plan.HoldMs);
                }

                if (!SleepWithGuards(Math.Max(0, plan.IntervalMs - plan.HoldMs), runtime, left, click))
                {
                    runtime.MarkIdle();
                    break;
                }
            }
        }

        if (breakBlockHolding)
        {
            _library.SendBreakBlockClick(false);
        }
    }

    private void ApplyJitter(RuntimeClickerSettings runtime)
    {
        if (!runtime.Jitter)
        {
            return;
        }

        if (_library.TargetWindow == nint.Zero || GetForegroundWindow() != _library.TargetWindow)
        {
            return;
        }

        var move = runtime.NextJitterMove();
        _library.SendJitterMove(move.X, move.Y);
    }

    private static void PerformantSleep(int targetMs)
    {
        if (targetMs <= 0)
        {
            return;
        }

        var deadline = Stopwatch.GetTimestamp() + targetMs * Stopwatch.Frequency / 1000L;
        while (true)
        {
            var remainingTicks = deadline - Stopwatch.GetTimestamp();
            if (remainingTicks <= 0)
            {
                return;
            }

            var remainingMs = remainingTicks * 1000.0 / Stopwatch.Frequency;
            if (remainingMs > 8)
            {
                Thread.Sleep(1);
            }
            else if (remainingMs > 2)
            {
                Thread.Yield();
            }
            else
            {
                Thread.SpinWait(80);
            }
        }
    }

    private bool SleepWithGuards(int targetMs, RuntimeClickerSettings runtime, bool left, byte click)
    {
        if (targetMs <= 0)
        {
            return true;
        }

        var deadline = Stopwatch.GetTimestamp() + targetMs * Stopwatch.Frequency / 1000L;
        while (true)
        {
            if (!_clickWorkersRunning)
            {
                return false;
            }

            var enabled = left ? _leftEnabled : _rightEnabled;
            if (!enabled || _library.IsClicking(click) == 0 || ShouldSkipClick(runtime, left))
            {
                return false;
            }

            var remainingTicks = deadline - Stopwatch.GetTimestamp();
            if (remainingTicks <= 0)
            {
                return true;
            }

            var remainingMs = (int)Math.Ceiling(remainingTicks * 1000.0 / Stopwatch.Frequency);
            PerformantSleep(Math.Min(remainingMs, 2));
        }
    }

    private bool ShouldSkipClick(RuntimeClickerSettings runtime, bool left)
    {
        if (runtime.OnlyFocused && (_library.TargetWindow == nint.Zero || GetForegroundWindow() != _library.TargetWindow))
        {
            return true;
        }

        var shifting = (GetAsyncKeyState(0x10) & 0x8000) != 0;

        if (runtime.IgnoreMenus && IsCursorVisible() && !(runtime.AllowInMenusOnShift && shifting))
        {
            return true;
        }

        if (runtime.IgnoreWhileShifting && shifting)
        {
            return true;
        }

        if (!left && runtime.IgnoreLeftClicker && _leftEnabled && _library.IsClicking(0) != 0)
        {
            return true;
        }

        if (runtime.SlotsWhitelist)
        {
            if (!IsRuntimeOnWhitelistedSlot(runtime))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsRuntimeOnWhitelistedSlot(RuntimeClickerSettings runtime)
    {
        RefreshCurrentSlotFromKeyboard();
        var slot = _library.GetCurrentSlot();
        return slot >= 0 && slot < runtime.Slots.Length && runtime.Slots[slot];
    }

    private static bool IsCursorVisible()
    {
        var info = new CursorInfo { cbSize = Marshal.SizeOf<CursorInfo>() };
        return GetCursorInfo(ref info) && (info.flags & 0x00000001) != 0;
    }

    private static void PollToggleKey(int vKey, ref bool previousDown, Action onPressed)
    {
        var down = (GetAsyncKeyState(vKey) & 0x8000) != 0;
        if (down && !previousDown)
        {
            onPressed();
        }
        previousDown = down;
    }

    private Control BuildClickerPage(bool left)
    {
        var runtime = left ? _leftRuntime : _rightRuntime;
        var enabled = left ? _leftEnabled : _rightEnabled;
        var cps = left ? _leftCps : _rightCps;

        var page = PaddedPage();
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = AppBg,
            ColumnCount = 2,
            RowCount = 2
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 136));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        page.Controls.Add(layout);

        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = SurfaceSoft,
            Padding = new Padding(18, 14, 18, 14),
            ColumnCount = 3,
            RowCount = 2,
            Margin = new Padding(0, 0, 14, 14)
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        header.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        header.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        header.Paint += (_, e) => DrawPanelBorder(header, e);

        var badge = new Label
        {
            Text = left ? "L" : "R",
            Dock = DockStyle.Fill,
            ForeColor = Color.Black,
            BackColor = Accent,
            Font = new Font("Segoe UI Semibold", 24f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter,
            Margin = new Padding(0, 0, 14, 0)
        };
        header.Controls.Add(badge, 0, 0);
        header.SetRowSpan(badge, 2);

        header.Controls.Add(new Label
        {
            Text = left ? "Left clicker" : "Right clicker",
            Dock = DockStyle.Fill,
            ForeColor = TextMain,
            Font = new Font("Segoe UI Semibold", 20f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        }, 1, 0);
        header.Controls.Add(Pill(enabled ? "ENABLED" : "DISABLED", enabled), 2, 0);
        header.Controls.Add(new Label
        {
            Text = left ? "F4 toggle  /  hold left mouse" : "F7 toggle  /  hold right mouse",
            Dock = DockStyle.Fill,
            ForeColor = TextMuted,
            TextAlign = ContentAlignment.MiddleLeft
        }, 1, 1);
        header.Controls.Add(ValueLabel($"{cps} CPS"), 2, 1);
        layout.Controls.Add(header, 0, 0);
        layout.SetColumnSpan(header, 2);

        var primary = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            Padding = new Padding(18),
            ColumnCount = 2,
            RowCount = 11,
            Margin = new Padding(0, 0, 14, 0)
        };
        primary.Paint += (_, e) => DrawPanelBorder(primary, e);
        primary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        primary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        primary.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        primary.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        primary.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        primary.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        for (var i = 0; i < 6; i++) primary.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        primary.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(primary, 0, 1);

        primary.Controls.Add(SectionTitle("Controls"), 0, 0);
        primary.SetColumnSpan(primary.GetControlFromPosition(0, 0)!, 2);

        var enable = ToggleBox("Enabled", enabled);
        enable.CheckedChanged += (_, _) => SetClickerEnabled(left, enable.Checked, refresh: true);
        primary.Controls.Add(enable, 0, 1);
        primary.Controls.Add(Pill(left ? "F4" : "F7", enabled), 1, 1);

        var cpsLabel = FieldLabel("Clicks per second");
        var value = ValueLabel($"{cps} CPS");
        primary.Controls.Add(cpsLabel, 0, 2);
        primary.Controls.Add(value, 1, 2);

        var slider = new TrackBar
        {
            Dock = DockStyle.Fill,
            Minimum = 1,
            Maximum = left ? 200 : 300,
            Value = Math.Clamp(cps, 1, left ? 200 : 300),
            TickStyle = TickStyle.None,
            BackColor = Surface,
            Margin = new Padding(0, 6, 0, 8)
        };
        slider.ValueChanged += (_, _) =>
        {
            SetCps(left, slider.Value);
            value.Text = $"{slider.Value} CPS";
        };
        primary.Controls.Add(slider, 0, 3);
        primary.SetColumnSpan(slider, 2);

        primary.Controls.Add(BoundToggle("Randomization", runtime.Randomization, value => runtime.Randomization = value), 0, 4);
        primary.Controls.Add(BoundToggle("Jitter", runtime.Jitter, value =>
        {
            runtime.Jitter = value;
            if (value && runtime.JitterPower <= 0)
            {
                runtime.JitterPower = 0.1f;
            }
        }), 1, 4);
        primary.Controls.Add(BoundToggle("Ignore menus", runtime.IgnoreMenus, value => runtime.IgnoreMenus = value), 0, 5);
        primary.Controls.Add(BoundToggle("Click sound", runtime.ClickSound, value => runtime.ClickSound = value), 1, 5);
        primary.Controls.Add(BoundToggle(left ? "Break block" : "Ignore left clicker", left ? runtime.BreakBlock : runtime.IgnoreLeftClicker, value =>
        {
            if (left) runtime.BreakBlock = value;
            else runtime.IgnoreLeftClicker = value;
        }), 0, 6);
        primary.Controls.Add(BoundToggle("Only focused", runtime.OnlyFocused, value => runtime.OnlyFocused = value), 1, 6);
        primary.Controls.Add(BoundToggle("Simulate exhaust", runtime.SimulateExhaust, value => runtime.SimulateExhaust = value), 0, 7);
        primary.Controls.Add(BoundToggle("Slots whitelist", runtime.SlotsWhitelist, value =>
        {
            runtime.SlotsWhitelist = value;
            SyncWhitelist(left);
        }), 1, 7);
        primary.Controls.Add(BoundToggle("Ignore shifting", runtime.IgnoreWhileShifting, value => runtime.IgnoreWhileShifting = value), 0, 8);
        primary.Controls.Add(BoundToggle(left ? "Spikes" : "Allow menus shift", left ? runtime.Spikes : runtime.AllowInMenusOnShift, value =>
        {
            if (left) runtime.Spikes = value;
            else runtime.AllowInMenusOnShift = value;
        }), 1, 8);

        var slots = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0, 12, 0, 0)
        };
        for (var i = 1; i <= 9; i++)
        {
            var slotIndex = i - 1;
            slots.Controls.Add(SlotButton(i, runtime.Slots[slotIndex], selected =>
            {
                runtime.Slots[slotIndex] = selected;
                SyncWhitelist(left);
            }));
        }
        primary.Controls.Add(slots, 0, 9);
        primary.SetColumnSpan(slots, 2);

        var advanced = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            Padding = new Padding(18),
            ColumnCount = 1,
            RowCount = 10,
            Margin = new Padding(0)
        };
        advanced.Paint += (_, e) => DrawPanelBorder(advanced, e);
        advanced.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        advanced.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        advanced.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        advanced.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        advanced.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        advanced.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        advanced.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        advanced.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        advanced.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(advanced, 1, 1);

        advanced.Controls.Add(SectionTitle("Profile options"), 0, 0);
        var mode = Combo("Mode", "Normal", "Butterfly", "Blatant");
        mode.SelectedIndex = Math.Clamp(runtime.RandomMode, 0, mode.Items.Count - 1);
        mode.SelectedIndexChanged += (_, _) => runtime.RandomMode = mode.SelectedIndex;
        advanced.Controls.Add(mode, 0, 1);
        advanced.Controls.Add(Combo("Activation", "Hold mouse", "Toggle key"), 0, 2);
        advanced.Controls.Add(MiniSlider("Random spread", (int)(runtime.RandomizationCps * 10), 0, 50, value => runtime.RandomizationCps = value / 10f, value => $"{value / 10f:0.0} cps"), 0, 3);
        advanced.Controls.Add(MiniSlider("Jitter strength", runtime.Jitter ? (int)Math.Round(runtime.JitterPower * 100f) : 0, 0, 25, value =>
        {
            runtime.Jitter = value > 0;
            if (value > 0)
            {
                runtime.JitterPower = value / 100f;
            }
        }, value => $"{value}%"), 0, 4);

        if (left)
        {
            advanced.Controls.Add(MiniSlider("Break hold", runtime.BreakBlockHoldMs, 150, 650, value => runtime.BreakBlockHoldMs = value, value => $"{value} ms"), 0, 5);
            advanced.Controls.Add(MiniSlider("Spike time", runtime.SpikeDurationMs, 50, 500, value => runtime.SpikeDurationMs = value, value => $"{value} ms"), 0, 6);
            advanced.Controls.Add(MiniSlider("Spike CPS", runtime.SpikeCps, 1, 20, value => runtime.SpikeCps = value, value => $"{value} cps"), 0, 7);
        }

        return page;
    }

    private Control BuildMiscPage()
    {
        var page = PaddedPage();
        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = AppBg,
            ColumnCount = 2,
            RowCount = 2
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        page.Controls.Add(grid);

        var intro = HeroBlock("Macros", "Bouzelouf local");
        grid.Controls.Add(intro, 0, 0);
        grid.SetColumnSpan(grid.GetControlFromPosition(0, 0)!, 2);

        var macroCard = CardPanel();
        macroCard.Margin = new Padding(0, 14, 14, 0);
        var macro = TwoColumnCard("TNT macro");
        macro.Controls.Add(ToggleBox("TNT macro", false), 0, 1);
        macro.Controls.Add(ToggleBox("Back to last slot", false), 1, 1);
        macro.Controls.Add(Combo("TNT slot", "3", "4", "5", "6"), 0, 2);
        macro.Controls.Add(Combo("Flint slot", "2", "1", "8", "9"), 1, 2);
        macroCard.Controls.Add(macro);
        grid.Controls.Add(macroCard, 0, 1);

        var stateCard = CardPanel();
        stateCard.Margin = new Padding(0, 14, 0, 0);
        var state = OneColumnCard("Runtime");
        state.Controls.Add(ReadOnlyField("Left CPS", $"{_leftCps}"), 0, 1);
        state.Controls.Add(ReadOnlyField("Right CPS", $"{_rightCps}"), 0, 2);
        state.Controls.Add(ReadOnlyField("Current slot", $"{_library.GetCurrentSlot() + 1}"), 0, 3);
        stateCard.Controls.Add(state);
        grid.Controls.Add(stateCard, 1, 1);
        return page;
    }

    private Control BuildSettingsPage()
    {
        var page = PaddedPage();
        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = AppBg,
            ColumnCount = 2,
            RowCount = 2
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        page.Controls.Add(grid);

        grid.Controls.Add(HeroBlock("Settings", "Bouzelouf local"), 0, 0);
        grid.SetColumnSpan(grid.GetControlFromPosition(0, 0)!, 2);

        var gameCard = CardPanel();
        gameCard.Margin = new Padding(0, 14, 14, 0);
        var game = OneColumnCard("Game connection");
        game.Controls.Add(ReadOnlyField("Target handle", _library.TargetWindow == nint.Zero ? "Not connected" : $"0x{_library.TargetWindow:X}"), 0, 1);

        var connect = PrimaryButton(_library.TargetWindow == nint.Zero ? "Connect to Minecraft" : "Refresh target");
        connect.Click += (_, _) =>
        {
            var hwnd = _library.FindGameWindow();
            if (hwnd == nint.Zero)
            {
                SetStatus("Minecraft window not found. Open the game first.", good: false);
                MessageBox.Show("Minecraft window not found.\nOpen AZ/Minecraft and go in game, then retry.", BrandName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _library.SetTargetWindow(hwnd);
            SetStatus($"Connected to game window 0x{hwnd:X}", good: true);
            SelectPage(Page.Settings);
        };
        game.Controls.Add(connect, 0, 2);
        game.Controls.Add(Combo("Detection", "Minecraft / LWJGL", "AZ fallback"), 0, 3);
        gameCard.Controls.Add(game);
        grid.Controls.Add(gameCard, 0, 1);

        var prefsCard = CardPanel();
        prefsCard.Margin = new Padding(0, 14, 0, 0);
        var prefs = TwoColumnCard("Preferences");
        prefs.Controls.Add(ToggleBox("CPS precision", true), 0, 1);
        prefs.Controls.Add(ToggleBox("Slots detection", true), 1, 1);
        prefs.Controls.Add(ToggleBox("Beep on toggle", false), 0, 2);
        prefs.Controls.Add(ToggleBox("Left handed mode", false), 1, 2);
        prefsCard.Controls.Add(prefs);
        grid.Controls.Add(prefsCard, 1, 1);
        return page;
    }

    private Control BuildLogPage()
    {
        var page = PaddedPage();
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = AppBg,
            ColumnCount = 2,
            RowCount = 1
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
        page.Controls.Add(layout);

        var logCard = CardPanel();
        logCard.Margin = new Padding(0, 0, 14, 0);
        var logLayout = OneColumnCard("Activity log");
        var log = new ListBox
        {
            Dock = DockStyle.Fill,
            BackColor = SurfaceSoft,
            ForeColor = TextMain,
            BorderStyle = BorderStyle.None,
            IntegralHeight = false,
            Font = new Font("Consolas", 9.5f),
            Margin = new Padding(0, 8, 0, 0)
        };
        foreach (var entry in _library.Log)
        {
            log.Items.Add(entry);
        }
        logLayout.Controls.Add(log, 0, 1);
        logLayout.RowStyles[1] = new RowStyle(SizeType.Percent, 100);
        logCard.Controls.Add(logLayout);
        layout.Controls.Add(logCard, 0, 0);

        var artifactsCard = CardPanel();
        var artifacts = OneColumnCard("Artifacts");
        artifacts.Controls.Add(BuildArtifactsList(), 0, 1);
        artifacts.RowStyles[1] = new RowStyle(SizeType.Percent, 100);
        artifactsCard.Controls.Add(artifacts);
        layout.Controls.Add(artifactsCard, 1, 0);
        return page;
    }

    private void SetClickerEnabled(bool left, bool enabled, bool refresh)
    {
        if (left)
        {
            _leftEnabled = enabled;
            _library.SetLeftEnabled(enabled);
            SyncWhitelist(left: true);
        }
        else
        {
            _rightEnabled = enabled;
            _library.SetRightEnabled(enabled);
            SyncWhitelist(left: false);
        }

        SetStatus($"{(left ? "Left" : "Right")} clicker {(enabled ? "enabled" : "disabled")}", enabled);
        UpdateTopBar();
        var page = left ? Page.Left : Page.Right;
        if (refresh && _activePage == page)
        {
            SelectPage(page);
        }
    }

    private void SyncWhitelist(bool left)
    {
        var runtime = left ? _leftRuntime : _rightRuntime;
        for (var i = 0; i < runtime.Slots.Length; i++)
        {
            _library.SetWhitelist(i, runtime.Slots[i]);
        }
    }

    private void SetCps(bool left, int cps)
    {
        if (left)
        {
            _leftCps = cps;
            _library.SetLeftCps(cps);
        }
        else
        {
            _rightCps = cps;
            _library.SetRightCps(cps);
        }
    }

    private void SetStatus(string text, bool good)
    {
        _status.Text = text;
        _status.ForeColor = good ? Good : TextMuted;
    }

    private static Panel PaddedPage()
    {
        return new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = AppBg,
            AutoScroll = false,
            Padding = new Padding(24, 6, 24, 22)
        };
    }

    private static Panel PanelBox()
    {
        return CardPanel();
    }

    private static Panel CardPanel()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            Padding = new Padding(1),
            Margin = new Padding(0)
        };
        panel.Paint += (_, e) => DrawPanelBorder(panel, e);
        return panel;
    }

    private static void DrawPanelBorder(Control control, PaintEventArgs e)
    {
        using var pen = new Pen(BorderSoft);
        e.Graphics.DrawRectangle(pen, 0, 0, control.Width - 1, control.Height - 1);
    }

    private static Control HeroBlock(string title, string subtitle)
    {
        var hero = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = SurfaceSoft,
            Padding = new Padding(18, 12, 18, 12),
            ColumnCount = 2,
            RowCount = 2,
            Margin = new Padding(0, 0, 0, 0)
        };
        hero.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        hero.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112));
        hero.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        hero.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        hero.Paint += (_, e) => DrawPanelBorder(hero, e);
        hero.Controls.Add(new Label
        {
            Text = title,
            Dock = DockStyle.Fill,
            ForeColor = TextMain,
            Font = new Font("Segoe UI Semibold", 18f, FontStyle.Bold),
            TextAlign = ContentAlignment.BottomLeft
        }, 0, 0);
        hero.Controls.Add(new Label
        {
            Text = subtitle,
            Dock = DockStyle.Fill,
            ForeColor = TextMuted,
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 1);
        hero.Controls.Add(LogoBox(), 1, 0);
        hero.SetRowSpan(hero.GetControlFromPosition(1, 0)!, 2);
        return hero;
    }

    private static TableLayoutPanel OneColumnCard(string title)
    {
        var card = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            Padding = new Padding(18),
            ColumnCount = 1,
            RowCount = 6
        };
        card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        for (var i = 0; i < 4; i++) card.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        card.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        card.Controls.Add(SectionTitle(title), 0, 0);
        return card;
    }

    private static TableLayoutPanel TwoColumnCard(string title)
    {
        var card = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            Padding = new Padding(18),
            ColumnCount = 2,
            RowCount = 6
        };
        card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        for (var i = 0; i < 4; i++) card.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        card.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        card.Controls.Add(SectionTitle(title), 0, 0);
        card.SetColumnSpan(card.GetControlFromPosition(0, 0)!, 2);
        return card;
    }

    private static Label Pill(string text, bool active)
    {
        var label = new Label();
        StylePill(label, text, active);
        return label;
    }

    private static void StylePill(Label label, string text, bool active)
    {
        label.Text = text;
        label.Dock = DockStyle.Fill;
        label.BackColor = active ? Color.FromArgb(22, 66, 43) : SurfaceAlt;
        label.ForeColor = active ? Good : TextMuted;
        label.Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold);
        label.TextAlign = ContentAlignment.MiddleCenter;
        label.Margin = new Padding(8, 12, 0, 12);
    }

    private static Label SectionTitle(string text)
    {
        return new Label
        {
            Text = text,
            Dock = DockStyle.Fill,
            ForeColor = TextMain,
            Font = new Font("Segoe UI Semibold", 13.5f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };
    }

    private static Label FieldLabel(string text)
    {
        return new Label
        {
            Text = text,
            Dock = DockStyle.Fill,
            ForeColor = TextMuted,
            Font = new Font("Segoe UI", 9.2f),
            TextAlign = ContentAlignment.MiddleLeft
        };
    }

    private static Label ValueLabel(string text)
    {
        return new Label
        {
            Text = text,
            Dock = DockStyle.Fill,
            ForeColor = Accent,
            Font = new Font("Segoe UI Semibold", 11f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleRight
        };
    }

    private static CheckBox ToggleBox(string text, bool isChecked)
    {
        var toggle = new CheckBox
        {
            Text = text,
            Checked = isChecked,
            Dock = DockStyle.Fill,
            ForeColor = TextMain,
            BackColor = isChecked ? AccentSoft : SurfaceSoft,
            Appearance = Appearance.Button,
            FlatStyle = FlatStyle.Flat,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleCenter,
            Padding = new Padding(6, 0, 6, 0),
            Margin = new Padding(0, 4, 8, 4),
            Cursor = Cursors.Hand
        };
        toggle.FlatAppearance.BorderColor = isChecked ? Accent : BorderSoft;
        toggle.FlatAppearance.CheckedBackColor = AccentSoft;
        toggle.FlatAppearance.MouseOverBackColor = SurfaceAlt;
        toggle.CheckedChanged += (_, _) =>
        {
            toggle.BackColor = toggle.Checked ? AccentSoft : SurfaceSoft;
            toggle.FlatAppearance.BorderColor = toggle.Checked ? Accent : BorderSoft;
            toggle.ForeColor = toggle.Checked ? Accent : TextMain;
        };
        toggle.ForeColor = isChecked ? Accent : TextMain;
        return toggle;
    }

    private static CheckBox BoundToggle(string text, bool isChecked, Action<bool> onChanged)
    {
        var toggle = ToggleBox(text, isChecked);
        toggle.CheckedChanged += (_, _) => onChanged(toggle.Checked);
        return toggle;
    }

    private static Control OptionRow(params Control[] controls)
    {
        var row = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            ColumnCount = controls.Length
        };
        for (var i = 0; i < controls.Length; i++)
        {
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / controls.Length));
            row.Controls.Add(controls[i], i, 0);
        }
        return row;
    }

    private static Control MiniSlider(string label, int value, int min, int max, Action<int> onChanged, Func<int, string> format)
    {
        var row = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            ColumnCount = 3,
            Margin = new Padding(0, 2, 0, 2)
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));

        var valueLabel = ValueLabel(format(value));
        valueLabel.Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold);

        var slider = new TrackBar
        {
            Dock = DockStyle.Fill,
            Minimum = min,
            Maximum = max,
            Value = Math.Clamp(value, min, max),
            TickStyle = TickStyle.None,
            BackColor = Surface,
            Margin = new Padding(0, 3, 10, 3)
        };
        slider.ValueChanged += (_, _) =>
        {
            onChanged(slider.Value);
            valueLabel.Text = format(slider.Value);
        };

        row.Controls.Add(FieldLabel(label), 0, 0);
        row.Controls.Add(slider, 1, 0);
        row.Controls.Add(valueLabel, 2, 0);
        return row;
    }

    private static ComboBox Combo(string label, params string[] values)
    {
        var combo = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = SurfaceAlt,
            ForeColor = TextMain,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold),
            Margin = new Padding(0, 5, 0, 5)
        };
        combo.Items.AddRange(values.Select(v => $"{label}: {v}").Cast<object>().ToArray());
        combo.SelectedIndex = 0;
        return combo;
    }

    private static Control ReadOnlyField(string label, string value)
    {
        var field = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = SurfaceSoft,
            Padding = new Padding(12, 0, 12, 0),
            ColumnCount = 2
        };
        field.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        field.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        field.Controls.Add(FieldLabel(label), 0, 0);
        field.Controls.Add(new Label
        {
            Text = value,
            Dock = DockStyle.Fill,
            ForeColor = TextMain,
            TextAlign = ContentAlignment.MiddleLeft
        }, 1, 0);
        return field;
    }

    private static Button PrimaryButton(string text)
    {
        var button = new Button
        {
            Text = text,
            Dock = DockStyle.Fill,
            BackColor = Accent,
            ForeColor = Color.FromArgb(10, 11, 14),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI Semibold", 11f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 8, 0, 8)
        };
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(255, 214, 108);
        return button;
    }

    private static Button SlotButton(int slot, bool selected, Action<bool>? onChanged = null)
    {
        var button = new Button
        {
            Text = slot.ToString(),
            Width = 42,
            Height = 36,
            BackColor = selected ? Accent : SurfaceAlt,
            ForeColor = selected ? Color.Black : TextMain,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 0, 8, 8)
        };
        button.FlatAppearance.BorderColor = selected ? Accent : Border;
        button.Click += (_, _) =>
        {
            selected = !selected;
            button.BackColor = selected ? Accent : SurfaceAlt;
            button.ForeColor = selected ? Color.Black : TextMain;
            button.FlatAppearance.BorderColor = selected ? Accent : Border;
            onChanged?.Invoke(selected);
        };
        return button;
    }

    private static Control BuildArtifactsList()
    {
        var list = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            BackColor = Surface,
            ForeColor = TextMain,
            BorderStyle = BorderStyle.None,
            FullRowSelect = true
        };
        list.Columns.Add("Name", 280);
        list.Columns.Add("Size", 90);
        list.Columns.Add("SHA256", 520);

        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "reconstructed_dlls_complete"));
        if (!Directory.Exists(root))
        {
            return list;
        }

        foreach (var file in Directory.GetFiles(root, "*.dll").OrderBy(Path.GetFileName))
        {
            var info = new FileInfo(file);
            var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file)));
            list.Items.Add(new ListViewItem(new[] { info.Name, info.Length.ToString("N0"), hash }));
        }
        return list;
    }
}
