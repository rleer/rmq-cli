using System.Text;
using System.Threading.Channels;
using Microsoft.Extensions.Logging.Abstractions;
using RabbitMQ.Client;
using RmqCli.Core.Models;
using RmqCli.Infrastructure.Configuration.Models;
using RmqCli.Infrastructure.Output;
using RmqCli.Infrastructure.Output.File;
using RmqCli.Infrastructure.Output.Formatters;
using RmqCli.Shared;

namespace RmqCli.Integration.Tests.Infrastructure.Output;

public class FileOutputTests
{
    private readonly string _tempDir;

    public FileOutputTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"rmq-tests-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
    }

    #region WriteMessagesAsync - Single File Mode

    public class SingleFileMode : IDisposable
    {
        private readonly string _tempDir;

        public SingleFileMode()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), $"rmq-tests-{Guid.NewGuid()}");
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }

        [Fact]
        public async Task WritesMessagesToSingleFile_WhenCountBelowThreshold()
        {
            // Arrange
            var logger = new NullLogger<FileOutput>();
            var outputFile = new FileInfo(Path.Combine(_tempDir, "output.txt"));
            var fileConfig = new FileConfig { MessagesPerFile = 100 };
            var output = new FileOutput(logger, new OutputOptions { Format = OutputFormat.Plain, OutputFile = outputFile, Compact = false, Quiet = false, Verbose = false, NoColor = false }, fileConfig, messageCount: 5);

            var messageChannel = Channel.CreateUnbounded<RetrievedMessage>();
            var ackChannel = Channel.CreateUnbounded<(ulong, bool)>();

            // Add messages
            for (int i = 1; i <= 5; i++)
            {
                await messageChannel.Writer.WriteAsync(CreateRetrievedMessage($"Message {i}", "exchange", "routing-key", "test-queue", (ulong)i));
            }
            messageChannel.Writer.Complete();

            // Act
            var result = await output.WriteMessagesAsync(
                messageChannel,
                ackChannel,
                CancellationToken.None);

            // Assert
            result.ProcessedCount.Should().Be(5);
            outputFile.Exists.Should().BeTrue();

            var content = await File.ReadAllTextAsync(outputFile.FullName);
            content.Should().Contain("Message 1");
            content.Should().Contain("Message 5");
        }

        [Fact]
        public async Task AddsDelimiters_BetweenMessages_InPlainFormat()
        {
            // Arrange
            var logger = new NullLogger<FileOutput>();
            var outputFile = new FileInfo(Path.Combine(_tempDir, "output.txt"));
            var fileConfig = new FileConfig
            {
                MessagesPerFile = 100,
                MessageDelimiter = "---"
            };
            var output = new FileOutput(logger, new OutputOptions { Format = OutputFormat.Plain, OutputFile = outputFile, Compact = false, Quiet = false, Verbose = false, NoColor = false }, fileConfig, messageCount: 3);

            var messageChannel = Channel.CreateUnbounded<RetrievedMessage>();
            var ackChannel = Channel.CreateUnbounded<(ulong, bool)>();

            await messageChannel.Writer.WriteAsync(CreateRetrievedMessage("Message 1", "exchange", "routing-key", "test-queue", 1));
            await messageChannel.Writer.WriteAsync(CreateRetrievedMessage("Message 2", "exchange", "routing-key", "test-queue", 2));
            await messageChannel.Writer.WriteAsync(CreateRetrievedMessage("Message 3", "exchange", "routing-key", "test-queue", 3));
            messageChannel.Writer.Complete();

            // Act
            await output.WriteMessagesAsync(
                messageChannel,
                ackChannel,
                CancellationToken.None);

            // Assert
            var content = await File.ReadAllTextAsync(outputFile.FullName);
            // Count only standalone "---" delimiters (on their own line), not section separators like "==="
            var delimiterCount = content.Split(new[] { "\n---\n", "\r\n---\r\n" }, StringSplitOptions.None).Length - 1;
            delimiterCount.Should().Be(2); // 2 delimiters between 3 messages
        }

        [Fact]
        public async Task WritesJsonFormat_WithoutDelimiters()
        {
            // Arrange
            var logger = new NullLogger<FileOutput>();
            var outputFile = new FileInfo(Path.Combine(_tempDir, "output.json"));
            var fileConfig = new FileConfig { MessagesPerFile = 100, MessageDelimiter = "---" };
            var output = new FileOutput(logger, new OutputOptions { Format = OutputFormat.Json, OutputFile = outputFile, Compact = false, Quiet = false, Verbose = false, NoColor = false }, fileConfig, messageCount: 2);

            var messageChannel = Channel.CreateUnbounded<RetrievedMessage>();
            var ackChannel = Channel.CreateUnbounded<(ulong, bool)>();

            await messageChannel.Writer.WriteAsync(CreateRetrievedMessage("Message 1", "exchange", "routing-key", "test-queue", 1));
            await messageChannel.Writer.WriteAsync(CreateRetrievedMessage("Message 2", "exchange", "routing-key", "test-queue", 2));
            messageChannel.Writer.Complete();

            // Act
            await output.WriteMessagesAsync(
                messageChannel,
                ackChannel,
                CancellationToken.None);

            // Assert
            var content = await File.ReadAllTextAsync(outputFile.FullName);
            content.Should().NotContain("---"); // No delimiters in JSON format
            content.Should().Contain("deliveryTag");
        }

        [Fact]
        public async Task HandlesEmptyMessageChannel()
        {
            // Arrange
            var logger = new NullLogger<FileOutput>();
            var outputFile = new FileInfo(Path.Combine(_tempDir, "empty.txt"));
            var fileConfig = new FileConfig { MessagesPerFile = 100 };
            var output = new FileOutput(logger, new OutputOptions { Format = OutputFormat.Plain, OutputFile = outputFile, Compact = false, Quiet = false, Verbose = false, NoColor = false }, fileConfig, messageCount: 10);

            var messageChannel = Channel.CreateUnbounded<RetrievedMessage>();
            var ackChannel = Channel.CreateUnbounded<(ulong, bool)>();
            messageChannel.Writer.Complete();

            // Act
            var result = await output.WriteMessagesAsync(
                messageChannel,
                ackChannel,
                CancellationToken.None);

            // Assert
            result.ProcessedCount.Should().Be(0);
            result.TotalBytes.Should().Be(0);
        }

        [Fact]
        public async Task CalculatesTotalBytes_Correctly()
        {
            // Arrange
            var logger = new NullLogger<FileOutput>();
            var outputFile = new FileInfo(Path.Combine(_tempDir, "bytes.txt"));
            var fileConfig = new FileConfig { MessagesPerFile = 100 };
            var output = new FileOutput(logger, new OutputOptions { Format = OutputFormat.Plain, OutputFile = outputFile, Compact = false, Quiet = false, Verbose = false, NoColor = false }, fileConfig, messageCount: 2);

            var messageChannel = Channel.CreateUnbounded<RetrievedMessage>();
            var ackChannel = Channel.CreateUnbounded<(ulong, bool)>();

            var body1 = "Test message 1";
            var body2 = "Test message 2 - longer";
            var expectedBytes = System.Text.Encoding.UTF8.GetByteCount(body1)
                              + System.Text.Encoding.UTF8.GetByteCount(body2);

            await messageChannel.Writer.WriteAsync(CreateRetrievedMessage(body1, "exchange", "routing-key", "test-queue", 1));
            await messageChannel.Writer.WriteAsync(CreateRetrievedMessage(body2, "exchange", "routing-key", "test-queue", 2));
            messageChannel.Writer.Complete();

            // Act
            var result = await output.WriteMessagesAsync(
                messageChannel,
                ackChannel,
                CancellationToken.None);

            // Assert
            result.TotalBytes.Should().Be(expectedBytes);
        }
    }

    #endregion

    #region WriteMessagesAsync - Rotating File Mode

    public class RotatingFileMode : IDisposable
    {
        private readonly string _tempDir;

        public RotatingFileMode()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), $"rmq-tests-{Guid.NewGuid()}");
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }

        [Fact]
        public async Task CreatesMultipleFiles_WhenExceedingMessagesPerFile()
        {
            // Arrange
            var logger = new NullLogger<FileOutput>();
            var outputFile = new FileInfo(Path.Combine(_tempDir, "output.txt"));
            var fileConfig = new FileConfig { MessagesPerFile = 2 }; // 2 messages per file
            var output = new FileOutput(logger, new OutputOptions { Format = OutputFormat.Plain, OutputFile = outputFile, Compact = false, Quiet = false, Verbose = false, NoColor = false }, fileConfig, messageCount: 5);

            var messageChannel = Channel.CreateUnbounded<RetrievedMessage>();
            var ackChannel = Channel.CreateUnbounded<(ulong, bool)>();

            // Add 5 messages (should create 3 files: 2+2+1)
            for (int i = 1; i <= 5; i++)
            {
                await messageChannel.Writer.WriteAsync(CreateRetrievedMessage($"Message {i}", "exchange", "routing-key", "test-queue", (ulong)i));
            }
            messageChannel.Writer.Complete();

            // Act
            var result = await output.WriteMessagesAsync(
                messageChannel,
                ackChannel,
                CancellationToken.None);

            // Assert
            result.ProcessedCount.Should().Be(5);

            // Check that multiple files were created
            var files = Directory.GetFiles(_tempDir, "output.*.txt");
            files.Should().HaveCountGreaterOrEqualTo(3);
        }

        [Fact]
        public async Task UsesRotatingMode_WhenMessageCountIsUnlimited()
        {
            // Arrange
            var logger = new NullLogger<FileOutput>();
            var outputFile = new FileInfo(Path.Combine(_tempDir, "unlimited.txt"));
            var fileConfig = new FileConfig { MessagesPerFile = 2 };
            var output = new FileOutput(logger, new OutputOptions { Format = OutputFormat.Plain, OutputFile = outputFile, Compact = false, Quiet = false, Verbose = false, NoColor = false }, fileConfig, messageCount: -1); // Unlimited

            var messageChannel = Channel.CreateUnbounded<RetrievedMessage>();
            var ackChannel = Channel.CreateUnbounded<(ulong, bool)>();

            // Add 5 messages
            for (int i = 1; i <= 5; i++)
            {
                await messageChannel.Writer.WriteAsync(CreateRetrievedMessage($"Message {i}", "exchange", "routing-key", "test-queue", (ulong)i));
            }
            messageChannel.Writer.Complete();

            // Act
            var result = await output.WriteMessagesAsync(
                messageChannel,
                ackChannel,
                CancellationToken.None);

            // Assert
            result.ProcessedCount.Should().Be(5);

            // Should create rotating files
            var files = Directory.GetFiles(_tempDir, "unlimited.*.txt");
            files.Should().HaveCountGreaterOrEqualTo(3);
        }

        [Fact]
        public async Task UsesRotatingMode_WhenCountExceedsMessagesPerFile()
        {
            // Arrange
            var logger = new NullLogger<FileOutput>();
            var outputFile = new FileInfo(Path.Combine(_tempDir, "rotating.txt"));
            var fileConfig = new FileConfig { MessagesPerFile = 3 };
            var output = new FileOutput(logger, new OutputOptions { Format = OutputFormat.Plain, OutputFile = outputFile, Compact = false, Quiet = false, Verbose = false, NoColor = false }, fileConfig, messageCount: 10); // 10 > 3

            var messageChannel = Channel.CreateUnbounded<RetrievedMessage>();
            var ackChannel = Channel.CreateUnbounded<(ulong, bool)>();

            for (int i = 1; i <= 6; i++)
            {
                await messageChannel.Writer.WriteAsync(CreateRetrievedMessage($"Message {i}", "exchange", "routing-key", "test-queue", (ulong)i));
            }
            messageChannel.Writer.Complete();

            // Act
            var result = await output.WriteMessagesAsync(
                messageChannel,
                ackChannel,
                CancellationToken.None);

            // Assert
            result.ProcessedCount.Should().Be(6);

            // Should create rotating files (6 messages / 3 per file = 2 files)
            var files = Directory.GetFiles(_tempDir, "rotating.*.txt");
            files.Should().HaveCount(2);
        }

        [Fact]
        public async Task NamesRotatingFiles_WithSequentialIndexes()
        {
            // Arrange
            var logger = new NullLogger<FileOutput>();
            var outputFile = new FileInfo(Path.Combine(_tempDir, "indexed.txt"));
            var fileConfig = new FileConfig { MessagesPerFile = 1 }; // 1 message per file
            var output = new FileOutput(logger, new OutputOptions { Format = OutputFormat.Plain, OutputFile = outputFile, Compact = false, Quiet = false, Verbose = false, NoColor = false }, fileConfig, messageCount: 3);

            var messageChannel = Channel.CreateUnbounded<RetrievedMessage>();
            var ackChannel = Channel.CreateUnbounded<(ulong, bool)>();

            for (int i = 1; i <= 3; i++)
            {
                await messageChannel.Writer.WriteAsync(CreateRetrievedMessage($"Message {i}", "exchange", "routing-key", "test-queue", (ulong)i));
            }
            messageChannel.Writer.Complete();

            // Act
            await output.WriteMessagesAsync(
                messageChannel,
                ackChannel,
                CancellationToken.None);

            // Assert
            File.Exists(Path.Combine(_tempDir, "indexed.0.txt")).Should().BeTrue();
            File.Exists(Path.Combine(_tempDir, "indexed.1.txt")).Should().BeTrue();
            File.Exists(Path.Combine(_tempDir, "indexed.2.txt")).Should().BeTrue();
        }

        [Fact]
        public async Task AddsDelimiters_WithinSameFile_InPlainFormat()
        {
            // Arrange
            var logger = new NullLogger<FileOutput>();
            var outputFile = new FileInfo(Path.Combine(_tempDir, "delimited.txt"));
            var fileConfig = new FileConfig
            {
                MessagesPerFile = 3,
                MessageDelimiter = "---"
            };
            var output = new FileOutput(logger, new OutputOptions { Format = OutputFormat.Plain, OutputFile = outputFile, Compact = false, Quiet = false, Verbose = false, NoColor = false }, fileConfig, messageCount: 6);

            var messageChannel = Channel.CreateUnbounded<RetrievedMessage>();
            var ackChannel = Channel.CreateUnbounded<(ulong, bool)>();

            for (int i = 1; i <= 6; i++)
            {
                await messageChannel.Writer.WriteAsync(CreateRetrievedMessage($"Message {i}", "exchange", "routing-key", "test-queue", (ulong)i));
            }
            messageChannel.Writer.Complete();

            // Act
            await output.WriteMessagesAsync(
                messageChannel,
                ackChannel,
                CancellationToken.None);

            // Assert
            var file1Content = await File.ReadAllTextAsync(Path.Combine(_tempDir, "delimited.0.txt"));
            // Count only standalone "---" delimiters (on their own line), not section separators like "==="
            var file1DelimiterCount = file1Content.Split(new[] { "\n---\n", "\r\n---\r\n" }, StringSplitOptions.None).Length - 1;
            file1DelimiterCount.Should().Be(2); // 2 delimiters between 3 messages in first file

            var file2Content = await File.ReadAllTextAsync(Path.Combine(_tempDir, "delimited.1.txt"));
            // Count only standalone "---" delimiters (on their own line), not section separators like "==="
            var file2DelimiterCount = file2Content.Split(new[] { "\n---\n", "\r\n---\r\n" }, StringSplitOptions.None).Length - 1;
            file2DelimiterCount.Should().Be(2); // 2 delimiters between 3 messages in second file
        }

        [Fact]
        public async Task HandlesJsonFormat_InRotatingFiles()
        {
            // Arrange
            var logger = new NullLogger<FileOutput>();
            var outputFile = new FileInfo(Path.Combine(_tempDir, "json.txt"));
            var fileConfig = new FileConfig { MessagesPerFile = 2 };
            var output = new FileOutput(logger, new OutputOptions { Format = OutputFormat.Json, OutputFile = outputFile, Compact = false, Quiet = false, Verbose = false, NoColor = false }, fileConfig, messageCount: 4);

            var messageChannel = Channel.CreateUnbounded<RetrievedMessage>();
            var ackChannel = Channel.CreateUnbounded<(ulong, bool)>();

            for (int i = 1; i <= 4; i++)
            {
                await messageChannel.Writer.WriteAsync(CreateRetrievedMessage($"Message {i}", "exchange", "routing-key", "test-queue", (ulong)i));
            }
            messageChannel.Writer.Complete();

            // Act
            await output.WriteMessagesAsync(
                messageChannel,
                ackChannel,
                CancellationToken.None);

            // Assert
            var files = Directory.GetFiles(_tempDir, "json.*.txt");
            files.Should().HaveCount(2);

            foreach (var file in files)
            {
                var content = await File.ReadAllTextAsync(file);
                content.Should().Contain("deliveryTag");
            }
        }
    }

    #endregion

    #region Common Scenarios

    public class CommonScenarios : IDisposable
    {
        private readonly string _tempDir;

        public CommonScenarios()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), $"rmq-tests-{Guid.NewGuid()}");
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }

        [Fact]
        public async Task SendsAcknowledgments_WithCorrectMode()
        {
            // Arrange
            var logger = new NullLogger<FileOutput>();
            var outputFile = new FileInfo(Path.Combine(_tempDir, "ack.txt"));
            var fileConfig = new FileConfig { MessagesPerFile = 100 };
            var output = new FileOutput(logger, new OutputOptions { Format = OutputFormat.Plain, OutputFile = outputFile, Compact = false, Quiet = false, Verbose = false, NoColor = false }, fileConfig, messageCount: 1);

            var messageChannel = Channel.CreateUnbounded<RetrievedMessage>();
            var ackChannel = Channel.CreateUnbounded<(ulong, bool)>();

            await messageChannel.Writer.WriteAsync(CreateRetrievedMessage("Test", "exchange", "routing-key", "test-queue", 42));
            messageChannel.Writer.Complete();

            // Act
            await output.WriteMessagesAsync(
                messageChannel,
                ackChannel,
                CancellationToken.None);

            // Assert
            ackChannel.Reader.TryRead(out var ack).Should().BeTrue();
            ack.Item1.Should().Be(42);
            ack.Item2.Should().Be(true);
        }

        [Fact]
        public async Task HandlesMessagesWithProperties()
        {
            // Arrange
            var logger = new NullLogger<FileOutput>();
            var outputFile = new FileInfo(Path.Combine(_tempDir, "props.txt"));
            var fileConfig = new FileConfig { MessagesPerFile = 100 };
            var output = new FileOutput(logger, new OutputOptions { Format = OutputFormat.Plain, OutputFile = outputFile, Compact = false, Quiet = false, Verbose = false, NoColor = false }, fileConfig, messageCount: 1);

            var messageChannel = Channel.CreateUnbounded<RetrievedMessage>();
            var ackChannel = Channel.CreateUnbounded<(ulong, bool)>();

            var props = Substitute.For<IReadOnlyBasicProperties>();
            props.IsMessageIdPresent().Returns(true);
            props.MessageId.Returns("msg-123");

            await messageChannel.Writer.WriteAsync(CreateRetrievedMessage("Test", "exchange", "routing-key", "test-queue", 1, props));
            messageChannel.Writer.Complete();

            // Act
            var result = await output.WriteMessagesAsync(
                messageChannel,
                ackChannel,
                CancellationToken.None);

            // Assert
            result.ProcessedCount.Should().Be(1);
            var content = await File.ReadAllTextAsync(outputFile.FullName);
            content.Should().Contain("msg-123");
        }

        [Fact]
        public async Task StopsProcessingAfterCancellation()
        {
            // Arrange
            var logger = new NullLogger<FileOutput>();
            var outputFile = new FileInfo(Path.Combine(_tempDir, "cancel.txt"));
            var fileConfig = new FileConfig { MessagesPerFile = 10000 };
            var output = new FileOutput(logger, new OutputOptions { Format = OutputFormat.Plain, OutputFile = outputFile, Compact = false, Quiet = false, Verbose = false, NoColor = false }, fileConfig, messageCount: 100000);

            // Use bounded channel to control message flow rate
            var messageChannel = Channel.CreateBounded<RetrievedMessage>(500);
            var ackChannel = Channel.CreateUnbounded<(ulong, bool)>();

            const int totalMessages = 100000;
            using var cts = new CancellationTokenSource();

            // Start processing in background BEFORE all messages are added
            var processingTask = output.WriteMessagesAsync(
                messageChannel,
                ackChannel,
                cts.Token);

            // Writer task that adds messages concurrently
            var writerTask = Task.Run(async () =>
            {
                for (int i = 1; i <= totalMessages; i++)
                {
                    // Stop writing if cancelled
                    if (cts.Token.IsCancellationRequested)
                        break;

                    await messageChannel.Writer.WriteAsync(CreateRetrievedMessage($"Message {i}", "exchange", "routing-key", "test-queue", (ulong)i));

                    // Cancel after some messages have been written
                    if (i == 1000)
                    {
                        cts.Cancel();
                    }
                }
                messageChannel.Writer.Complete();
            });

            // Wait for processing to complete
            var result = await processingTask;
            await writerTask; // Ensure writer completes

            // Assert - Should have processed some messages but not all
            result.Should().NotBeNull();
            result.ProcessedCount.Should().BeGreaterThan(100, "at least 100 messages should be processed before cancellation");
            result.ProcessedCount.Should().BeLessThan(totalMessages, "cancellation should stop processing before all messages are consumed");
        }
    }

    #endregion

    #region Test Helpers

    private static RetrievedMessage CreateRetrievedMessage(
        string body,
        string exchange = "exchange",
        string routingKey = "routing.key",
        string queue = "test-queue",
        ulong deliveryTag = 1,
        IReadOnlyBasicProperties? props = null,
        bool redelivered = false)
    {
        var (properties, headers) = MessagePropertyExtractor.ExtractPropertiesAndHeaders(props);
        var bodySizeBytes = Encoding.UTF8.GetByteCount(body);

        return new RetrievedMessage
        {
            Body = body,
            Exchange = exchange,
            RoutingKey = routingKey,
            Queue = queue,
            DeliveryTag = deliveryTag,
            Properties = properties,
            Headers = headers,
            Redelivered = redelivered,
            BodySizeBytes = bodySizeBytes,
            BodySize = OutputUtilities.ToSizeString(bodySizeBytes)
        };
    }

    #endregion
}
