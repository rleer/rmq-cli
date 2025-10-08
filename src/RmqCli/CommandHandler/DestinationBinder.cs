using System.CommandLine;
using System.CommandLine.Binding;
using RmqCli.Common;

namespace RmqCli.CommandHandler;

public class DestinationBinder : BinderBase<DestinationInfo>
{
    private readonly Option<string> _queueOption;
    private readonly Option<string> _exchangeOption;
    private readonly Option<string> _routingKeyOption;

    public DestinationBinder(Option<string> queueOption, Option<string> exchangeOption, Option<string> routingKeyOption)
    {
        _queueOption = queueOption;
        _exchangeOption = exchangeOption;
        _routingKeyOption = routingKeyOption;
    }
    
    protected override DestinationInfo GetBoundValue(BindingContext bindingContext)
    {
        var queue = bindingContext.ParseResult.GetValueForOption(_queueOption);
        var exchange = bindingContext.ParseResult.GetValueForOption(_exchangeOption);
        var routingKey = bindingContext.ParseResult.GetValueForOption(_routingKeyOption);
        
        var type = string.IsNullOrWhiteSpace(queue) ? "exchange" : "queue";
        
        return new DestinationInfo
        {
            Queue = queue,
            Exchange = exchange,
            RoutingKey = routingKey,
            Type = type
        };
    }
}