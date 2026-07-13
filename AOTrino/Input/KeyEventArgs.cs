namespace AOTrino.Input;

public class KeyEventArgs(VIRTUAL_KEY vk, uint states, string? character) : HandledEventArgs
{
    public VIRTUAL_KEY Key { get; } = vk;
    public string? WebCode { get; set; }
    public string? Character { get; } = character;
    public int ScanCode { get; } = (int)(states >> 16 & 0xF);
    public int RepeatCount { get; } = (int)(states & 0xFF);
    public bool IsUp { get; } = (states & 0x80000000) != 0; // bit 31
    public bool IsDown => !IsUp;
    public bool IsExtendedKey { get; } = (states & 0x1000) != 0;
    public bool WasDown { get; } = (states & 0x40000000) != 0;
    public bool WithoutAnyModifiers => !WithShift && !WithControl && !WithMenu;
    public virtual bool WithShift { get; set; } = VIRTUAL_KEY.VK_SHIFT.IsPressed();
    public virtual bool WithControl { get; set; } = VIRTUAL_KEY.VK_CONTROL.IsPressed();
    public virtual bool WithMenu { get; set; } = VIRTUAL_KEY.VK_MENU.IsPressed();

    public override string ToString()
    {
        var s = IsUp ? "UP" : "DOWN";
        if (IsDown)
        {
            s += ",WD=" + WasDown;
        }

        if (IsExtendedKey)
        {
            s += ",EX=" + IsExtendedKey;
        }

        if (RepeatCount != 1)
        {
            s += ",RC=" + RepeatCount;
        }
        return s + ",SC=" + ScanCode + ",VK='" + Key + "',CH='" + Character + "'";
    }
}
