using Xunit.Abstractions;

namespace Rmq.E2E.Tests;

/// <summary>
/// Needs no broker. Proves the published binary runs at all, which is the one thing
/// worth knowing before any of the command tests are meaningful.
/// </summary>
public class SmokeTests(ITestOutputHelper output)
{
    private readonly Cli _rmq = new(output);

    [Fact]
    public async Task Help_exits_zero()
    {
        var result = await _rmq.Run(["--help"]);

        result.ExitCode.Should().Be(0);
        result.Stdout.Should().Contain("RabbitMQ");
    }

    [Fact]
    public async Task Version_exits_zero()
    {
        var result = await _rmq.Run(["--version"]);

        result.ExitCode.Should().Be(0);
        result.Stdout.Trim().Should().NotBeEmpty();
    }

    [Fact]
    public async Task Unknown_command_is_a_usage_error()
    {
        var result = await _rmq.Run(["definitely-not-a-command"]);

        result.ExitCode.Should().NotBe(0);
    }
}
