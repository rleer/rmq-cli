using RmqCli.Core.Models;

namespace RmqCli.Shared.Factories;

public static class ErrorInfoFactory
{
    public static ErrorInfo GenericErrorInfo(string error, string code, string suggestion, string category = "internal", Exception? exception = null)
    {
        return new ErrorInfo
        {
            Category = category,
            Code = code,
            Error = error,
            Suggestion = suggestion,
            Details = exception != null ? new Dictionary<string, object>
            {
                { "exception_type", exception.GetType().Name },
                { "exception_message", exception.Message }
            } : null
        };
    } 
}