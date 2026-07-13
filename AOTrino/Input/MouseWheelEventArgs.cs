namespace AOTrino;

public class MouseWheelEventArgs(POINT pt, MODIFIERKEYS_FLAGS vk, int delta, Orientation orientation)
    : MouseEventArgs(pt, vk)
{
    public int Delta { get; } = delta / Constants.WHEEL_DELTA;
    public Orientation Orientation { get; } = orientation;

    public override string ToString() => base.ToString() + ",DE=" + Delta + ",O=" + Orientation;
}
