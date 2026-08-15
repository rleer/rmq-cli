using RabbitMQ.Client;

namespace Rmq.Unit.Tests;

/// <summary>
/// The AMQP field table is the one place where types the serializer cannot write reach
/// the message, and the normalization that fixes it is recursive. Both directions are
/// pinned here — E2E proves it works against a broker, this proves the closed set.
/// </summary>
public class HeaderConversionTests
{
    private static Dictionary<string, object> Convert(Dictionary<string, object?> headers)
    {
        var properties = new BasicProperties { Headers = headers };
        return Amqp.ToProperties(properties)!.Headers!;
    }

    [Fact]
    public void Longstr_arrives_as_bytes_and_is_decoded_to_text()
    {
        // Left alone this serializes to "d2Vi", which is the trap the whole rule exists for.
        var headers = Convert(new Dictionary<string, object?> { ["x-source"] = "web"u8.ToArray() });

        headers["x-source"].Should().Be("web");
    }

    [Fact]
    public void A_longstr_that_is_not_valid_utf8_becomes_base64()
    {
        byte[] binary = [0xFF, 0xFE, 0x00];

        var headers = Convert(new Dictionary<string, object?> { ["x-blob"] = binary });

        headers["x-blob"].Should().Be(System.Convert.ToBase64String(binary));
    }

    [Fact]
    public void Numeric_widths_collapse_to_long_and_double()
    {
        var headers = Convert(new Dictionary<string, object?>
        {
            ["byte"] = (byte)1,
            ["short"] = (short)2,
            ["int"] = 3,
            ["long"] = 4L,
            ["float"] = 1.5f,
            ["double"] = 2.5d,
            ["bool"] = true
        });

        headers["byte"].Should().Be(1L);
        headers["short"].Should().Be(2L);
        headers["int"].Should().Be(3L);
        headers["long"].Should().Be(4L);
        headers["float"].Should().Be(1.5d);
        headers["double"].Should().Be(2.5d);
        headers["bool"].Should().Be(true);
    }

    [Fact]
    public void Amqp_timestamps_become_unix_seconds()
    {
        var headers = Convert(new Dictionary<string, object?> { ["when"] = new AmqpTimestamp(1700000000) });

        headers["when"].Should().Be(1700000000L);
    }

    [Fact]
    public void Null_header_values_become_empty_strings()
    {
        var headers = Convert(new Dictionary<string, object?> { ["nothing"] = null });

        headers["nothing"].Should().Be(string.Empty);
    }

    /// <summary>
    /// x-death on any dead-lettered message: a list of field tables, each holding its own
    /// list. Left unnormalized this throws at serialize time.
    /// </summary>
    [Fact]
    public void Nested_tables_and_lists_are_normalized_all_the_way_down()
    {
        var death = new Dictionary<string, object?>
        {
            ["queue"] = "orders"u8.ToArray(),
            ["count"] = 2L,
            ["routing-keys"] = new List<object?> { "rk"u8.ToArray() }
        };

        var headers = Convert(new Dictionary<string, object?> { ["x-death"] = new List<object?> { death } });

        var entries = headers["x-death"].Should().BeOfType<List<object>>().Subject;
        var entry = entries.Single().Should().BeOfType<Dictionary<string, object>>().Subject;

        entry["queue"].Should().Be("orders");
        entry["count"].Should().Be(2L);
        entry["routing-keys"].Should().BeOfType<List<object>>().Which.Single().Should().Be("rk");
    }

    [Fact]
    public void A_message_with_no_properties_at_all_converts_to_null()
    {
        Amqp.ToProperties(new BasicProperties()).Should().BeNull("an empty properties object is noise on every line");
    }

    [Fact]
    public void Properties_survive_the_trip_out_to_amqp_and_back()
    {
        var original = new MessageProperties
        {
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent,
            Priority = 7,
            Timestamp = 1700000000,
            Headers = new Dictionary<string, object>
            {
                ["x-source"] = "web",
                ["x-attempt"] = 3L,
                ["x-nested"] = new Dictionary<string, object> { ["inner"] = "value" }
            }
        };

        var round = Amqp.ToProperties(Amqp.ToBasicProperties(original));

        round.Should().BeEquivalentTo(original);
    }

    /// <summary>
    /// The read half: publish has to rebuild a field table from JSON, or a consumed x-death
    /// republishes as a string and the round trip quietly stops holding.
    /// </summary>
    [Fact]
    public void Json_objects_and_arrays_parse_back_into_tables_and_lists()
    {
        var message = MessageJson.Parse("""
            {"body":"x","properties":{"headers":{"x-death":[{"count":1,"routing-keys":["rk"]}],"x-flag":true}}}
            """);

        var headers = message.Properties!.Headers!;
        var entries = headers["x-death"].Should().BeOfType<List<object>>().Subject;
        var entry = entries.Single().Should().BeOfType<Dictionary<string, object>>().Subject;

        entry["count"].Should().Be(1L);
        entry["routing-keys"].Should().BeOfType<List<object>>().Which.Single().Should().Be("rk");
        headers["x-flag"].Should().Be(true);
    }
}
