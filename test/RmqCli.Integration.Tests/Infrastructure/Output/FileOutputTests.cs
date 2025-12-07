using System.Text;
using System.Threading.Channels;
using Microsoft.Extensions.Logging.Abstractions;
using RabbitMQ.Client;
using RmqCli.Core.Models;
using RmqCli.Infrastructure.Configuration.Models;
using RmqCli.Shared.Output;
using RmqCli.Shared.Output.Formatters;
using Xunit.Abstractions;

namespace RmqCli.Integration.Tests.Infrastructure.Output;

public class FileOutputTests
{
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
            var output = new FileOutput(logger,
                new OutputOptions { Format = OutputFormat.Plain, OutputFile = outputFile, Compact = false, Quiet = false, Verbose = false, NoColor = false },
                fileConfig, messageCount: 5);

            var messageChannel = Channel.CreateUnbounded<RetrievedMessage>();
            var ackChannel = Channel.CreateUnbounded<(ulong, bool)>();

            // Add messages
            for (int i = 1; i <= 5; i++)
            {
                await messageChannel.Writer.WriteAsync(CreateRetrievedMessage($"Message {i}", deliveryTag: (ulong)i));
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

            // Should not create rotating files
            var files = Directory.GetFiles(_tempDir, "output.*.txt");
            files.Should().HaveCount(0);

            var content = await File.ReadAllTextAsync(outputFile.FullName);
            content.Should().Contain("Message 1");
            content.Should().Contain("Message 2");
            content.Should().Contain("Message 3");
            content.Should().Contain("Message 4");
            content.Should().Contain("Message 5");
        }
    }

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

        [Theory]
        [InlineData(-1)] // Unlimited
        [InlineData(5)] // Exceeds MessagesPerFile
        public async Task UsesRotatingMode_WhenMessageCountIsUnlimited_Or_ExceedsMessagesPerFile(int messageCount)
        {
            // Arrange
            var logger = new NullLogger<FileOutput>();
            var outputFile = new FileInfo(Path.Combine(_tempDir, "unlimited.txt"));
            var fileConfig = new FileConfig { MessagesPerFile = 2 };
            var output = new FileOutput(logger,
                new OutputOptions { Format = OutputFormat.Plain, OutputFile = outputFile, Compact = false, Quiet = false, Verbose = false, NoColor = false },
                fileConfig, messageCount: messageCount);

            var messageChannel = Channel.CreateUnbounded<RetrievedMessage>();
            var ackChannel = Channel.CreateUnbounded<(ulong, bool)>();

            // Add 5 messages
            for (int i = 1; i <= 5; i++)
            {
                await messageChannel.Writer.WriteAsync(CreateRetrievedMessage($"Message {i}", deliveryTag: (ulong)i));
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
        public async Task NamesRotatingFiles_WithSequentialIndexes()
        {
            // Arrange
            var logger = new NullLogger<FileOutput>();
            var outputFile = new FileInfo(Path.Combine(_tempDir, "indexed.txt"));
            var fileConfig = new FileConfig { MessagesPerFile = 1 }; // 1 message per file
            var output = new FileOutput(logger,
                new OutputOptions { Format = OutputFormat.Plain, OutputFile = outputFile, Compact = false, Quiet = false, Verbose = false, NoColor = false },
                fileConfig, messageCount: 3);

            var messageChannel = Channel.CreateUnbounded<RetrievedMessage>();
            var ackChannel = Channel.CreateUnbounded<(ulong, bool)>();

            for (int i = 1; i <= 3; i++)
            {
                await messageChannel.Writer.WriteAsync(CreateRetrievedMessage($"Message {i}", deliveryTag: (ulong)i));
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
    }

    public class CommonScenarios : IDisposable
    {
        private readonly string _tempDir;
        private readonly ITestOutputHelper _output;

        public CommonScenarios(ITestOutputHelper output)
        {
            _output = output;
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

        [Theory]
        [InlineData(1)] // Single file
        [InlineData(2)] // Rotating files
        public async Task HandlesEmptyMessageChannel(int messageCount)
        {
            // Arrange
            var logger = new NullLogger<FileOutput>();
            var outputFile = new FileInfo(Path.Combine(_tempDir, "empty.txt"));
            var fileConfig = new FileConfig { MessagesPerFile = 1 };
            var output = new FileOutput(logger,
                new OutputOptions { Format = OutputFormat.Plain, OutputFile = outputFile, Compact = false, Quiet = false, Verbose = false, NoColor = false },
                fileConfig, messageCount: messageCount);

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

        [Theory]
        [InlineData(OutputFormat.Json, 2)] // Single file
        [InlineData(OutputFormat.Json, 4)] // Rotating files
        [InlineData(OutputFormat.Table, 2)] // Single file
        [InlineData(OutputFormat.Table, 4)] // Rotating files
        [InlineData(OutputFormat.Plain, 2)] // Single file
        [InlineData(OutputFormat.Plain, 4)] // Rotating files
        public async Task AddDelimiters_WhenInPlainModeOnly(OutputFormat format, int messageCount)
        {
            // Arrange
            var logger = new NullLogger<FileOutput>();
            var outputFile = new FileInfo(Path.Combine(_tempDir, "delimiter.txt"));
            var fileConfig = new FileConfig
            {
                MessagesPerFile = 2,
                MessageDelimiter = "---"
            };
            var output = new FileOutput(logger,
                new OutputOptions { Format = format, OutputFile = outputFile, Compact = false, Quiet = false, Verbose = false, NoColor = false }, fileConfig,
                messageCount: messageCount);

            var messageChannel = Channel.CreateUnbounded<RetrievedMessage>();
            var ackChannel = Channel.CreateUnbounded<(ulong, bool)>();

            for (int i = 1; i <= messageCount; i++)
            {
                await messageChannel.Writer.WriteAsync(CreateRetrievedMessage($"Message {i}", deliveryTag: (ulong)i));
            }

            messageChannel.Writer.Complete();

            // Act
            await output.WriteMessagesAsync(
                messageChannel,
                ackChannel,
                CancellationToken.None);

            // Assert
            var files = Directory.GetFiles(_tempDir, "delimiter*txt");
            foreach (var file in files)
            {
                var content = await File.ReadAllTextAsync(file);
                // Count only standalone "---" delimiters (on their own line), not section separators like "==="
                var delimiterCount = content.Split([$"{Environment.NewLine}---{Environment.NewLine}"], StringSplitOptions.None).Length - 1;

                if (format == OutputFormat.Plain)
                {
                    delimiterCount.Should().Be(1); // Delimiter between 2 messages in each file in plain format
                }
                else
                {
                    delimiterCount.Should().Be(0); // No delimiters in Table/JSON format
                }
            }
        }

        [Theory]
        [InlineData(2)] // Single file
        [InlineData(4)] // Rotating files
        public async Task HandlesDelimiterWithNewLine(int messageCount)
        {
            // Arrange
            var logger = new NullLogger<FileOutput>();
            var outputFile = new FileInfo(Path.Combine(_tempDir, "newline_delim.txt"));
            var delimiter = $"{Environment.NewLine}{Environment.NewLine}";
            var fileConfig = new FileConfig
            {
                MessagesPerFile = 2,
                MessageDelimiter = delimiter
            };
            var output = new FileOutput(logger,
                new OutputOptions { Format = OutputFormat.Plain, OutputFile = outputFile, Compact = false, Quiet = false, Verbose = false, NoColor = false },
                fileConfig, messageCount: messageCount);

            var messageChannel = Channel.CreateUnbounded<RetrievedMessage>();
            var ackChannel = Channel.CreateUnbounded<(ulong, bool)>();

            for (int i = 1; i <= messageCount; i++)
            {
                await messageChannel.Writer.WriteAsync(CreateRetrievedMessage($"Message {i}", deliveryTag: (ulong)i));
            }

            messageChannel.Writer.Complete();

            // Act
            await output.WriteMessagesAsync(messageChannel, ackChannel, CancellationToken.None);

            // Assert
            var files = Directory.GetFiles(_tempDir, "newline_delim*txt");
            foreach (var file in files)
            {
                var content = await File.ReadAllTextAsync(file);
                // TODO: Improve delimiter check to count for added new lines
                content.Should().Contain(delimiter);
            }
        }

        [Theory]
        [InlineData(1)] // Single file
        [InlineData(2)] // Rotating files
        public async Task CalculatesTotalBytes_Correctly(int messagesPerFile)
        {
            // Arrange
            var logger = new NullLogger<FileOutput>();
            var outputFile = new FileInfo(Path.Combine(_tempDir, "bytes.txt"));
            var fileConfig = new FileConfig { MessagesPerFile = messagesPerFile };
            var output = new FileOutput(logger,
                new OutputOptions { Format = OutputFormat.Plain, OutputFile = outputFile, Compact = false, Quiet = false, Verbose = false, NoColor = false },
                fileConfig, messageCount: 2);

            var messageChannel = Channel.CreateUnbounded<RetrievedMessage>();
            var ackChannel = Channel.CreateUnbounded<(ulong, bool)>();

            var body1 = "Test message 1";
            var body2 = "Test message 2 - longer";
            var expectedBytes = Encoding.UTF8.GetByteCount(body1) + Encoding.UTF8.GetByteCount(body2);

            await messageChannel.Writer.WriteAsync(CreateRetrievedMessage(body1, deliveryTag: 1));
            await messageChannel.Writer.WriteAsync(CreateRetrievedMessage(body2, deliveryTag: 2));
            messageChannel.Writer.Complete();

            // Act
            var result = await output.WriteMessagesAsync(
                messageChannel,
                ackChannel,
                CancellationToken.None);

            // Assert
            result.TotalBytes.Should().Be(expectedBytes);
        }

        [Theory]
        [InlineData(1)] // Single file
        [InlineData(2)] // Rotating files
        public async Task SendsAcknowledgments_WithCorrectMode(int messageCount)
        {
            // Arrange
            var logger = new NullLogger<FileOutput>();
            var outputFile = new FileInfo(Path.Combine(_tempDir, "ack.txt"));
            var fileConfig = new FileConfig { MessagesPerFile = 1 };
            var output = new FileOutput(logger,
                new OutputOptions { Format = OutputFormat.Plain, OutputFile = outputFile, Compact = false, Quiet = false, Verbose = false, NoColor = false },
                fileConfig, messageCount: messageCount);

            var messageChannel = Channel.CreateUnbounded<RetrievedMessage>();
            var ackChannel = Channel.CreateUnbounded<(ulong, bool)>();

            for (int i = 1; i <= messageCount; i++)
            {
                await messageChannel.Writer.WriteAsync(CreateRetrievedMessage($"Message {i}", deliveryTag: (ulong)i));
            }

            messageChannel.Writer.Complete();

            // Act
            await output.WriteMessagesAsync(
                messageChannel,
                ackChannel,
                CancellationToken.None);

            // Assert
            for (int i = 1; i <= messageCount; i++)
            {
                ackChannel.Reader.TryRead(out var ack).Should().BeTrue();
                ack.Item1.Should().Be((ulong)i);
                ack.Item2.Should().Be(true);
            }
        }

        [Theory]
        [InlineData(100000)] // Single file
        [InlineData(10000)] // Rotating files
        public async Task StopsProcessingAfterCancellation(int messagesPerFile)
        {
            // Arrange
            var logger = new NullLogger<FileOutput>();
            var outputFile = new FileInfo(Path.Combine(_tempDir, "cancel.txt"));
            var fileConfig = new FileConfig { MessagesPerFile = messagesPerFile };
            var output = new FileOutput(logger,
                new OutputOptions { Format = OutputFormat.Plain, OutputFile = outputFile, Compact = false, Quiet = false, Verbose = false, NoColor = false },
                fileConfig, messageCount: 100000);

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

                    await messageChannel.Writer.WriteAsync(CreateRetrievedMessage($"Message {i}", deliveryTag: (ulong)i));

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
            _output.WriteLine($"Processed {result.ProcessedCount} messages before cancellation.");
        }
        
        [Theory]
        [InlineData(2)] // Single file
        [InlineData(1)] // Rotating files
        public async Task HandlesWriteError_SendsNack(int messagesPerFile)
        {
            // Arrange
            var logger = new NullLogger<FileOutput>();
            // Use a valid path, but trigger error during processing
            var outputFile = new FileInfo(Path.Combine(_tempDir, "output.txt"));
        
            var fileConfig = new FileConfig { MessagesPerFile = messagesPerFile };
            var output = new FileOutput(logger, new OutputOptions { Format = OutputFormat.Plain, OutputFile = outputFile, Compact = false, Quiet = false, Verbose = false, NoColor = false }, fileConfig, messageCount: 2);

            var messageChannel = Channel.CreateUnbounded<RetrievedMessage>();
            var ackChannel = Channel.CreateUnbounded<(ulong, bool)>();

            var message = CreateRetrievedMessage("Message 1", deliveryTag: 1);
            // Create a new properties object with invalid DeliveryMode to force FormatMessage to throw
            var invalidProps = message.Properties! with { DeliveryMode = (DeliveryModes)99 };
            var invalidMessage = message with { Properties = invalidProps };

            await messageChannel.Writer.WriteAsync(invalidMessage);
            messageChannel.Writer.Complete();

            // Act
            var result = await output.WriteMessagesAsync(messageChannel, ackChannel, CancellationToken.None);

            // Assert
            // Should receive a NACK (false)
            ackChannel.Reader.TryRead(out var ack).Should().BeTrue();
            ack.Item1.Should().Be(1);
            ack.Item2.Should().BeFalse();
            result.ProcessedCount.Should().Be(0);
            result.TotalBytes.Should().Be(0);
        }
    }

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