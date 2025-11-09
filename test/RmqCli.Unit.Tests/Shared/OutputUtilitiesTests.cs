using RmqCli.Shared.Output;

namespace RmqCli.Unit.Tests.Shared;

public class OutputUtilitiesTests
{
    public class ToSizeString
    {
        [Theory]
        [InlineData(0, "0 bytes")]
        [InlineData(1, "1 bytes")]
        [InlineData(512, "512 bytes")]
        [InlineData(1023, "1023 bytes")]
        public void ReturnsBytes_WhenSizeLessThanKilobyte(double bytes, string expected)
        {
            // Act
            var result = OutputUtilities.ToSizeString(bytes);

            // Assert
            result.Should().Be(expected);
        }

        [Theory]
        [InlineData(1024, "1 KB")]
        [InlineData(2048, "2 KB")]
        [InlineData(1536, "1.5 KB")]
        [InlineData(102400, "100 KB")]
        [InlineData(1048575, "1024 KB")]
        public void ReturnsKilobytes_WhenSizeBetween1KBand1MB(double bytes, string expected)
        {
            // Act
            var result = OutputUtilities.ToSizeString(bytes);

            // Assert
            result.Should().Be(expected);
        }

        [Theory]
        [InlineData(1048576, "1 MB")]
        [InlineData(2097152, "2 MB")]
        [InlineData(1572864, "1.5 MB")]
        [InlineData(104857600, "100 MB")]
        [InlineData(1073741823, "1024 MB")]
        public void ReturnsMegabytes_WhenSizeBetween1MBand1GB(double bytes, string expected)
        {
            // Act
            var result = OutputUtilities.ToSizeString(bytes);

            // Assert
            result.Should().Be(expected);
        }

        [Theory]
        [InlineData(1073741824, "1 GB")]
        [InlineData(2147483648, "2 GB")]
        [InlineData(1610612736, "1.5 GB")]
        [InlineData(107374182400, "100 GB")]
        public void ReturnsGigabytes_WhenSize1GBOrMore(double bytes, string expected)
        {
            // Act
            var result = OutputUtilities.ToSizeString(bytes);

            // Assert
            result.Should().Be(expected);
        }

        [Fact]
        public void RoundsToTwoDecimalPlaces()
        {
            // Arrange
            var bytes = 1536.789; // Should round to 1.5 KB

            // Act
            var result = OutputUtilities.ToSizeString(bytes);

            // Assert
            result.Should().Be("1.5 KB");
        }
    }

    public class GetDigitCount
    {
        [Theory]
        [InlineData(0, 1)]
        [InlineData(1, 1)]
        [InlineData(5, 1)]
        [InlineData(9, 1)]
        public void Returns1_ForSingleDigitNumbers(int number, int expected)
        {
            // Act
            var result = OutputUtilities.GetDigitCount(number);

            // Assert
            result.Should().Be(expected);
        }

        [Theory]
        [InlineData(10, 2)]
        [InlineData(50, 2)]
        [InlineData(99, 2)]
        public void Returns2_ForTwoDigitNumbers(int number, int expected)
        {
            // Act
            var result = OutputUtilities.GetDigitCount(number);

            // Assert
            result.Should().Be(expected);
        }

        [Theory]
        [InlineData(100, 3)]
        [InlineData(500, 3)]
        [InlineData(999, 3)]
        [InlineData(1000, 4)]
        [InlineData(9999, 4)]
        [InlineData(10000, 5)]
        [InlineData(99999, 5)]
        [InlineData(100000, 6)]
        [InlineData(999999, 6)]
        [InlineData(1000000, 7)]
        [InlineData(9999999, 7)]
        [InlineData(10000000, 8)]
        [InlineData(99999999, 8)]
        [InlineData(100000000, 9)]
        [InlineData(999999999, 9)]
        [InlineData(1000000000, 10)]
        public void ReturnsCorrectDigitCount_ForVariousSizes(int number, int expected)
        {
            // Act
            var result = OutputUtilities.GetDigitCount(number);

            // Assert
            result.Should().Be(expected);
        }

        [Theory]
        [InlineData(-1, 1)]
        [InlineData(-50, 2)]
        [InlineData(-999, 3)]
        public void HandlesNegativeNumbers_ByReturningAbsoluteDigitCount(int number, int expected)
        {
            // Act
            var result = OutputUtilities.GetDigitCount(number);

            // Assert
            result.Should().Be(expected);
        }
    }

    public class GetMessageCountString
    {
        [Fact]
        public void ReturnsSingularForm_WhenCountIsOne_NoColor()
        {
            // Act
            var result = OutputUtilities.GetMessageCountString(1, noColor: true);

            // Assert
            result.Should().Be("1 message");
        }

        [Fact]
        public void ReturnsPluralForm_WhenCountIsZero_NoColor()
        {
            // Act
            var result = OutputUtilities.GetMessageCountString(0, noColor: true);

            // Assert
            result.Should().Be("0 messages");
        }

        [Fact]
        public void ReturnsPluralForm_WhenCountIsGreaterThanOne_NoColor()
        {
            // Act
            var result = OutputUtilities.GetMessageCountString(5, noColor: true);

            // Assert
            result.Should().Be("5 messages");
        }

        [Fact]
        public void ReturnsColoredOutput_WhenNoColorIsFalse()
        {
            // Act
            var result = OutputUtilities.GetMessageCountString(10, noColor: false);

            // Assert
            result.Should().Be("[orange1]10[/] messages");
        }

        [Fact]
        public void ReturnsColoredSingularForm_WhenCountIsOne_WithColor()
        {
            // Act
            var result = OutputUtilities.GetMessageCountString(1, noColor: false);

            // Assert
            result.Should().Be("[orange1]1[/] message");
        }

        [Theory]
        [InlineData(100)]
        [InlineData(1000)]
        [InlineData(999999)]
        public void HandlesLargeCounts_Correctly(long count)
        {
            // Act
            var result = OutputUtilities.GetMessageCountString(count, noColor: true);

            // Assert
            result.Should().Be($"{count} messages");
        }
    }

    public class GetElapsedTimeString
    {
        [Fact]
        public void ReturnsOnlyMilliseconds_WhenLessThanOneSecond()
        {
            // Arrange
            var elapsed = TimeSpan.FromMilliseconds(500);

            // Act
            var result = OutputUtilities.GetElapsedTimeString(elapsed);

            // Assert
            result.Should().Be("500ms");
        }

        [Fact]
        public void ReturnsSecondsAndMilliseconds_WhenLessThanOneMinute()
        {
            // Arrange
            var elapsed = TimeSpan.FromSeconds(5.5);

            // Act
            var result = OutputUtilities.GetElapsedTimeString(elapsed);

            // Assert
            result.Should().Be("5s 500ms");
        }

        [Fact]
        public void ReturnsMinutesSecondsAndMilliseconds_WhenLessThanOneHour()
        {
            // Arrange
            var elapsed = new TimeSpan(0, 0, 2, 30, 250);

            // Act
            var result = OutputUtilities.GetElapsedTimeString(elapsed);

            // Assert
            result.Should().Be("2m 30s 250ms");
        }

        [Fact]
        public void ReturnsFullFormat_WithHours()
        {
            // Arrange
            var elapsed = new TimeSpan(0, 1, 30, 45, 500);

            // Act
            var result = OutputUtilities.GetElapsedTimeString(elapsed);

            // Assert
            result.Should().Be("1h 30m 45s 500ms");
        }

        [Fact]
        public void ReturnsFullFormat_WithDays()
        {
            // Arrange
            var elapsed = new TimeSpan(2, 3, 15, 30, 100);

            // Act
            var result = OutputUtilities.GetElapsedTimeString(elapsed);

            // Assert
            result.Should().Be("2d 3h 15m 30s 100ms");
        }

        [Fact]
        public void OmitsZeroComponents_ExceptMilliseconds()
        {
            // Arrange
            var elapsed = new TimeSpan(0, 0, 0, 0, 100);

            // Act
            var result = OutputUtilities.GetElapsedTimeString(elapsed);

            // Assert
            result.Should().Be("100ms");
        }

        [Fact]
        public void HandlesExactSeconds_WithZeroMilliseconds()
        {
            // Arrange
            var elapsed = TimeSpan.FromSeconds(10);

            // Act
            var result = OutputUtilities.GetElapsedTimeString(elapsed);

            // Assert
            result.Should().Be("10s 0ms");
        }

        [Fact]
        public void HandlesZeroTimeSpan()
        {
            // Arrange
            var elapsed = TimeSpan.Zero;

            // Act
            var result = OutputUtilities.GetElapsedTimeString(elapsed);

            // Assert
            result.Should().Be("0ms");
        }
    }
}