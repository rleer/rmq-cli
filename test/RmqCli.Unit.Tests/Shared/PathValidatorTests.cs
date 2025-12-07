using RmqCli.Shared;
using FluentAssertions;
using Xunit;

namespace RmqCli.Unit.Tests.Shared;

public class PathValidatorTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsValidFilePath_ReturnsFalse_WhenPathIsNullOrWhitespace(string? path)
    {
        // Act
        var result = PathValidator.IsValidFilePath(path);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsValidFilePath_ReturnsFalse_WhenPathContainsInvalidCharacters()
    {
        // Arrange
        var invalidChars = Path.GetInvalidPathChars();
        if (invalidChars.Length == 0)
        {
            // Skip test if no invalid characters are defined for the platform
            return;
        }
        
        string path = $"test{invalidChars[0]}file.txt";

        // Act
        var result = PathValidator.IsValidFilePath(path);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("file.txt")]
    [InlineData("./file.txt")]
    [InlineData("../file.txt")]
    [InlineData("folder/file.txt")]
    public void IsValidFilePath_ReturnsTrue_ForValidRelativePaths(string path)
    {
        // Act
        var result = PathValidator.IsValidFilePath(path);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsValidFilePath_ReturnsTrue_ForValidAbsolutePath()
    {
        // Arrange
        string path = Path.Combine(Path.GetTempPath(), "testfile.txt");

        // Act
        var result = PathValidator.IsValidFilePath(path);

        // Assert
        result.Should().BeTrue();
    }
    
    [Fact]
    public void IsValidFilePath_ReturnsFalse_WhenPathIsJustDirectory()
    {
        // Arrange
        string path = Path.DirectorySeparatorChar.ToString();

        // Act
        var result = PathValidator.IsValidFilePath(path);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsValidFilePath_ReturnsFalse_WhenPathEndsWithSeparator()
    {
        // Arrange
        string path = $"some{Path.DirectorySeparatorChar}directory{Path.DirectorySeparatorChar}";

        // Act
        var result = PathValidator.IsValidFilePath(path);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsValidFilePath_ReturnsFalse_ForReservedNames_OnWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        // Arrange
        string[] reservedNames = { "CON", "PRN", "AUX", "NUL", "COM1", "LPT1" };

        foreach (var name in reservedNames)
        {
            // Act
            var result = PathValidator.IsValidFilePath(name);

            // Assert
            result.Should().BeFalse($"because {name} is a reserved name on Windows");
        }
    }
}
