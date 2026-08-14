using System.CommandLine;

namespace Rmq;

public static class PurgeCommand
{
    public static Command Create()
    {
        var queue = new Argument<string>("queue")
        {
            Description = "Queue to empty"
        };

        var command = new Command("purge", "Discard every message in a queue. There is no undo.");
        command.Add(queue);

        command.SetAction(async (parse, ct) =>
        {
            var settings = GlobalOptions.Settings(parse);
            var name = parse.GetRequiredValue(queue);

            await using var connection = await Amqp.ConnectAsync(settings, ct);
            await using var channel = await connection.CreateChannelAsync(cancellationToken: ct);

            var purged = await channel.QueuePurgeAsync(name, ct);

            Console.Error.WriteLine($"purged {purged} message{(purged == 1 ? "" : "s")} from {name}");
            return ExitCode.Success;
        });

        return command;
    }
}
