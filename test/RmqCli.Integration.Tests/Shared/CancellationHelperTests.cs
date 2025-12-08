using RmqCli.Shared;

namespace RmqCli.Integration.Tests.Shared;

public class CancellationHelperTests
{
    public class LinkWithCtrlCHandler
    {
        [Fact]
        public void ReturnsValidCancellationTokenSource()
        {
            // Arrange
            using var inputCts = new CancellationTokenSource();

            // Act
            using var result = CancellationHelper.LinkWithCtrlCHandler(inputCts.Token);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeAssignableTo<CancellationTokenSource>();
        }

        [Fact]
        public void ReturnedToken_IsNotCancelled_Initially()
        {
            // Arrange
            using var inputCts = new CancellationTokenSource();

            // Act
            using var result = CancellationHelper.LinkWithCtrlCHandler(inputCts.Token);

            // Assert
            result.Token.IsCancellationRequested.Should().BeFalse();
            result.IsCancellationRequested.Should().BeFalse();
        }

        [Fact]
        public void ReturnedToken_IsLinkedToInputToken()
        {
            // Arrange
            using var inputCts = new CancellationTokenSource();
            using var linkedCts = CancellationHelper.LinkWithCtrlCHandler(inputCts.Token);

            // Act - Cancel the input token
            inputCts.Cancel();

            // Assert - The linked token should also be cancelled
            linkedCts.Token.IsCancellationRequested.Should().BeTrue();
            linkedCts.IsCancellationRequested.Should().BeTrue();
        }

        [Fact]
        public void ReturnedToken_CanBeCancelledIndependently()
        {
            // Arrange
            using var inputCts = new CancellationTokenSource();
            using var linkedCts = CancellationHelper.LinkWithCtrlCHandler(inputCts.Token);

            // Act - Cancel the linked token
            linkedCts.Cancel();

            // Assert - The linked token is cancelled, but input is not
            linkedCts.Token.IsCancellationRequested.Should().BeTrue();
            inputCts.Token.IsCancellationRequested.Should().BeFalse();
        }

        [Fact]
        public void WorksWithAlreadyCancelledToken()
        {
            // Arrange
            using var inputCts = new CancellationTokenSource();
            inputCts.Cancel();

            // Act
            using var result = CancellationHelper.LinkWithCtrlCHandler(inputCts.Token);

            // Assert - The returned token should also be cancelled
            result.Token.IsCancellationRequested.Should().BeTrue();
        }

        [Fact]
        public void WorksWithCancellationTokenNone()
        {
            // Act
            using var result = CancellationHelper.LinkWithCtrlCHandler(CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.Token.IsCancellationRequested.Should().BeFalse();
        }

        [Fact]
        public void MultipleInvocations_CreateIndependentTokens()
        {
            // Arrange
            using var inputCts1 = new CancellationTokenSource();
            using var inputCts2 = new CancellationTokenSource();

            // Act
            using var result1 = CancellationHelper.LinkWithCtrlCHandler(inputCts1.Token);
            using var result2 = CancellationHelper.LinkWithCtrlCHandler(inputCts2.Token);

            // Assert - Cancelling one doesn't affect the other
            result1.Cancel();
            result1.Token.IsCancellationRequested.Should().BeTrue();
            result2.Token.IsCancellationRequested.Should().BeFalse();
        }

        [Fact]
        public void ReturnedTokenSource_CanBeDisposed()
        {
            // Arrange
            using var inputCts = new CancellationTokenSource();
            var linkedCts = CancellationHelper.LinkWithCtrlCHandler(inputCts.Token);

            // Act
            Action act = () => linkedCts.Dispose();

            // Assert - Should not throw when disposing
            act.Should().NotThrow();
        }

        [Fact]
        public void CancellationPropagates_BeforeDisposal()
        {
            // Arrange
            using var inputCts = new CancellationTokenSource();
            var linkedCts = CancellationHelper.LinkWithCtrlCHandler(inputCts.Token);
            var tokenBeforeDispose = linkedCts.Token;

            // Act
            inputCts.Cancel();
            linkedCts.Dispose();

            // Assert
            tokenBeforeDispose.IsCancellationRequested.Should().BeTrue();
        }

        [Fact]
        public void ReturnedToken_AllowsRegistration()
        {
            // Arrange
            using var inputCts = new CancellationTokenSource();
            using var linkedCts = CancellationHelper.LinkWithCtrlCHandler(inputCts.Token);
            var callbackInvoked = false;
            using var registration = linkedCts.Token.Register(() => callbackInvoked = true);

            // Act
            linkedCts.Cancel();

            // Assert
            callbackInvoked.Should().BeTrue();
        }

        [Fact]
        public void ReturnedToken_PropagatesCancellationToRegisteredCallbacks()
        {
            // Arrange
            using var inputCts = new CancellationTokenSource();
            using var linkedCts = CancellationHelper.LinkWithCtrlCHandler(inputCts.Token);
            var callbackInvoked = false;
            using var registration = linkedCts.Token.Register(() => callbackInvoked = true);

            // Act - Cancel via the input token
            inputCts.Cancel();

            // Assert - Callback should be invoked through linkage
            callbackInvoked.Should().BeTrue();
        }

        [Fact]
        public void HandlesRapidCancellation()
        {
            // Arrange
            using var inputCts = new CancellationTokenSource();
            using var linkedCts = CancellationHelper.LinkWithCtrlCHandler(inputCts.Token);

            // Act - Cancel immediately
            inputCts.Cancel();

            // Assert
            linkedCts.Token.IsCancellationRequested.Should().BeTrue();
        }

        [Fact]
        public void RegistersConsoleHandler_WithoutThrowing()
        {
            // Arrange
            using var inputCts = new CancellationTokenSource();

            // Act - This registers the Console.CancelKeyPress handler
            Action act = () =>
            {
                using var linkedCts = CancellationHelper.LinkWithCtrlCHandler(inputCts.Token);
            };

            // Assert - Should not throw when registering handler
            act.Should().NotThrow();
        }

        [Fact]
        public void MultipleConsoleHandlers_CanBeRegistered()
        {
            // Arrange
            using var inputCts1 = new CancellationTokenSource();
            using var inputCts2 = new CancellationTokenSource();

            // Act - Register multiple handlers
            Action act = () =>
            {
                using var linkedCts1 = CancellationHelper.LinkWithCtrlCHandler(inputCts1.Token);
                using var linkedCts2 = CancellationHelper.LinkWithCtrlCHandler(inputCts2.Token);
            };

            // Assert - Should not throw when registering multiple handlers
            act.Should().NotThrow();
        }

        [Fact]
        public void TokenCapturedBeforeDisposal_RemainsValid()
        {
            // Arrange
            using var inputCts = new CancellationTokenSource();
            var linkedCts = CancellationHelper.LinkWithCtrlCHandler(inputCts.Token);
            var token = linkedCts.Token; // Capture token before disposal

            // Act
            linkedCts.Dispose();
            Action act = () => _ = token.IsCancellationRequested;

            // Assert - Token captured before disposal remains accessible
            act.Should().NotThrow();
        }
    }
}
