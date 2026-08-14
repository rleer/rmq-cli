using System.Globalization;

namespace Rmq;

/// <summary>
/// Parses repeated --header key:value flags. Values are typed by inspection, because
/// AMQP headers are typed and "3" as a string is not the same header as 3 as a long.
/// </summary>
public static class HeaderParser
{
    /// <exception cref="ArgumentException">A header is missing its colon or its key.</exception>
    public static Dictionary<string, object> Parse(IEnumerable<string> headers)
    {
        var result = new Dictionary<string, object>();

        foreach (var header in headers)
        {
            var colon = header.IndexOf(':');
            if (colon < 0)
            {
                throw new ArgumentException($"Invalid header '{header}': expected 'key:value'");
            }

            var key = header[..colon].Trim();
            if (key.Length == 0)
            {
                throw new ArgumentException($"Invalid header '{header}': key cannot be empty");
            }

            result[key] = DetectType(header[(colon + 1)..].Trim());
        }

        return result;
    }

    private static object DetectType(string value)
    {
        if (bool.TryParse(value, out var boolean))
        {
            return boolean;
        }

        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer))
        {
            return integer;
        }

        if (double.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var real))
        {
            return real;
        }

        return value;
    }
}
