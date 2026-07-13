namespace AOTrino;

public class PointerEnterEventArgs(uint pointerId, POINT pt, POINTER_MESSAGE_FLAGS flags)
    : PointerUpdateEventArgs(pointerId, pt, flags)
{
}
