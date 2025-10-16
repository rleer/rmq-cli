using RmqCli.Infrastructure.RabbitMq;

namespace RmqCli.Unit.Tests.Infrastructure.RabbitMq;

public class ConsumeErrorInfoFactoryTests
{
    #region QueueNotFoundErrorInfo

    public class QueueNotFoundErrorInfo
    {
        [Fact]
        public void ReturnsCorrectErrorInfo_WithQueueName()
        {
            // Arrange
            var queueName = "test-queue";

            // Act
            var result = ConsumeErrorInfoFactory.QueueNotFoundErrorInfo(queueName);

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
            var result = ConsumeErrorInfoFactory.QueueNotFoundErrorInfo(queueName);

            // Assert
            result.Error.Should().Contain(queueName);
        }

        [Fact]
        public void HandlesEmptyQueueName()
        {
            // Arrange
            var queueName = "";

            // Act
            var result = ConsumeErrorInfoFactory.QueueNotFoundErrorInfo(queueName);

            // Assert
            result.Error.Should().Be("Queue '' not found");
        }

        [Fact]
        public void HandlesQueueWithSpecialCharacters()
        {
            // Arrange
            var queueName = "test-queue-123.abc_xyz";

            // Act
            var result = ConsumeErrorInfoFactory.QueueNotFoundErrorInfo(queueName);

            // Assert
            result.Error.Should().Contain(queueName);
        }

        [Fact]
        public void HasRoutingCategory()
        {
            // Arrange
            var queueName = "test-queue";

            // Act
            var result = ConsumeErrorInfoFactory.QueueNotFoundErrorInfo(queueName);

            // Assert
            result.Category.Should().Be("routing");
        }

        [Fact]
        public void HasSuggestion()
        {
            // Arrange
            var queueName = "test-queue";

            // Act
            var result = ConsumeErrorInfoFactory.QueueNotFoundErrorInfo(queueName);

            // Assert
            result.Suggestion.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public void HasNoDetails()
        {
            // Arrange
            var queueName = "test-queue";

            // Act
            var result = ConsumeErrorInfoFactory.QueueNotFoundErrorInfo(queueName);

            // Assert
            result.Details.Should().BeNull();
        }
    }

    #endregion
}
