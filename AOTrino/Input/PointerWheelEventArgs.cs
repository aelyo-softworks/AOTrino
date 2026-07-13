namespace AOTrino;

public class PointerWheelEventArgs(uint pointerId, POINT pt, int delta, Orientation orientation)
    : PointerPositionEventArgs(pointerId, pt)
{
    public int Delta { get; } = delta / Constants.WHEEL_DELTA;
    public Orientation Orientation { get; } = orientation;

    public override string ToString() => base.ToString() + ",DE=" + Delta + ",O=" + Orientation;
}
