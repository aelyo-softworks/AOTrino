namespace AOTrino.Input;

public class MouseEventArgs(POINT pt, MODIFIERKEYS_FLAGS vk)
    : HandledEventArgs
{
    public MODIFIERKEYS_FLAGS Keys { get; } = vk;
    public PointerEventArgs? SourcePointerEvent { get; internal set; } // will be null if EnableMouseInPointer was not called
    public int X { get; } = pt.x;
    public int Y { get; } = pt.y;
    public POINT Point { get; } = pt;

    public override string ToString() => "X=" + X + ",Y=" + Y + ",VK=" + Keys;
}
