using System.Text;

namespace Rmq.Unit.Tests;

/// <summary>
/// The NDJSON schema has to survive `rmq consume | rmq publish`, so every case here is
/// bytes → line → bytes. See docs/message-schema.md for the guarantee being asserted.
/// </summary>
public class MessageJsonTests
{
    private static byte[] RoundTrip(byte[] body)
        => MessageJson.Parse(MessageJson.Serialize(Message.FromBytes(body))).BodyBytes;

    private static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text);

    [Fact]
    public void Json_body_is_emitted_inline_so_jq_can_reach_into_it()
    {
        var line = MessageJson.Serialize(Message.FromBytes(Utf8("""{"orderId":42}""")));

        line.Should().Contain("""{"body":{"orderId":42}""");
    }

    [Fact]
    public void Json_body_round_trips_semantically()
    {
        // Whitespace is not preserved — the documented trade for inline bodies.
        var parsed = MessageJson.Parse(MessageJson.Serialize(Message.FromBytes(Utf8("{\n  \"orderId\": 42\n}"))));

        parsed.Body.Should().Be("""{"orderId":42}""");
        parsed.BodyEncoding.Should().BeNull();
    }

    [Theory]
    [InlineData("hello world")]
    [InlineData("")]
    [InlineData("{not json")]
    [InlineData("[1,2")]
    [InlineData("äöü — ünïcödé")]
    [InlineData("line one\nline two")]
    [InlineData("42")]
    public void Text_bodies_round_trip_byte_for_byte(string text)
    {
        RoundTrip(Utf8(text)).Should().Equal(Utf8(text));
    }

    [Fact]
    public void Array_body_round_trips()
    {
        RoundTrip(Utf8("[1,2,3]")).Should().Equal(Utf8("[1,2,3]"));
    }

    [Fact]
    public void Binary_body_round_trips_byte_for_byte_via_base64()
    {
        var binary = new byte[] { 0xFF, 0xFE, 0x00, 0x01, 0x80 };

        var line = MessageJson.Serialize(Message.FromBytes(binary));
        var parsed = MessageJson.Parse(line);

        parsed.BodyEncoding.Should().Be("base64");
        parsed.BodyBytes.Should().Equal(binary);
    }

    [Fact]
    public void Text_that_looks_like_base64_is_not_decoded_without_the_marker()
    {
        RoundTrip(Utf8("SGVsbG8=")).Should().Equal(Utf8("SGVsbG8="));
    }

    [Fact]
    public void Unicode_is_not_escaped_on_the_wire()
    {
        MessageJson.Serialize(Message.FromBytes(Utf8("grüße"))).Should().Contain("grüße");
    }

    [Fact]
    public void Properties_and_headers_round_trip()
    {
        var message = Message.FromBytes(
            Utf8("hi"),
            new MessageProperties
            {
                ContentType = "application/json",
                ContentEncoding = "gzip",
                DeliveryMode = RabbitMQ.Client.DeliveryModes.Persistent,
                Priority = 5,
                CorrelationId = "corr-1",
                ReplyTo = "rpc.reply",
                Expiration = "60000",
                MessageId = "msg-1",
                Timestamp = 1755168000,
                Type = "OrderCreated",
                UserId = "guest",
                AppId = "checkout",
                Headers = new Dictionary<string, object> { ["x-attempt"] = 3L, ["x-flag"] = true, ["x-src"] = "web" }
            },
            exchange: "events",
            routingKey: "orders.created",
            redelivered: true);

        var parsed = MessageJson.Parse(MessageJson.Serialize(message));

        parsed.Should().BeEquivalentTo(message);
    }

    [Fact]
    public void Absent_properties_are_omitted_from_the_line()
    {
        MessageJson.Serialize(Message.FromBytes(Utf8("hi"))).Should().Be("""{"body":"hi"}""");
    }

    [Fact]
    public void Header_numbers_survive_as_numbers_rather_than_JsonElement()
    {
        var line = """{"body":"x","properties":{"headers":{"n":3,"d":1.5,"b":false,"s":"t"}}}""";

        var headers = MessageJson.Parse(line).Properties!.Headers!;

        headers["n"].Should().Be(3L);
        headers["d"].Should().Be(1.5d);
        headers["b"].Should().Be(false);
        headers["s"].Should().Be("t");
    }

    [Fact]
    public void Publish_accepts_a_bare_body_line()
    {
        MessageJson.Parse("""{"body":"hello"}""").BodyBytes.Should().Equal(Utf8("hello"));
    }

    [Fact]
    public void Malformed_json_is_a_usage_error_not_a_crash()
    {
        var parse = () => MessageJson.Parse("{not json");

        parse.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Base64_marker_with_a_non_base64_body_is_rejected()
    {
        var parse = () => MessageJson.Parse("""{"body":"not base64!!","bodyEncoding":"base64"}""");

        parse.Should().Throw<ArgumentException>().WithMessage("*base64*");
    }

    [Fact]
    public void Unknown_body_encoding_is_rejected()
    {
        var parse = () => MessageJson.Parse("""{"body":"x","bodyEncoding":"hex"}""");

        parse.Should().Throw<ArgumentException>().WithMessage("*bodyEncoding*");
    }

    [Fact]
    public async Task Ndjson_is_read_a_line_at_a_time()
    {
        using var reader = new StringReader("{\"body\":\"one\"}\n\n{\"body\":{\"n\":2}}\n");

        var bodies = new List<string>();
        await foreach (var message in MessageJson.ReadLinesAsync(reader))
        {
            bodies.Add(message.Body);
        }

        bodies.Should().Equal("one", """{"n":2}""");
    }

    [Fact]
    public async Task A_bad_line_names_its_line_number()
    {
        using var reader = new StringReader("{\"body\":\"one\"}\n{oops\n");

        var read = async () =>
        {
            await foreach (var _ in MessageJson.ReadLinesAsync(reader)) { }
        };

        await read.Should().ThrowAsync<ArgumentException>().WithMessage("Line 2:*");
    }
}
