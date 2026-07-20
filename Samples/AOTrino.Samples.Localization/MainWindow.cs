namespace AOTrino.Samples.Localization;

[System.Runtime.InteropServices.Marshalling.GeneratedComClass]
public partial class MainWindow : AOTrinoWindow
{
    public MainWindow()
        : base(Program._strings.GetString("Title"))
    {
    }

    protected override void RegisterHostObjects() => AddHostObject("localization", new LocalizationApi(this));

    // the caption is a string like any other, so it comes from the same resources the page does,
    // and it changes with them. a window whose title stays in one language while its contents change
    // is the usual sign of the front end and the host keeping separate copies of the same text.
    public void ApplyTitle() => Text = Program._strings.GetString("Title");
}
