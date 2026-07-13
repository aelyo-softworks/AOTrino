namespace AOTrino;

public class PointerUpdateEventArgs(uint pointerId, POINT pt, POINTER_MESSAGE_FLAGS flags)
    : PointerPositionEventArgs(pointerId, pt)
{
    public POINTER_MESSAGE_FLAGS Flags { get; } = flags;
    public bool IsInRange => Flags.HasFlag(POINTER_MESSAGE_FLAGS.POINTER_MESSAGE_FLAG_INRANGE);
    public bool IsInContact => Flags.HasFlag(POINTER_MESSAGE_FLAGS.POINTER_MESSAGE_FLAG_INCONTACT);
    public bool IsPrimary => Flags.HasFlag(POINTER_MESSAGE_FLAGS.POINTER_MESSAGE_FLAG_PRIMARY);

    public override string ToString() => base.ToString() + ",Flags=" + Flags;
}
