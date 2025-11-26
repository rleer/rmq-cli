using RabbitMQ.Client;
using RmqCli.Core.Models;

namespace RmqCli.Unit.Tests.Core.Models;

public class QueueInfoTests
{
    public class PropertyTests
    {
        [Fact]
        public void InitializesExistsAsFalse()
        {
            // Act
            var queueInfo = new QueueInfo();

            // Assert
            queueInfo.Exists.Should().BeFalse();
        }

        [Fact]
        public void InitializesQueueAsEmpty()
        {
            // Act
            var queueInfo = new QueueInfo();

            // Assert
            queueInfo.Queue.Should().BeEmpty();
        }

        [Fact]
        public void InitializesMessageCountAsZero()
        {
            // Act
            var queueInfo = new QueueInfo();

            // Assert
            queueInfo.MessageCount.Should().Be(0);
        }

        [Fact]
        public void InitializesConsumerCountAsZero()
        {
            // Act
            var queueInfo = new QueueInfo();

            // Assert
            queueInfo.ConsumerCount.Should().Be(0);
        }

        [Fact]
        public void InitializesQueueErrorAsNull()
        {
            // Act
            var queueInfo = new QueueInfo();

            // Assert
            queueInfo.QueueError.Should().BeNull();
        }

        [Fact]
        public void InitializesHasErrorAsFalse()
        {
            // Act
            var queueInfo = new QueueInfo();

            // Assert
            queueInfo.HasError.Should().BeFalse();
        }

        [Fact]
        public void AllowsSettingHasError()
        {
            // Arrange
            var queueInfo = new QueueInfo();

            // Act
            queueInfo.HasError = true;

            // Assert
            queueInfo.HasError.Should().BeTrue();
        }
    }

    public class CreateMethod
    {
        private static QueueDeclareOk CreateQueueDeclareOk(string queueName, uint messageCount, uint consumerCount)
        {
            return new QueueDeclareOk(queueName, messageCount, consumerCount);
        }

        [Fact]
        public void CreatesQueueInfoFromQueueDeclareOk()
        {
            // Arrange
            var queueDeclareOk = CreateQueueDeclareOk("test-queue", 5, 2);

            // Act
            var queueInfo = QueueInfo.Create(queueDeclareOk);

            // Assert
            queueInfo.Should().NotBeNull();
            queueInfo.Exists.Should().BeTrue();
            queueInfo.Queue.Should().Be("test-queue");
            queueInfo.MessageCount.Should().Be(5);
            queueInfo.ConsumerCount.Should().Be(2);
            queueInfo.HasError.Should().BeFalse();
            queueInfo.QueueError.Should().BeNull();
        }

        [Fact]
        public void HandlesZeroMessageCount()
        {
            // Arrange
            var queueDeclareOk = CreateQueueDeclareOk("empty-queue", 0, 0);

            // Act
            var queueInfo = QueueInfo.Create(queueDeclareOk);

            // Assert
            queueInfo.MessageCount.Should().Be(0);
            queueInfo.ConsumerCount.Should().Be(0);
        }

        [Fact]
        public void HandlesLargeMessageCount()
        {
            // Arrange
            var queueDeclareOk = CreateQueueDeclareOk("large-queue", 1000000, 10);

            // Act
            var queueInfo = QueueInfo.Create(queueDeclareOk);

            // Assert
            queueInfo.MessageCount.Should().Be(1000000);
            queueInfo.ConsumerCount.Should().Be(10);
        }

        [Fact]
        public void SetsExistsToTrue()
        {
            // Arrange
            var queueDeclareOk = CreateQueueDeclareOk("test-queue", 0, 0);

            // Act
            var queueInfo = QueueInfo.Create(queueDeclareOk);

            // Assert
            queueInfo.Exists.Should().BeTrue();
        }

        [Fact]
        public void SetsHasErrorToFalse()
        {
            // Arrange
            var queueDeclareOk = CreateQueueDeclareOk("test-queue", 0, 0);

            // Act
            var queueInfo = QueueInfo.Create(queueDeclareOk);

            // Assert
            queueInfo.HasError.Should().BeFalse();
        }

        [Fact]
        public void SetsQueueErrorToNull()
        {
            // Arrange
            var queueDeclareOk = CreateQueueDeclareOk("test-queue", 0, 0);

            // Act
            var queueInfo = QueueInfo.Create(queueDeclareOk);

            // Assert
            queueInfo.QueueError.Should().BeNull();
        }
    }

    public class CreateErrorMethod
    {
        [Fact]
        public void CreatesErrorQueueInfo()
        {
            // Arrange
            var errorInfo = new ErrorInfo
            {
                Error = "Queue not found",
                Suggestion = "Check queue name"
            };

            // Act
            var queueInfo = QueueInfo.CreateError("missing-queue", errorInfo);

            // Assert
            queueInfo.Should().NotBeNull();
            queueInfo.Exists.Should().BeFalse();
            queueInfo.Queue.Should().Be("missing-queue");
            queueInfo.MessageCount.Should().Be(0);
            queueInfo.ConsumerCount.Should().Be(0);
            queueInfo.HasError.Should().BeTrue();
            queueInfo.QueueError.Should().BeSameAs(errorInfo);
        }

        [Fact]
        public void SetsExistsToFalse()
        {
            // Arrange
            var errorInfo = new ErrorInfo { Error = "Test error" };

            // Act
            var queueInfo = QueueInfo.CreateError("test-queue", errorInfo);

            // Assert
            queueInfo.Exists.Should().BeFalse();
        }

        [Fact]
        public void SetsHasErrorToTrue()
        {
            // Arrange
            var errorInfo = new ErrorInfo { Error = "Test error" };

            // Act
            var queueInfo = QueueInfo.CreateError("test-queue", errorInfo);

            // Assert
            queueInfo.HasError.Should().BeTrue();
        }

        [Fact]
        public void SetsMessageCountToZero()
        {
            // Arrange
            var errorInfo = new ErrorInfo { Error = "Test error" };

            // Act
            var queueInfo = QueueInfo.CreateError("test-queue", errorInfo);

            // Assert
            queueInfo.MessageCount.Should().Be(0);
        }

        [Fact]
        public void SetsConsumerCountToZero()
        {
            // Arrange
            var errorInfo = new ErrorInfo { Error = "Test error" };

            // Act
            var queueInfo = QueueInfo.CreateError("test-queue", errorInfo);

            // Assert
            queueInfo.ConsumerCount.Should().Be(0);
        }

        [Fact]
        public void AssignsQueueName()
        {
            // Arrange
            var errorInfo = new ErrorInfo { Error = "Test error" };

            // Act
            var queueInfo = QueueInfo.CreateError("my-queue", errorInfo);

            // Assert
            queueInfo.Queue.Should().Be("my-queue");
        }

        [Fact]
        public void AssignsErrorInfo()
        {
            // Arrange
            var errorInfo = new ErrorInfo
            {
                Error = "Connection failed",
                Suggestion = "Check credentials",
                Details = new Dictionary<string, object> { { "code", 404 } }
            };

            // Act
            var queueInfo = QueueInfo.CreateError("test-queue", errorInfo);

            // Assert
            queueInfo.QueueError.Should().BeSameAs(errorInfo);
            queueInfo.QueueError.Error.Should().Be("Connection failed");
            queueInfo.QueueError.Suggestion.Should().Be("Check credentials");
            queueInfo.QueueError.Details.Should().ContainKey("code");
        }
    }
}

