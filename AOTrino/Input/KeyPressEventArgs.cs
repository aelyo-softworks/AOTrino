namespace AOTrino.Input;

public class KeyPressEventArgs : HandledEventArgs
{
    public KeyPressEventArgs(char[] characters)
    {
        ArgumentNullException.ThrowIfNull(characters);
        if (characters.Length == 0)
            throw new ArgumentException(null, nameof(characters));

        WithShift = VIRTUAL_KEY.VK_SHIFT.IsPressed();
        WithControl = VIRTUAL_KEY.VK_CONTROL.IsPressed();
        WithMenu = VIRTUAL_KEY.VK_MENU.IsPressed();
        UTF32Character = characters[0];
        Characters = characters;
    }

    public KeyPressEventArgs(uint character)
    {
        WithShift = VIRTUAL_KEY.VK_SHIFT.IsPressed();
        WithControl = VIRTUAL_KEY.VK_CONTROL.IsPressed();
        WithMenu = VIRTUAL_KEY.VK_MENU.IsPressed();
        UTF32Character = character;

        if (character > 0xFFFF)
        {
            // http://unicode.org/faq/utf_bom.html#35
            Characters = new char[2];
            Characters[0] = (char)(0xD800 + (character >> 10) - (0x10000 >> 10));
            Characters[1] = (char)(0xDC00 + (character & 0x3FF));
        }
        else
        {
            Characters = [(char)character];
        }
    }

    public uint UTF32Character { get; }
    public char UTF16Character => Characters[0];
    public char[] Characters { get; }
    public virtual bool WithShift { get; set; }
    public virtual bool WithControl { get; set; }
    public virtual bool WithMenu { get; set; }

    public override string ToString() => "C:" + UTF32Character + " CH:'" + UTF16Character + "'";
}
