using Microsoft.Extensions.Logging;
using RmqCli.Infrastructure.Configuration.Models;
using RmqCli.Shared.Output;

namespace RmqCli.Unit.Tests.Shared.Output;

public class MessageOutputFactoryTests
{
    [Fact]
    public void Create_ReturnsConsoleOutput_WhenOutputFileInfoIsNull()
    {
        // Arrange
        var loggerFactory = Substitute.For<ILoggerFactory>();
        var fileConfig = new FileConfig();
        var messageCount = 10;
        var outputOptions = new OutputOptions
        {
            Format = OutputFormat.Plain,
            OutputFile = null,
            Compact = false,
            Quiet = false,
            Verbose = false,
            NoColor = false
        };

        // Act
        var result = MessageOutputFactory.Create(
            loggerFactory,
            outputOptions,
            fileConfig,
            messageCount);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<ConsoleOutput>();
        loggerFactory.Received(1).CreateLogger<ConsoleOutput>();
    }

    [Fact]
    public void Create_ReturnsFileOutput_WhenOutputFileInfoIsProvided()
    {
        // Arrange
        var loggerFactory = Substitute.For<ILoggerFactory>();
        var fileConfig = new FileConfig();
        var messageCount = 10;
        var outputFile = new FileInfo(Path.Combine(Path.GetTempPath(), "test.txt"));
        var outputOptions = new OutputOptions
        {
            Format = OutputFormat.Plain,
            OutputFile = outputFile,
            Compact = false,
            Quiet = false,
            Verbose = false,
            NoColor = false
        };

        // Act
        var result = MessageOutputFactory.Create(
            loggerFactory,
            outputOptions,
            fileConfig,
            messageCount);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<FileOutput>();
        loggerFactory.Received(1).CreateLogger<FileOutput>();
    }
}