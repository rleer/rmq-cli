namespace RmqCli.Commands.MessageRetrieval;

public sealed class ReceivedMessageCounter
{
    private long _count;

    public long Increment() => Interlocked.Increment(ref _count);

    public long Value => Interlocked.Read(ref _count);
}