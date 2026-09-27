namespace PetUsageOverlay;

internal readonly record struct LogicalBounds(double Left, double Top, double Right, double Bottom);
internal readonly record struct OrbitPlacement(
    double RingCenterX, double FiveHourX, double WeeklyX,
    double FiveHourY, double WeeklyY, bool Stacked);
internal readonly record struct BelowPlacement(
    double WindowTop, double FiveHourX, double WeeklyX,
    double FiveHourY, double WeeklyY, bool Stacked);
internal readonly record struct RingMotion(double Progress, double Velocity)
{
    public RingMotion Advance(double target, double elapsedSeconds)
    {
        if (elapsedSeconds <= 0) return this;
        const double frequency = 24;
        var displacement = Progress - target;
        var decay = Math.Exp(-frequency * elapsedSeconds);
        var correction = Velocity + frequency * displacement;
        return new RingMotion(
            target + (displacement + correction * elapsedSeconds) * decay,
            (Velocity - frequency * correction * elapsedSeconds) * decay);
    }
}

internal static class Placement
{
    // Codex's native avatar layout uses a 112 px mascot anchor.
    public const double MascotWidth = 112;
    // The visible sprite is padded within the mascot anchor.
    public const double SpriteCenterYOffset = 82;
    public const double RingRadius = 52;
    public const double RingCenterY = 67;
    private const double BadgeGap = 8;
    private const double ScreenInset = 8;
    private const double BelowGap = 8;
    private const double BadgeInsetY = 6;

    public static OrbitPlacement Calculate(double petX, LogicalBounds display)
    {
        var width = display.Right - display.Left;
        var center = petX + MascotWidth / 2 - display.Left;
        var left = center - RingRadius - BadgeGap - QuotaBadge.Width;
        var right = center + RingRadius + BadgeGap;
        var maxBadgeX = Math.Max(ScreenInset, width - QuotaBadge.Width - ScreenInset);

        // Keep both labels readable when there is no room on one side.
        if (left < ScreenInset && right + QuotaBadge.Width > width - ScreenInset)
        {
            var stackedX = Math.Clamp(center - QuotaBadge.Width / 2, ScreenInset, maxBadgeX);
            return new OrbitPlacement(center, stackedX, stackedX, 10, 82, true);
        }
        if (left < ScreenInset)
        {
            var stackedX = Math.Clamp(right, ScreenInset, maxBadgeX);
            return new OrbitPlacement(center, stackedX, stackedX, 10, 82, true);
        }
        if (right + QuotaBadge.Width > width - ScreenInset)
        {
            var stackedX = Math.Clamp(left, ScreenInset, maxBadgeX);
            return new OrbitPlacement(center, stackedX, stackedX, 10, 82, true);
        }
        return new OrbitPlacement(center, left, right, 19, 19, false);
    }

    public static BelowPlacement CalculateBelow(double petX, double petY, LogicalBounds display)
    {
        var width = display.Right - display.Left;
        var center = petX + MascotWidth / 2 - display.Left;
        var totalWidth = QuotaBadge.Width * 2 + BelowGap;
        var stacked = width < totalWidth + ScreenInset * 2;
        var badgeWidth = stacked ? QuotaBadge.Width : totalWidth;
        var left = Math.Clamp(center - badgeWidth / 2, ScreenInset,
            Math.Max(ScreenInset, width - badgeWidth - ScreenInset));
        var contentHeight = stacked ? QuotaBadge.Height * 2 + BelowGap : QuotaBadge.Height;
        var windowHeight = contentHeight + BadgeInsetY * 2;
        // Place the cards just below Codex's action row. Near the desktop bottom,
        // place them directly above the mascot so they stay attached and visible.
        var belowTop = petY + UsageWindow.ToolbarBottomOffset - 4;
        var aboveTop = petY - windowHeight - 8;
        var top = belowTop + windowHeight <= display.Bottom
            ? belowTop : Math.Max(display.Top, aboveTop);
        return new BelowPlacement(top, left,
            stacked ? left : left + QuotaBadge.Width + BelowGap,
            BadgeInsetY, stacked ? BadgeInsetY + QuotaBadge.Height + BelowGap : BadgeInsetY,
            stacked);
    }
}

internal static class PlacementChecks
{
    public static void Run()
    {
        var screen = new LogicalBounds(0, 0, 1707, 960);
        var center = Placement.Calculate(700, screen);
        if (center.RingCenterX != 756 || center.Stacked ||
            center.FiveHourX + QuotaBadge.Width + Placement.RingRadius + 8 != center.RingCenterX ||
            center.WeeklyX - Placement.RingRadius - 8 != center.RingCenterX)
            throw new Exception("环绕居中错误");

        var left = Placement.Calculate(0, screen);
        var right = Placement.Calculate(1640, screen);
        if (!left.Stacked || left.FiveHourX != left.WeeklyX ||
            !right.Stacked || right.FiveHourX != right.WeeklyX ||
            left.FiveHourX < 8 || right.WeeklyX + QuotaBadge.Width > screen.Right - 8 ||
            right.RingCenterX != 1696)
            throw new Exception("贴边叠放错误");

        var otherScreen = new LogicalBounds(1707, 0, 3414, 960);
        var secondMonitor = Placement.Calculate(2407, otherScreen);
        if (secondMonitor.RingCenterX != center.RingCenterX || secondMonitor.Stacked)
            throw new Exception("多屏定位错误");

        if (UsageWindow.OrbitTopOffset + Placement.RingCenterY != Placement.SpriteCenterYOffset)
            throw new Exception("底部宠物与圆环联动错误");

        var drag = new PetDragTracker();
        var time = DateTime.UtcNow;
        var saved = time;
        var ringPress = drag.Update(704, 402, true, 700, 320, screen, saved, time);
        var ringMove = drag.Update(900, 402, true, 700, 320, screen, saved, time.AddMilliseconds(16));
        if (ringPress != (700, 320) || ringMove != (700, 320))
            throw new Exception("圆环不应独立拖动");
        drag.Update(900, 402, false, 700, 320, screen, saved, time.AddMilliseconds(32));
        var petPress = drag.Update(756, 402, true, 700, 320, screen, saved, time.AddMilliseconds(48));
        var petMove = drag.Update(856, 452, true, 700, 320, screen, saved, time.AddMilliseconds(64));
        var released = drag.Update(856, 452, false, 700, 320, screen, saved, time.AddMilliseconds(80));
        var persisted = drag.Update(856, 452, false, 800, 370, screen,
            saved.AddMilliseconds(1), time.AddMilliseconds(96));
        if (petPress != (700, 320) || petMove != (800, 370) ||
            released != (800, 370) || persisted != (800, 370))
            throw new Exception("宠物拖动与保存位置交接错误");
        var edgeDrag = new PetDragTracker();
        edgeDrag.Update(1615, 820, true, 1559, 738, screen, saved, time);
        var edgeMove = edgeDrag.Update(1715, 920, true, 1559, 738, screen,
            saved, time.AddMilliseconds(16));
        if (edgeMove != (1595, 793))
            throw new Exception("宠物贴边拖动的圆环定位错误");

        var oneStep = new RingMotion(0, 0).Advance(1, .16);
        var smallSteps = new RingMotion(0, 0);
        for (var frame = 0; frame < 10; frame++) smallSteps = smallSteps.Advance(1, .016);
        var reversed = smallSteps.Advance(0, .016);
        for (var frame = 0; frame < 40; frame++) reversed = reversed.Advance(0, .016);
        if (Math.Abs(oneStep.Progress - smallSteps.Progress) > .0001 ||
            Math.Abs(oneStep.Velocity - smallSteps.Velocity) > .0001 ||
            Math.Abs(reversed.Progress) > .001)
            throw new Exception("圆环收缩动画的连续性错误");

        var belowCenter = Placement.CalculateBelow(700, 320, screen);
        var belowBottom = Placement.CalculateBelow(700, 799, screen);
        var belowRight = Placement.CalculateBelow(1595, 320, screen);
        var narrow = Placement.CalculateBelow(0, 320, new LogicalBounds(0, 0, 300, 960));
        if (belowCenter.Stacked || belowCenter.WindowTop <= 320 ||
            belowCenter.FiveHourX + QuotaBadge.Width + 8 != belowCenter.WeeklyX ||
            belowBottom.WindowTop >= 799 ||
            belowRight.WeeklyX + QuotaBadge.Width > screen.Right - 8 ||
            !narrow.Stacked || narrow.FiveHourX != narrow.WeeklyX ||
            narrow.WeeklyY <= narrow.FiveHourY)
            throw new Exception("下方双卡片贴边布局错误");

        if (!ProgressDetector.LooksLikeProgress("正在确认路径转义问题") ||
            !ProgressDetector.LooksLikeProgress("Preparing pet reference image") ||
            ProgressDetector.LooksLikeProgress("我正在写一段普通聊天内容"))
            throw new Exception("进度提示识别错误");
        var badge = new System.Windows.Rect(0, 330, 174, 62);
        if (!ProgressDetector.MatchesToastText("正在确认路径转义问题",
                new System.Windows.Rect(32, 338, 130, 24), badge) ||
            ProgressDetector.MatchesToastText("正在确认路径转义问题",
                new System.Windows.Rect(32, 400, 130, 24), badge))
            throw new Exception("进度提示避让区域错误");
        Console.WriteLine("layout checks passed");
    }
}
