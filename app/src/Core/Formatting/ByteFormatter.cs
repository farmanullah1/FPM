namespace PCManager.Core.Formatting;

/// <summary>Human readable byte formatting shared by services and view models.</summary>
public static class ByteFormatter
{
    private static readonly string[] Units = { "B", "KB", "MB", "GB", "TB" };

    public static string Format(long bytes)
    {
        double value = Math.Abs((double)bytes);
        int unit = 0;
        while (value >= 1024 && unit < Units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        string sign = bytes < 0 ? "-" : string.Empty;
        return unit == 0
            ? $"{sign}{value:0} {Units[unit]}"
            : $"{sign}{value:0.##} {Units[unit]}";
    }

    public static string FormatRate(long bytesPerSecond) => $"{Format(bytesPerSecond)}/s";
}
