using RmqCli.Commands.Publish;

namespace RmqCli.Unit.Tests.Commands.Publish;

public class HeaderParserTests
{
    public class ParseValidHeaders
    {
        [Fact]
        public void ParsesSimpleStringHeader()
        {
            // Arrange
            var headers = new[] { "x-custom-header:value" };

            // Act
            var result = HeaderParser.Parse(headers);

            // Assert
            result.Should().HaveCount(1);
            result["x-custom-header"].Should().Be("value");
        }

        [Fact]
        public void ParsesMultipleHeaders()
        {
            // Arrange
            var headers = new[]
            {
                "x-header-1:value1",
                "x-header-2:value2",
                "x-header-3:value3"
            };

            // Act
            var result = HeaderParser.Parse(headers);

            // Assert
            result.Should().HaveCount(3);
            result["x-header-1"].Should().Be("value1");
            result["x-header-2"].Should().Be("value2");
            result["x-header-3"].Should().Be("value3");
        }

        [Fact]
        public void TrimsWhitespaceFromKeyAndValue()
        {
            // Arrange
            var headers = new[] { "  x-header  :  value  " };

            // Act
            var result = HeaderParser.Parse(headers);

            // Assert
            result.Should().ContainKey("x-header");
            result["x-header"].Should().Be("value");
        }

        [Fact]
        public void HandlesHeaderWithColonInValue()
        {
            // Arrange
            var headers = new[] { "x-url:http://example.com:8080" };

            // Act
            var result = HeaderParser.Parse(headers);

            // Assert
            result["x-url"].Should().Be("http://example.com:8080");
        }

        [Fact]
        public void HandlesHeaderWithMultipleColonsInValue()
        {
            // Arrange
            var headers = new[] { "x-time:12:34:56" };

            // Act
            var result = HeaderParser.Parse(headers);

            // Assert
            result["x-time"].Should().Be("12:34:56");
        }

        [Fact]
        public void HandlesEmptyValue()
        {
            // Arrange
            var headers = new[] { "x-empty:" };

            // Act
            var result = HeaderParser.Parse(headers);

            // Assert
            result["x-empty"].Should().Be("");
        }

        [Fact]
        public void HandlesEmptyValueWithWhitespace()
        {
            // Arrange
            var headers = new[] { "x-empty:   " };

            // Act
            var result = HeaderParser.Parse(headers);

            // Assert
            result["x-empty"].Should().Be("");
        }
    }

    public class ParseTypeDetection
    {
        [Fact]
        public void DetectsBoolean_True()
        {
            // Arrange
            var headers = new[] { "x-enabled:true" };

            // Act
            var result = HeaderParser.Parse(headers);

            // Assert
            result["x-enabled"].Should().BeOfType<bool>();
            result["x-enabled"].Should().Be(true);
        }

        [Fact]
        public void DetectsBoolean_False()
        {
            // Arrange
            var headers = new[] { "x-enabled:false" };

            // Act
            var result = HeaderParser.Parse(headers);

            // Assert
            result["x-enabled"].Should().BeOfType<bool>();
            result["x-enabled"].Should().Be(false);
        }

        [Fact]
        public void DetectsBoolean_CaseInsensitive()
        {
            // Arrange
            var headers = new[] { "x-bool-1:True", "x-bool-2:FALSE" };

            // Act
            var result = HeaderParser.Parse(headers);

            // Assert
            result["x-bool-1"].Should().Be(true);
            result["x-bool-2"].Should().Be(false);
        }

        [Fact]
        public void DetectsInteger()
        {
            // Arrange
            var headers = new[] { "x-count:42" };

            // Act
            var result = HeaderParser.Parse(headers);

            // Assert
            result["x-count"].Should().BeOfType<long>();
            result["x-count"].Should().Be(42L);
        }

        [Fact]
        public void DetectsNegativeInteger()
        {
            // Arrange
            var headers = new[] { "x-negative:-100" };

            // Act
            var result = HeaderParser.Parse(headers);

            // Assert
            result["x-negative"].Should().BeOfType<long>();
            result["x-negative"].Should().Be(-100L);
        }

        [Fact]
        public void DetectsLargeInteger()
        {
            // Arrange
            var headers = new[] { "x-large:9223372036854775807" }; // long.MaxValue

            // Act
            var result = HeaderParser.Parse(headers);

            // Assert
            result["x-large"].Should().BeOfType<long>();
            result["x-large"].Should().Be(9223372036854775807L);
        }

        [Fact]
        public void DetectsDouble()
        {
            // Arrange
            var headers = new[] { "x-pi:3.14159" };

            // Act
            var result = HeaderParser.Parse(headers);

            // Assert
            result["x-pi"].Should().BeOfType<double>();
            result["x-pi"].Should().Be(3.14159);
        }

        [Fact]
        public void DetectsNegativeDouble()
        {
            // Arrange
            var headers = new[] { "x-temp:-273.15" };

            // Act
            var result = HeaderParser.Parse(headers);

            // Assert
            result["x-temp"].Should().BeOfType<double>();
            result["x-temp"].Should().Be(-273.15);
        }

        [Fact]
        public void DefaultsToString_WhenNotANumber()
        {
            // Arrange
            var headers = new[] { "x-text:not-a-number" };

            // Act
            var result = HeaderParser.Parse(headers);

            // Assert
            result["x-text"].Should().BeOfType<string>();
            result["x-text"].Should().Be("not-a-number");
        }

        [Fact]
        public void DetectsMixedTypes()
        {
            // Arrange
            var headers = new[]
            {
                "x-string:hello",
                "x-int:42",
                "x-double:3.14",
                "x-bool:true"
            };

            // Act
            var result = HeaderParser.Parse(headers);

            // Assert
            result.Should().HaveCount(4);
            result["x-string"].Should().BeOfType<string>().And.Be("hello");
            result["x-int"].Should().BeOfType<long>().And.Be(42L);
            result["x-double"].Should().BeOfType<double>().And.Be(3.14);
            result["x-bool"].Should().BeOfType<bool>().And.Be(true);
        }

        [Fact]
        public void TreatsZeroAsInteger()
        {
            // Arrange
            var headers = new[] { "x-zero:0" };

            // Act
            var result = HeaderParser.Parse(headers);

            // Assert
            result["x-zero"].Should().BeOfType<long>();
            result["x-zero"].Should().Be(0L);
        }
    }

    public class ParseInvalidInput
    {
        [Fact]
        public void ThrowsException_WhenMissingColon()
        {
            // Arrange
            var headers = new[] { "invalid-header-no-colon" };

            // Act
            var act = () => HeaderParser.Parse(headers);

            // Assert
            act.Should().Throw<ArgumentException>()
                .WithMessage("Invalid header format: 'invalid-header-no-colon'. Expected 'key:value'.");
        }

        [Fact]
        public void ThrowsException_WhenKeyIsEmpty()
        {
            // Arrange
            var headers = new[] { ":value" };

            // Act
            var act = () => HeaderParser.Parse(headers);

            // Assert
            act.Should().Throw<ArgumentException>()
                .WithMessage("Invalid header format: ':value'. Key cannot be empty.");
        }

        [Fact]
        public void ThrowsException_WhenKeyIsWhitespace()
        {
            // Arrange
            var headers = new[] { "   :value" };

            // Act
            var act = () => HeaderParser.Parse(headers);

            // Assert
            act.Should().Throw<ArgumentException>()
                .WithMessage("Invalid header format: '   :value'. Key cannot be empty.");
        }

        [Fact]
        public void ThrowsException_OnFirstInvalidHeader()
        {
            // Arrange
            var headers = new[]
            {
                "x-valid:value",
                "invalid-no-colon",
                "x-another:value"
            };

            // Act
            var act = () => HeaderParser.Parse(headers);

            // Assert
            act.Should().Throw<ArgumentException>()
                .WithMessage("Invalid header format: 'invalid-no-colon'*");
        }
    }

    public class ParseEdgeCases
    {
        [Fact]
        public void ReturnsEmptyDictionary_WhenInputIsEmpty()
        {
            // Arrange
            var headers = Array.Empty<string>();

            // Act
            var result = HeaderParser.Parse(headers);

            // Assert
            result.Should().BeEmpty();
        }

        [Fact]
        public void HandlesDuplicateKeys_LastValueWins()
        {
            // Arrange
            var headers = new[]
            {
                "x-header:first",
                "x-header:second"
            };

            // Act
            var result = HeaderParser.Parse(headers);

            // Assert
            result.Should().HaveCount(1);
            result["x-header"].Should().Be("second");
        }

        [Fact]
        public void HandlesSpecialCharacters_InKey()
        {
            // Arrange
            var headers = new[] { "x-special_char$:value" };

            // Act
            var result = HeaderParser.Parse(headers);

            // Assert
            result["x-special_char$"].Should().Be("value");
        }

        [Fact]
        public void HandlesSpecialCharacters_InValue()
        {
            // Arrange
            var headers = new[] { "x-header:value!@#$%^&*()" };

            // Act
            var result = HeaderParser.Parse(headers);

            // Assert
            result["x-header"].Should().Be("value!@#$%^&*()");
        }

        [Fact]
        public void HandlesUnicodeCharacters()
        {
            // Arrange
            var headers = new[] { "x-unicode:Hello 世界 🌍" };

            // Act
            var result = HeaderParser.Parse(headers);

            // Assert
            result["x-unicode"].Should().Be("Hello 世界 🌍");
        }

        [Fact]
        public void HandlesVeryLongValue()
        {
            // Arrange
            var longValue = new string('x', 10000);
            var headers = new[] { $"x-long:{longValue}" };

            // Act
            var result = HeaderParser.Parse(headers);

            // Assert
            result["x-long"].Should().Be(longValue);
        }
    }
}
