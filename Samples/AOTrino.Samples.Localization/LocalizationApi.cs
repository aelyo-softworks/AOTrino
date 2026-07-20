namespace AOTrino.Samples.Localization;

// what the page can ask about languages. four members, and only one of them is the actual translation.
//
// GetCatalog is deliberately the whole catalog rather than one string at a time. every bridge call is a promise,
// so a translate function that crossed the bridge could not be called from a render, and one that crossed it
// synchronously would stop the renderer once per string. this crosses once per language change instead.
[System.Runtime.InteropServices.Marshalling.GeneratedComClass]
public partial class LocalizationApi(MainWindow window) : DispatchObject
{
    // every string for the language in use, as one object the page keeps and looks up in.
#pragma warning disable CA1822 // Mark members as static
    public string GetCatalog() => Program._strings.GetCatalogJson();

    // the languages this app actually ships, with the name each one calls itself,
    // which is what belongs in a language picker rather than an English name for a language someone does not read.
    public string GetCultures()
    {
        // gathered into a list and handed to JsonArray's params constructor, never added one by one:
        // JsonArray.Add<T>() is generic, and creating a JsonValue of a non primitive type that way needs code
        // generated at runtime, which a Native AOT build has no way to do. it compiles, then fails where it ships.
        var cultures = new List<JsonNode?>();
        foreach (var culture in Program._strings.AvailableCultures)
        {
            cultures.Add(new JsonObject
            {
                ["name"] = culture.Name,
                ["nativeName"] = Capitalize(culture.NativeName, culture),
                ["isCurrent"] = culture.Name.EqualsIgnoreCase(Program._strings.Current.Name),
            });
        }

        return new JsonArray([.. cultures]).ToJsonString();
    }

    // what Windows was asked for, in order, and what this app could do about it.
    // the page shows both so the negotiation is visible rather than implied.
    public string GetPreferred() => new JsonArray([.. CultureUtilities.GetUserPreferredUILanguages()]).ToJsonString();

    public string GetCurrent() => Program._strings.Current.Name;

    // the picker. nothing restarts, the page asks for the catalog again and redraws,
    // and the window renames itself from the same resources.
    public void SetCulture(string name)
    {
        Program._strings.Current = CultureInfo.GetCultureInfo(name);
        window.ApplyTitle();
    }

    public void Close() => window.Close();

    // NativeName is lower case for most languages, "français", which is correct in a sentence
    // and wrong on its own in a list.
    //
    // upper cased with the rules of the language being named, never the current one.
    // Turkish is why: its i upper cases to İ, so capitalizing "italiano" on a Turkish machine with the current
    // culture produces "İtaliano", a word in no language at all.
    private static string Capitalize(string text, CultureInfo culture) => string.IsNullOrEmpty(text) ? text : char.ToUpper(text[0], culture) + text[1..];
#pragma warning restore CA1822 // Mark members as static
}
