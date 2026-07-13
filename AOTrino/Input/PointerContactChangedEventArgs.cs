namespace AOTrino.Input;

public class PointerContactChangedEventArgs(uint pointerId, POINT pt, POINTER_MESSAGE_FLAGS flags, bool up)
    : PointerUpdateEventArgs(pointerId, pt, flags)
{
    public bool IsUp { get; } = up;
    public bool IsDown => !IsUp;
    public bool IsDoubleClick { get; internal set; }

    public override string ToString() => base.ToString() + ",UP=" + IsUp;
}
