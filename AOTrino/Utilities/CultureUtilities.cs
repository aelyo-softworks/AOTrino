namespace AOTrino.Utilities;

public static class CultureUtilities
{
    // the languages the user chose in Windows, in the order they chose them, which is more than a browser knows.
    // navigator.language reports the one locale the WebView was started with, not the ordered list,
    // and not the fallbacks that follow it. an app that wants to serve someone in their second language
    // rather than in its own default has to be able to read the whole list, and only a native process can.
    //
    // an empty result is a normal answer, it means Windows had nothing to say, and the caller falls back.
    public static IReadOnlyList<string> GetUserPreferredUILanguages()
    {
        var length = 0u;
        if (!DirectNFunctions.GetUserPreferredUILanguages(DirectNConstants.MUI_LANGUAGE_NAME, out _, PWSTR.Null, ref length) || length == 0)
            return [];

        // the call is made twice on purpose, the first asks how long the answer is and the second reads it.
        var buffer = Marshal.AllocHGlobal((int)length * 2);
        try
        {
            if (!DirectNFunctions.GetUserPreferredUILanguages(DirectNConstants.MUI_LANGUAGE_NAME, out var count, new PWSTR(buffer), ref length))
                return [];

            // a double null terminated list of null terminated names.
            var names = new List<string>();
            var offset = 0;
            for (var i = 0u; i < count; i++)
            {
                var name = Marshal.PtrToStringUni(buffer + offset * 2);
                if (string.IsNullOrEmpty(name))
                    break;

                names.Add(name);
                offset += name.Length + 1;
            }

            return names;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }
}
