using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace PetUsageOverlay;

internal sealed class UsageWindow : Window
{
    internal const int PetWidth = 152;
    // Distance from the saved pet origin to the bottom of Codex's action bar.
    internal const int ToolbarBottomOffset = 167;
    private const int OrbitHeight = 166;
    internal const double OrbitTopOffset = 15;
    private const int RingCanvasSize = 120;
    private const double RingStrokeThickness = 7.5;
    private const double LeftTopStart = 264;
    private const double RightTopStart = 276;
    private const double IdleLeftBottom = 120;
    private const double IdleRightBottom = 60;
    private const double ControlsLeftBottom = 140;
    private const double ControlsRightBottom = 40;
    private const string FiveHourAccent = "#187BDF";
    private const string WeeklyAccent = "#11B8C5";

    private readonly string stateFile = Path.Combine(CodexPaths.Home, ".codex-global-state.json");
    private readonly DispatcherTimer placementTimer = new(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(16) };
    private readonly DispatcherTimer stateTimer = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private readonly DispatcherTimer progressTimer = new() { Interval = TimeSpan.FromMilliseconds(200) };
    private readonly DispatcherTimer quotaTimer = new() { Interval = TimeSpan.FromSeconds(60) };
    private readonly DispatcherTimer countdownTimer = new() { Interval = TimeSpan.FromSeconds(15) };
    private readonly DispatcherTimer settingsTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private readonly DispatcherTimer appTimer = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private readonly DispatcherTimer shimmerTimer = new(DispatcherPriority.Background)
        { Interval = TimeSpan.FromMilliseconds(40) };
    private readonly Canvas surface = new();
    private readonly Canvas ring = new() { Width = RingCanvasSize, Height = RingCanvasSize };
    private readonly System.Windows.Shapes.Path fiveHourTrack;
    private readonly System.Windows.Shapes.Path weeklyTrack;
    private readonly System.Windows.Shapes.Path fiveHourArc;
    private readonly System.Windows.Shapes.Path weeklyArc;
    private readonly System.Windows.Shapes.Path fiveHourUnderlay;
    private readonly System.Windows.Shapes.Path weeklyUnderlay;
    private readonly System.Windows.Shapes.Path fiveHourSheen;
    private readonly System.Windows.Shapes.Path weeklySheen;
    private readonly System.Windows.Shapes.Path fiveHourWater;
    private readonly System.Windows.Shapes.Path weeklyWater;
    private readonly System.Windows.Shapes.Path fiveHourCaustics;
    private readonly System.Windows.Shapes.Path weeklyCaustics;
    private readonly TranslateTransform fiveHourWaterShift = new();
    private readonly TranslateTransform weeklyWaterShift = new();
    private readonly TranslateTransform fiveHourCausticShift = new();
    private readonly TranslateTransform weeklyCausticShift = new();
    private readonly TranslateTransform fiveHourCausticFadeShift = new();
    private readonly TranslateTransform weeklyCausticFadeShift = new();
    private readonly QuotaBadge fiveHour;
    private readonly QuotaBadge weekly;
    private readonly Forms.NotifyIcon tray;
    private readonly Forms.ToolStripMenuItem orbitMenuItem;
    private readonly Forms.ToolStripMenuItem belowMenuItem;

    private DateTime stateModifiedUtc;
    private PetState petState;
    private LogicalBounds displayArea;
    private OrbitPlacement orbitPlacement;
    private (Quota FiveHour, Quota Weekly)? quota;
    private DateTime lastHoveredUtc = DateTime.MinValue;
    private readonly PetDragTracker petDrag = new();
    private bool visible;
    private bool ringVisible;
    private bool readingQuota;
    private bool readingPetState;
    private bool readingProgress;
    private bool progressChecked;
    private DateTime progressBlockUntilUtc;
    private IntPtr avatarWindow;
    private DateTime lastZOrderCheckUtc;
    private DisplayMode displayMode = DisplayModeSettings.Load();
    private DateTime settingsModifiedUtc;
    private Process? mainAppProcess;
    private IntPtr mainAppWindow;
    private bool closingForApp;
    private bool desktopAppRunning;
    private RingMotion ringMotion;
    private DateTime lastRingFrameUtc;
    private DateTime shimmerStartedUtc;
    private double shimmerPhase = .38;
    private double fiveHourFillFraction;
    private double weeklyFillFraction;

    public UsageWindow()
    {
        Title = "Codex 宠物用量";
        Width = 530;
        Height = OrbitHeight;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = System.Windows.Media.Brushes.Transparent;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        Opacity = 0;
        IsHitTestVisible = false;

        fiveHourTrack = ArcStroke("#E0EEF7", RingStrokeThickness, LeftTopStart, IdleLeftBottom - LeftTopStart);
        weeklyTrack = ArcStroke("#E0EEF7", RingStrokeThickness, RightTopStart, IdleRightBottom + 360 - RightTopStart);
        ring.Children.Add(fiveHourTrack);
        ring.Children.Add(weeklyTrack);
        fiveHourUnderlay = ArcStroke("#303FBBED", 10, LeftTopStart, 0);
        weeklyUnderlay = ArcStroke("#3028CFCB", 10, RightTopStart, 0);
        ring.Children.Add(fiveHourUnderlay);
        ring.Children.Add(weeklyUnderlay);
        fiveHourArc = ArcStroke(OceanGradient("#439DF1", "#0869DC", "#5BDAEC"), RingStrokeThickness, LeftTopStart, 0);
        weeklyArc = ArcStroke(OceanGradient("#168FE6", "#08B6D0", "#65DDD2"), RingStrokeThickness, RightTopStart, 0);
        ring.Children.Add(fiveHourArc);
        ring.Children.Add(weeklyArc);
        fiveHourSheen = ArcStroke("#8AFFFFFF", .7, LeftTopStart, 0);
        weeklySheen = ArcStroke("#8AFFFFFF", .7, RightTopStart, 0);
        fiveHourSheen.StrokeStartLineCap = PenLineCap.Flat;
        fiveHourSheen.StrokeEndLineCap = PenLineCap.Flat;
        weeklySheen.StrokeStartLineCap = PenLineCap.Flat;
        weeklySheen.StrokeEndLineCap = PenLineCap.Flat;
        fiveHourWater = WaterStroke(fiveHourWaterShift);
        weeklyWater = WaterStroke(weeklyWaterShift);
        ring.Children.Add(fiveHourWater);
        ring.Children.Add(weeklyWater);
        var causticTexture = new BitmapImage(new Uri("pack://application:,,,/Assets/water-caustics.png"));
        causticTexture.Freeze();
        fiveHourCaustics = CausticStroke(causticTexture, fiveHourCausticShift, fiveHourCausticFadeShift);
        weeklyCaustics = CausticStroke(causticTexture, weeklyCausticShift, weeklyCausticFadeShift);
        ring.Children.Add(fiveHourCaustics);
        ring.Children.Add(weeklyCaustics);
        ring.Children.Add(fiveHourSheen);
        ring.Children.Add(weeklySheen);
        Canvas.SetTop(ring, Placement.RingCenterY - RingCanvasSize / 2.0);
        surface.Children.Add(ring);
        ring.Visibility = displayMode == DisplayMode.Orbit ? Visibility.Visible : Visibility.Hidden;

        fiveHour = new QuotaBadge("5 小时", FiveHourAccent);
        weekly = new QuotaBadge("本周", WeeklyAccent);
        fiveHour.Root.Visibility = Visibility.Hidden;
        weekly.Root.Visibility = Visibility.Hidden;
        surface.Children.Add(fiveHour.Root);
        surface.Children.Add(weekly.Root);
        Content = surface;

        var menu = new Forms.ContextMenuStrip();
        orbitMenuItem = new Forms.ToolStripMenuItem("环绕圆环") { Checked = displayMode == DisplayMode.Orbit };
        belowMenuItem = new Forms.ToolStripMenuItem("下方双卡片") { Checked = displayMode == DisplayMode.Below };
        orbitMenuItem.Click += (_, _) => Dispatcher.BeginInvoke(() => SetMode(DisplayMode.Orbit, save: true));
        belowMenuItem.Click += (_, _) => Dispatcher.BeginInvoke(() => SetMode(DisplayMode.Below, save: true));
        menu.Items.Add(orbitMenuItem);
        menu.Items.Add(belowMenuItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("退出用量浮层", null, (_, _) => Close());
        tray = new Forms.NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Information,
            Text = "Codex 宠物用量",
            ContextMenuStrip = menu,
            Visible = true
        };

        SourceInitialized += (_, _) => MakeClickThrough();
        Loaded += (_, _) => Start();
        Closed += (_, _) =>
        {
            appTimer.Stop();
            shimmerTimer.Stop();
            if (mainAppProcess is not null)
            {
                mainAppProcess.Exited -= MainAppExited;
                mainAppProcess.Dispose();
            }
            tray.Visible = false;
            tray.Dispose();
            menu.Dispose();
        };
    }

    private static LinearGradientBrush OceanGradient(string top, string middle, string bottom)
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = new System.Windows.Point(0, 0),
            EndPoint = new System.Windows.Point(0, 1)
        };
        brush.GradientStops.Add(new GradientStop((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(top), 0));
        brush.GradientStops.Add(new GradientStop((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(middle), .48));
        brush.GradientStops.Add(new GradientStop((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(bottom), 1));
        brush.Freeze();
        return brush;
    }

    private static System.Windows.Shapes.Path ArcStroke(string color, double thickness, double start, double sweep) =>
        ArcStroke((System.Windows.Media.Brush)new BrushConverter().ConvertFromString(color)!, thickness, start, sweep);

    private static System.Windows.Shapes.Path ArcStroke(System.Windows.Media.Brush stroke, double thickness, double start, double sweep) => new()
    {
        Data = ArcData(start, sweep),
        Stroke = stroke,
        StrokeThickness = thickness,
        StrokeStartLineCap = PenLineCap.Round,
        StrokeEndLineCap = PenLineCap.Round,
        SnapsToDevicePixels = false
    };

    private static System.Windows.Shapes.Path WaterStroke(TranslateTransform shift)
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = new System.Windows.Point(0, 0),
            EndPoint = new System.Windows.Point(.38, 1),
            RelativeTransform = shift,
            SpreadMethod = GradientSpreadMethod.Reflect
        };
        foreach (var (offset, color) in new (double, string)[]
        {
            (0, "#00FFFFFF"), (.13, "#4ADFFBFF"), (.25, "#0AFFFFFF"),
            (.39, "#58FFFFFF"), (.52, "#10FFFFFF"), (.70, "#46C7F5FF"),
            (.84, "#08FFFFFF"), (1, "#32EAFFFF")
        })
            brush.GradientStops.Add(new GradientStop(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color), offset));
        var path = ArcStroke(brush, 6.5, LeftTopStart, 0);
        path.StrokeStartLineCap = PenLineCap.Flat;
        path.StrokeEndLineCap = PenLineCap.Flat;
        return path;
    }

    private static System.Windows.Shapes.Path CausticStroke(BitmapSource texture, TranslateTransform shift,
        TranslateTransform fadeShift)
    {
        // Map the texture onto the whole ring instead of stretching it into each arc's bounds.
        // The left filled arc can be quite narrow, which used to squash all of its fine lines.
        var textureBrush = new ImageBrush(texture)
        {
            ViewportUnits = BrushMappingMode.Absolute,
            Viewport = new Rect(0, 0, RingCanvasSize, RingCanvasSize),
            TileMode = TileMode.Tile,
            Stretch = Stretch.Fill,
            Transform = shift
        };
        var fade = new LinearGradientBrush
        {
            MappingMode = BrushMappingMode.Absolute,
            StartPoint = new System.Windows.Point(0, 8),
            EndPoint = new System.Windows.Point(RingCanvasSize, RingCanvasSize - 8),
            SpreadMethod = GradientSpreadMethod.Reflect,
            Transform = fadeShift
        };
        foreach (var (offset, alpha) in new (double, byte)[]
        {
            (0, 25), (.14, 120), (.27, 235), (.42, 45),
            (.55, 130), (.72, 220), (.86, 45), (1, 30)
        })
            fade.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromArgb(alpha, 255, 255, 255), offset));
        return new System.Windows.Shapes.Path
        {
            Stroke = textureBrush,
            OpacityMask = fade,
            StrokeThickness = 6.6,
            StrokeStartLineCap = PenLineCap.Flat,
            StrokeEndLineCap = PenLineCap.Flat,
            SnapsToDevicePixels = false
        };
    }

    private static Geometry ArcData(double start, double sweep, double radius = Placement.RingRadius)
    {
        if (Math.Abs(sweep) < .01) return Geometry.Empty;
        const double center = RingCanvasSize / 2.0;
        static System.Windows.Point PointAt(double angle, double radius)
        {
            var radians = angle * Math.PI / 180;
            return new System.Windows.Point(center + radius * Math.Cos(radians),
                center + radius * Math.Sin(radians));
        }
        var figure = new PathFigure { StartPoint = PointAt(start, radius), IsClosed = false };
        figure.Segments.Add(new ArcSegment
        {
            Point = PointAt(start + sweep, radius),
            Size = new System.Windows.Size(radius, radius),
            SweepDirection = sweep >= 0 ? SweepDirection.Clockwise : SweepDirection.Counterclockwise,
            IsLargeArc = Math.Abs(sweep) > 180
        });
        return new PathGeometry(new[] { figure });
    }

    private void Start()
    {
        mainAppProcess = DesktopApp.FindMainProcess();
        if (mainAppProcess is null) { CloseForAppExit(); return; }
        try
        {
            mainAppWindow = mainAppProcess.MainWindowHandle;
            mainAppProcess.EnableRaisingEvents = true;
            mainAppProcess.Exited += MainAppExited;
            if (mainAppProcess.HasExited || !IsWindow(mainAppWindow)) { CloseForAppExit(); return; }
        }
        catch (InvalidOperationException) { CloseForAppExit(); return; }
        desktopAppRunning = true;
        settingsModifiedUtc = File.GetLastWriteTimeUtc(DisplayModeSettings.Path);
        _ = RefreshPetState();
        UpdateMeters();
        placementTimer.Tick += (_, _) => RefreshPlacement();
        stateTimer.Tick += async (_, _) => await RefreshPetState();
        progressTimer.Tick += async (_, _) => await RefreshProgress();
        quotaTimer.Tick += async (_, _) => await RefreshQuota();
        countdownTimer.Tick += (_, _) => UpdateMeters();
        settingsTimer.Tick += (_, _) => RefreshModeSetting();
        appTimer.Tick += (_, _) =>
        {
            if (mainAppProcess?.HasExited == true || !IsWindow(mainAppWindow)) CloseForAppExit();
        };
        shimmerTimer.Tick += (_, _) =>
        {
            shimmerPhase = (DateTime.UtcNow - shimmerStartedUtc).TotalSeconds / 4.8 % 1;
            UpdateWaterShimmer();
        };
        placementTimer.Start();
        stateTimer.Start();
        progressTimer.Start();
        quotaTimer.Start();
        countdownTimer.Start();
        settingsTimer.Start();
        appTimer.Start();
        _ = RefreshQuota();
    }

    private void MainAppExited(object? sender, EventArgs e)
    {
        if (!Dispatcher.HasShutdownStarted)
            Dispatcher.BeginInvoke(new Action(CloseForAppExit));
    }

    private void CloseForAppExit()
    {
        if (closingForApp) return;
        closingForApp = true;
        try
        {
            using var signal = EventWaitHandle.OpenExisting("Local\\CodexPetUsageAppClosed");
            signal.Set();
        }
        catch (WaitHandleCannotBeOpenedException) { }
        Close();
    }

    private void RefreshModeSetting()
    {
        var modified = File.GetLastWriteTimeUtc(DisplayModeSettings.Path);
        if (modified == settingsModifiedUtc) return;
        settingsModifiedUtc = modified;
        SetMode(DisplayModeSettings.Load(), save: false);
    }

    private void SetMode(DisplayMode mode, bool save)
    {
        if (displayMode != mode)
        {
            displayMode = mode;
            ring.Visibility = mode == DisplayMode.Orbit ? Visibility.Visible : Visibility.Hidden;
            UpdateShimmerTimer();
            SetBadgesVisible(false);
            progressChecked = false;
            orbitMenuItem.Checked = mode == DisplayMode.Orbit;
            belowMenuItem.Checked = mode == DisplayMode.Below;
            RefreshPlacement();
        }
        if (!save) return;
        DisplayModeSettings.Save(mode);
        settingsModifiedUtc = File.GetLastWriteTimeUtc(DisplayModeSettings.Path);
    }

    private async Task RefreshPetState()
    {
        if (readingPetState) return;
        readingPetState = true;
        try
        {
            var modified = File.GetLastWriteTimeUtc(stateFile);
            if (modified == stateModifiedUtc) return;
            var next = await Task.Run(ParsePetState);
            if (next is null) return;
            petState = next.Value;
            displayArea = new LogicalBounds(petState.ScreenX, petState.ScreenY,
                petState.ScreenX + petState.ScreenWidth, petState.ScreenY + petState.ScreenHeight);
            stateModifiedUtc = modified;
            if (Math.Abs(Left - displayArea.Left) > .5) Left = displayArea.Left;
            if (Math.Abs(Width - petState.ScreenWidth) > .5) Width = petState.ScreenWidth;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        {
            // Codex replaces this file atomically; retry on the next tick.
        }
        finally { readingPetState = false; }
    }

    private PetState? ParsePetState()
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(stateFile));
            var root = document.RootElement;
            var isOpen = JsonRead.Get(root, "electron-avatar-overlay-open")?.GetBoolean() ?? false;
            var bounds = JsonRead.Get(root, "electron-avatar-overlay-bounds");
            if (bounds is null) return null;
            var x = JsonRead.Number(JsonRead.Get(bounds.Value, "x"));
            var y = JsonRead.Number(JsonRead.Get(bounds.Value, "y"));
            var screen = JsonRead.Get(bounds.Value, "displayBounds");
            var sx = screen is null ? null : JsonRead.Number(JsonRead.Get(screen.Value, "x"));
            var sy = screen is null ? null : JsonRead.Number(JsonRead.Get(screen.Value, "y"));
            var sw = screen is null ? null : JsonRead.Number(JsonRead.Get(screen.Value, "width"));
            var sh = screen is null ? null : JsonRead.Number(JsonRead.Get(screen.Value, "height"));
            if (x is null || y is null || sx is null || sy is null || sw is null || sh is null) return null;
            return new PetState(isOpen, x.Value, y.Value, sx.Value, sy.Value, sw.Value, sh.Value);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        {
            return null;
        }
    }

    private void RefreshPlacement()
    {
        if (!petState.Open || !desktopAppRunning)
        {
            SetTrackingRate(fast: false);
            petDrag.Reset();
            progressChecked = false;
            SetBadgesVisible(false);
            if (ringVisible) { Opacity = 0; ringVisible = false; UpdateShimmerTimer(); }
            avatarWindow = IntPtr.Zero;
            return;
        }

        if (!ringVisible) { Opacity = 1; ringVisible = true; UpdateShimmerTimer(); }

        // Codex stores pet placement in desktop logical coordinates. WPF maps
        // the physical cursor into the same coordinate space as this window.
        GetCursorPos(out var cursor);
        var local = PointFromScreen(new System.Windows.Point(cursor.X, cursor.Y));
        var cursorX = Left + local.X;
        var cursorY = Top + local.Y;
        var now = DateTime.UtcNow;
        var pointerDown = GetAsyncKeyState(0x01) < 0;
        var nearRawPet = cursorX >= petState.X - 20 && cursorX <= petState.X + PetWidth + 20 &&
            cursorY >= petState.Y - 12 && cursorY <= petState.Y + ToolbarBottomOffset + 20;
        var reserveControls = nearRawPet || now - lastHoveredUtc < TimeSpan.FromMilliseconds(350);
        AnimateRing(reserveControls, now);
        var (displayedX, displayedY) = petDrag.Update(cursorX, cursorY,
            pointerDown, petState.X, petState.Y,
            displayArea, stateModifiedUtc, now);

        double targetTop;
        if (displayMode == DisplayMode.Orbit)
        {
            orbitPlacement = Placement.Calculate(displayedX, displayArea);
            targetTop = displayedY + OrbitTopOffset;
        }
        else
        {
            var below = Placement.CalculateBelow(displayedX, displayedY, displayArea);
            orbitPlacement = new OrbitPlacement(displayedX + Placement.MascotWidth / 2 - displayArea.Left,
                below.FiveHourX, below.WeeklyX, below.FiveHourY, below.WeeklyY, below.Stacked);
            targetTop = below.WindowTop;
        }
        LayoutOrbit(orbitPlacement);
        if (Math.Abs(Top - targetTop) > .5) Top = targetTop;
        SyncToAvatarZOrder(now);

        var hoveredPetOrToolbar = cursorX >= displayedX - 20 && cursorX <= displayedX + PetWidth + 20 &&
            cursorY >= displayedY - 12 && cursorY <= displayedY + ToolbarBottomOffset + 20;
        var hoveredOrbit = visible &&
            (BadgeContains(cursorX, cursorY, orbitPlacement.FiveHourX, orbitPlacement.FiveHourY) ||
             BadgeContains(cursorX, cursorY, orbitPlacement.WeeklyX, orbitPlacement.WeeklyY));
        if (hoveredPetOrToolbar || hoveredOrbit)
        {
            if (now - lastHoveredUtc >= TimeSpan.FromMilliseconds(350)) progressChecked = false;
            lastHoveredUtc = now;
        }
        var hoverActive = now - lastHoveredUtc < TimeSpan.FromMilliseconds(350);
        SetTrackingRate(petDrag.IsDragging || nearRawPet || hoverActive ||
            Math.Abs(ringMotion.Velocity) > .01 ||
            Math.Abs(ringMotion.Progress - (reserveControls ? 1 : 0)) > .001);
        if (!hoverActive) progressChecked = false;
        SetBadgesVisible(hoverActive && progressChecked && now >= progressBlockUntilUtc);
    }

    private void SetTrackingRate(bool fast)
    {
        var placementInterval = TimeSpan.FromMilliseconds(fast ? 16 : 50);
        var stateInterval = TimeSpan.FromMilliseconds(fast ? 33 : 100);
        if (placementTimer.Interval != placementInterval) placementTimer.Interval = placementInterval;
        if (stateTimer.Interval != stateInterval) stateTimer.Interval = stateInterval;
    }

    private bool BadgeContains(double x, double y, double badgeX, double badgeY) =>
        x >= Left + badgeX - 4 && x <= Left + badgeX + QuotaBadge.Width + 4 &&
        y >= Top + badgeY - 4 && y <= Top + badgeY + QuotaBadge.Height + 4;

    private void LayoutOrbit(OrbitPlacement placement)
    {
        SetCanvasLeft(ring, placement.RingCenterX - RingCanvasSize / 2.0);
        SetCanvasLeft(fiveHour.Root, placement.FiveHourX);
        SetCanvasTop(fiveHour.Root, placement.FiveHourY);
        SetCanvasLeft(weekly.Root, placement.WeeklyX);
        SetCanvasTop(weekly.Root, placement.WeeklyY);
    }

    private static void SetCanvasLeft(UIElement element, double left)
    {
        if (double.IsNaN(Canvas.GetLeft(element)) || Math.Abs(Canvas.GetLeft(element) - left) > .5)
            Canvas.SetLeft(element, left);
    }

    private static void SetCanvasTop(UIElement element, double top)
    {
        if (double.IsNaN(Canvas.GetTop(element)) || Math.Abs(Canvas.GetTop(element) - top) > .5)
            Canvas.SetTop(element, top);
    }

    private async Task RefreshProgress()
    {
        if (readingProgress || !petState.Open ||
            DateTime.UtcNow - lastHoveredUtc >= TimeSpan.FromMilliseconds(500)) return;

        readingProgress = true;
        try
        {
            var cards = new[]
            {
                ScreenBadge(orbitPlacement.FiveHourX, orbitPlacement.FiveHourY),
                ScreenBadge(orbitPlacement.WeeklyX, orbitPlacement.WeeklyY)
            };
            var blocked = await Task.Run(() => ProgressDetector.OverlapsCards(cards));
            if (blocked) progressBlockUntilUtc = DateTime.UtcNow.AddMilliseconds(900);
            progressChecked = true;
        }
        catch (Exception)
        {
            progressChecked = true;
        }
        finally { readingProgress = false; }
    }

    private System.Windows.Rect ScreenBadge(double x, double y)
    {
        var first = PointToScreen(new System.Windows.Point(x, y));
        var last = PointToScreen(new System.Windows.Point(x + QuotaBadge.Width, y + QuotaBadge.Height));
        return new System.Windows.Rect(first, last);
    }

    private void SetBadgesVisible(bool show)
    {
        if (visible == show) return;
        visible = show;
        fiveHour.Root.Visibility = show ? Visibility.Visible : Visibility.Hidden;
        weekly.Root.Visibility = show ? Visibility.Visible : Visibility.Hidden;
    }

    private void SyncToAvatarZOrder(DateTime now)
    {
        if (now - lastZOrderCheckUtc < TimeSpan.FromMilliseconds(100)) return;
        lastZOrderCheckUtc = now;
        var petCenter = PointToScreen(new System.Windows.Point(
            petState.X - Left + Placement.MascotWidth / 2,
            petState.Y - Top + Placement.SpriteCenterYOffset));
        if (!IsAvatarWindow(avatarWindow, petCenter))
        {
            avatarWindow = IntPtr.Zero;
            EnumWindows((candidate, _) =>
            {
                if (!IsAvatarWindow(candidate, petCenter)) return true;
                avatarWindow = candidate;
                return false;
            }, IntPtr.Zero);
        }
        if (avatarWindow == IntPtr.Zero) return;

        var hwnd = new WindowInteropHelper(this).Handle;
        // Place the quota window immediately behind Codex's avatar window.
        // Menus and other windows then cover both together, while the pet stays
        // visually in front of the ring where they overlap.
        if (GetWindow(hwnd, 3) != avatarWindow)
            SetWindowPos(hwnd, avatarWindow, 0, 0, 0, 0, 0x0213);
    }

    private bool IsAvatarWindow(IntPtr candidate, System.Windows.Point petCenter)
    {
        if (candidate == IntPtr.Zero || !IsWindow(candidate) || !IsWindowVisible(candidate)) return false;
        try
        {
            GetWindowThreadProcessId(candidate, out var processId);
            if (mainAppProcess is null || processId != mainAppProcess.Id) return false;
            const long avatarStyle = 0x8 | 0x80; // topmost tool window
            if ((GetWindowLongPtr(candidate, -20).ToInt64() & avatarStyle) != avatarStyle) return false;
            return GetWindowRect(candidate, out var bounds) &&
                petCenter.X >= bounds.Left && petCenter.X < bounds.Right &&
                petCenter.Y >= bounds.Top && petCenter.Y < bounds.Bottom;
        }
        catch (InvalidOperationException) { return false; }
    }

    private async Task RefreshQuota()
    {
        if (readingQuota) return;
        readingQuota = true;
        try
        {
            var result = await QuotaReader.ReadAsync();
            if (result is not null) quota = result;
            UpdateMeters();
        }
        finally { readingQuota = false; }
    }

    private void UpdateMeters()
    {
        fiveHour.Set(quota?.FiveHour.RemainingPercent, FormatReset(quota?.FiveHour.ResetsAt));
        weekly.Set(quota?.Weekly.RemainingPercent, FormatReset(quota?.Weekly.ResetsAt));
        UpdateRingGeometry();
        UpdateShimmerTimer();
    }

    private void UpdateRingGeometry() => UpdateRingGeometry(
        quota?.FiveHour.RemainingPercent, quota?.Weekly.RemainingPercent);

    private void UpdateRingGeometry(double? fiveHourPercent, double? weeklyPercent)
    {
        var progress = Math.Clamp(ringMotion.Progress, 0, 1);
        var leftBottom = IdleLeftBottom + (ControlsLeftBottom - IdleLeftBottom) * progress;
        var rightBottom = IdleRightBottom + (ControlsRightBottom - IdleRightBottom) * progress;
        var leftSweep = leftBottom - LeftTopStart;
        var rightSweep = rightBottom + 360 - RightTopStart;
        fiveHourTrack.Data = ArcData(LeftTopStart, leftSweep);
        weeklyTrack.Data = ArcData(RightTopStart, rightSweep);
        fiveHourFillFraction = Math.Clamp((fiveHourPercent ?? 0) / 100, 0, 1);
        weeklyFillFraction = Math.Clamp((weeklyPercent ?? 0) / 100, 0, 1);
        var leftFill = leftSweep * fiveHourFillFraction;
        var rightFill = rightSweep * weeklyFillFraction;
        var leftGeometry = ArcData(LeftTopStart, leftFill);
        var rightGeometry = ArcData(RightTopStart, rightFill);
        fiveHourUnderlay.Data = leftGeometry;
        weeklyUnderlay.Data = rightGeometry;
        fiveHourArc.Data = leftGeometry;
        weeklyArc.Data = rightGeometry;
        fiveHourWater.Data = leftGeometry;
        weeklyWater.Data = rightGeometry;
        fiveHourCaustics.Data = leftGeometry;
        weeklyCaustics.Data = rightGeometry;
        fiveHourSheen.Data = ArcData(LeftTopStart, leftFill, Placement.RingRadius - 3.1);
        weeklySheen.Data = ArcData(RightTopStart, rightFill, Placement.RingRadius - 3.1);
        UpdateWaterShimmer();
    }

    private void UpdateShimmerTimer()
    {
        if (ringVisible && displayMode == DisplayMode.Orbit && quota is not null)
        {
            if (shimmerTimer.IsEnabled) return;
            shimmerStartedUtc = DateTime.UtcNow - TimeSpan.FromSeconds(shimmerPhase * 4.8);
            shimmerTimer.Start();
        }
        else shimmerTimer.Stop();
    }

    private void UpdateWaterShimmer()
    {
        var angle = shimmerPhase * 2 * Math.PI;
        var progress = Math.Clamp(ringMotion.Progress, 0, 1);
        var leftFlow = ArcEndpointMotion(LeftTopStart +
            (IdleLeftBottom - LeftTopStart) * fiveHourFillFraction,
            LeftTopStart + (IdleLeftBottom +
                (ControlsLeftBottom - IdleLeftBottom) * progress - LeftTopStart) * fiveHourFillFraction);
        var rightFlow = ArcEndpointMotion(RightTopStart +
            (IdleRightBottom + 360 - RightTopStart) * weeklyFillFraction,
            RightTopStart + (IdleRightBottom +
                (ControlsRightBottom - IdleRightBottom) * progress + 360 - RightTopStart) * weeklyFillFraction);
        fiveHourWaterShift.X = leftFlow.X / RingCanvasSize + .10 * Math.Sin(angle);
        fiveHourWaterShift.Y = leftFlow.Y / RingCanvasSize + .16 * Math.Cos(angle);
        weeklyWaterShift.X = rightFlow.X / RingCanvasSize + .10 * Math.Sin(angle + 1.3);
        weeklyWaterShift.Y = rightFlow.Y / RingCanvasSize + .16 * Math.Cos(angle + 1.3);
        fiveHourCausticShift.X = leftFlow.X + 1.8 * Math.Sin(angle + .4);
        fiveHourCausticShift.Y = leftFlow.Y + 2.4 * Math.Cos(angle + .4);
        weeklyCausticShift.X = rightFlow.X + 1.8 * Math.Sin(angle + 1.7);
        weeklyCausticShift.Y = rightFlow.Y + 2.4 * Math.Cos(angle + 1.7);
        fiveHourCausticFadeShift.X = leftFlow.X + 8 * Math.Sin(angle + .2);
        fiveHourCausticFadeShift.Y = leftFlow.Y + 11 * Math.Cos(angle + .2);
        weeklyCausticFadeShift.X = rightFlow.X + 8 * Math.Sin(angle + 1.6);
        weeklyCausticFadeShift.Y = rightFlow.Y + 11 * Math.Cos(angle + 1.6);
        fiveHourCaustics.Opacity = .81 + .04 * Math.Sin(angle + .6);
        weeklyCaustics.Opacity = .81 + .04 * Math.Sin(angle + 1.9);
    }

    private static System.Windows.Vector ArcEndpointMotion(double idleAngle, double currentAngle)
    {
        var idleRadians = idleAngle * Math.PI / 180;
        var currentRadians = currentAngle * Math.PI / 180;
        return new System.Windows.Vector(
            Placement.RingRadius * (Math.Cos(currentRadians) - Math.Cos(idleRadians)),
            Placement.RingRadius * (Math.Sin(currentRadians) - Math.Sin(idleRadians)));
    }

    private void AnimateRing(bool controlsVisible, DateTime now)
    {
        var elapsed = lastRingFrameUtc == default ? 0 : (now - lastRingFrameUtc).TotalSeconds;
        lastRingFrameUtc = now;
        var target = controlsVisible ? 1.0 : 0.0;
        var next = ringMotion.Advance(target, elapsed);
        if (Math.Abs(next.Progress - target) < .0005 && Math.Abs(next.Velocity) < .01)
            next = new RingMotion(target, 0);
        var changed = Math.Abs(next.Progress - ringMotion.Progress) >= .0001;
        ringMotion = next;
        if (changed) UpdateRingGeometry();
    }

    public void WritePreview(string path, DisplayMode previewMode = DisplayMode.Orbit,
        bool controlsVisible = false)
    {
        ringMotion = new RingMotion(controlsVisible ? 1 : 0, 0);
        shimmerPhase = .38;
        UpdateRingGeometry(64, 74);
        fiveHour.Root.Visibility = Visibility.Visible;
        weekly.Root.Visibility = Visibility.Visible;
        fiveHour.Set(64, "4 小时 6 分后重置");
        weekly.Set(74, "3 天 18 小时后重置");
        Width = 530;
        var previewHeight = previewMode == DisplayMode.Orbit ? OrbitHeight : 80;
        Height = previewHeight;
        ring.Visibility = previewMode == DisplayMode.Orbit ? Visibility.Visible : Visibility.Hidden;
        if (previewMode == DisplayMode.Orbit)
            LayoutOrbit(Placement.Calculate(209, new LogicalBounds(0, 0, 530, 960)));
        else
        {
            var below = Placement.CalculateBelow(209, 200, new LogicalBounds(0, 0, 530, 960));
            LayoutOrbit(new OrbitPlacement(265, below.FiveHourX, below.WeeklyX,
                below.FiveHourY, below.WeeklyY, below.Stacked));
        }
        var visual = (FrameworkElement)Content;
        visual.Measure(new System.Windows.Size(530, previewHeight));
        visual.Arrange(new System.Windows.Rect(0, 0, 530, previewHeight));
        visual.UpdateLayout();
        var bitmap = new RenderTargetBitmap(530, previewHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(path);
        encoder.Save(file);
    }

    private static string FormatReset(DateTimeOffset? reset)
    {
        if (reset is null) return "等待额度数据";
        var remaining = reset.Value - DateTimeOffset.Now;
        if (remaining <= TimeSpan.Zero) return "即将重置";
        if (remaining.TotalDays >= 1) return $"{(int)remaining.TotalDays} 天 {remaining.Hours} 小时后重置";
        if (remaining.TotalHours >= 1) return $"{(int)remaining.TotalHours} 小时 {remaining.Minutes} 分后重置";
        return $"{Math.Max(1, (int)Math.Ceiling(remaining.TotalMinutes))} 分后重置";
    }

    private void MakeClickThrough()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        var style = GetWindowLongPtr(hwnd, -20).ToInt64();
        SetWindowLongPtr(hwnd, -20, new IntPtr(style | 0x20 | 0x80 | 0x08000000));
        HwndSource.FromHwnd(hwnd)?.AddHook((IntPtr sourceHwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled) =>
        {
            if (message != 0x0084) return IntPtr.Zero;
            handled = true;
            return new IntPtr(-1);
        });
    }

    [StructLayout(LayoutKind.Sequential)] private struct CursorPoint { public int X; public int Y; }
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
    private delegate bool EnumWindowsProc(IntPtr window, IntPtr parameter);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out CursorPoint point);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int virtualKey);
    [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr window);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr window, out NativeRect bounds);
    [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr window, uint command);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool SetWindowPos(IntPtr hwnd, IntPtr insertAfter, int x, int y, int width, int height, uint flags);

    private readonly record struct PetState(bool Open, double X, double Y, double ScreenX, double ScreenY, double ScreenWidth, double ScreenHeight);
}
