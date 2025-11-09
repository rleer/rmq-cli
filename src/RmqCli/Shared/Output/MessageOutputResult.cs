namespace RmqCli.Shared.Output;

/// <summary>
/// Contains statistics about messages that were processed by a MessageOutput implementation
/// </summary>
/// <param name="ProcessedCount">Number of messages successfully processed (written to output)</param>
/// <param name="TotalBytes">Total number of bytes processed (written to output)</param>
public record MessageOutputResult(
    long ProcessedCount,
    long TotalBytes);