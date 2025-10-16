using System.Text.Json;
using RmqCli.Core.Models;

namespace RmqCli.Unit.Tests.Core.Models;

public class ErrorInfoTests
{
    #region Property Tests

    public class PropertyTests
    {
        [Fact]
        public void AllowsSettingCode()
        {
            // Arrange
            var errorInfo = new ErrorInfo();

            // Act
            errorInfo.Code = "TEST_CODE";

            // Assert
            errorInfo.Code.Should().Be("TEST_CODE");
        }

        [Fact]
        public void AllowsSettingError()
        {
            // Arrange
            var errorInfo = new ErrorInfo();

            // Act
            errorInfo.Error = "Test error message";

            // Assert
            errorInfo.Error.Should().Be("Test error message");
        }

        [Fact]
        public void AllowsSettingSuggestion()
        {
            // Arrange
            var errorInfo = new ErrorInfo();

            // Act
            errorInfo.Suggestion = "Test suggestion";

            // Assert
            errorInfo.Suggestion.Should().Be("Test suggestion");
        }

        [Fact]
        public void AllowsSettingCategory()
        {
            // Arrange
            var errorInfo = new ErrorInfo();

            // Act
            errorInfo.Category = "validation";

            // Assert
            errorInfo.Category.Should().Be("validation");
        }

        [Fact]
        public void AllowsSettingDetails()
        {
            // Arrange
            var errorInfo = new ErrorInfo();
            var details = new Dictionary<string, object> { { "key", "value" } };

            // Act
            errorInfo.Details = details;

            // Assert
            errorInfo.Details.Should().BeSameAs(details);
        }

        [Fact]
        public void InitializesCodeAsEmpty()
        {
            // Act
            var errorInfo = new ErrorInfo();

            // Assert
            errorInfo.Code.Should().BeEmpty();
        }

        [Fact]
        public void InitializesErrorAsEmpty()
        {
            // Act
            var errorInfo = new ErrorInfo();

            // Assert
            errorInfo.Error.Should().BeEmpty();
        }

        [Fact]
        public void InitializesCategoryAsEmpty()
        {
            // Act
            var errorInfo = new ErrorInfo();

            // Assert
            errorInfo.Category.Should().BeEmpty();
        }

        [Fact]
        public void InitializesSuggestionAsNull()
        {
            // Act
            var errorInfo = new ErrorInfo();

            // Assert
            errorInfo.Suggestion.Should().BeNull();
        }

        [Fact]
        public void InitializesDetailsAsNull()
        {
            // Act
            var errorInfo = new ErrorInfo();

            // Assert
            errorInfo.Details.Should().BeNull();
        }
    }

    #endregion

    #region JSON Serialization

    public class JsonSerialization
    {
        [Fact]
        public void SerializesAllProperties_WhenAllSet()
        {
            // Arrange
            var errorInfo = new ErrorInfo
            {
                Code = "TEST_CODE",
                Error = "Test error",
                Suggestion = "Test suggestion",
                Category = "validation",
                Details = new Dictionary<string, object> { { "key", "value" } }
            };

            // Act
            var json = JsonSerializer.Serialize(errorInfo);
            var parsed = JsonDocument.Parse(json);

            // Assert
            parsed.RootElement.GetProperty("code").GetString().Should().Be("TEST_CODE");
            parsed.RootElement.GetProperty("error").GetString().Should().Be("Test error");
            parsed.RootElement.GetProperty("suggestion").GetString().Should().Be("Test suggestion");
            parsed.RootElement.GetProperty("category").GetString().Should().Be("validation");
            parsed.RootElement.TryGetProperty("details", out var details).Should().BeTrue();
            details.GetProperty("key").GetString().Should().Be("value");
        }

        [Fact]
        public void OmitsNullSuggestion()
        {
            // Arrange
            var errorInfo = new ErrorInfo
            {
                Code = "CODE",
                Error = "Error",
                Category = "internal"
            };

            // Act
            var json = JsonSerializer.Serialize(errorInfo);
            var parsed = JsonDocument.Parse(json);

            // Assert
            parsed.RootElement.TryGetProperty("suggestion", out _).Should().BeFalse();
        }

        [Fact]
        public void OmitsNullDetails()
        {
            // Arrange
            var errorInfo = new ErrorInfo
            {
                Code = "CODE",
                Error = "Error",
                Category = "internal"
            };

            // Act
            var json = JsonSerializer.Serialize(errorInfo);
            var parsed = JsonDocument.Parse(json);

            // Assert
            parsed.RootElement.TryGetProperty("details", out _).Should().BeFalse();
        }

        [Fact]
        public void IncludesEmptyStrings()
        {
            // Arrange
            var errorInfo = new ErrorInfo
            {
                Code = "",
                Error = "",
                Category = "",
                Suggestion = ""
            };

            // Act
            var json = JsonSerializer.Serialize(errorInfo);
            var parsed = JsonDocument.Parse(json);

            // Assert
            parsed.RootElement.GetProperty("code").GetString().Should().BeEmpty();
            parsed.RootElement.GetProperty("error").GetString().Should().BeEmpty();
            parsed.RootElement.GetProperty("category").GetString().Should().BeEmpty();
            parsed.RootElement.GetProperty("suggestion").GetString().Should().BeEmpty();
        }

        [Fact]
        public void SerializesComplexDetails()
        {
            // Arrange
            var errorInfo = new ErrorInfo
            {
                Code = "CODE",
                Error = "Error",
                Category = "internal",
                Details = new Dictionary<string, object>
                {
                    { "string_value", "text" },
                    { "int_value", 42 },
                    { "bool_value", true },
                    { "nested", new Dictionary<string, object> { { "inner", "value" } } }
                }
            };

            // Act
            var json = JsonSerializer.Serialize(errorInfo);
            var parsed = JsonDocument.Parse(json);

            // Assert
            var details = parsed.RootElement.GetProperty("details");
            details.GetProperty("string_value").GetString().Should().Be("text");
            details.GetProperty("int_value").GetInt32().Should().Be(42);
            details.GetProperty("bool_value").GetBoolean().Should().BeTrue();
        }

        [Fact]
        public void UsesSnakeCase_ForPropertyNames()
        {
            // Arrange - Note: ErrorInfo uses explicit JsonPropertyName attributes with snake_case
            var errorInfo = new ErrorInfo
            {
                Code = "CODE",
                Error = "Error",
                Category = "internal"
            };

            // Act
            var json = JsonSerializer.Serialize(errorInfo);

            // Assert
            json.Should().Contain("\"code\"");
            json.Should().Contain("\"error\"");
            json.Should().Contain("\"category\"");
        }
    }

    #endregion

    #region JSON Deserialization

    public class JsonDeserialization
    {
        [Fact]
        public void DeserializesAllProperties()
        {
            // Arrange
            var json = @"{
                ""code"": ""TEST_CODE"",
                ""error"": ""Test error"",
                ""suggestion"": ""Test suggestion"",
                ""category"": ""validation"",
                ""details"": { ""key"": ""value"" }
            }";

            // Act
            var errorInfo = JsonSerializer.Deserialize<ErrorInfo>(json);

            // Assert
            errorInfo.Should().NotBeNull();
            errorInfo!.Code.Should().Be("TEST_CODE");
            errorInfo.Error.Should().Be("Test error");
            errorInfo.Suggestion.Should().Be("Test suggestion");
            errorInfo.Category.Should().Be("validation");
            errorInfo.Details.Should().NotBeNull();
            errorInfo.Details!["key"].ToString().Should().Be("value");
        }

        [Fact]
        public void DeserializesWithoutOptionalFields()
        {
            // Arrange
            var json = @"{
                ""code"": ""CODE"",
                ""error"": ""Error"",
                ""category"": ""internal""
            }";

            // Act
            var errorInfo = JsonSerializer.Deserialize<ErrorInfo>(json);

            // Assert
            errorInfo.Should().NotBeNull();
            errorInfo!.Code.Should().Be("CODE");
            errorInfo.Error.Should().Be("Error");
            errorInfo.Category.Should().Be("internal");
            errorInfo.Suggestion.Should().BeNull();
            errorInfo.Details.Should().BeNull();
        }

        [Fact]
        public void HandlesEmptyObject()
        {
            // Arrange
            var json = "{}";

            // Act
            var errorInfo = JsonSerializer.Deserialize<ErrorInfo>(json);

            // Assert
            errorInfo.Should().NotBeNull();
            errorInfo!.Code.Should().BeEmpty();
            errorInfo.Error.Should().BeEmpty();
            errorInfo.Category.Should().BeEmpty();
            errorInfo.Suggestion.Should().BeNull();
            errorInfo.Details.Should().BeNull();
        }

        [Fact]
        public void DeserializesComplexDetails()
        {
            // Arrange
            var json = @"{
                ""code"": ""CODE"",
                ""error"": ""Error"",
                ""category"": ""internal"",
                ""details"": {
                    ""string_value"": ""text"",
                    ""int_value"": 42,
                    ""bool_value"": true
                }
            }";

            // Act
            var errorInfo = JsonSerializer.Deserialize<ErrorInfo>(json);

            // Assert
            errorInfo.Should().NotBeNull();
            errorInfo!.Details.Should().NotBeNull();
            errorInfo.Details!.Should().ContainKey("string_value");
            errorInfo.Details.Should().ContainKey("int_value");
            errorInfo.Details.Should().ContainKey("bool_value");
        }
    }

    #endregion

    #region Round-trip Tests

    public class RoundTripTests
    {
        [Fact]
        public void RoundTripsCompleteErrorInfo()
        {
            // Arrange
            var original = new ErrorInfo
            {
                Code = "TEST_CODE",
                Error = "Test error",
                Suggestion = "Test suggestion",
                Category = "validation",
                Details = new Dictionary<string, object>
                {
                    { "key1", "value1" },
                    { "key2", 42 }
                }
            };

            // Act
            var json = JsonSerializer.Serialize(original);
            var deserialized = JsonSerializer.Deserialize<ErrorInfo>(json);

            // Assert
            deserialized.Should().NotBeNull();
            deserialized!.Code.Should().Be(original.Code);
            deserialized.Error.Should().Be(original.Error);
            deserialized.Suggestion.Should().Be(original.Suggestion);
            deserialized.Category.Should().Be(original.Category);
            deserialized.Details.Should().NotBeNull();
        }

        [Fact]
        public void RoundTripsMinimalErrorInfo()
        {
            // Arrange
            var original = new ErrorInfo
            {
                Code = "CODE",
                Error = "Error",
                Category = "internal"
            };

            // Act
            var json = JsonSerializer.Serialize(original);
            var deserialized = JsonSerializer.Deserialize<ErrorInfo>(json);

            // Assert
            deserialized.Should().NotBeNull();
            deserialized!.Code.Should().Be(original.Code);
            deserialized.Error.Should().Be(original.Error);
            deserialized.Category.Should().Be(original.Category);
            deserialized.Suggestion.Should().BeNull();
            deserialized.Details.Should().BeNull();
        }
    }

    #endregion

    #region Category Tests

    public class CategoryTests
    {
        [Theory]
        [InlineData("validation")]
        [InlineData("connection")]
        [InlineData("routing")]
        [InlineData("internal")]
        [InlineData("authentication")]
        public void SupportsCommonCategories(string category)
        {
            // Arrange
            var errorInfo = new ErrorInfo { Category = category };

            // Act
            var json = JsonSerializer.Serialize(errorInfo);
            var deserialized = JsonSerializer.Deserialize<ErrorInfo>(json);

            // Assert
            deserialized.Should().NotBeNull();
            deserialized!.Category.Should().Be(category);
        }
    }

    #endregion
}
