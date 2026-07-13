namespace AOTrino;

public class MouseButtonEventArgs(POINT pt, MODIFIERKEYS_FLAGS vk, MouseButton button)
    : MouseEventArgs(pt, vk)
{
    public MouseButton Button { get; } = button;
    public virtual uint RepeatDelay { get; set; } // if > 0, for mouse button down events only
    public virtual uint RepeatInterval { get; set; } // if > 0, for mouse button down events only
}
