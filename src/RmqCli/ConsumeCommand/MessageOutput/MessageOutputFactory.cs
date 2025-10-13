using Microsoft.Extensions.Logging;
using RmqCli.Common;
using RmqCli.Configuration;

namespace RmqCli.ConsumeCommand.MessageOutput;

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
    /// <param name="fileConfig">File configuration (for message delimiter and rotation)</param>
    /// <param name="messageCount">Number of messages to consume (-1 for unlimited)</param>
    /// <returns>A configured MessageOutput instance</returns>
    public static MessageOutput Create(
        ILoggerFactory loggerFactory,
        FileInfo? outputFileInfo,
        OutputFormat format,
        FileConfig fileConfig,
        int messageCount)
    {
        if (outputFileInfo is null)
        {
            // Write to console
            var logger = loggerFactory.CreateLogger<ConsoleOutput>();
            return new ConsoleOutput(logger, format);
        }
        else
        {
            // Write to file(s)
            var logger = loggerFactory.CreateLogger<FileOutput>();
            return new FileOutput(logger, outputFileInfo, format, fileConfig, messageCount);
        }
    }
}
