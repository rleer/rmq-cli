using Microsoft.Extensions.Logging;
using RmqCli.Infrastructure.Configuration.Models;

namespace RmqCli.Shared.Output;

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
    /// <param name="outputOptions">Output options (format, compact, output file)</param>
    /// <param name="fileConfig">File configuration (for message delimiter and rotation)</param>
    /// <param name="messageCount">Number of messages to consume (-1 for unlimited)</param>
    /// <returns>A configured MessageOutput instance</returns>
    public static MessageOutput Create(
        ILoggerFactory loggerFactory,
        OutputOptions outputOptions,
        FileConfig fileConfig,
        int messageCount)
    {
        if (outputOptions.OutputFile is null)
        {
            // Write to console
            var logger = loggerFactory.CreateLogger<ConsoleOutput>();
            return new ConsoleOutput(logger, outputOptions);
        }
        else
        {
            // Write to file(s)
            var logger = loggerFactory.CreateLogger<FileOutput>();
            return new FileOutput(logger, outputOptions, fileConfig, messageCount);
        }
    }
}
