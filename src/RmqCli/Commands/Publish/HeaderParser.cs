using System.Globalization;

namespace RmqCli.Commands.Publish;

/// <summary>
/// Parses header strings in format "key:value" into a dictionary.
/// </summary>
public static class HeaderParser
{
    /// <summary>
    /// Parses header strings in format "key:value" into a dictionary.
    /// </summary>
    /// <param name="headers">Array of header strings</param>
    /// <returns>Dictionary of parsed headers</returns>
    /// <exception cref="ArgumentException">Thrown when header format is invalid</exception>
    public static Dictionary<string, object> Parse(string[] headers)
    {
        var result = new Dictionary<string, object>();

        foreach (var header in headers)
        {
            var colonIndex = header.IndexOf(':');
            if (colonIndex == -1)
            {
                throw new ArgumentException(
                    $"Invalid header format: '{header}'. Expected 'key:value'.");
            }

            var key = header[..colonIndex].Trim();
            var value = header[(colonIndex + 1)..].Trim();

            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException(
                    $"Invalid header format: '{header}'. Key cannot be empty.");
            }

            // Try to detect type (number, boolean, or string)
            result[key] = DetectType(value);
        }

        return result;
    }

    private static object DetectType(string value)
    {
        // Try boolean
        if (bool.TryParse(value, out var boolValue))
            return boolValue;

        // Try integer (culture-invariant)
        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var longValue))
            return longValue;

        // Try decimal (culture-invariant)
        if (double.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var doubleValue))
            return doubleValue;

        // Default to string
        return value;
    }
}
