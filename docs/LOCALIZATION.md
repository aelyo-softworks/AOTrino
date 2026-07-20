# Localization

One place where a string is written, whatever the front end is made of.

An app with a web front end usually ends up with its text in two places, a `.resx` for the host and a JSON catalog
for the page, and then spends the rest of its life keeping them in step. AOTrino lets the .NET side own every string
and hands the front end a catalog, so a translation is added once and a key cannot exist on one side and not the
other.

None of this is compulsory. A front end that would rather own its own text, or that already uses an i18n library,
keeps working exactly as it did.

`Samples\AOTrino.Samples.Localization` is the working example, with a language picker and three languages,
one of which is deliberately half translated.

## What crosses the bridge

The whole catalog, once per language change, and never a call per string.

That is deliberate. Every host object call is a promise, so a `t()` that crossed the bridge could not be called from
a render function, and one that crossed it synchronously would stop the renderer once per string on screen. Fetching
the catalog once and looking up locally is neither of those, and the front end's translation code stays this small:

```js
const host = chrome.webview.hostObjects.localization;
let catalog = {};

function t(key) {
    return catalog[key] ?? key;
}

async function load() {
    catalog = JSON.parse(await host.GetCatalog());
    render();
}
```

## The three pieces

### The languages the user actually asked for

```csharp
CultureUtilities.GetUserPreferredUILanguages()   //   fr-FR, fr, en-GB, en
```

This is the ordered list from Windows, fallbacks included, and it is the piece a page cannot get for itself.
`navigator.language` reports the one locale the WebView was started with, not the ordering the user chose and not
what they want when their first language is unavailable. It is also on `SystemInfo` as `culture.preferredUI`,
next to `current`, `currentUI` and `installedUI`, so an app already reading system information gets it there.

### The catalog

```csharp
var strings = new Localization(new ResourceManager("MyApp.Strings", typeof(Program).Assembly), "en", "fr");
```

The cultures are declared rather than discovered, because discovering them means probing culture after culture for a
resource set that is nearly always absent, and .NET has several hundred to probe. The first is the language written
into the main assembly, and the fallback for anything a translation is missing.

* `AvailableCultures` is what the app ships, for a language picker.
* `Resolve()` matches the user's ordered list against that, exactly first, then by language, so someone asking for
  `fr-CA` is served `fr` when `fr` is what exists.
* `Current` is the one in use, and setting it is all a language picker does. It also sets `CurrentUICulture`, so
  dates, numbers and the host's own strings agree with the page.
* `GetCatalogJson()` is every string for a culture, resolved key by key, so a partial translation shows the language
  it has and the fallback language everywhere else.

  It is built one key at a time on purpose. Handing over a resource set does not fall back: `GetResourceSet` with
  `tryParents` returns the parent's set only when the culture has none of its own, so a culture with a partial
  translation returns exactly the keys it translated and the rest are simply absent, which reaches the page as a
  missing key and renders the key name where the text should be. `GetString` does fall back, per key, so the catalog
  asks it once per key. `Strings.de.resx` in the sample is deliberately incomplete to keep this honest.

### Looking up in the page

`t(key)` over the loaded object, and nothing more. See the sample, where a `data-t` attribute names the key and one
loop fills every element in the document.

## Adding a language

1. Copy `Strings.resx` to `Strings.fr.resx` and translate the values, leaving the names alone.
2. Add the culture to the `Localization` constructor.

A translation does not have to be finished to be shipped. Whatever it does not have comes from the fallback language,
key by key, so a language can be added as soon as any of it is translated.

That is all. The SDK compiles `Strings.fr.resx` into a satellite assembly, and a Native AOT publish compiles those
into the executable along with everything else, so a translated app is still the one file. The culture folders you
see next to the exe in a normal build, `fr\`, `de\`, are a build output, not what you ship.

Set `<NeutralLanguage>en</NeutralLanguage>` in the project, naming whichever language the untranslated `.resx` is
written in. Without it the runtime looks for a satellite for that language too, which will never exist.

## What this is not

There is no plural rule here, no gender, no ordinal and no message format, and that is on purpose. Those need CLDR
data and real linguistic tables, Polish has four plural categories for cardinals and Arabic six, and getting them
wrong produces text that reads as broken to the people who speak the language.

If you need them, keep the single source of truth and hand this catalog to a library that does it properly:

```js
i18next.init({ lng: await host.GetCurrent(), resources: { [lng]: { translation: JSON.parse(await host.GetCatalog()) } } });
```

The strings still live in one `.resx`, and the pluralization is done by something that has the tables for it.

## Blazor, where it gets simpler

Everything above exists because a front end written in something other than C# cannot read a `.resx`. A Blazor front
end can, and when it shares a source project with its host, as `Blazor.DiskMap` does, the answer is better than the
catalog: put the `.resx` in the shared project.

Both sides then compile the same resources, the way they already compile the same DTOs and the same serializer. The
page reads its strings directly, with no catalog to fetch, nothing to keep in step, and no way for a key to exist on
one side and not the other, because there are not two sides to the strings any more.

Pin the logical name, or this does not work. A shared project contributes items, and an embedded resource takes its
name from the **consuming** project's root namespace, so the same `.resx` arrives as two different names and one
`ResourceManager` cannot name both:

```
host: AOTrino.Samples.Blazor.DiskMap.Probe.resources
wasm: AOTrino.Samples.Blazor.DiskMap.Wasm.Probe.resources
```

Saying it explicitly in the `.projitems` gives both assemblies the same name, which is what lets one line of code
find the resources from either side:

```xml
<EmbeddedResource Include="$(MSBuildThisFileDirectory)Strings.resx">
  <LogicalName>MyApp.Shared.Strings.resources</LogicalName>
</EmbeddedResource>
```

What still crosses the bridge is the *choice*, not the text. Only the host can read the ordered list of languages the
user asked Windows for, so it resolves the culture and tells the page which one to use, and the page sets its own
`CurrentUICulture` from that. One string crosses at startup and one more when someone picks a language.

## One thing worth knowing

Blazor WebAssembly loads its own satellite assemblies at runtime, so a Blazor front end that carries its own
resources pays a download for a culture the first time it needs one. That is a property of the wasm runtime rather
than of anything here, and it is another reason the shared project is the better arrangement when it is available.
