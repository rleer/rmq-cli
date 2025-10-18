using Microsoft.Extensions.Logging;
using RmqCli.Infrastructure.Configuration.Models;
using RmqCli.Infrastructure.Output.Console;
using RmqCli.Infrastructure.Output.File;
using RmqCli.Shared;

namespace RmqCli.Infrastructure.Output;

/// <summary>
/// Factory for creating MessageOutput instances based on CLI parameters.
/// This is a simple static factory that doesn't require DI registration.
/// </summary>
public static class MessageOutputFactory
{
    /// <summary>
    /// Creates the appropriate MessageOutput instance based on output destination.
    /// </summary>
    /// <param name="loggerFactory">Logger factory for creating loggers</param>
    /// <param name="outputFileInfo">File to write to, or null for console output</param>
    /// <param name="format">Output format (Plain, Json, Table)</param>
    /// <param name="compact">Use compact table format (only show properties with values)</param>
    /// <param name="fileConfig">File configuration (for message delimiter and rotation)</param>
    /// <param name="messageCount">Number of messages to consume (-1 for unlimited)</param>
    /// <returns>A configured MessageOutput instance</returns>
    public static MessageOutput Create(
        ILoggerFactory loggerFactory,
        FileInfo? outputFileInfo,
        OutputFormat format,
        bool compact,
        FileConfig fileConfig,
        int messageCount)
    {
        if (outputFileInfo is null)
        {
            // Write to console
            var logger = loggerFactory.CreateLogger<ConsoleOutput>();
            return new ConsoleOutput(logger, format, compact);
        }
        else
        {
            // Write to file(s)
            var logger = loggerFactory.CreateLogger<FileOutput>();
            return new FileOutput(logger, outputFileInfo, format, compact, fileConfig, messageCount);
        }
    }
}
