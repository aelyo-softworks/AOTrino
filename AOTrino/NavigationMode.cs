namespace AOTrino;

// where an AOTrino window is allowed to navigate. this governs navigation only.
// it makes no security claim (it does not touch web security, file access, or host-object exposure).
public enum NavigationMode
{
    // default: only the app's own content loads in the window. off-app web navigations are cancelled and
    // handed to the user's default browser instead.
    Local,

    // the window is a browser: navigation to any origin is allowed.
    Web,
}
