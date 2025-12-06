using System.Text;
using RabbitMQ.Client;
using RmqCli.Core.Models;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace RmqCli.Shared.Output.Formatters;

/// <summary>
/// Formats RabbitMQ messages as pretty-printed tables using Spectre.Console.
/// </summary>
public static class TableMessageFormatter
{
    private const int LabelWidth = 17; // Width for left-aligned labels

    /// <summary>
    /// Formats a single message as a table panel.
    /// </summary>
    /// <param name="message">The message to format</param>
    /// <param name="compact">If true, only show properties with values. If false, show all properties with "-" for empty values.</param>
    /// <param name="ansiSupport">Determines ANSI escape sequence support</param>
    public static string FormatMessage(RetrievedMessage message, bool compact = false, AnsiSupport ansiSupport = AnsiSupport.Detect)
    {
        var panel = CreateMessagePanel(message, compact);

        // Render to string using AnsiConsole
        var stringWriter = new StringWriter();
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = ansiSupport,
            ColorSystem = ansiSupport == AnsiSupport.No ? ColorSystemSupport.NoColors : ColorSystemSupport.Detect,
            Out = new AnsiConsoleOutput(stringWriter)
        });

        console.Write(panel);
        return stringWriter.ToString().TrimEnd();
    }

    /// <summary>
    /// Formats multiple messages separated by newlines.
    /// </summary>
    public static string FormatMessages(IEnumerable<RetrievedMessage> messages, bool compact = false, AnsiSupport ansiSupport = AnsiSupport.Detect)
    {
        var messageList = messages.ToList();
        var sb = new StringBuilder();

        for (int i = 0; i < messageList.Count; i++)
        {
            if (i > 0)
            {
                sb.AppendLine(); // Blank line between messages
            }
            sb.Append(FormatMessage(messageList[i], compact, ansiSupport));
        }

        return sb.ToString();
    }

    private static Panel CreateMessagePanel(RetrievedMessage message, bool compact)
    {
        var content = CreateMessageContent(message, compact);

        var panel = new Panel(content)
        {
            Header = new PanelHeader($" Message #{message.DeliveryTag} ", Justify.Left),
            Border = BoxBorder.Rounded,
            Padding = new Padding(1, 0, 1, 0)
        };

        return panel;
    }

    private static IRenderable CreateMessageContent(RetrievedMessage message, bool compact)
    {
        var renderables = new List<IRenderable>();

        // Routing Information Section
        var routingTable = CreateSectionTable();
        AddRoutingRows(routingTable, message);
        renderables.Add(routingTable);

        // Properties Section
        if (message.Properties != null && (message.Properties.HasAnyProperty() || !compact))
        {
            renderables.Add(new Rule("[dim]Properties[/]").LeftJustified().RuleStyle("dim"));
            var propertiesTable = CreateSectionTable();
            AddPropertiesRows(propertiesTable, message.Properties, compact);
            renderables.Add(propertiesTable);
        }

        // Custom Headers Section
        if (message.Headers != null && message.Headers.Count > 0)
        {
            renderables.Add(new Rule("[dim]Custom Headers[/]").LeftJustified().RuleStyle("dim"));
            var headersTable = CreateSectionTable();
            AddHeadersRows(headersTable, message.Headers);
            renderables.Add(headersTable);
        }

        // Body Section
        renderables.Add(new Rule($"[dim]Body ({message.BodySize})[/]").LeftJustified().RuleStyle("dim"));
        renderables.Add(new Markup(Markup.Escape(message.Body)));

        // Combine all renderables into a Rows layout
        return new Rows(renderables);
    }

    private static Table CreateSectionTable()
    {
        var table = new Table()
            .Border(TableBorder.None)
            .HideHeaders();

        table.AddColumn(new TableColumn("Label").Width(LabelWidth).NoWrap());
        table.AddColumn(new TableColumn("Value"));

        return table;
    }

    private static void AddRoutingRows(Table table, RetrievedMessage message)
    {
        table.AddRow(new Markup("Queue"), new Markup(Markup.Escape(message.Queue)));
        table.AddRow(new Markup("Routing Key"), new Markup(Markup.Escape(message.RoutingKey)));
        table.AddRow(new Markup("Exchange"), new Markup(Markup.Escape(string.IsNullOrEmpty(message.Exchange) ? "-" : message.Exchange)));

        // Color-code redelivered status
        var redelivered = message.Redelivered ? "[yellow]Yes[/]" : "No";
        table.AddRow(new Markup("Redelivered"), new Markup(redelivered));
    }

    private static void AddPropertiesRows(Table table, MessageProperties props, bool compact)
    {
        if (compact)
        {
            // Compact mode: only show properties with values
            if (props.MessageId != null)
                AddRow(table, "Message ID", props.MessageId);
            if (props.CorrelationId != null)
                AddRow(table, "Correlation ID", props.CorrelationId);
            if (props.Timestamp != null)
                AddRow(table, "Timestamp", FormatTimestamp(props.Timestamp.Value) + " UTC");
            if (props.ContentType != null)
                AddRow(table, "Content Type", props.ContentType);
            if (props.ContentEncoding != null)
                AddRow(table, "Content Encoding", props.ContentEncoding);
            if (props.DeliveryMode != null)
            {
                var deliveryModeText = FormatDeliveryMode(props.DeliveryMode.Value);
                AddRow(table, "Delivery Mode", deliveryModeText);
            }
            if (props.Priority != null)
                AddRow(table, "Priority", props.Priority.ToString()!);
            if (props.Expiration != null)
                AddRow(table, "Expiration", props.Expiration);
            if (props.ReplyTo != null)
                AddRow(table, "Reply To", props.ReplyTo);
            if (props.Type != null)
                AddRow(table, "Type", props.Type);
            if (props.AppId != null)
                AddRow(table, "App ID", props.AppId);
            if (props.UserId != null)
                AddRow(table, "User ID", props.UserId);
            if (props.ClusterId != null)
                AddRow(table, "Cluster ID", props.ClusterId);
        }
        else
        {
            // Full mode: show all properties with "-" for empty
            AddRow(table, "Message ID", props.MessageId ?? "[dim]-[/]", allowMarkup: props.MessageId == null);
            AddRow(table, "Correlation ID", props.CorrelationId ?? "[dim]-[/]", allowMarkup: props.CorrelationId == null);
            AddRow(table, "Timestamp", props.Timestamp != null ? FormatTimestamp(props.Timestamp.Value) + " UTC" : "[dim]-[/]", allowMarkup: props.Timestamp == null);
            AddRow(table, "Content Type", props.ContentType ?? "[dim]-[/]", allowMarkup: props.ContentType == null);
            AddRow(table, "Content Encoding", props.ContentEncoding ?? "[dim]-[/]", allowMarkup: props.ContentEncoding == null);

            if (props.DeliveryMode != null)
            {
                var deliveryModeText = FormatDeliveryMode(props.DeliveryMode.Value);
                AddRow(table, "Delivery Mode", deliveryModeText);
            }
            else
            {
                AddRow(table, "Delivery Mode", "[dim]-[/]", allowMarkup: true);
            }

            AddRow(table, "Priority", props.Priority?.ToString() ?? "[dim]-[/]", allowMarkup: props.Priority == null);
            AddRow(table, "Expiration", props.Expiration ?? "[dim]-[/]", allowMarkup: props.Expiration == null);
            AddRow(table, "Reply To", props.ReplyTo ?? "[dim]-[/]", allowMarkup: props.ReplyTo == null);
            AddRow(table, "Type", props.Type ?? "[dim]-[/]", allowMarkup: props.Type == null);
            AddRow(table, "App ID", props.AppId ?? "[dim]-[/]", allowMarkup: props.AppId == null);
            AddRow(table, "User ID", props.UserId ?? "[dim]-[/]", allowMarkup: props.UserId == null);
            AddRow(table, "Cluster ID", props.ClusterId ?? "[dim]-[/]", allowMarkup: props.ClusterId == null);
        }
    }

    private static void AddHeadersRows(Table table, IDictionary<string, object> headers)
    {
        foreach (var header in headers)
        {
            var value = HeaderValueFormatter.FormatValue(header.Value);
            AddRow(table, header.Key, value);
        }
    }

    private static void AddRow(Table table, string label, string value, bool allowMarkup = false)
    {
        if (allowMarkup)
        {
            table.AddRow(new Markup(label), new Markup(value));
        }
        else
        {
            table.AddRow(new Markup(label), new Markup(Markup.Escape(value)));
        }
    }

    private static string FormatDeliveryMode(DeliveryModes mode)
    {
        return mode switch
        {
            DeliveryModes.Transient => "Non-persistent (1)",
            DeliveryModes.Persistent => "Persistent (2)"
        };
    }

    /// <summary>
    /// Formats a Unix timestamp (seconds) to a standard string format.
    /// </summary>
    private static string FormatTimestamp(long unixSeconds)
    {
        var dateTime = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
        return dateTime.ToString("yyyy-MM-dd HH:mm:ss");
    }
}
