using System.Collections.Concurrent;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Execution;

namespace Aspose.Cli.Sdk.IO;

/// <summary>Versioned defaults and hard safety maxima for ordinary CLI input.</summary>
public static class ResourceBudgetDefaults
{
    public const int ContractVersion = 2;
    public const long DefaultInputBytes = 1L << 30;
    public const long MaximumInputBytes = 4L << 30;
    public const long DefaultStandardInputBytes = 16L << 20;
    public const long MaximumStandardInputBytes = 64L << 20;
    public const long DefaultDecodedTextCharacters = 16L << 20;
    public const long MaximumDecodedTextCharacters = 64L << 20;
    public const long DefaultMemoryBufferBytes = 64L << 20;
    public const long MaximumMemoryBufferBytes = 256L << 20;
    public const long DefaultOutputBytes = 512L << 20;
    public const long MaximumOutputBytes = 2L << 30;
    public const long DefaultSecretCharacters = 4L << 10;
    public const long MaximumSecretCharacters = 16L << 10;

    /// <summary>Pixels of one raster image: 256 megapixels, about 1 GiB of 32-bit color.</summary>
    public const long RasterPixels = 256L << 20;

    /// <summary>Public global limits in stable resource-name order.</summary>
    public static IReadOnlyList<ResourceBudgetCapabilities> Global { get; } =
    [
        ResourceBudgetCapabilities.Domain(
            ResourceBudgetKinds.DecodedTextCharacters,
            DefaultDecodedTextCharacters,
            MaximumDecodedTextCharacters,
            "characters",
            "decode"),
        ResourceBudgetCapabilities.Domain(
            ResourceBudgetKinds.InputBytes,
            DefaultInputBytes,
            MaximumInputBytes,
            "bytes",
            "pre-runtime-and-stream",
            "--max-input-bytes",
            InputSizeGuard.BudgetVariable),
        ResourceBudgetCapabilities.Domain(
            ResourceBudgetKinds.MemoryBufferBytes,
            DefaultMemoryBufferBytes,
            MaximumMemoryBufferBytes,
            "bytes",
            "buffer-allocation"),
        ResourceBudgetCapabilities.Domain(
            ResourceBudgetKinds.OutputBytes,
            DefaultOutputBytes,
            MaximumOutputBytes,
            "bytes",
            "output-stream"),
        ResourceBudgetCapabilities.Domain(ResourceBudgetKinds.OutputSetEntries,
            PublicationLimits.MaximumEntries, PublicationLimits.MaximumEntries, "items", "output-set-admission"),
        ResourceBudgetCapabilities.Domain(ResourceBudgetKinds.OutputSetDirectories,
            PublicationLimits.MaximumDirectories, PublicationLimits.MaximumDirectories, "items", "output-directory-admission"),
        ResourceBudgetCapabilities.Domain(ResourceBudgetKinds.PublicationMetadataBytes,
            PublicationLimits.MaximumMetadataBytes, PublicationLimits.MaximumMetadataBytes, "bytes", "publication-seal"),
        ResourceBudgetCapabilities.Domain(ResourceBudgetKinds.RasterPixels,
            RasterPixels, RasterPixels, "pixels", "pre-render"),
        ResourceBudgetCapabilities.Domain(
            ResourceBudgetKinds.SecretCharacters,
            DefaultSecretCharacters,
            MaximumSecretCharacters,
            "characters",
            "secret-read"),
        ResourceBudgetCapabilities.Domain(
            ResourceBudgetKinds.StandardInputBytes,
            DefaultStandardInputBytes,
            MaximumStandardInputBytes,
            "bytes",
            "stdin-stream"),
    ];
}

/// <summary>
/// Invocation-scoped budget ledger shared by every operation in one command.
/// </summary>
public sealed class ResourceBudgetLedger
{
    private readonly IReadOnlyDictionary<string, long> _limits;
    private CliException? _failure;
    private readonly ConcurrentDictionary<string, long> _consumed;
    private readonly ConcurrentDictionary<string, AdmittedFile> _admitted;

    public ResourceBudgetLedger(
        OperationDeadline deadline,
        IReadOnlyDictionary<string, long>? limits = null,
        WorkerOutputSession? outputSession = null)
    {
        Deadline = deadline ?? throw new ArgumentNullException(nameof(deadline));
        _limits = NormalizeLimits(limits);
        _consumed = new ConcurrentDictionary<string, long>(
            StringComparer.Ordinal);
        _admitted = new ConcurrentDictionary<string, AdmittedFile>(
            OperatingSystem.IsWindows()
                ? StringComparer.OrdinalIgnoreCase
                : StringComparer.Ordinal);
        Inputs = new InputSource(this);
        OutputSession = outputSession;
    }

    /// <summary>Absolute invocation deadline shared with all budget checks.</summary>
    public OperationDeadline Deadline { get; }

    /// <summary>Explicit deferred publication selected by the owning host, if any.</summary>
    public WorkerOutputSession? OutputSession { get; }

    /// <summary>Whether this invocation has durably committed a final output set.</summary>
    public bool HasCommittedOutputs { get; private set; }
    internal void MarkOutputsCommitted() => HasCommittedOutputs = true;


    /// <summary>Bounded file/stdin/text reader backed by this ledger.</summary>
    public InputSource Inputs { get; }

    /// <summary>Rethrows a resource failure even if an engine caught the original read exception.</summary>
    public void ThrowIfFailed()
    {
        if (Volatile.Read(ref _failure) is { } failure) { throw failure; }
    }

    private CliException Reject(CliException failure)
    {
        Interlocked.CompareExchange(ref _failure, failure, null);
        return _failure!;
    }

    /// <summary>Returns the active limit for one stable resource name.</summary>
    public long Limit(string kind) =>
        _limits.TryGetValue(kind, out long value)
            ? value
            : throw new KeyNotFoundException(
                $"Resource budget '{kind}' is not registered.");

    /// <summary>Returns the currently unconsumed amount for one resource.</summary>
    public long Remaining(string kind)
    {
        long limit = Limit(kind);
        long consumed = _consumed.GetValueOrDefault(kind);
        return Math.Max(0, limit - consumed);
    }

    /// <summary>Atomically consumes a positive amount or raises the stable budget error.</summary>
    public void Consume(
        string kind,
        long amount,
        string unit,
        string phase)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentException.ThrowIfNullOrWhiteSpace(unit);
        ArgumentException.ThrowIfNullOrWhiteSpace(phase);
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount));
        }

        ThrowIfFailed();
        Deadline.ThrowIfExpired(phase);
        long limit = Limit(kind);
        while (true)
        {
            bool exists = _consumed.TryGetValue(kind, out long current);
            if (amount > limit - current)
            {
                long observed = amount > long.MaxValue - current
                    ? long.MaxValue
                    : current + amount;
                throw Reject(CliErrors.InputBudgetExceeded(
                    kind, observed, limit, unit, phase));
            }

            long updated = current + amount;
            if (exists
                ? _consumed.TryUpdate(kind, updated, current)
                : _consumed.TryAdd(kind, updated))
            {
                return;
            }
        }
    }

    /// <summary>Checks one observed domain count without changing cumulative counters.</summary>
    public void EnsureWithin(
        string kind,
        long observed,
        string unit,
        string phase)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentException.ThrowIfNullOrWhiteSpace(unit);
        ArgumentException.ThrowIfNullOrWhiteSpace(phase);
        if (observed < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(observed));
        }
        ThrowIfFailed();
        Deadline.ThrowIfExpired(phase);
        long limit = Limit(kind);
        if (observed > limit)
        {
            throw Reject(CliErrors.InputBudgetExceeded(
                kind, observed, limit, unit, phase));
        }
    }

    /// <summary>Admits a file before runtime or license initialization.</summary>
    public void AdmitFile(string path)
    {
        string full = Path.GetFullPath(path);
        FileInfo info = InputSizeGuard.ReadInfo(full);
        long limit = Limit(ResourceBudgetKinds.InputBytes);
        if (info.Length > limit)
        {
            // File admission has a specific error; stream and domain budgets
            // use the shared resource-budget error.
            throw Reject(CliErrors.FileTooLarge(info.Length, limit));
        }
        _admitted[full] = new AdmittedFile(
            info.Length,
            info.LastWriteTimeUtc,
            info.CreationTimeUtc);
    }

    /// <summary>Rejects a file that changed after its pre-runtime admission.</summary>
    internal void VerifyAdmission(string path)
    {
        string full = Path.GetFullPath(path);
        if (!_admitted.TryGetValue(full, out AdmittedFile? expected))
        {
            AdmitFile(full);
            return;
        }

        FileInfo info = InputSizeGuard.ReadInfo(full);
        if (info.Length != expected.Length
            || info.LastWriteTimeUtc != expected.LastWriteTimeUtc
            || info.CreationTimeUtc != expected.CreationTimeUtc)
        {
            throw CliErrors.InputChanged(
                full,
                expected.Length,
                info.Length);
        }
    }

    /// <summary>
    /// Refreshes an existing admission after this invocation has successfully
    /// published the file itself. Outputs that were never admitted remain
    /// outputs; they are not silently promoted to trusted inputs.
    /// </summary>
    internal void RefreshAdmissionAfterPublication(string path)
    {
        string full = Path.GetFullPath(path);
        if (!_admitted.ContainsKey(full))
        {
            return;
        }

        FileInfo info = InputSizeGuard.ReadInfo(full);
        _admitted[full] = new AdmittedFile(
            info.Length,
            info.LastWriteTimeUtc,
            info.CreationTimeUtc);
    }

    private static IReadOnlyDictionary<string, long> NormalizeLimits(
        IReadOnlyDictionary<string, long>? overrides)
    {
        var result = ResourceBudgetDefaults.Global.ToDictionary(
            static item => item.Kind,
            static item => item.Default,
            StringComparer.Ordinal);
        if (overrides is null)
        {
            return result;
        }

        foreach ((string kind, long value) in overrides)
        {
            ResourceBudgetCapabilities? descriptor =
                ResourceBudgetDefaults.Global.SingleOrDefault(item =>
                    string.Equals(item.Kind, kind, StringComparison.Ordinal));
            if (value <= 0
                || (descriptor is not null && value > descriptor.Maximum))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(overrides),
                    descriptor is null
                        ? $"Budget '{kind}' must be positive."
                        : $"Budget '{kind}' must be between 1 and {descriptor.Maximum}.");
            }
            result[kind] = value;
        }
        return result;
    }

    private sealed record AdmittedFile(
        long Length,
        DateTime LastWriteTimeUtc,
        DateTime CreationTimeUtc);
}
