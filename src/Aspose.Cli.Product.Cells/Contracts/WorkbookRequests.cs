using Aspose.Cli.Product.Cells.Contracts.Addressing;
using Aspose.Cli.Sdk.Text;

namespace Aspose.Cli.Product.Cells.Contracts;

/// <summary>Options of <c>cells inspect</c>.</summary>
public sealed record InfoRequest
{
    /// <summary>Include a small sample of display values per sheet.</summary>
    public bool IncludePreview { get; init; }

    /// <summary>Number of preview rows per sheet.</summary>
    public int PreviewRows { get; init; } = 5;

    /// <summary>Extra detail sections to include (see <c>InfoDetails</c>).</summary>
    public IReadOnlyList<string>? Details { get; init; }

    /// <summary>Password for encrypted files.</summary>
    public string? Password { get; init; }
}

/// <summary>Options of <c>cells query range</c>.</summary>
public sealed record ReadRequest
{
    /// <summary>Sheet to project; the active sheet when null.</summary>
    public string? SheetName { get; init; }

    /// <summary>
    /// Window to project. Null means the used range, subject to the cell
    /// budget: over-budget default reads return a summary plus a follow-up
    /// command instead of data, while an explicit over-budget range is an
    /// error — the caller asked for something specific and gets an honest no.
    /// </summary>
    public RangeRef? Range { get; init; }

    /// <summary>
    /// The region of a planned scan that <see cref="Range"/> is one page of, carried by a
    /// generated follow-up command; null for a default read or a complete explicit range.
    /// </summary>
    public RangeRef? Scan { get; init; }

    /// <summary>Projection scope.</summary>
    public ReadScope Scope { get; init; } = ReadScope.Values;

    /// <summary>Maximum number of cells the projection may return.</summary>
    public int MaxCells { get; init; } = 10_000;

    /// <summary>Password for encrypted files.</summary>
    public string? Password { get; init; }
}

/// <summary>Options of <c>cells convert</c>.</summary>
public sealed record ConvertRequest
{
    /// <summary>Canonical target format id (already resolved).</summary>
    public required string TargetFormatId { get; init; }

    /// <summary>Absolute output path.</summary>
    public required string OutputPath { get; init; }

    /// <summary>Whether an existing output file may be replaced.</summary>
    public bool Overwrite { get; init; }

    /// <summary>Restrict the conversion to one sheet, where the format supports it.</summary>
    public string? SheetName { get; init; }

    /// <summary>Password for encrypted files.</summary>
    public string? Password { get; init; }

    /// <summary>New output password; null preserves source encryption where the target format supports it.</summary>
    public string? EncryptPassword { get; init; }

    /// <summary>How a delimited text input is decoded and its numbers and dates parsed.</summary>
    public TextImportOptions? TextImport { get; init; }

    /// <summary>Whether a CSV or TSV output starts with a UTF-8 byte order mark.</summary>
    public bool ByteOrderMark { get; init; }
}

/// <summary>
/// How <c>cells convert</c> reads a delimited text input. Other commands read text inputs as
/// UTF-8 with invariant number and date formats and refuse inputs that need these choices.
/// </summary>
public sealed record TextImportOptions
{
    /// <summary>Text encoding name, such as <c>gb18030</c>; null reads a byte order mark or UTF-8.</summary>
    public string? Encoding { get; init; }

    /// <summary>Culture name for numbers and dates, such as <c>de-DE</c>; null uses invariant formats.</summary>
    public string? Culture { get; init; }
}

/// <summary>Options of <c>cells render</c>.</summary>
public sealed record RenderRequest
{
    /// <summary>Canonical target format id (already resolved).</summary>
    public required string TargetFormatId { get; init; }

    /// <summary>Absolute output path.</summary>
    public required string OutputPath { get; init; }

    /// <summary>Whether an existing output file may be replaced.</summary>
    public bool Overwrite { get; init; }

    /// <summary>Sheet to render; the active sheet when null.</summary>
    public string? SheetName { get; init; }

    /// <summary>Restrict rendering to this range; the whole sheet when null.</summary>
    public RangeRef? Range { get; init; }

    /// <summary>
    /// Render every visible sheet, each to its own file derived from
    /// <see cref="OutputPath"/> (<c>&lt;base&gt;.&lt;Sheet&gt;&lt;ext&gt;</c>).
    /// Mutually exclusive with <see cref="SheetName"/> and <see cref="Range"/>;
    /// the command layer enforces that before the request is built.
    /// </summary>
    public bool AllSheets { get; init; }

    /// <summary>Raster resolution in dots per inch (ignored for vector formats).</summary>
    public int Dpi { get; init; } = 192;

    /// <summary>Password for encrypted files.</summary>
    public string? Password { get; init; }
}

/// <summary>Options of <c>cells edit</c>.</summary>
public sealed record EditRequest
{
    /// <summary>Transient environment secret values; never part of ops JSON or result envelopes.</summary>
    public IReadOnlyDictionary<string, string>? OpSecrets { get; init; }

    /// <summary>Absolute output path.</summary>
    public required string OutputPath { get; init; }

    /// <summary>Whether an existing output file may be replaced.</summary>
    public bool Overwrite { get; init; }

    /// <summary>Stable backup path for an in-place mutation; null disables backup.</summary>
    public string? BackupPath { get; init; }

    /// <summary>Shared stale-input, dry-run, and best-effort semantics.</summary>
    public EditCommandOptions Options { get; init; } = new();

    /// <summary>
    /// Verify the staged workbook and publish its render evidence with the edit. The edit
    /// command admits it only for a published, recalculated edit.
    /// </summary>
    public bool Verify { get; init; }

    /// <summary>Recalculate formulas after applying the ops.</summary>
    public bool Recalculate { get; init; } = true;

    /// <summary>Password for opening an encrypted input file.</summary>
    public string? Password { get; init; }

    /// <summary>New output password; null preserves source encryption where the target format supports it.</summary>
    public string? EncryptPassword { get; init; }
}

/// <summary>Options of <c>cells create</c>.</summary>
public sealed record NewWorkbookRequest
{
    /// <summary>Absolute output path.</summary>
    public required string OutputPath { get; init; }

    /// <summary>Whether an existing output file may be replaced.</summary>
    public bool Overwrite { get; init; }

    /// <summary>Sheet names, in order; already validated non-empty and unique.</summary>
    public required IReadOnlyList<string> SheetNames { get; init; }

    /// <summary>Resolved password to protect the output file; null leaves it unencrypted.</summary>
    public string? EncryptPassword { get; init; }
}

/// <summary>Options of <c>cells query search</c>.</summary>
public sealed record SearchRequest
{
    /// <summary>The validated pattern, hit budget and paging of the search.</summary>
    public required SearchQuery Query { get; init; }

    /// <summary>Where to look.</summary>
    public SearchIn In { get; init; } = SearchIn.Values;

    /// <summary>Restrict to one sheet; all sheets when null.</summary>
    public string? SheetName { get; init; }

    /// <summary>Password for encrypted files.</summary>
    public string? Password { get; init; }
}

/// <summary>Where <c>cells query search</c> looks for matches.</summary>
public enum SearchIn
{
    /// <summary>Display values only.</summary>
    Values,

    /// <summary>Formulas only.</summary>
    Formulas,

    /// <summary>Values and formulas.</summary>
    Both,
}

/// <summary>Options of <c>cells compare</c>.</summary>
public sealed record DiffRequest
{
    /// <summary>What to compare.</summary>
    public DiffScope Scope { get; init; } = DiffScope.Formulas;

    /// <summary>Maximum number of differing cells to list before truncating.</summary>
    public int MaxDiffs { get; init; } = 1000;

    /// <summary>Password for the left (baseline) file.</summary>
    public string? LeftPassword { get; init; }

    /// <summary>Password for the right (candidate) file.</summary>
    public string? RightPassword { get; init; }
}

/// <summary>What <c>cells compare</c> compares.</summary>
public enum DiffScope
{
    /// <summary>Cell values only.</summary>
    Values,

    /// <summary>Cell values and formulas.</summary>
    Formulas,
}
