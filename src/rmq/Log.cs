namespace Rmq;

/// <summary>
/// Diagnostics, always on stderr. Piping is a first-class use case, so nothing here
/// may ever reach stdout. This is the whole logging story — no ILogger, no package.
/// </summary>
public static class Log
{
    /// <summary>Set once from --verbose in Program.cs.</summary>
    public static bool Verbose { get; set; }

    private static readonly bool UseColor =
        !Console.IsErrorRedirected &&
        Environment.GetEnvironmentVariable("NO_COLOR") is null &&
        Environment.GetEnvironmentVariable("TERM") != "dumb";

    private const string Dim = "\u001b[2m";
    private const string Yellow = "\u001b[33m";
    private const string Red = "\u001b[31m";
    private const string Reset = "\u001b[0m";

    public static void Debug(string message)
    {
        if (Verbose)
        {
            Write(Dim, "debug", message);
        }
    }

    public static void Warn(string message) => Write(Yellow, "warning", message);

    public static void Error(string message) => Write(Red, "error", message);

    public static void Error(string message, Exception exception)
    {
        Error(message);
        if (Verbose)
        {
            Write(Dim, "debug", exception.ToString());
        }
    }

    private static void Write(string color, string level, string message)
    {
        Console.Error.WriteLine(UseColor
            ? $"{color}rmq: {level}: {message}{Reset}"
            : $"rmq: {level}: {message}");
    }
}
