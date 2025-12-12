using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using RmqCli.Shared.Json;

namespace RmqCli.Unit.Tests.Shared.Json;

public partial class BodyJsonConverterTests
{
    public class WriteWithNullOrEmptyValues
    {
        [Fact]
        public void WritesNull()
        {
            // Arrange & Ac
            var wrapper = new TestWrapper { Value = null };

            var json = JsonSerializer.Serialize(wrapper, CreateOptions());

            // Assert
            json.Should().Be("""{"value":null}""");
        }

        [Fact]
        public void WritesEmptyStringAsStringValue()
        {
            // Arrange
            var wrapper = new TestWrapper { Value = "" };

            // Act
            var json = JsonSerializer.Serialize(wrapper, CreateOptions());
            var parsed = JsonDocument.Parse(json);

            // Assert
            parsed.RootElement.GetProperty("value").GetString().Should().BeEmpty();
        }
    }

    public class WriteWithPlainText
    {
        [Theory]
        [InlineData("Hello World")]
        [InlineData("Plain text message")]
        [InlineData("No JSON here")]
        [InlineData("12345")]
        [InlineData("true")]
        [InlineData("false")]
        [InlineData("null")]
        public void WritesPlainTextAsStringValue(string plainText)
        {
            // Arrange
            var wrapper = new TestWrapper { Value = plainText };

            // Act
            var json = JsonSerializer.Serialize(wrapper, CreateOptions());
            var parsed = JsonDocument.Parse(json);

            // Assert
            parsed.RootElement.GetProperty("value").ValueKind.Should().Be(JsonValueKind.String);
            parsed.RootElement.GetProperty("value").GetString().Should().Be(plainText);
        }

        [Fact]
        public void WritesTextWithSpecialCharactersAsString()
        {
            // Arrange
            var wrapper = new TestWrapper { Value = "Line1\nLine2\tTabbed" };

            // Act
            var json = JsonSerializer.Serialize(wrapper, CreateOptions());
            var parsed = JsonDocument.Parse(json);

            // Assert
            parsed.RootElement.GetProperty("value").ValueKind.Should().Be(JsonValueKind.String);
            parsed.RootElement.GetProperty("value").GetString().Should().Be("Line1\nLine2\tTabbed");
        }

        [Fact]
        public void UsesRelaxedEscaping_ForSpecialCharacters()
        {
            // Arrange - Characters that are escaped differently with relaxed escaping
            var wrapper = new TestWrapper { Value = "Test with 'quotes' and \"double quotes\"" };

            // Act
            var json = JsonSerializer.Serialize(wrapper, CreateOptions());

            // Assert
            // With relaxed escaping, single quotes should NOT be escaped
            json.Should().Contain("'quotes'");
            // Double quotes should still be escaped
            json.Should().Contain("\\\"double quotes\\\"");
        }
    }

    public class WriteWithValidJson
    {
        [Fact]
        public void WritesValidJsonObjectAsRawJson()
        {
            // Arrange
            var jsonBody = """{"name": "John", "age": 30}""";
            var wrapper = new TestWrapper { Value = jsonBody };

            // Act
            var json = JsonSerializer.Serialize(wrapper, CreateOptions());
            var parsed = JsonDocument.Parse(json);

            // Assert
            var valueElement = parsed.RootElement.GetProperty("value");
            valueElement.ValueKind.Should().Be(JsonValueKind.Object);
            valueElement.GetProperty("name").GetString().Should().Be("John");
            valueElement.GetProperty("age").GetInt32().Should().Be(30);
        }

        [Fact]
        public void WritesValidJsonArrayAsRawJson()
        {
            // Arrange
            var jsonBody = """[1, 2, 3, 4, 5]""";
            var wrapper = new TestWrapper { Value = jsonBody };

            // Act
            var json = JsonSerializer.Serialize(wrapper, CreateOptions());
            var parsed = JsonDocument.Parse(json);

            // Assert
            var valueElement = parsed.RootElement.GetProperty("value");
            valueElement.ValueKind.Should().Be(JsonValueKind.Array);
            valueElement.GetArrayLength().Should().Be(5);
            valueElement[0].GetInt32().Should().Be(1);
            valueElement[4].GetInt32().Should().Be(5);
        }

        [Fact]
        public void WritesNestedJsonAsRawJson()
        {
            // Arrange
            var jsonBody = """
                           {
                               "user": {
                                   "name": "Alice",
                                   "address": {
                                       "city": "Boston",
                                       "zip": "02101"
                                   }
                               },
                               "items": [1, 2, 3]
                           }
                           """;
            var wrapper = new TestWrapper { Value = jsonBody };

            // Act
            var json = JsonSerializer.Serialize(wrapper, CreateOptions());
            var parsed = JsonDocument.Parse(json);

            // Assert
            var valueElement = parsed.RootElement.GetProperty("value");
            valueElement.ValueKind.Should().Be(JsonValueKind.Object);
            valueElement.GetProperty("user").GetProperty("name").GetString().Should().Be("Alice");
            valueElement.GetProperty("user").GetProperty("address").GetProperty("city").GetString().Should().Be("Boston");
            valueElement.GetProperty("items").GetArrayLength().Should().Be(3);
        }

        [Fact]
        public void WritesJsonWithWhitespaceAsRawJson()
        {
            // Arrange - JSON with leading/trailing whitespace should still be parsed
            var jsonBody = """  {"name": "John"}  """;
            var wrapper = new TestWrapper { Value = jsonBody };

            // Act
            var json = JsonSerializer.Serialize(wrapper, CreateOptions());
            var parsed = JsonDocument.Parse(json);

            // Assert
            var valueElement = parsed.RootElement.GetProperty("value");
            valueElement.ValueKind.Should().Be(JsonValueKind.Object);
            valueElement.GetProperty("name").GetString().Should().Be("John");
        }

        [Fact]
        public void WritesCompactJsonAsRawJson()
        {
            // Arrange - No whitespace
            var jsonBody = """{"a":1,"b":2}""";
            var wrapper = new TestWrapper { Value = jsonBody };

            // Act
            var json = JsonSerializer.Serialize(wrapper, CreateOptions());
            var parsed = JsonDocument.Parse(json);

            // Assert
            var valueElement = parsed.RootElement.GetProperty("value");
            valueElement.ValueKind.Should().Be(JsonValueKind.Object);
            valueElement.GetProperty("a").GetInt32().Should().Be(1);
            valueElement.GetProperty("b").GetInt32().Should().Be(2);
        }

        [Fact]
        public void WritesEmptyJsonObjectAsRawJson()
        {
            // Arrange
            var jsonBody = "{}";
            var wrapper = new TestWrapper { Value = jsonBody };

            // Act
            var json = JsonSerializer.Serialize(wrapper, CreateOptions());
            var parsed = JsonDocument.Parse(json);

            // Assert
            parsed.RootElement.GetProperty("value").ValueKind.Should().Be(JsonValueKind.Object);
        }

        [Fact]
        public void WritesEmptyJsonArrayAsRawJson()
        {
            // Arrange
            var jsonBody = "[]";
            var wrapper = new TestWrapper { Value = jsonBody };

            // Act
            var json = JsonSerializer.Serialize(wrapper, CreateOptions());
            var parsed = JsonDocument.Parse(json);

            // Assert
            parsed.RootElement.GetProperty("value").ValueKind.Should().Be(JsonValueKind.Array);
            parsed.RootElement.GetProperty("value").GetArrayLength().Should().Be(0);
        }
    }

    public class WriteWithInvalidJson
    {
        [Theory]
        [InlineData("{not valid json}")]
        [InlineData("{\"unclosed\": ")]
        [InlineData("[1, 2, 3")]
        [InlineData("{\"key\": undefined}")]
        [InlineData("[,]")]
        public void WritesInvalidJsonAsString(string invalidJson)
        {
            // Arrange
            var wrapper = new TestWrapper { Value = invalidJson };

            // Act
            var json = JsonSerializer.Serialize(wrapper, CreateOptions());
            var parsed = JsonDocument.Parse(json);

            // Assert
            parsed.RootElement.GetProperty("value").ValueKind.Should().Be(JsonValueKind.String);
            parsed.RootElement.GetProperty("value").GetString().Should().Be(invalidJson);
        }

        [Theory]
        [InlineData("{ incomplete")]
        [InlineData("[ incomplete")]
        [InlineData("} wrong order {")]
        [InlineData("] wrong order [")]
        public void WritesIncompleteJsonLikeStringAsString(string text)
        {
            // Arrange
            var wrapper = new TestWrapper { Value = text };

            // Act
            var json = JsonSerializer.Serialize(wrapper, CreateOptions());
            var parsed = JsonDocument.Parse(json);

            // Assert
            parsed.RootElement.GetProperty("value").ValueKind.Should().Be(JsonValueKind.String);
            parsed.RootElement.GetProperty("value").GetString().Should().Be(text);
        }

        [Fact]
        public void WritesJsonLikeTextWithMismatchedBracesAsString()
        {
            // Arrange - Starts with { but doesn't end with }
            var wrapper = new TestWrapper { Value = "{some text]" };

            // Act
            var json = JsonSerializer.Serialize(wrapper, CreateOptions());
            var parsed = JsonDocument.Parse(json);

            // Assert
            parsed.RootElement.GetProperty("value").ValueKind.Should().Be(JsonValueKind.String);
            parsed.RootElement.GetProperty("value").GetString().Should().Be("{some text]");
        }

        [Fact]
        public void WritesTextContainingJsonAsString()
        {
            // Arrange - Contains JSON but doesn't start/end correctly
            var wrapper = new TestWrapper { Value = "prefix {\"key\": \"value\"} suffix" };

            // Act
            var json = JsonSerializer.Serialize(wrapper, CreateOptions());
            var parsed = JsonDocument.Parse(json);

            // Assert
            parsed.RootElement.GetProperty("value").ValueKind.Should().Be(JsonValueKind.String);
        }
    }

    public class WriteEdgeCases
    {
        [Fact]
        public void WritesJsonWithUnicodeCharactersAsRawJson()
        {
            // Arrange
            var jsonBody = """{"greeting": "Hello 👋", "emoji": "🎉"}""";
            var wrapper = new TestWrapper { Value = jsonBody };

            // Act
            var json = JsonSerializer.Serialize(wrapper, CreateOptions());
            var parsed = JsonDocument.Parse(json);

            // Assert
            var valueElement = parsed.RootElement.GetProperty("value");
            valueElement.ValueKind.Should().Be(JsonValueKind.Object);
            valueElement.GetProperty("greeting").GetString().Should().Be("Hello 👋");
            valueElement.GetProperty("emoji").GetString().Should().Be("🎉");
        }

        [Fact]
        public void WritesJsonWithEscapedCharactersAsRawJson()
        {
            // Arrange
            var jsonBody = """{"text": "Line1\nLine2\tTabbed"}""";
            var wrapper = new TestWrapper { Value = jsonBody };

            // Act
            var json = JsonSerializer.Serialize(wrapper, CreateOptions());
            var parsed = JsonDocument.Parse(json);

            // Assert
            var valueElement = parsed.RootElement.GetProperty("value");
            valueElement.ValueKind.Should().Be(JsonValueKind.Object);
            valueElement.GetProperty("text").GetString().Should().Be("Line1\nLine2\tTabbed");
        }

        [Fact]
        public void WritesJsonArrayOfObjectsAsRawJson()
        {
            // Arrange
            var jsonBody = """[{"id": 1}, {"id": 2}, {"id": 3}]""";
            var wrapper = new TestWrapper { Value = jsonBody };

            // Act
            var json = JsonSerializer.Serialize(wrapper, CreateOptions());
            var parsed = JsonDocument.Parse(json);

            // Assert
            var valueElement = parsed.RootElement.GetProperty("value");
            valueElement.ValueKind.Should().Be(JsonValueKind.Array);
            valueElement.GetArrayLength().Should().Be(3);
            valueElement[0].GetProperty("id").GetInt32().Should().Be(1);
        }

        [Fact]
        public void WritesJsonWithNullValuesAsRawJson()
        {
            // Arrange
            var jsonBody = """{"name": "John", "age": null}""";
            var wrapper = new TestWrapper { Value = jsonBody };

            // Act
            var json = JsonSerializer.Serialize(wrapper, CreateOptions());
            var parsed = JsonDocument.Parse(json);

            // Assert
            var valueElement = parsed.RootElement.GetProperty("value");
            valueElement.ValueKind.Should().Be(JsonValueKind.Object);
            valueElement.GetProperty("name").GetString().Should().Be("John");
            valueElement.GetProperty("age").ValueKind.Should().Be(JsonValueKind.Null);
        }

        [Fact]
        public void WritesJsonWithBooleanAndNumbersAsRawJson()
        {
            // Arrange
            var jsonBody = """{"active": true, "count": 42, "price": 19.99}""";
            var wrapper = new TestWrapper { Value = jsonBody };

            // Act
            var json = JsonSerializer.Serialize(wrapper, CreateOptions());
            var parsed = JsonDocument.Parse(json);

            // Assert
            var valueElement = parsed.RootElement.GetProperty("value");
            valueElement.ValueKind.Should().Be(JsonValueKind.Object);
            valueElement.GetProperty("active").GetBoolean().Should().BeTrue();
            valueElement.GetProperty("count").GetInt32().Should().Be(42);
            valueElement.GetProperty("price").GetDouble().Should().BeApproximately(19.99, 0.01);
        }
    }

    public class ReadDeserialization
    {
        [Fact]
        public void ReadsStringValue()
        {
            // Arrange
            var json = """{"value": "Plain text"}""";

            // Act
            var result = JsonSerializer.Deserialize<TestWrapper>(json, CreateOptions());

            // Assert
            result.Should().NotBeNull();
            result.Value.Should().Be("Plain text");
        }

        [Fact]
        public void ReadsNullValue()
        {
            // Arrange
            var json = """{"value": null}""";

            // Act
            var result = JsonSerializer.Deserialize<TestWrapper>(json, CreateOptions());

            // Assert
            result.Should().NotBeNull();
            result.Value.Should().BeNull();
        }

        [Fact]
        public void ReadsEmptyString()
        {
            // Arrange
            var json = """{"value": ""}""";

            // Act
            var result = JsonSerializer.Deserialize<TestWrapper>(json, CreateOptions());

            // Assert
            result.Should().NotBeNull();
            result!.Value.Should().BeEmpty();
        }

        [Fact]
        public void ReadsStringWithEscapedCharacters()
        {
            // Arrange
            var json = """{"value": "Line1\nLine2\tTabbed"}""";

            // Act
            var result = JsonSerializer.Deserialize<TestWrapper>(json, CreateOptions());

            // Assert
            result.Should().NotBeNull();
            result!.Value.Should().Be("Line1\nLine2\tTabbed");
        }

        [Fact]
        public void ReadsStringWithUnicodeCharacters()
        {
            // Arrange
            var json = """{"value": "Hello 👋"}""";

            // Act
            var result = JsonSerializer.Deserialize<TestWrapper>(json, CreateOptions());

            // Assert
            result.Should().NotBeNull();
            result.Value.Should().Be("Hello 👋");
        }
    }

    public class RoundTripSerialization
    {
        [Theory]
        [InlineData("Plain text message")]
        [InlineData("")]
        [InlineData("Text with\nnewlines")]
        [InlineData("Special chars: <>&\"'")]
        public void RoundTripsPlainTextCorrectly(string originalText)
        {
            // Arrange
            var original = new TestWrapper { Value = originalText };

            // Act
            var json = JsonSerializer.Serialize(original, CreateOptions());
            var deserialized = JsonSerializer.Deserialize<TestWrapper>(json, CreateOptions());

            // Assert
            deserialized.Should().NotBeNull();
            deserialized!.Value.Should().Be(originalText);
        }

        [Fact]
        public void ConverterIsAsymmetric_JsonObjectsCannotRoundTrip()
        {
            // Arrange - The converter is asymmetric by design:
            // - Write: Valid JSON strings → embedded JSON objects
            // - Read: Only handles string tokens (not embedded objects)
            var jsonBody = """{"name": "John", "age": 30}""";
            var original = new TestWrapper { Value = jsonBody };

            // Act - Serialize (JSON becomes embedded object)
            var serialized = JsonSerializer.Serialize(original, CreateOptions());

            // Verify it was written as embedded JSON object
            var parsed = JsonDocument.Parse(serialized);
            parsed.RootElement.GetProperty("value").ValueKind.Should().Be(JsonValueKind.Object);

            // Assert - Attempting to deserialize fails because Read expects a string token
            // This is expected behavior - the converter cannot round-trip JSON objects
            var act = () => JsonSerializer.Deserialize<TestWrapper>(serialized, CreateOptions());
            act.Should().Throw<JsonException>()
                .WithMessage("*could not be converted to System.String*");
        }

        [Fact]
        public void RoundTripsNullCorrectly()
        {
            // Arrange
            var original = new TestWrapper { Value = null };

            // Act
            var json = JsonSerializer.Serialize(original, CreateOptions());
            var deserialized = JsonSerializer.Deserialize<TestWrapper>(json, CreateOptions());

            // Assert
            deserialized.Should().NotBeNull();
            deserialized.Value.Should().BeNull();
        }
    }

    // Helper class for testing
    private class TestWrapper
    {
        [JsonConverter(typeof(BodyJsonConverter))]
        public string? Value { get; set; }
    }

    // Test-specific serialization context for TestWrapper
    [JsonSerializable(typeof(TestWrapper))]
    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
    private partial class TestJsonContext : JsonSerializerContext
    {
    }

    private static JsonSerializerOptions CreateOptions()
    {
        return new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            TypeInfoResolver = TestJsonContext.Default,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }
}