using RmqCli.Shared.Factories;

namespace RmqCli.Unit.Tests.Shared.Factories;

public class ErrorInfoFactoryTests
{
    #region GenericErrorInfo

    public class GenericErrorInfo
    {
        [Fact]
        public void ReturnsCorrectErrorInfo_WithAllParameters()
        {
            // Arrange
            var error = "Something went wrong";
            var code = "ERROR_CODE";
            var suggestion = "Try again later";
            var category = "custom";

            // Act
            var result = ErrorInfoFactory.GenericErrorInfo(error, code, suggestion, category);

            // Assert
            result.Error.Should().Be(error);
            result.Code.Should().Be(code);
            result.Suggestion.Should().Be(suggestion);
            result.Category.Should().Be(category);
            result.Details.Should().BeNull();
        }

        [Fact]
        public void UsesDefaultCategory_WhenNotProvided()
        {
            // Arrange
            var error = "Error";
            var code = "CODE";
            var suggestion = "Suggestion";

            // Act
            var result = ErrorInfoFactory.GenericErrorInfo(error, code, suggestion);

            // Assert
            result.Category.Should().Be("internal");
        }

        [Fact]
        public void IncludesExceptionDetails_WhenExceptionProvided()
        {
            // Arrange
            var error = "Error occurred";
            var code = "EXCEPTION_CODE";
            var suggestion = "Check logs";
            var exception = new InvalidOperationException("Test exception");

            // Act
            var result = ErrorInfoFactory.GenericErrorInfo(error, code, suggestion, exception: exception);

            // Assert
            result.Details.Should().NotBeNull();
            result.Details!.Should().ContainKey("exception_type");
            result.Details["exception_type"].Should().Be("InvalidOperationException");
            result.Details.Should().ContainKey("exception_message");
            result.Details["exception_message"].Should().Be("Test exception");
        }

        [Fact]
        public void HasNullDetails_WhenNoExceptionProvided()
        {
            // Arrange
            var error = "Error";
            var code = "CODE";
            var suggestion = "Suggestion";

            // Act
            var result = ErrorInfoFactory.GenericErrorInfo(error, code, suggestion);

            // Assert
            result.Details.Should().BeNull();
        }

        [Fact]
        public void HandlesNullException_Explicitly()
        {
            // Arrange
            var error = "Error";
            var code = "CODE";
            var suggestion = "Suggestion";

            // Act
            var result = ErrorInfoFactory.GenericErrorInfo(error, code, suggestion, exception: null);

            // Assert
            result.Details.Should().BeNull();
        }

        [Fact]
        public void HandlesExceptionWithInnerException()
        {
            // Arrange
            var innerException = new ArgumentException("Inner exception");
            var exception = new InvalidOperationException("Outer exception", innerException);

            // Act
            var result = ErrorInfoFactory.GenericErrorInfo("Error", "CODE", "Suggestion", exception: exception);

            // Assert
            result.Details.Should().NotBeNull();
            result.Details!["exception_type"].Should().Be("InvalidOperationException");
            result.Details["exception_message"].Should().Be("Outer exception");
        }

        [Fact]
        public void HandlesVariousExceptionTypes()
        {
            // Arrange
            var exceptions = new Exception[]
            {
                new IOException("IO error"),
                new TimeoutException("Timeout"),
                new ArgumentNullException("param", "Null argument"),
                new UnauthorizedAccessException("No access")
            };

            // Act & Assert
            foreach (var exception in exceptions)
            {
                var result = ErrorInfoFactory.GenericErrorInfo("Error", "CODE", "Suggestion", exception: exception);

                result.Details.Should().NotBeNull();
                result.Details!["exception_type"].Should().Be(exception.GetType().Name);
                result.Details["exception_message"].Should().Be(exception.Message);
            }
        }

        [Fact]
        public void HandlesEmptyStrings()
        {
            // Arrange
            var error = "";
            var code = "";
            var suggestion = "";

            // Act
            var result = ErrorInfoFactory.GenericErrorInfo(error, code, suggestion);

            // Assert
            result.Error.Should().BeEmpty();
            result.Code.Should().BeEmpty();
            result.Suggestion.Should().BeEmpty();
            result.Category.Should().Be("internal");
        }

        [Fact]
        public void AllowsCustomCategories()
        {
            // Arrange
            var categories = new[] { "validation", "connection", "routing", "authentication", "custom" };

            // Act & Assert
            foreach (var category in categories)
            {
                var result = ErrorInfoFactory.GenericErrorInfo("Error", "CODE", "Suggestion", category);
                result.Category.Should().Be(category);
            }
        }

        [Fact]
        public void DetailsContainOnlyExceptionInfo_WhenExceptionProvided()
        {
            // Arrange
            var exception = new InvalidOperationException("Test");

            // Act
            var result = ErrorInfoFactory.GenericErrorInfo("Error", "CODE", "Suggestion", exception: exception);

            // Assert
            result.Details.Should().NotBeNull();
            result.Details!.Should().HaveCount(2);
            result.Details.Should().ContainKeys("exception_type", "exception_message");
        }
    }

    #endregion
}
