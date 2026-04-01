using MotoBlackBoxViewer.App.Helpers;

namespace MotoBlackBoxViewer.Tests;

public sealed class AsyncRelayCommandTests
{
    [Fact]
    public async Task ExecuteAsync_WhenDelegateThrows_ReenablesCommand()
    {
        AsyncRelayCommand command = new(() => throw new InvalidOperationException("boom"));

        Assert.True(command.CanExecute(null));

        await command.ExecuteAsync();

        Assert.True(command.CanExecute(null));
    }

    [Fact]
    public async Task ExecuteAsync_WhenDelegateIsCanceled_ReenablesCommand()
    {
        AsyncRelayCommand command = new(() => throw new OperationCanceledException("canceled"));

        Assert.True(command.CanExecute(null));

        await command.ExecuteAsync();

        Assert.True(command.CanExecute(null));
    }

    [Fact]
    public async Task ExecuteAsync_WhenAlreadyRunning_DoesNotStartSecondExecution()
    {
        int executionCount = 0;
        TaskCompletionSource gate = new();
        AsyncRelayCommand command = new(async () =>
        {
            executionCount++;
            await gate.Task;
        });

        Task firstExecution = command.ExecuteAsync();
        Task secondExecution = command.ExecuteAsync();
        gate.SetResult();

        await Task.WhenAll(firstExecution, secondExecution);

        Assert.Equal(1, executionCount);
    }
}
