using System.Text.Json;
using RmqCli.Core.Models;
using RmqCli.Shared.Json;

namespace RmqCli.Unit.Tests.Core.Models;

public class ErrorInfoTests
{
    public class PropertyTests
    {
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
        public void InitializesErrorAsEmpty()
        {
            // Act
            var errorInfo = new ErrorInfo();

            // Assert
            errorInfo.Error.Should().BeEmpty();
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

    public class JsonSerialization
    {
        [Fact]
        public void SerializesAllProperties_WhenAllSet()
        {
            // Arrange
            var errorInfo = new ErrorInfo
            {
                Error = "Test error",
                Suggestion = "Test suggestion",
                Details = new Dictionary<string, object> { { "key", "value" } }
            };

            // Act
            var json = JsonSerializer.Serialize(errorInfo, JsonSerializationContext.RelaxedEscaping.ErrorInfo);
            var parsed = JsonDocument.Parse(json);

            // Assert
            parsed.RootElement.GetProperty("error").GetString().Should().Be("Test error");
            parsed.RootElement.GetProperty("suggestion").GetString().Should().Be("Test suggestion");
            parsed.RootElement.TryGetProperty("details", out var details).Should().BeTrue();
            details.GetProperty("key").GetString().Should().Be("value");
        }

        [Fact]
        public void OmitsNullSuggestion()
        {
            // Arrange
            var errorInfo = new ErrorInfo
            {
                Error = "Error"
            };

            // Act
            var json = JsonSerializer.Serialize(errorInfo, JsonSerializationContext.RelaxedEscaping.ErrorInfo);
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
                Error = "Error"
            };

            // Act
            var json = JsonSerializer.Serialize(errorInfo, JsonSerializationContext.RelaxedEscaping.ErrorInfo);
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
                Error = "",
                Suggestion = ""
            };

            // Act
            var json = JsonSerializer.Serialize(errorInfo, JsonSerializationContext.RelaxedEscaping.ErrorInfo);
            var parsed = JsonDocument.Parse(json);

            // Assert
            parsed.RootElement.GetProperty("error").GetString().Should().BeEmpty();
            parsed.RootElement.GetProperty("suggestion").GetString().Should().BeEmpty();
        }

        [Fact]
        public void SerializesComplexDetails()
        {
            // Arrange
            var errorInfo = new ErrorInfo
            {
                Error = "Error",
                Details = new Dictionary<string, object>
                {
                    { "string_value", "text" },
                    { "int_value", 42 },
                    { "bool_value", true },
                    { "nested", new Dictionary<string, object> { { "inner", "value" } } }
                }
            };

            // Act
            var json = JsonSerializer.Serialize(errorInfo, JsonSerializationContext.RelaxedEscaping.ErrorInfo);
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
                Error = "Error"
            };

            // Act
            var json = JsonSerializer.Serialize(errorInfo, JsonSerializationContext.RelaxedEscaping.ErrorInfo);

            // Assert
            json.Should().Contain("\"error\"");
        }
    }

    public class JsonDeserialization
    {
        [Fact]
        public void DeserializesAllProperties()
        {
            // Arrange
            var json = @"{
                ""error"": ""Test error"",
                ""suggestion"": ""Test suggestion"",
                ""details"": { ""key"": ""value"" }
            }";

            // Act
            var errorInfo = JsonSerializer.Deserialize(json, JsonSerializationContext.RelaxedEscaping.ErrorInfo);

            // Assert
            errorInfo.Should().NotBeNull();
            errorInfo!.Error.Should().Be("Test error");
            errorInfo.Suggestion.Should().Be("Test suggestion");
            errorInfo.Details.Should().NotBeNull();
            errorInfo.Details!["key"].ToString().Should().Be("value");
        }

        [Fact]
        public void DeserializesWithoutOptionalFields()
        {
            // Arrange
            var json = @"{
                ""error"": ""Error""
            }";

            // Act
            var errorInfo = JsonSerializer.Deserialize(json, JsonSerializationContext.RelaxedEscaping.ErrorInfo);

            // Assert
            errorInfo.Should().NotBeNull();
            errorInfo!.Error.Should().Be("Error");
            errorInfo.Suggestion.Should().BeNull();
            errorInfo.Details.Should().BeNull();
        }

        [Fact]
        public void HandlesEmptyObject()
        {
            // Arrange
            var json = "{}";

            // Act
            var errorInfo = JsonSerializer.Deserialize(json, JsonSerializationContext.RelaxedEscaping.ErrorInfo);

            // Assert
            errorInfo.Should().NotBeNull();
            errorInfo!.Error.Should().BeEmpty();
            errorInfo.Suggestion.Should().BeNull();
            errorInfo.Details.Should().BeNull();
        }

        [Fact]
        public void DeserializesComplexDetails()
        {
            // Arrange
            var json = @"{
                ""error"": ""Error"",
                ""details"": {
                    ""string_value"": ""text"",
                    ""int_value"": 42,
                    ""bool_value"": true
                }
            }";

            // Act
            var errorInfo = JsonSerializer.Deserialize(json, JsonSerializationContext.RelaxedEscaping.ErrorInfo);

            // Assert
            errorInfo.Should().NotBeNull();
            errorInfo!.Details.Should().NotBeNull();
            errorInfo.Details!.Should().ContainKey("string_value");
            errorInfo.Details.Should().ContainKey("int_value");
            errorInfo.Details.Should().ContainKey("bool_value");
        }
    }

    public class RoundTripTests
    {
        [Fact]
        public void RoundTripsCompleteErrorInfo()
        {
            // Arrange
            var original = new ErrorInfo
            {
                Error = "Test error",
                Suggestion = "Test suggestion",
                Details = new Dictionary<string, object>
                {
                    { "key1", "value1" },
                    { "key2", 42 }
                }
            };

            // Act
            var json = JsonSerializer.Serialize(original, JsonSerializationContext.RelaxedEscaping.ErrorInfo);
            var deserialized = JsonSerializer.Deserialize(json, JsonSerializationContext.RelaxedEscaping.ErrorInfo);

            // Assert
            deserialized.Should().NotBeNull();
            deserialized!.Error.Should().Be(original.Error);
            deserialized.Suggestion.Should().Be(original.Suggestion);
            deserialized.Details.Should().NotBeNull();
        }

        [Fact]
        public void RoundTripsMinimalErrorInfo()
        {
            // Arrange
            var original = new ErrorInfo
            {
                Error = "Error"
            };

            // Act
            var json = JsonSerializer.Serialize(original, JsonSerializationContext.RelaxedEscaping.ErrorInfo);
            var deserialized = JsonSerializer.Deserialize(json, JsonSerializationContext.RelaxedEscaping.ErrorInfo);

            // Assert
            deserialized.Should().NotBeNull();
            deserialized!.Error.Should().Be(original.Error);
            deserialized.Suggestion.Should().BeNull();
            deserialized.Details.Should().BeNull();
        }
    }
}