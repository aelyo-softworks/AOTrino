namespace AOTrino;

// the app's strings, kept in .NET resources and handed to the front end one culture at a time.
//
// the point is to have one place where a string is written, whatever the front end is made of.
// a page, a React component and a Razor page all read the same .resx, so a translation is added once
// and a key can never exist on one side and not the other.
//
// what crosses is a whole catalog, not a call per string. every bridge call is a promise, so a translate function
// that went over the bridge could not be called from a render, and one that went synchronously would block the
// renderer once per string. fetching the catalog once and looking up locally is neither.
//
// deliberately not a translation engine. there is no plural rule here, no gender and no message format,
// because those need CLDR data and are solved well by the i18n libraries already on npm.
// a front end that needs them feeds this catalog into one of those, which stays perfectly possible.
public class Localization
{
    private readonly ResourceManager _resources;
    private readonly List<CultureInfo> _available = [];
    private CultureInfo _current;

    // 'cultures' is what the app actually ships, in the order it prefers them, the first being the fallback
    // that lives in the main assembly. the rest are satellite assemblies, which survive a Native AOT publish.
    //
    // they are declared rather than discovered because discovering them means probing culture after culture
    // for a resource set that is nearly always absent, and .NET has several hundred of them to probe.
    public Localization(ResourceManager resources, params string[] cultures)
    {
        ArgumentNullException.ThrowIfNull(resources);
        _resources = resources;

        foreach (var name in cultures ?? [])
        {
            try { _available.Add(CultureInfo.GetCultureInfo(name)); }
            catch (CultureNotFoundException) { /* a name that is not a culture is not worth failing a startup over. */ }
        }

        if (_available.Count == 0)
        {
            _available.Add(CultureInfo.InvariantCulture);
        }

        _current = Resolve();
    }

    // the cultures this app has strings for.
    public IEnumerable<CultureInfo> AvailableCultures => _available;

    // the one in use. setting it is what a language picker does, and nothing needs restarting.
    public CultureInfo Current
    {
        get => _current;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _current = Match(value) ?? _current;

            // so anything formatting a date, a number or a string on the .NET side agrees with the page.
            CultureInfo.CurrentUICulture = _current;
        }
    }

    // what the user asked Windows for, in order, which is more than a browser gets to know.
    // navigator.language reports the locale the WebView was started with, not the ordered list of languages
    // the user actually chose, and not the fallbacks they chose after it.
    public static IEnumerable<string> PreferredCultures
    {
        get
        {
            var seen = new List<string>();
            foreach (var name in CultureUtilities.GetUserPreferredUILanguages())
            {
                if (!seen.Contains(name, StringComparer.OrdinalIgnoreCase))
                {
                    seen.Add(name);
                }
            }

            // the thread's own UI culture last, it is where .NET landed and is a reasonable final answer.
            var current = CultureInfo.CurrentUICulture.Name;
            if (current.Length > 0 && !seen.Contains(current, StringComparer.OrdinalIgnoreCase))
            {
                seen.Add(current);
            }

            return seen;
        }
    }

    // the best culture this app can actually serve for what the user asked for.
    // an exact match wins, then the neutral parent, so a user asking for fr-CA gets fr when that is what is shipped.
    public CultureInfo Resolve() => Resolve(PreferredCultures);

    // the same against a list you supply, for an app that remembers a choice in its settings
    // or takes one on the command line, which should win over what Windows asked for.
    public CultureInfo Resolve(IEnumerable<string> preferredCultures)
    {
        ArgumentNullException.ThrowIfNull(preferredCultures);
        foreach (var preferred in preferredCultures)
        {
            CultureInfo culture;
            try { culture = CultureInfo.GetCultureInfo(preferred); }
            catch (CultureNotFoundException) { continue; }

            var match = Match(culture);
            if (match != null)
                return match;
        }

        return _available[0];
    }

    // every string for a culture, as JSON, which is what the front end loads once and looks up in.
    //
    // built key by key rather than by handing over a resource set, because a resource set does not fall back.
    // GetResourceSet with tryParents returns the parent's set only when the culture has none of its own:
    // a culture with a partial translation returns exactly the keys it translated, and the rest are simply absent,
    // which reaches the page as a missing key and renders the key name where the text should be.
    // GetString does fall back, per key, so asking it once per key is what makes a half finished translation
    // show the language it has and the fallback language everywhere else.
    //
    // the fallback language decides which keys exist, being the one the app is written in
    // and the only one guaranteed to have all of them.
    public string GetCatalogJson(CultureInfo? culture = null)
    {
        culture ??= _current;
        var entries = new SortedDictionary<string, string>(StringComparer.Ordinal);
        try
        {
            var keys = _resources.GetResourceSet(_available[0], createIfNotExists: true, tryParents: true);
            if (keys != null)
            {
                foreach (DictionaryEntry entry in keys)
                {
                    if (entry.Key is string key)
                    {
                        entries[key] = _resources.GetString(key, culture) ?? entry.Value as string ?? key;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            AOTrinoApplication.Current?.TraceWarning($"The resources for '{culture.Name}' could not be read: {ex.Message}");
        }

        return JsonSerializer.Serialize(entries, LocalizationJsonContext.Default.SortedDictionaryStringString);
    }

    // one string, for the .NET side of the app, which reads its own resources the ordinary way.
    public string GetString(string key) => _resources.GetString(key, _current) ?? key;

    private CultureInfo? Match(CultureInfo culture)
    {
        foreach (var available in _available)
        {
            if (available.Name.EqualsIgnoreCase(culture.Name))
                return available;
        }

        // fr-CA falls back to fr, and a neutral request matches the first specific culture of that language.
        foreach (var available in _available)
        {
            if (available.TwoLetterISOLanguageName.EqualsIgnoreCase(culture.TwoLetterISOLanguageName))
                return available;
        }

        return null;
    }
}
