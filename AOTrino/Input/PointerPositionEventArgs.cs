namespace AOTrino;

public abstract class PointerPositionEventArgs(uint pointerId, POINT pt) : PointerEventArgs(pointerId)
{
    public int X { get; } = pt.x;
    public int Y { get; } = pt.y;
    public POINT Point { get; } = pt;

    public override string ToString() => base.ToString() + ",X=" + X + ",Y=" + Y;
}
