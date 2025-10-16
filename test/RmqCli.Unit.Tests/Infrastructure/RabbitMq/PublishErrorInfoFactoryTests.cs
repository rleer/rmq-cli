using RmqCli.Infrastructure.RabbitMq;

namespace RmqCli.Unit.Tests.Infrastructure.RabbitMq;

public class PublishErrorInfoFactoryTests
{
    #region NoRouteErrorInfo

    public class NoRouteErrorInfo
    {
        [Fact]
        public void ReturnsQueueSuggestion_WhenIsQueueIsTrue()
        {
            // Act
            var result = PublishErrorInfoFactory.NoRouteErrorInfo(isQueue: true);

            // Assert
            result.Code.Should().Be("NO_ROUTE");
            result.Category.Should().Be("routing");
            result.Error.Should().Be("No route to destination");
            result.Suggestion.Should().Be("Check if the queue exists");
        }

        [Fact]
        public void ReturnsExchangeSuggestion_WhenIsQueueIsFalse()
        {
            // Act
            var result = PublishErrorInfoFactory.NoRouteErrorInfo(isQueue: false);

            // Assert
            result.Code.Should().Be("NO_ROUTE");
            result.Category.Should().Be("routing");
            result.Error.Should().Be("No route to destination");
            result.Suggestion.Should().Be("Check if the exchange and routing key exist");
        }

        [Fact]
        public void ReturnsConsistentCode_RegardlessOfIsQueueValue()
        {
            // Act
            var resultQueue = PublishErrorInfoFactory.NoRouteErrorInfo(isQueue: true);
            var resultExchange = PublishErrorInfoFactory.NoRouteErrorInfo(isQueue: false);

            // Assert
            resultQueue.Code.Should().Be(resultExchange.Code);
            resultQueue.Category.Should().Be(resultExchange.Category);
            resultQueue.Error.Should().Be(resultExchange.Error);
        }
    }

    #endregion

    #region ExchangeNotFoundErrorInfo

    public class ExchangeNotFoundErrorInfo
    {
        [Fact]
        public void ReturnsCorrectErrorInfo()
        {
            // Act
            var result = PublishErrorInfoFactory.ExchangeNotFoundErrorInfo();

            // Assert
            result.Code.Should().Be("EXCHANGE_NOT_FOUND");
            result.Category.Should().Be("routing");
            result.Error.Should().Be("Exchange not found");
            result.Suggestion.Should().Be("Check if the exchange exists and is correctly configured");
        }

        [Fact]
        public void HasNoDetails()
        {
            // Act
            var result = PublishErrorInfoFactory.ExchangeNotFoundErrorInfo();

            // Assert
            result.Details.Should().BeNull();
        }
    }

    #endregion

    #region MaxSizeExceededErrorInfo

    public class MaxSizeExceededErrorInfo
    {
        [Fact]
        public void ParsesMessageSize_WhenErrorTextMatchesPattern()
        {
            // Arrange
            var errorText = "message size 1048576 exceeds max size 524288";

            // Act
            var result = PublishErrorInfoFactory.MaxSizeExceededErrorInfo(errorText);

            // Assert
            result.Code.Should().Be("MESSAGE_SIZE_EXCEEDED");
            result.Category.Should().Be("validation");
            result.Error.Should().Contain("1 MB");
            result.Error.Should().Contain("512 KB");
            result.Details.Should().NotBeNull();
            result.Details!.Should().ContainKey("max_size");
            result.Details.Should().ContainKey("message_size");
        }

        [Fact]
        public void HandlesNoMatch_WhenErrorTextDoesNotMatchPattern()
        {
            // Arrange
            var errorText = "Some random error text";

            // Act
            var result = PublishErrorInfoFactory.MaxSizeExceededErrorInfo(errorText);

            // Assert
            result.Code.Should().Be("MESSAGE_SIZE_EXCEEDED");
            result.Category.Should().Be("validation");
            result.Error.Should().Be("Message size exceeds maximum allowed size");
            result.Suggestion.Should().Contain("Reduce the message size");
        }

        [Fact]
        public void IncludesDetails_WithParsedSizes()
        {
            // Arrange
            var errorText = "message size 2097152 exceeds max size 1048576";

            // Act
            var result = PublishErrorInfoFactory.MaxSizeExceededErrorInfo(errorText);

            // Assert
            result.Details.Should().NotBeNull();
            result.Details!["max_size"].Should().Be(" 1 MB");
            result.Details["message_size"].Should().Be("2 MB ");
        }

        [Fact]
        public void FormatsLargeSizes_Correctly()
        {
            // Arrange
            var errorText = "message size 1073741824 exceeds max size 536870912";

            // Act
            var result = PublishErrorInfoFactory.MaxSizeExceededErrorInfo(errorText);

            // Assert
            result.Error.Should().Contain("1 GB");
            result.Error.Should().Contain("512 MB");
        }

        [Fact]
        public void FormatsSmallSizes_InBytes()
        {
            // Arrange
            var errorText = "message size 512 exceeds max size 256";

            // Act
            var result = PublishErrorInfoFactory.MaxSizeExceededErrorInfo(errorText);

            // Assert
            result.Error.Should().Contain("512 bytes");
            result.Error.Should().Contain("256 bytes");
        }

        [Fact]
        public void HandlesSuggestion_WithMaxSizeInBytes()
        {
            // Arrange
            var errorText = "message size 1000 exceeds max size 500";

            // Act
            var result = PublishErrorInfoFactory.MaxSizeExceededErrorInfo(errorText);

            // Assert
            result.Suggestion.Should().Contain("500 bytes");
        }

        [Fact]
        public void HandlesEmptyString()
        {
            // Arrange
            var errorText = "";

            // Act
            var result = PublishErrorInfoFactory.MaxSizeExceededErrorInfo(errorText);

            // Assert
            result.Code.Should().Be("MESSAGE_SIZE_EXCEEDED");
            result.Error.Should().Be("Message size exceeds maximum allowed size");
        }
    }

    #endregion
}
