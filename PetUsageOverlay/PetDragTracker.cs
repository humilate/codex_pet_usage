namespace PetUsageOverlay;

// The saved avatar position is updated after a drag. Follow the actual pointer
// only while a press that began on the mascot is held, then hand back to Codex.
internal sealed class PetDragTracker
{
    private bool wasPressed;
    private bool dragging;
    private bool waitingForSavedPosition;
    private double pointerDownX;
    private double pointerDownY;
    private double petDownX;
    private double petDownY;
    private double predictedX;
    private double predictedY;
    private DateTime releasedUtc;
    private DateTime stateStampAtRelease;

    public (double X, double Y) Update(double cursorX, double cursorY, bool pressed,
        double petX, double petY, LogicalBounds display, DateTime stateStamp, DateTime now)
    {
        if (waitingForSavedPosition && ((stateStamp != stateStampAtRelease &&
            (Math.Abs(petX - petDownX) > 2 || Math.Abs(petY - petDownY) > 2)) ||
            (Math.Abs(petX - predictedX) <= 5 && Math.Abs(petY - predictedY) <= 5) ||
            now - releasedUtc > TimeSpan.FromMilliseconds(1200)))
            waitingForSavedPosition = false;

        if (pressed && !wasPressed)
        {
            var startX = waitingForSavedPosition ? predictedX : petX;
            var startY = waitingForSavedPosition ? predictedY : petY;
            waitingForSavedPosition = false;
            dragging = IsOnMascot(cursorX, cursorY, startX, startY);
            if (dragging)
            {
                pointerDownX = cursorX;
                pointerDownY = cursorY;
                petDownX = startX;
                petDownY = startY;
                predictedX = startX;
                predictedY = startY;
            }
        }

        if (pressed && dragging)
        {
            predictedX = Math.Clamp(petDownX + cursorX - pointerDownX,
                display.Left, Math.Max(display.Left, display.Right - Placement.MascotWidth));
            predictedY = Math.Clamp(petDownY + cursorY - pointerDownY,
                display.Top, Math.Max(display.Top, display.Bottom - UsageWindow.ToolbarBottomOffset));
        }
        else if (!pressed && wasPressed && dragging)
        {
            dragging = false;
            waitingForSavedPosition = Math.Abs(predictedX - petDownX) > 2 ||
                Math.Abs(predictedY - petDownY) > 2;
            releasedUtc = now;
            stateStampAtRelease = stateStamp;
        }
        wasPressed = pressed;

        if (dragging) return (predictedX, predictedY);
        if (waitingForSavedPosition) return (predictedX, predictedY);
        return (petX, petY);
    }

    public void Reset()
    {
        wasPressed = false;
        dragging = false;
        waitingForSavedPosition = false;
    }

    private static bool IsOnMascot(double cursorX, double cursorY, double petX, double petY)
    {
        // The visible sprite is inside this ellipse; the surrounding ring and
        // quota cards cannot start a drag of their own.
        var dx = (cursorX - petX - Placement.MascotWidth / 2) / 49;
        var dy = (cursorY - petY - Placement.SpriteCenterYOffset) / 52;
        return dx * dx + dy * dy <= 1;
    }
}
