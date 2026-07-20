namespace AOTrino.Samples.Blazor.DiskMap.Shared;

using System.Globalization;

// one formatter, used by the page to label a row and by the host to trace a total.
// small, and exactly the sort of thing that gets written twice, once per language, in a project with a JavaScript front end.
public static class SizeFormat
{
    private static readonly string[] _units = ["B", "KB", "MB", "GB", "TB", "PB"];

    public static string Bytes(long bytes)
    {
        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < _units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return string.Create(CultureInfo.InvariantCulture, $"{value.ToString(unit == 0 ? "F0" : "F1", CultureInfo.InvariantCulture)} {_units[unit]}");
    }

    public static string Duration(long milliseconds)
    {
        if (milliseconds < 1000)
            return string.Create(CultureInfo.InvariantCulture, $"{milliseconds} ms");

        var seconds = milliseconds / 1000.0;
        if (seconds < 90)
            return string.Create(CultureInfo.InvariantCulture, $"{seconds:F1} s");

        var minutes = seconds / 60;
        return string.Create(CultureInfo.InvariantCulture, $"{minutes:F1} min");
    }
}
