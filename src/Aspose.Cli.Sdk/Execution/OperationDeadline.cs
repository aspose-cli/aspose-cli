using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Sdk.Execution;

/// <summary>
/// One monotonic absolute deadline shared by every phase of an invocation.
/// Child phases consume the remaining budget; they never restart it.
/// </summary>
public sealed class OperationDeadline : IDisposable
{
    private readonly long? _expiresAtTick;
    private readonly CancellationTokenSource? _deadlineCancellation;
    private readonly CancellationTokenSource? _linkedCancellation;

    private OperationDeadline(
        TimeSpan? originalBudget,
        long? expiresAtTick,
        CancellationToken cancellationToken)
    {
        OriginalBudget = originalBudget;
        _expiresAtTick = expiresAtTick;
        if (expiresAtTick is { } expires)
        {
            _deadlineCancellation = new CancellationTokenSource();
            TimeSpan remaining = Remaining ?? TimeSpan.Zero;
            if (remaining <= TimeSpan.Zero)
            {
                _deadlineCancellation.Cancel();
            }
            else
            {
                _deadlineCancellation.CancelAfter(remaining);
            }
        }

        if (cancellationToken.CanBeCanceled && _deadlineCancellation is not null)
        {
            _linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                _deadlineCancellation.Token);
        }
        else if (cancellationToken.CanBeCanceled)
        {
            _linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);
        }

        Token = _linkedCancellation?.Token
            ?? _deadlineCancellation?.Token
            ?? CancellationToken.None;
    }

    /// <summary>The original caller budget, or null for an unbounded invocation.</summary>
    public TimeSpan? OriginalBudget { get; }

    /// <summary>The cancellation token linked to the deadline and caller cancellation.</summary>
    public CancellationToken Token { get; }

    /// <summary>The absolute monotonic expiration tick used by supervised child processes.</summary>
    public long? ExpiresAtTick => _expiresAtTick;

    /// <summary>The remaining budget at the instant this property is read.</summary>
    public TimeSpan? Remaining => _expiresAtTick is { } expires
        ? TimeSpan.FromMilliseconds(Math.Max(0, expires - Environment.TickCount64))
        : null;

    /// <summary>Whether the absolute deadline has elapsed.</summary>
    public bool IsExpired =>
        _deadlineCancellation?.IsCancellationRequested == true
        || _expiresAtTick is { } expires && Environment.TickCount64 >= expires;

    /// <summary>Creates one deadline from a relative caller budget.</summary>
    public static OperationDeadline Start(
        TimeSpan? budget,
        CancellationToken cancellationToken = default)
    {
        if (budget is { } finite && finite <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(budget),
                "A finite operation deadline must be positive.");
        }

        long? expires = budget is { } duration
            ? checked(Environment.TickCount64 + (long)Math.Ceiling(duration.TotalMilliseconds))
            : null;
        return new OperationDeadline(budget, expires, cancellationToken);
    }

    /// <summary>
    /// Rehydrates a parent-created monotonic deadline in a supervised worker.
    /// TickCount64 is system-boot relative and therefore shared by local
    /// processes on the same machine.
    /// </summary>
    public static OperationDeadline FromAbsoluteTick(
        TimeSpan originalBudget,
        long expiresAtTick,
        CancellationToken cancellationToken = default) =>
        new(originalBudget, expiresAtTick, cancellationToken);

    /// <summary>Throws the stable timeout error when no budget remains.</summary>
    public void ThrowIfExpired(string phase)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(phase);
        if (IsExpired)
        {
            throw CliErrors.OperationTimeout(
                Math.Max(1, (int)Math.Ceiling(OriginalBudget?.TotalSeconds ?? 1)),
                phase);
        }

        Token.ThrowIfCancellationRequested();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _linkedCancellation?.Dispose();
        _deadlineCancellation?.Dispose();
    }
}
