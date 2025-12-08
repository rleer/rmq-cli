using RmqCli.Commands.Purge;
using RmqCli.Core.Models;
using RmqCli.Shared.Json;
using RmqCli.Shared.Output;

namespace RmqCli.Integration.Tests.Commands.Purge;

[Collection("ConsoleOutputTests")]
public class PurgeOutputServiceTests : IDisposable
{
    private readonly StringWriter _stringWriter;
    private readonly TextWriter _originalError;

    public PurgeOutputServiceTests()
    {
        _stringWriter = new StringWriter();
        _originalError = Console.Error;
        Console.SetError(_stringWriter);
    }

    public void Dispose()
    {
        Console.SetError(_originalError);
        _stringWriter.Dispose();
    }

    [Theory]
    [InlineData(OutputFormat.Plain)]
    [InlineData(OutputFormat.Table)]
    public void Write_PlainFormat_WritesExpectedOutput(OutputFormat format)
    {
        // Arrange
        var options = new OutputOptions { Format = format, NoColor = true };
        var service = new PurgeOutputService(options);
        var response = new PurgeResponse
        {
            Queue = "test-queue",
            Vhost = "/"
        };

        // Act
        service.Write(response);

        // Assert
        var output = _stringWriter.ToString();
        output.Should().Contain("Queue test-queue in vhost / was purged successfully");
    }

    [Fact]
    public void Write_JsonFormat_WritesJson()
    {
        // Arrange
        var options = new OutputOptions { Format = OutputFormat.Json };
        var service = new PurgeOutputService(options);
        var timestamp = DateTime.Now;
        var response = new PurgeResponse
        {
            Status = "success",
            Timestamp = timestamp,
            Queue = "test-queue",
            Vhost = "/",
            Operation = "purge"
        };

        // Act
        service.Write(response);

        // Assert
        Console.Error.Flush();
        var output = _stringWriter.ToString();
        output.Should().NotBeNullOrWhiteSpace();
        var deserialized = System.Text.Json.JsonSerializer.Deserialize(output, JsonSerializationContext.RelaxedEscaping.PurgeResponse);
        deserialized.Should().NotBeNull();
        deserialized.Status.Should().Be("success");
        deserialized.Timestamp.Should().Be(timestamp);
        deserialized.Queue.Should().Be("test-queue");
        deserialized.Vhost.Should().Be("/");
        deserialized.Operation.Should().Be("purge");
    }

    [Fact]
    public void Write_QuietMode_WritesNothing()
    {
        // Arrange
        var options = new OutputOptions { Format = OutputFormat.Plain, Quiet = true };
        var service = new PurgeOutputService(options);
        var response = new PurgeResponse
        {
            Queue = "test-queue",
            Vhost = "/"
        };

        // Act
        service.Write(response);

        // Assert
        _stringWriter.ToString().Should().BeEmpty();
    }

    [Fact]
    public void Write_JsonFormat_WithErrorStatus_WritesJson()
    {
        // Arrange
        var options = new OutputOptions { Format = OutputFormat.Json };
        var service = new PurgeOutputService(options);
        var timestamp = DateTime.Now;
        var response = new PurgeResponse
        {
            Status = "failure",
            Timestamp = timestamp,
            Error = new ErrorInfo
            {
                Error = "Queue not found",
                Suggestion = "Please verify the queue name and try again."
            },
            Queue = "nonexistent-queue",
            Vhost = "/",
            Operation = "purge"
        };

        // Act
        service.Write(response);

        // Assert
        Console.Error.Flush();
        var output = _stringWriter.ToString();
        output.Should().NotBeNullOrWhiteSpace();
        var deserialized = System.Text.Json.JsonSerializer.Deserialize(output, JsonSerializationContext.RelaxedEscaping.PurgeResponse);
        deserialized.Should().NotBeNull();
        deserialized.Status.Should().Be("failure");
        deserialized.Queue.Should().Be("nonexistent-queue");
        deserialized.Vhost.Should().Be("/");
        deserialized.Operation.Should().Be("purge");
        deserialized.Error.Should().NotBeNull();
        deserialized.Error.Error.Should().Be("Queue not found");
        deserialized.Error.Suggestion.Should().Be("Please verify the queue name and try again.");
    }
}
