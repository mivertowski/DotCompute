namespace DotCompute.Core.Pipelines.Stages;

/// <summary>
/// Represents a custom synchronization strategy for parallel stage execution.
/// Provides flexible synchronization patterns beyond standard barrier and async approaches.
/// </summary>
public abstract class CustomSyncStrategy : IAsyncDisposable
{
    /// <summary>
    /// Gets the name of this synchronization strategy.
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    /// Gets a value indicating whether this strategy supports cancellation.
    /// </summary>
    public virtual bool SupportsCancellation => true;

    /// <summary>
    /// Gets a value indicating whether this strategy supports timeout operations.
    /// </summary>
    public virtual bool SupportsTimeout => true;

    /// <summary>
    /// Initializes the synchronization strategy for a given number of participants.
    /// </summary>
    /// <param name="participantCount">The number of parallel participants that will use this strategy.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A task representing the initialization operation.</returns>
    public virtual Task InitializeAsync(int participantCount, CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <summary>
    /// Synchronizes execution at a coordination point for the specified participant.
    /// </summary>
    /// <param name="participantId">The unique identifier for the participant.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A task that completes when synchronization is achieved.</returns>
    public abstract Task SynchronizeAsync(int participantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Synchronizes execution with a timeout for the specified participant.
    /// </summary>
    /// <param name="participantId">The unique identifier for the participant.</param>
    /// <param name="timeout">The maximum time to wait for synchronization.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A task that completes when synchronization is achieved or times out.</returns>
    /// <exception cref="TimeoutException">Thrown when the synchronization times out.</exception>
    public virtual async Task SynchronizeAsync(int participantId, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        if (!SupportsTimeout)
        {

            throw new NotSupportedException($"Synchronization strategy '{Name}' does not support timeout operations.");
        }


        using var timeoutCts = new CancellationTokenSource(timeout);
        using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            await SynchronizeAsync(participantId, combinedCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested)
        {
            throw new TimeoutException($"Synchronization for participant {participantId} timed out after {timeout}.");
        }
    }

    /// <summary>
    /// Signals completion of a participant and performs any necessary cleanup.
    /// </summary>
    /// <param name="participantId">The unique identifier for the participant.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A task representing the completion operation.</returns>
    public virtual Task CompleteAsync(int participantId, CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <summary>
    /// Resets the synchronization strategy to its initial state for reuse.
    /// </summary>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A task representing the reset operation.</returns>
    public virtual Task ResetAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <summary>
    /// Performs cleanup and releases any resources held by the strategy.
    /// </summary>
    /// <returns>A ValueTask representing the cleanup operation.</returns>
    public virtual ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>
/// A barrier-based synchronization strategy that waits for all participants at each sync point.
/// </summary>
public sealed class BarrierSyncStrategy : CustomSyncStrategy
{
    private Barrier? _barrier;
    private int _participantCount;

    /// <inheritdoc />
    public override string Name => "Barrier";

    /// <inheritdoc />
    public override Task InitializeAsync(int participantCount, CancellationToken cancellationToken = default)
    {
        _participantCount = participantCount;
        _barrier = new Barrier(participantCount);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task SynchronizeAsync(int participantId, CancellationToken cancellationToken = default)
    {
        if (_barrier == null)
        {

            throw new InvalidOperationException("Strategy must be initialized before use.");
        }


        return Task.Run(() =>
        {
            _barrier.SignalAndWait(cancellationToken);
        }, cancellationToken);
    }

    /// <inheritdoc />
    public override ValueTask DisposeAsync()
    {
        _barrier?.Dispose();
        _barrier = null;
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// A countdown-based synchronization strategy that waits for a specified number of participants.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="CountdownSyncStrategy"/> class.
/// </remarks>
/// <param name="requiredCount">The number of participants required to proceed. If 0, uses all participants.</param>
public sealed class CountdownSyncStrategy(int requiredCount = 0) : CustomSyncStrategy
{
    private CountdownEvent? _countdown;
    private readonly int _requiredCount = requiredCount;

    /// <inheritdoc />
    public override string Name => $"Countdown({_requiredCount})";

    /// <inheritdoc />
    public override Task InitializeAsync(int participantCount, CancellationToken cancellationToken = default)
    {
        var count = _requiredCount > 0 ? Math.Min(_requiredCount, participantCount) : participantCount;
        _countdown = new CountdownEvent(count);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task SynchronizeAsync(int participantId, CancellationToken cancellationToken = default)
    {
        if (_countdown == null)
        {

            throw new InvalidOperationException("Strategy must be initialized before use.");
        }


        return Task.Run(() =>
        {
            _ = _countdown.Signal();
            _countdown.Wait(cancellationToken);
        }, cancellationToken);
    }

    /// <inheritdoc />
    public override Task ResetAsync(CancellationToken cancellationToken = default)
    {
        _countdown?.Reset();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override ValueTask DisposeAsync()
    {
        _countdown?.Dispose();
        _countdown = null;
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// A no-op synchronization strategy that performs no actual synchronization.
/// Useful for fire-and-forget parallel execution patterns.
/// </summary>
public sealed class NoOpSyncStrategy : CustomSyncStrategy
{
    /// <inheritdoc />
    public override string Name => "NoOp";

    /// <inheritdoc />
    public override bool SupportsCancellation => false;

    /// <inheritdoc />
    public override bool SupportsTimeout => false;

    /// <inheritdoc />
    public override Task SynchronizeAsync(int participantId, CancellationToken cancellationToken = default) => Task.CompletedTask;
}