namespace RmqCli.Commands.Consume;

public enum AckModes
{
    /// <summary>
    /// Acknowledge the message, removing it from the queue
    /// </summary>
    Ack,

    /// <summary>
    /// Reject the message and discard it
    /// </summary>
    Reject,

    /// <summary>
    /// Reject the message and requeue it
    /// </summary>
    Requeue
}