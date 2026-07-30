namespace AOTrino.Samples.ShellCommander;

// one shell the machine can run, offered to the page's console picker. Key selects it back on Start, Name is shown.
public sealed record TerminalShell(string Key, string Name);
