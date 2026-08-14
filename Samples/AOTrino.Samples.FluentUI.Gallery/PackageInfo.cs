namespace AOTrino.Samples.FluentUI.Gallery;

// one row of the Packages page. Current is the version loaded (nuget) or resolved (npm) right now, Declared is what
// the project asked for, RegistryId is the id the "Check for updates" button looks up (null: not checkable, e.g. the runtime).
public sealed record PackageInfo(string Ecosystem, string Name, string Current, string? Declared, string? RegistryId);
