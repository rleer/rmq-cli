using RmqCli.Core.Models;
using RmqCli.Shared;

namespace RmqCli.Commands.Publish;

public static class PublishResponseFactory
{
    public static PublishResponse Success(
        DestinationInfo destination,
        List<PublishOperationDto> publishRawResults,
        TimeSpan duration)
    {
        var response = BuildResponse(destination, publishRawResults, duration);
        response.Status = "success";
        response.Result!.MessagesFailed = 0;

        return response;
    }

    public static PublishResponse Partial(
        DestinationInfo destination,
        List<PublishOperationDto> publishRawResults,
        int failedMessages,
        TimeSpan duration)
    {
        var response = BuildResponse(destination, publishRawResults, duration);
        response.Status = "partial";
        response.Result!.MessagesFailed = failedMessages;

        return response;
    }

    public static PublishResponse Failure(
        DestinationInfo destination,
        int totalMessages,
        TimeSpan duration)
    {
        return new PublishResponse
        {
            Timestamp = DateTime.Now,
            Status = "failure",
            Destination = destination,
            Result = new PublishResult
            {
                MessagesPublished = 0,
                MessagesFailed = totalMessages,
                Duration = OutputUtilities.GetElapsedTimeString(duration),
                DurationMs = duration.TotalMilliseconds,
                AverageMessageSize = "0 B",
                TotalSize = "0 B"
            }
        };
    }

    private static PublishResponse BuildResponse(
        DestinationInfo destination,
        List<PublishOperationDto> publishRawResults,
        TimeSpan duration)
    {
        var totalBytes = publishRawResults.Sum(p => p.MessageLength);
        var avgBytes = Math.Round(totalBytes / (double)publishRawResults.Count, 2);

        var totalBytesString = OutputUtilities.ToSizeString(totalBytes);
        var avgBytesString = OutputUtilities.ToSizeString(avgBytes);

        return new PublishResponse
        {
            Timestamp = DateTime.Now,
            Destination = destination,
            Result = new PublishResult
            {
                MessagesPublished = publishRawResults.Count,
                Duration = OutputUtilities.GetElapsedTimeString(duration),
                DurationMs = duration.TotalMilliseconds,
                MessageIds = publishRawResults.Select(p => p.MessageId).ToList(),
                FirstMessageId = publishRawResults[0].MessageId,
                LastMessageId = publishRawResults[^1].MessageId,
                FirstTimestamp = publishRawResults[0].Timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
                LastTimestamp = publishRawResults[^1].Timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
                AverageMessageSizeBytes = avgBytes,
                AverageMessageSize = avgBytesString,
                TotalSizeBytes = totalBytes,
                TotalSize = totalBytesString,
                MessagesPerSecond = Math.Round(publishRawResults.Count / duration.TotalSeconds, 2)
            }
        };
    }
}