using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RmqCli.Infrastructure.Configuration.Models;
using RmqCli.Shared.Output;

namespace RmqCli.Integration.Tests.Infrastructure.Output;

public class MessageOutputFactoryTests
{
    #region Create

    public class Create
    {
        [Fact]
        public void ReturnsConsoleOutput_WhenOutputFileInfoIsNull()
        {
            // Arrange
            var loggerFactory = new NullLoggerFactory();
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
        }

        [Fact]
        public void ReturnsFileOutput_WhenOutputFileInfoIsProvided()
        {
            // Arrange
            var loggerFactory = new NullLoggerFactory();
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
        }

        [Theory]
        [InlineData(OutputFormat.Plain)]
        [InlineData(OutputFormat.Json)]
        public void CreatesConsoleOutput_WithDifferentFormats(OutputFormat format)
        {
            // Arrange
            var loggerFactory = new NullLoggerFactory();
            var fileConfig = new FileConfig();
            var messageCount = 10;
            var outputOptions = new OutputOptions
            {
                Format = format,
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
            result.Should().BeOfType<ConsoleOutput>();
        }

        [Theory]
        [InlineData(OutputFormat.Plain)]
        [InlineData(OutputFormat.Json)]
        public void CreatesFileOutput_WithDifferentFormats(OutputFormat format)
        {
            // Arrange
            var loggerFactory = new NullLoggerFactory();
            var fileConfig = new FileConfig();
            var messageCount = 10;
            var outputFile = new FileInfo(Path.Combine(Path.GetTempPath(), "test.txt"));
            var outputOptions = new OutputOptions
            {
                Format = format,
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
            result.Should().BeOfType<FileOutput>();
        }

        [Fact]
        public void HandlesUnlimitedMessageCount()
        {
            // Arrange
            var loggerFactory = new NullLoggerFactory();
            var fileConfig = new FileConfig();
            var messageCount = -1; // Unlimited
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
        }

        [Fact]
        public void HandlesZeroMessageCount()
        {
            // Arrange
            var loggerFactory = new NullLoggerFactory();
            var fileConfig = new FileConfig();
            var messageCount = 0;
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
        }

        [Fact]
        public void UsesProvidedLoggerFactory()
        {
            // Arrange
            var loggerFactory = Substitute.For<ILoggerFactory>();
            var mockLogger = Substitute.For<ILogger<ConsoleOutput>>();
            loggerFactory.CreateLogger<ConsoleOutput>().Returns(mockLogger);
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
            loggerFactory.Received(1).CreateLogger<ConsoleOutput>();
        }

        [Fact]
        public void CreatesFileOutputLogger_WhenFileProvided()
        {
            // Arrange
            var loggerFactory = Substitute.For<ILoggerFactory>();
            var mockLogger = Substitute.For<ILogger<FileOutput>>();
            loggerFactory.CreateLogger<FileOutput>().Returns(mockLogger);
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
            loggerFactory.Received(1).CreateLogger<FileOutput>();
        }

        [Fact]
        public void HandlesFileInNonExistentDirectory()
        {
            // Arrange
            var loggerFactory = new NullLoggerFactory();
            var fileConfig = new FileConfig();
            var messageCount = 10;
            var outputFile = new FileInfo(Path.Combine(Path.GetTempPath(), "nonexistent", "test.txt"));
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

            // Assert - Should create FileOutput without throwing
            result.Should().NotBeNull();
            result.Should().BeOfType<FileOutput>();
        }
    }

    #endregion
}
