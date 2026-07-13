namespace AOTrino.Input;

public static class VirtualKeyExtensions
{
    public static bool IsPressed(this VIRTUAL_KEY vk, bool async = true) => (async ? DirectNFunctions.GetAsyncKeyState((int)vk) : DirectNFunctions.GetKeyState((int)vk)) < 0;
    public static bool IsDigit(this VIRTUAL_KEY vk) => (vk >= VIRTUAL_KEY.VK_0 && vk <= VIRTUAL_KEY.VK_9) || IsNumericKeypadKey(vk);
    public static bool IsLetter(this VIRTUAL_KEY vk) => vk >= VIRTUAL_KEY.VK_A && vk <= VIRTUAL_KEY.VK_Z;
    public static bool IsControlKey(this VIRTUAL_KEY vk) => vk == VIRTUAL_KEY.VK_CONTROL || vk == VIRTUAL_KEY.VK_LCONTROL || vk == VIRTUAL_KEY.VK_RCONTROL;
    public static bool IsShiftKey(this VIRTUAL_KEY vk) => vk == VIRTUAL_KEY.VK_SHIFT || vk == VIRTUAL_KEY.VK_LSHIFT || vk == VIRTUAL_KEY.VK_RSHIFT;
    public static bool IsAltKey(this VIRTUAL_KEY vk) => vk == VIRTUAL_KEY.VK_MENU || vk == VIRTUAL_KEY.VK_LMENU || vk == VIRTUAL_KEY.VK_RMENU;
    public static bool IsModifierKey(this VIRTUAL_KEY vk) => vk.IsControlKey() || vk.IsShiftKey() || vk.IsAltKey();
    public static bool IsFunctionKey(this VIRTUAL_KEY vk) => vk >= VIRTUAL_KEY.VK_F1 && vk <= VIRTUAL_KEY.VK_F24;
    public static bool IsNavigationKey(this VIRTUAL_KEY vk) => vk == VIRTUAL_KEY.VK_LEFT || vk == VIRTUAL_KEY.VK_RIGHT ||
               vk == VIRTUAL_KEY.VK_UP || vk == VIRTUAL_KEY.VK_DOWN ||
               vk == VIRTUAL_KEY.VK_HOME || vk == VIRTUAL_KEY.VK_END ||
               vk == VIRTUAL_KEY.VK_PRIOR || vk == VIRTUAL_KEY.VK_NEXT;
    public static bool IsWhitespaceKey(this VIRTUAL_KEY vk) => vk == VIRTUAL_KEY.VK_SPACE || vk == VIRTUAL_KEY.VK_TAB || vk == VIRTUAL_KEY.VK_RETURN;
    public static bool IsEnterKey(this VIRTUAL_KEY vk) => vk == VIRTUAL_KEY.VK_RETURN;
    public static bool IsBackspaceKey(this VIRTUAL_KEY vk) => vk == VIRTUAL_KEY.VK_BACK || vk == VIRTUAL_KEY.VK_DELETE;
    public static bool IsEscapeKey(this VIRTUAL_KEY vk) => vk == VIRTUAL_KEY.VK_ESCAPE;
    public static bool IsNumericKeypadKey(this VIRTUAL_KEY vk) => vk >= VIRTUAL_KEY.VK_NUMPAD0 && vk <= VIRTUAL_KEY.VK_NUMPAD9 ||
               vk == VIRTUAL_KEY.VK_ADD || vk == VIRTUAL_KEY.VK_SUBTRACT ||
               vk == VIRTUAL_KEY.VK_MULTIPLY || vk == VIRTUAL_KEY.VK_DIVIDE ||
               vk == VIRTUAL_KEY.VK_DECIMAL || vk == VIRTUAL_KEY.VK_NUMLOCK;

}
