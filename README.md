# AOTrino
Electron-like desktop apps on .NET AOT + WebView2, single-exe, Windows x64, front-end-framework-agnostic (React/Fluent layers optional).

## Documentation
- [Security model](docs/SECURITY.md) local-first by default, `NavigationMode`, and the one rule AOTrino enforces for you.
- [The bridge](docs/BRIDGE.md) how JS calls .NET and back: host objects, what crosses, async results, exceptions, and the escape hatch.
- [Front end](docs/FRONTEND.md) hand-written pages need nothing; the optional `@aotrino/client` types the bridge for React/TypeScript apps, without a registry.
- [Theming](docs/THEMING.md) light/dark that follows Windows, picked from the caption and remembered — what the `FluentUI` samples get for two lines, and how to change it.
