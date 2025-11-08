using System.Text.Json;
using System.Text.Json.Serialization;
using RmqCli.Core.Models;
using RmqCli.Shared.Json;

namespace RmqCli.Unit.Tests.Core.Models;

public class ResponseTests
{
    #region Property Tests

    public class PropertyTests
    {
        [Fact]
        public void AllowsSettingStatus()
        {
            // Arrange
            var response = new Response();

            // Act
            response.Status = "success";

            // Assert
            response.Status.Should().Be("success");
        }

        [Fact]
        public void AllowsSettingTimestamp()
        {
            // Arrange
            var response = new Response();
            var timestamp = new DateTime(2025, 1, 15, 10, 30, 0);

            // Act
            response.Timestamp = timestamp;

            // Assert
            response.Timestamp.Should().Be(timestamp);
        }

        [Fact]
        public void AllowsSettingError()
        {
            // Arrange
            var response = new Response();
            var error = new ErrorInfo { Code = "TEST", Error = "Test error", Category = "internal" };

            // Act
            response.Error = error;

            // Assert
            response.Error.Should().BeSameAs(error);
        }

        [Fact]
        public void InitializesStatusAsEmpty()
        {
            // Act
            var response = new Response();

            // Assert
            response.Status.Should().BeEmpty();
        }

        [Fact]
        public void InitializesTimestamp_ToNow()
        {
            // Arrange
            var before = DateTime.Now;

            // Act
            var response = new Response();

            // Arrange continued
            var after = DateTime.Now;

            // Assert
            response.Timestamp.Should().BeOnOrAfter(before);
            response.Timestamp.Should().BeOnOrBefore(after);
        }

        [Fact]
        public void InitializesErrorAsNull()
        {
            // Act
            var response = new Response();

            // Assert
            response.Error.Should().BeNull();
        }
    }

    #endregion

    #region Status Values

    public class StatusValues
    {
        [Theory]
        [InlineData("success")]
        [InlineData("partial")]
        [InlineData("error")]
        public void SupportsCommonStatusValues(string status)
        {
            // Arrange
            var response = new Response { Status = status };

            // Act
            var json = JsonSerializer.Serialize(response, JsonSerializationContext.RelaxedEscaping.Response);
            var deserialized = JsonSerializer.Deserialize(json, JsonSerializationContext.RelaxedEscaping.Response);

            // Assert
            deserialized.Should().NotBeNull();
            deserialized.Status.Should().Be(status);
        }

        [Fact]
        public void AllowsCustomStatusValues()
        {
            // Arrange
            var response = new Response { Status = "custom_status" };

            // Act
            var json = JsonSerializer.Serialize(response, JsonSerializationContext.RelaxedEscaping.Response);
            var deserialized = JsonSerializer.Deserialize(json, JsonSerializationContext.RelaxedEscaping.Response);

            // Assert
            deserialized.Should().NotBeNull();
            deserialized.Status.Should().Be("custom_status");
        }
    }

    #endregion

    #region JSON Serialization

    public class JsonSerialization
    {
        [Fact]
        public void SerializesAllProperties_WhenErrorSet()
        {
            // Arrange
            var timestamp = new DateTime(2025, 1, 15, 10, 30, 0, DateTimeKind.Utc);
            var response = new Response
            {
                Status = "error",
                Timestamp = timestamp,
                Error = new ErrorInfo
                {
                    Code = "TEST_ERROR",
                    Error = "Test error message",
                    Category = "internal"
                }
            };

            // Act
            var json = JsonSerializer.Serialize(response,  JsonSerializationContext.RelaxedEscaping.Response);
            var parsed = JsonDocument.Parse(json);

            // Assert
            parsed.RootElement.GetProperty("status").GetString().Should().Be("error");
            parsed.RootElement.TryGetProperty("timestamp", out _).Should().BeTrue();
            parsed.RootElement.TryGetProperty("error", out var error).Should().BeTrue();
            error.GetProperty("code").GetString().Should().Be("TEST_ERROR");
        }

        [Fact]
        public void SerializesWithoutError_WhenNull()
        {
            // Arrange
            var response = new Response
            {
                Status = "success",
                Timestamp = DateTime.Now
            };

            // Act
            var json = JsonSerializer.Serialize(response, JsonSerializationContext.RelaxedEscaping.Response);
            var parsed = JsonDocument.Parse(json);

            // Assert
            parsed.RootElement.GetProperty("status").GetString().Should().Be("success");
            parsed.RootElement.TryGetProperty("error", out var error).Should().BeTrue();
            error.ValueKind.Should().Be(JsonValueKind.Null);
        }

        [Fact]
        public void SerializesTimestamp_InCorrectFormat()
        {
            // Arrange
            var timestamp = new DateTime(2025, 1, 15, 10, 30, 45, DateTimeKind.Utc);
            var response = new Response
            {
                Status = "success",
                Timestamp = timestamp
            };

            // Act
            var json = JsonSerializer.Serialize(response, JsonSerializationContext.RelaxedEscaping.Response);
            var parsed = JsonDocument.Parse(json);

            // Assert
            parsed.RootElement.TryGetProperty("timestamp", out var ts).Should().BeTrue();
            var deserializedTime = ts.GetDateTime();
            deserializedTime.Year.Should().Be(2025);
            deserializedTime.Month.Should().Be(1);
            deserializedTime.Day.Should().Be(15);
        }

        [Fact]
        public void UsesSnakeCase_ForPropertyNames()
        {
            // Arrange
            var response = new Response
            {
                Status = "success",
                Timestamp = DateTime.Now
            };

            // Act
            var json = JsonSerializer.Serialize(response, JsonSerializationContext.RelaxedEscaping.Response);

            // Assert
            json.Should().Contain("\"status\"");
            json.Should().Contain("\"timestamp\"");
            json.Should().Contain("\"error\"");
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
                ""status"": ""error"",
                ""timestamp"": ""2025-01-15T10:30:00Z"",
                ""error"": {
                    ""code"": ""TEST_ERROR"",
                    ""error"": ""Test error"",
                    ""category"": ""internal""
                }
            }";

            // Act
            var response = JsonSerializer.Deserialize(json,  JsonSerializationContext.RelaxedEscaping.Response);

            // Assert
            response.Should().NotBeNull();
            response.Status.Should().Be("error");
            response.Timestamp.Year.Should().Be(2025);
            response.Timestamp.Month.Should().Be(1);
            response.Timestamp.Day.Should().Be(15);
            response.Error.Should().NotBeNull();
            response.Error!.Code.Should().Be("TEST_ERROR");
        }

        [Fact]
        public void DeserializesWithoutError()
        {
            // Arrange
            var json = @"{
                ""status"": ""success"",
                ""timestamp"": ""2025-01-15T10:30:00Z""
            }";

            // Act
            var response = JsonSerializer.Deserialize(json, JsonSerializationContext.RelaxedEscaping.Response);

            // Assert
            response.Should().NotBeNull();
            response.Status.Should().Be("success");
            response.Error.Should().BeNull();
        }

        [Fact]
        public void DeserializesWithNullError()
        {
            // Arrange
            var json = @"{
                ""status"": ""success"",
                ""timestamp"": ""2025-01-15T10:30:00Z"",
                ""error"": null
            }";

            // Act
            var response = JsonSerializer.Deserialize(json,  JsonSerializationContext.RelaxedEscaping.Response);

            // Assert
            response.Should().NotBeNull();
            response.Error.Should().BeNull();
        }

        [Fact]
        public void HandlesMinimalJson()
        {
            // Arrange
            var json = "{}";

            // Act
            var response = JsonSerializer.Deserialize(json, JsonSerializationContext.RelaxedEscaping.Response);

            // Assert
            response.Should().NotBeNull();
            response.Status.Should().BeEmpty();
            response.Error.Should().BeNull();
        }
    }

    #endregion

    #region Round-trip Tests

    public class RoundTripTests
    {
        [Fact]
        public void RoundTripsSuccessResponse()
        {
            // Arrange
            var original = new Response
            {
                Status = "success",
                Timestamp = new DateTime(2025, 1, 15, 10, 30, 0, DateTimeKind.Utc)
            };

            // Act
            var json = JsonSerializer.Serialize(original, JsonSerializationContext.RelaxedEscaping.Response);
            var deserialized = JsonSerializer.Deserialize(json, JsonSerializationContext.RelaxedEscaping.Response);

            // Assert
            deserialized.Should().NotBeNull();
            deserialized.Status.Should().Be(original.Status);
            deserialized.Timestamp.Should().BeCloseTo(original.Timestamp, TimeSpan.FromSeconds(1));
            deserialized.Error.Should().BeNull();
        }

        [Fact]
        public void RoundTripsErrorResponse()
        {
            // Arrange
            var original = new Response
            {
                Status = "error",
                Timestamp = new DateTime(2025, 1, 15, 10, 30, 0, DateTimeKind.Utc),
                Error = new ErrorInfo
                {
                    Code = "TEST_ERROR",
                    Error = "Test error",
                    Category = "internal",
                    Suggestion = "Try again"
                }
            };

            // Act
            var json = JsonSerializer.Serialize(original, JsonSerializationContext.RelaxedEscaping.Response);
            var deserialized = JsonSerializer.Deserialize(json, JsonSerializationContext.RelaxedEscaping.Response);

            // Assert
            deserialized.Should().NotBeNull();
            deserialized.Status.Should().Be(original.Status);
            deserialized.Timestamp.Should().BeCloseTo(original.Timestamp, TimeSpan.FromSeconds(1));
            deserialized.Error.Should().NotBeNull();
            deserialized.Error!.Code.Should().Be(original.Error.Code);
            deserialized.Error.Error.Should().Be(original.Error.Error);
            deserialized.Error.Category.Should().Be(original.Error.Category);
            deserialized.Error.Suggestion.Should().Be(original.Error.Suggestion);
        }

        [Fact]
        public void RoundTripsPartialResponse()
        {
            // Arrange
            var original = new Response
            {
                Status = "partial",
                Timestamp = DateTime.UtcNow,
                Error = new ErrorInfo
                {
                    Code = "PARTIAL_FAILURE",
                    Error = "Some operations failed",
                    Category = "validation"
                }
            };

            // Act
            var json = JsonSerializer.Serialize(original, JsonSerializationContext.RelaxedEscaping.Response);
            var deserialized = JsonSerializer.Deserialize(json, JsonSerializationContext.RelaxedEscaping.Response);

            // Assert
            deserialized.Should().NotBeNull();
            deserialized.Status.Should().Be("partial");
            deserialized.Error.Should().NotBeNull();
        }
    }

    #endregion

    #region Inheritance Tests

    public class InheritanceTests
    {
        // Derived class for testing
        private class TestResponse : Response
        {
            [JsonPropertyName("test_data")]
            public string? TestData { get; set; }
        }

        [Fact]
        public void DerivedClass_InheritsBaseProperties()
        {
            // Arrange
            var derived = new TestResponse
            {
                Status = "success",
                Timestamp = DateTime.UtcNow,
                TestData = "test"
            };

            // Act
            var json = JsonSerializer.Serialize(derived);
            var parsed = JsonDocument.Parse(json);

            // Assert
            parsed.RootElement.GetProperty("status").GetString().Should().Be("success");
            parsed.RootElement.TryGetProperty("timestamp", out _).Should().BeTrue();
            parsed.RootElement.GetProperty("test_data").GetString().Should().Be("test");
        }

        [Fact]
        public void DerivedClass_CanSetError()
        {
            // Arrange
            var derived = new TestResponse
            {
                Status = "error",
                Timestamp = DateTime.UtcNow,
                Error = new ErrorInfo { Code = "TEST", Error = "Error", Category = "internal" }
            };

            // Act
            var json = JsonSerializer.Serialize(derived);
            var parsed = JsonDocument.Parse(json);

            // Assert
            parsed.RootElement.TryGetProperty("error", out var error).Should().BeTrue();
            error.GetProperty("code").GetString().Should().Be("TEST");
        }
    }

    #endregion
}
