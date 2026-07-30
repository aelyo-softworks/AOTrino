namespace AOTrino.Samples.ShellCommander;

// the header of a folder listing, returned at once by ListBegin so the page can show where it is and the way up while the rows still stream in.
// Token names the background enumeration the page then drains with ListDrain.
public sealed record ListStart(string Token, string Name, string Self, string? ParentId, string? Error);
