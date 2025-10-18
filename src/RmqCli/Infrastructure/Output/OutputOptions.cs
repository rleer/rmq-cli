using RmqCli.Shared;

namespace RmqCli.Infrastructure.Output;

/// <summary>
/// Options for output formatting and display.
/// Consolidates global CLI options (format, quiet, verbose, no-color) with command-specific output options (output file, compact mode).
/// </summary>
public class OutputOptions
{
    // Global output options (from root command)
    public OutputFormat Format { get; init; }
    public bool Quiet { get; init; }
    public bool Verbose { get; init; }
    public bool NoColor { get; init; }

    // Command-specific output options
    public FileInfo? OutputFile { get; init; }
    public bool Compact { get; init; }
}
