using RmqCli.Shared.Factories;

namespace RmqCli.Unit.Tests.Shared.Factories;

public class RabbitErrorInfoFactoryTests
{
    #region QueueNotFound

    public class QueueNotFound
    {
        [Fact]
        public void ReturnsCorrectErrorInfo_WithQueueName()
        {
            // Arrange
            var queueName = "test-queue";

            // Act
            var result = RabbitErrorInfoFactory.QueueNotFound(queueName);

            // Assert
            result.Code.Should().Be("QUEUE_NOT_FOUND");
            result.Category.Should().Be("routing");
            result.Error.Should().Be("Queue 'test-queue' not found");
            result.Suggestion.Should().Be("Check if the queue exists and is correctly configured");
        }

        [Fact]
        public void IncludesQueueName_InErrorMessage()
        {
            // Arrange
            var queueName = "my-important-queue";

            // Act
            var result = RabbitErrorInfoFactory.QueueNotFound(queueName);

            // Assert
            result.Error.Should().Contain(queueName);
        }

        [Fact]
        public void HandlesEmptyQueueName()
        {
            // Arrange
            var queueName = "";

            // Act
            var result = RabbitErrorInfoFactory.QueueNotFound(queueName);

            // Assert
            result.Error.Should().Be("Queue '' not found");
        }

        [Fact]
        public void HandlesQueueWithSpecialCharacters()
        {
            // Arrange
            var queueName = "test-queue-123.abc_xyz";

            // Act
            var result = RabbitErrorInfoFactory.QueueNotFound(queueName);

            // Assert
            result.Error.Should().Contain(queueName);
        }

        [Fact]
        public void HasRoutingCategory()
        {
            // Arrange
            var queueName = "test-queue";

            // Act
            var result = RabbitErrorInfoFactory.QueueNotFound(queueName);

            // Assert
            result.Category.Should().Be("routing");
        }

        [Fact]
        public void HasSuggestion()
        {
            // Arrange
            var queueName = "test-queue";

            // Act
            var result = RabbitErrorInfoFactory.QueueNotFound(queueName);

            // Assert
            result.Suggestion.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public void HasNoDetails()
        {
            // Arrange
            var queueName = "test-queue";

            // Act
            var result = RabbitErrorInfoFactory.QueueNotFound(queueName);

            // Assert
            result.Details.Should().BeNull();
        }
    }

    #endregion

    #region OperationInterrupted

    public class OperationInterrupted
    {
        [Fact]
        public void ReturnsCorrectErrorInfo_WithReasonAndCode()
        {
            // Arrange
            var reason = "NOT_FOUND - no exchange 'test-exchange'";
            var code = "404";

            // Act
            var result = RabbitErrorInfoFactory.OperationInterrupted(reason, code);

            // Assert
            result.Code.Should().Be("RABBITMQ_OPERATION_INTERRUPTED");
            result.Category.Should().Be("connection");
            result.Error.Should().Be($"{reason} ({code})");
            result.Suggestion.Should().Be("Check RabbitMQ server status and network connectivity");
        }

        [Fact]
        public void HandlesEmptyReason()
        {
            // Arrange
            var reason = "";
            var code = "500";

            // Act
            var result = RabbitErrorInfoFactory.OperationInterrupted(reason, code);

            // Assert
            result.Error.Should().Be(" (500)");
        }

        [Fact]
        public void HandlesEmptyCode()
        {
            // Arrange
            var reason = "Connection lost";
            var code = "";

            // Act
            var result = RabbitErrorInfoFactory.OperationInterrupted(reason, code);

            // Assert
            result.Error.Should().Be("Connection lost ()");
        }
    }

    #endregion

    #region VirtualHostNotFound

    public class VirtualHostNotFound
    {
        [Fact]
        public void ReturnsCorrectErrorInfo_WithVhostName()
        {
            // Arrange
            var vhost = "/test-vhost";

            // Act
            var result = RabbitErrorInfoFactory.VirtualHostNotFound(vhost);

            // Assert
            result.Code.Should().Be("VIRTUAL_HOST_NOT_FOUND");
            result.Category.Should().Be("connection");
            result.Error.Should().Be("Virtual host '/test-vhost' not found");
            result.Suggestion.Should().Be("Check if the virtual host exists and is correctly configured");
        }

        [Fact]
        public void HandlesDefaultVhost()
        {
            // Arrange
            var vhost = "/";

            // Act
            var result = RabbitErrorInfoFactory.VirtualHostNotFound(vhost);

            // Assert
            result.Error.Should().Contain("'/'");
        }

        [Fact]
        public void HandlesEmptyVhost()
        {
            // Arrange
            var vhost = "";

            // Act
            var result = RabbitErrorInfoFactory.VirtualHostNotFound(vhost);

            // Assert
            result.Error.Should().Be("Virtual host '' not found");
        }
    }

    #endregion

    #region AccessDenied

    public class AccessDenied
    {
        [Fact]
        public void ReturnsCorrectErrorInfo_WithUserAndVhost()
        {
            // Arrange
            var user = "testuser";
            var vhost = "/test";

            // Act
            var result = RabbitErrorInfoFactory.AccessDenied(user, vhost);

            // Assert
            result.Code.Should().Be("ACCESS_DENIED");
            result.Category.Should().Be("connection");
            result.Error.Should().Be("Access denied for user 'testuser' to virtual host '/test'");
            result.Suggestion.Should().Be("Check user permissions and virtual host configuration");
        }

        [Fact]
        public void HandlesGuestUser()
        {
            // Arrange
            var user = "guest";
            var vhost = "/";

            // Act
            var result = RabbitErrorInfoFactory.AccessDenied(user, vhost);

            // Assert
            result.Error.Should().Contain("guest");
        }

        [Fact]
        public void HandlesEmptyValues()
        {
            // Arrange
            var user = "";
            var vhost = "";

            // Act
            var result = RabbitErrorInfoFactory.AccessDenied(user, vhost);

            // Assert
            result.Error.Should().Be("Access denied for user '' to virtual host ''");
        }
    }

    #endregion

    #region AuthenticationFailed

    public class AuthenticationFailed
    {
        [Fact]
        public void ReturnsCorrectErrorInfo_WithUsername()
        {
            // Arrange
            var user = "testuser";

            // Act
            var result = RabbitErrorInfoFactory.AuthenticationFailed(user);

            // Assert
            result.Code.Should().Be("AUTHENTICATION_FAILED");
            result.Category.Should().Be("connection");
            result.Error.Should().Be("Authentication failed for user 'testuser'");
            result.Suggestion.Should().Be("Check username and password");
        }

        [Fact]
        public void HandlesGuestUser()
        {
            // Arrange
            var user = "guest";

            // Act
            var result = RabbitErrorInfoFactory.AuthenticationFailed(user);

            // Assert
            result.Error.Should().Contain("guest");
        }

        [Fact]
        public void HandlesEmptyUsername()
        {
            // Arrange
            var user = "";

            // Act
            var result = RabbitErrorInfoFactory.AuthenticationFailed(user);

            // Assert
            result.Error.Should().Be("Authentication failed for user ''");
        }
    }

    #endregion

    #region ConnectionFailed

    public class ConnectionFailed
    {
        [Fact]
        public void ReturnsCorrectErrorInfo_WithHostAndPort()
        {
            // Arrange
            var host = "localhost";
            var port = 5672;

            // Act
            var result = RabbitErrorInfoFactory.ConnectionFailed(host, port);

            // Assert
            result.Code.Should().Be("CONNECTION_FAILED");
            result.Category.Should().Be("connection");
            result.Error.Should().Be("Could not connect to RabbitMQ at localhost:5672");
            result.Suggestion.Should().Be("Check RabbitMQ server status and network connectivity");
        }

        [Fact]
        public void HandlesRemoteHost()
        {
            // Arrange
            var host = "rmq.example.com";
            var port = 5672;

            // Act
            var result = RabbitErrorInfoFactory.ConnectionFailed(host, port);

            // Assert
            result.Error.Should().Contain("rmq.example.com:5672");
        }

        [Fact]
        public void HandlesCustomPort()
        {
            // Arrange
            var host = "localhost";
            var port = 15672;

            // Act
            var result = RabbitErrorInfoFactory.ConnectionFailed(host, port);

            // Assert
            result.Error.Should().Contain(":15672");
        }

        [Fact]
        public void HandlesIPAddress()
        {
            // Arrange
            var host = "192.168.1.100";
            var port = 5672;

            // Act
            var result = RabbitErrorInfoFactory.ConnectionFailed(host, port);

            // Assert
            result.Error.Should().Contain("192.168.1.100:5672");
        }
    }

    #endregion

    #region BrokerUnreachable

    public class BrokerUnreachable
    {
        [Fact]
        public void ReturnsCorrectErrorInfo_WithHostAndPort()
        {
            // Arrange
            var host = "localhost";
            var port = 5672;

            // Act
            var result = RabbitErrorInfoFactory.BrokerUnreachable(host, port);

            // Assert
            result.Code.Should().Be("BROKER_UNREACHABLE");
            result.Category.Should().Be("connection");
            result.Error.Should().Be("RabbitMQ broker unreachable at localhost:5672");
            result.Suggestion.Should().Be("Check RabbitMQ server status and network connectivity");
        }

        [Fact]
        public void HandlesRemoteHost()
        {
            // Arrange
            var host = "rmq-prod.example.com";
            var port = 5672;

            // Act
            var result = RabbitErrorInfoFactory.BrokerUnreachable(host, port);

            // Assert
            result.Error.Should().Contain("rmq-prod.example.com:5672");
        }

        [Fact]
        public void HandlesCustomPort()
        {
            // Arrange
            var host = "localhost";
            var port = 5673;

            // Act
            var result = RabbitErrorInfoFactory.BrokerUnreachable(host, port);

            // Assert
            result.Error.Should().Contain(":5673");
        }
    }

    #endregion

    #region Consistency Tests

    public class ConsistencyTests
    {
        [Fact]
        public void AllConnectionErrors_HaveConnectionCategory()
        {
            // Act
            var errors = new[]
            {
                RabbitErrorInfoFactory.OperationInterrupted("reason", "code"),
                RabbitErrorInfoFactory.VirtualHostNotFound("vhost"),
                RabbitErrorInfoFactory.AccessDenied("user", "vhost"),
                RabbitErrorInfoFactory.AuthenticationFailed("user"),
                RabbitErrorInfoFactory.ConnectionFailed("host", 5672),
                RabbitErrorInfoFactory.BrokerUnreachable("host", 5672)
            };

            // Assert
            errors.Should().AllSatisfy(error => error.Category.Should().Be("connection"));
        }

        [Fact]
        public void AllErrors_HaveNonEmptyCode()
        {
            // Act
            var errors = new[]
            {
                RabbitErrorInfoFactory.OperationInterrupted("reason", "code"),
                RabbitErrorInfoFactory.VirtualHostNotFound("vhost"),
                RabbitErrorInfoFactory.AccessDenied("user", "vhost"),
                RabbitErrorInfoFactory.AuthenticationFailed("user"),
                RabbitErrorInfoFactory.ConnectionFailed("host", 5672),
                RabbitErrorInfoFactory.BrokerUnreachable("host", 5672)
            };

            // Assert
            errors.Should().AllSatisfy(error => error.Code.Should().NotBeNullOrEmpty());
        }

        [Fact]
        public void AllErrors_HaveSuggestions()
        {
            // Act
            var errors = new[]
            {
                RabbitErrorInfoFactory.OperationInterrupted("reason", "code"),
                RabbitErrorInfoFactory.VirtualHostNotFound("vhost"),
                RabbitErrorInfoFactory.AccessDenied("user", "vhost"),
                RabbitErrorInfoFactory.AuthenticationFailed("user"),
                RabbitErrorInfoFactory.ConnectionFailed("host", 5672),
                RabbitErrorInfoFactory.BrokerUnreachable("host", 5672)
            };

            // Assert
            errors.Should().AllSatisfy(error => error.Suggestion.Should().NotBeNullOrEmpty());
        }
    }

    #endregion
}