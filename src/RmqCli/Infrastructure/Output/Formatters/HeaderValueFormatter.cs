namespace RmqCli.Infrastructure.Output.Formatters;

/// <summary>
/// Provides formatting for RabbitMQ message header values with support for nested structures.
/// </summary>
public static class HeaderValueFormatter
{
    /// <summary>
    /// Formats a header value with smart handling of dictionaries, arrays, and primitives.
    /// </summary>
    /// <param name="value">The value to format</param>
    /// <param name="indent">Current indentation level</param>
    /// <returns>Formatted string representation</returns>
    public static string FormatValue(object value, int indent = 0)
    {
        return value switch
        {
            null => "-",

            // Handle byte arrays (binary data) - must come before other array handling
            byte[] bytes => $"<binary data: {bytes.Length} bytes>",

            // Handle dictionaries (nested objects)
            IDictionary<string, object> dict when dict.Count == 0 => "{}",
            IDictionary<string, object> dict => FormatDictionary(dict, indent),

            // Handle strings - preserve binary data markers (before arrays since string is IEnumerable)
            string str when str.StartsWith("<binary data:") => str,
            string str => EscapeString(str),

            // Handle arrays and enumerables
            IEnumerable<object> enumerable when enumerable.Any() => FormatArray(enumerable.ToArray(), indent),
            IEnumerable<object> _ => "[]",

            // Handle primitives
            _ => value.ToString() ?? "-"
        };
    }
    
    private static string EscapeString(string str)
    {
        return str.Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
    }

    /// <summary>
    /// Formats a dictionary with inline or multi-line formatting based on complexity.
    /// </summary>
    private static string FormatDictionary(IDictionary<string, object> dict, int indent)
    {
        // For simple dictionaries (no nested objects), show inline
        var hasNestedObjects = dict.Values.Any(v =>
            v is IDictionary<string, object> ||
            (v is IEnumerable<object> enumerable && enumerable.Any() && enumerable.First() is IDictionary<string, object>));

        if (!hasNestedObjects && dict.Count <= 3)
        {
            // Inline format: {key1: value1, key2: value2}
            var pairs = dict.Select(kvp => $"{kvp.Key}: {FormatValue(kvp.Value, indent)}");
            return "{" + string.Join(", ", pairs) + "}";
        }

        // Multi-line format for complex objects
        var indentStr = new string(' ', (indent + 1) * 2);
        var lines = new List<string> { "{" };
        foreach (var kvp in dict)
        {
            var formattedValue = FormatValue(kvp.Value, indent + 1);
            lines.Add($"{indentStr}{kvp.Key}: {formattedValue}");
        }
        lines.Add(new string(' ', indent * 2) + "}");
        return string.Join("\n", lines);
    }

    /// <summary>
    /// Formats an array with inline or multi-line formatting based on complexity.
    /// </summary>
    private static string FormatArray(Array arr, int indent)
    {
        var items = arr.Cast<object>().ToList();

        // Check if array contains complex objects
        var hasComplexObjects = items.Any(item => item is IDictionary<string, object>);

        if (!hasComplexObjects && items.Count <= 5)
        {
            // Inline format: [a, b, c]
            return "[" + string.Join(", ", items.Select(item => FormatValue(item, indent))) + "]";
        }

        // Multi-line format for complex arrays
        var indentStr = new string(' ', (indent + 1) * 2);
        var lines = new List<string> { "[" };
        foreach (var item in items)
        {
            var formattedItem = FormatValue(item, indent + 1);
            lines.Add($"{indentStr}{formattedItem}");
        }
        lines.Add(new string(' ', indent * 2) + "]");
        return string.Join("\n", lines);
    }
}
