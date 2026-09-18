using System.CommandLine;
using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Sdk.Extensibility.Commanding;

/// <summary>
/// Owns the standard item and byte budgets used by guarded extraction
/// commands.
/// </summary>
public sealed class ExtractionBudgetOptions
{
    private readonly int _maximumItems;
    private readonly long _maximumBytes;
    private readonly string _itemHint;
    private readonly string _byteHint;

    /// <summary>Initializes a pair of bounded extraction options.</summary>
    public ExtractionBudgetOptions(
        string itemDescription,
        string byteDescription,
        string itemHint = "Use a positive bounded file count.",
        int defaultItems = PublicationLimits.MaximumEntries,
        long defaultBytes = 512L * 1024 * 1024,
        int maximumItems = PublicationLimits.MaximumEntries,
        long maximumBytes = 16L * 1024 * 1024 * 1024)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(itemDescription);
        ArgumentException.ThrowIfNullOrWhiteSpace(byteDescription);
        ArgumentException.ThrowIfNullOrWhiteSpace(itemHint);
        if (defaultItems is < 1 || defaultItems > maximumItems)
        {
            throw new ArgumentOutOfRangeException(nameof(defaultItems));
        }
        if (defaultBytes is < 1 || defaultBytes > maximumBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(defaultBytes));
        }

        _maximumItems = maximumItems;
        _maximumBytes = maximumBytes;
        _itemHint = itemHint;
        _byteHint = $"Use a positive byte budget no greater than {FormatBytes(maximumBytes)}.";
        MaxItems = new Option<int>("--max-items")
        {
            Description = itemDescription,
            DefaultValueFactory = _ => defaultItems,
        };
        MaxBytes = new Option<long>("--max-bytes")
        {
            Description = byteDescription,
            DefaultValueFactory = _ => defaultBytes,
        };
    }

    /// <summary>Gets the maximum-item option.</summary>
    public Option<int> MaxItems { get; }

    /// <summary>Gets the maximum-byte option.</summary>
    public Option<long> MaxBytes { get; }

    /// <summary>Adds both options to a command in their stable order.</summary>
    public void AddTo(Command command)
    {
        ArgumentNullException.ThrowIfNull(command);
        command.Options.Add(MaxItems);
        command.Options.Add(MaxBytes);
    }

    /// <summary>Reads and validates both budgets from a parse result.</summary>
    public ExtractionBudget Read(ParseResult parse)
    {
        ArgumentNullException.ThrowIfNull(parse);
        int maxItems = parse.GetValue(MaxItems);
        long maxBytes = parse.GetValue(MaxBytes);
        OptionGuards.EnsureInRange(
            "--max-items",
            maxItems,
            1,
            _maximumItems,
            _itemHint);
        OptionGuards.EnsureInRange(
            "--max-bytes",
            maxBytes,
            1,
            _maximumBytes,
            _byteHint);
        return new ExtractionBudget(maxItems, maxBytes);
    }

    private static string FormatBytes(long value) =>
        value % (1024L * 1024 * 1024) == 0
            ? $"{value / (1024L * 1024 * 1024)} GiB"
            : $"{value} bytes";
}

/// <summary>Validated extraction limits read from the command line.</summary>
public readonly record struct ExtractionBudget(
    int MaxItems,
    long MaxBytes);
