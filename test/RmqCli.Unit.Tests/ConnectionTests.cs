using Rmq;

namespace RmqCli.Unit.Tests;

/// <summary>
/// Connection resolution is the most-exercised pure function in the tool: every command
/// goes through it, and getting precedence wrong points rmq at the wrong broker silently.
/// </summary>
public class ConnectionTests
{
    private static ConnectionSettings Resolve(
        string? url = null,
        string? host = null,
        int? port = null,
        string? virtualHost = null,
        string? user = null,
        string? password = null,
        Transport? transport = null,
        int? managementPort = null,
        string? env = null)
        => Connection.Resolve(url, host, port, virtualHost, user, password, transport, managementPort,
            environment: _ => env);

    [Fact]
    public void Defaults_to_local_guest_broker()
    {
        var settings = Resolve();

        settings.Host.Should().Be("localhost");
        settings.AmqpPort.Should().Be(5672);
        settings.VirtualHost.Should().Be("/");
        settings.User.Should().Be("guest");
        settings.Password.Should().Be("guest");
        settings.UseTls.Should().BeFalse();
        settings.Transport.Should().Be(Transport.Amqp);
    }

    [Fact]
    public void Url_supplies_every_component()
    {
        var settings = Resolve("amqp://alice:s3cret@broker.internal:5673/orders");

        settings.Host.Should().Be("broker.internal");
        settings.AmqpPort.Should().Be(5673);
        settings.VirtualHost.Should().Be("orders");
        settings.User.Should().Be("alice");
        settings.Password.Should().Be("s3cret");
    }

    [Fact]
    public void Environment_variable_is_used_when_no_url_flag()
    {
        Resolve(env: "amqp://envhost:5674/envvhost").Host.Should().Be("envhost");
    }

    [Fact]
    public void Url_flag_beats_environment_variable()
    {
        Resolve(url: "amqp://flaghost/", env: "amqp://envhost/").Host.Should().Be("flaghost");
    }

    [Fact]
    public void Individual_flags_override_url_per_component()
    {
        var settings = Resolve("amqp://alice:s3cret@prod:5673/prodvhost", virtualHost: "/test");

        settings.VirtualHost.Should().Be("/test");
        settings.Host.Should().Be("prod");
        settings.AmqpPort.Should().Be(5673);
        settings.User.Should().Be("alice");
        settings.Password.Should().Be("s3cret");
    }

    [Theory]
    [InlineData("amqp://h/", 5672, false)]
    [InlineData("amqps://h/", 5671, true)]
    public void Amqp_scheme_selects_tls_and_default_port(string url, int expectedPort, bool expectedTls)
    {
        var settings = Resolve(url);

        settings.AmqpPort.Should().Be(expectedPort);
        settings.UseTls.Should().Be(expectedTls);
        settings.Transport.Should().Be(Transport.Amqp);
    }

    [Theory]
    [InlineData("http://h/", 15672, false)]
    [InlineData("https://h/", 443, true)]
    public void Http_scheme_implies_http_transport_on_the_management_port(string url, int expectedPort, bool expectedTls)
    {
        var settings = Resolve(url);

        settings.Transport.Should().Be(Transport.Http);
        settings.ManagementPort.Should().Be(expectedPort);
        settings.UseTls.Should().Be(expectedTls);
    }

    [Fact]
    public void Explicit_port_survives_a_scheme_that_shares_its_default()
    {
        // http's Uri default is 80, which must not be mistaken for "no port given".
        Resolve("http://h:80/").ManagementPort.Should().Be(80);
    }

    [Fact]
    public void Amqp_url_with_http_transport_uses_the_management_port_on_the_same_host()
    {
        var settings = Resolve("amqp://broker:5673/", transport: Transport.Http);

        settings.Host.Should().Be("broker");
        settings.ManagementPort.Should().Be(15672);
        settings.ManagementBaseUrl.Should().Be("http://broker:15672");
    }

    [Fact]
    public void Amqps_url_with_http_transport_uses_the_tls_management_port()
    {
        Resolve("amqps://broker/", transport: Transport.Http).ManagementBaseUrl.Should().Be("https://broker:15671");
    }

    [Fact]
    public void Management_port_flag_wins_over_the_default()
    {
        Resolve("amqps://broker/", managementPort: 8443).ManagementBaseUrl.Should().Be("https://broker:8443");
    }

    [Theory]
    [InlineData("amqp://h", "/")]
    [InlineData("amqp://h/", "/")]
    [InlineData("amqp://h/orders", "orders")]
    [InlineData("amqp://h/%2f", "/")]
    [InlineData("amqp://h/a%2Fb", "a/b")]
    public void Vhost_comes_from_the_url_path(string url, string expected)
    {
        Resolve(url).VirtualHost.Should().Be(expected);
    }

    [Fact]
    public void Percent_escapes_in_credentials_are_decoded()
    {
        var settings = Resolve("amqp://us%40er:p%3Ass@h/");

        settings.User.Should().Be("us@er");
        settings.Password.Should().Be("p:ss");
    }

    [Fact]
    public void Unsupported_scheme_is_rejected()
    {
        var resolve = () => Resolve("ftp://h/");

        resolve.Should().Throw<ArgumentException>().WithMessage("*scheme*");
    }

    [Fact]
    public void Describe_never_leaks_the_password()
    {
        Resolve("amqp://alice:s3cret@h/").Describe().Should().NotContain("s3cret").And.Contain("alice");
    }
}
