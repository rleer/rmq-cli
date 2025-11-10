using Microsoft.Extensions.Logging.Abstractions;
using RmqCli.Commands.MessageRetrieval.Strategies;

namespace RmqCli.Integration.Tests.Commands.MessageRetrieval.Strategies;

/// <summary>
/// Tests for SubscriberStrategy.
///
/// Note: Full testing of the async event-driven consumer behavior (message delivery, cancellation handling, etc.)
/// is complex to test with mocks due to:
/// 1. NSubstitute's limitations with multiple parameters of the same type in BasicConsumeAsync
/// 2. The event-driven nature of AsyncEventingBasicConsumer
/// 3. Complex async cancellation token coordination
///
/// These scenarios are better covered by E2E tests with a real RabbitMQ instance.
/// Tests that would be valuable in E2E:
/// - Message delivery via AsyncEventingBasicConsumer
/// - Message limit reached cancellation
/// - User cancellation handling
/// - Channel completion on cancellation
/// - Property and header extraction from delivered messages
/// </summary>
public class SubscriberStrategyTests
{
    [Fact]
    public void StrategyName_ReturnsSubscribe()
    {
        // Arrange
        var logger = new NullLogger<SubscriberStrategy>();
        var strategy = new SubscriberStrategy(logger);

        // Act & Assert
        strategy.StrategyName.Should().Be("Subscribe");
    }

}