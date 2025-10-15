using System.Globalization;
using System.Text;

namespace RmqCli.Shared;

public static class OutputUtilities
{
    public static string ToSizeString(double l)
    {
        const long kb = 1024;
        const long mb = kb * 1024;
        const long gb = mb * 1024;
        double size = l;
        switch (l)
        {
            case >= gb:
                size = Math.Round(l / gb, 2);
                return $"{size.ToString(CultureInfo.InvariantCulture)} GB";
            case >= mb:
                size = Math.Round(l / mb, 2);
                return $"{size.ToString(CultureInfo.InvariantCulture)} MB";
            case >= kb:
                size = Math.Round(l / kb, 2);
                return $"{size.ToString(CultureInfo.InvariantCulture)} KB";
            default:
                return $"{size.ToString(CultureInfo.InvariantCulture)} bytes";
        }
    }

    public static int GetDigitCount(int number)
    {
        number = Math.Abs(number);
        if (number < 10) return 1;
        if (number < 100) return 2;
        if (number < 1_000) return 3;
        if (number < 10_000) return 4;
        if (number < 100_000) return 5;
        if (number < 1_000_000) return 6;
        if (number < 10_000_000) return 7;
        if (number < 100_000_000) return 8;
        if (number < 1_000_000_000) return 9;

        return 10;
    }

    public static string GetMessageCountString(long count, bool noColor = true)
    {
        var countString = noColor ? count.ToString(CultureInfo.InvariantCulture) : $"[orange1]{count.ToString(CultureInfo.InvariantCulture)}[/]";
        var pluralSuffix = count == 1 ? string.Empty : "s";
        return $"{countString} message{pluralSuffix}";
    }

    public static string GetElapsedTimeString(TimeSpan elapsed)
    {
        var sb = new StringBuilder();
        if (elapsed.Days > 0)
            sb.Append($"{elapsed.Days}d ");
        if (elapsed.Hours > 0)
            sb.Append($"{elapsed.Hours}h ");
        if (elapsed.Minutes > 0)
            sb.Append($"{elapsed.Minutes}m ");
        if (elapsed.Seconds > 0)
            sb.Append($"{elapsed.Seconds}s ");
        sb.Append($"{elapsed.Milliseconds}ms");
        return sb.ToString().Trim();
    }
}