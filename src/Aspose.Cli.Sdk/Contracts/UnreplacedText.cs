using System.Text.Json.Nodes;

namespace Aspose.Cli.Sdk.Contracts;

/// <summary>
/// A part of a document that holds text an operation found and does not change: the closed set
/// of kebab-case tokens a <c>TEXT_NOT_REPLACED</c> names in <c>details.part</c>. Only the members
/// here exist; a product picks from them and cannot make another.
/// </summary>
public sealed class TextPart
{
    private TextPart(string token, string description)
    {
        Token = token;
        Description = description;
    }

    /// <summary>The token <c>details.part</c> holds for this part.</summary>
    public string Token { get; }

    /// <summary>Where the text is, as the error-payload schema publishes it for <see cref="Token"/>.</summary>
    public string Description { get; }

    /// <summary>A header or footer.</summary>
    public static TextPart HeaderFooter { get; } = new("header-footer", "A header or footer.");

    /// <summary>A comment.</summary>
    public static TextPart Comment { get; } = new("comment", "A comment.");

    /// <summary>A chart's title.</summary>
    public static TextPart ChartTitle { get; } = new("chart-title", "The title of a chart.");

    /// <summary>A chart's data.</summary>
    public static TextPart ChartData { get; } = new("chart-data", "The data of a chart, such as its categories and series names.");

    /// <summary>An axis title.</summary>
    public static TextPart AxisTitle { get; } = new("axis-title", "The title of a chart's axis.");

    /// <summary>A text box.</summary>
    public static TextPart TextBox { get; } = new("text-box", "A text box, or another drawing that holds text.");

    /// <summary>A chart's data label.</summary>
    public static TextPart DataLabel { get; } = new("data-label", "A data label of a chart.");

    /// <summary>The alternative text of a picture or shape.</summary>
    public static TextPart AltText { get; } = new("alt-text", "The alternative text of a picture or shape, which screen readers and exported PDFs carry.");

    /// <summary>The text of a slide layout or master, which every slide on it shows.</summary>
    public static TextPart Layout { get; } = new("layout", "The text of a slide layout or master, which every slide on it shows.");

    /// <summary>The formula of a cell, such as a text literal in it.</summary>
    public static TextPart Formula { get; } = new("formula", "The formula of a cell, such as a text literal in it.");

    /// <summary>A cell that shows the text but holds a number, a date, a boolean or an error.</summary>
    public static TextPart TypedValue { get; } = new("typed-value", "A cell that shows the text but holds a number, a date, a boolean or an error, not text.");

    /// <summary>Every part.</summary>
    public static IReadOnlyList<TextPart> All { get; } = [HeaderFooter, Comment, ChartTitle, ChartData, AxisTitle, TextBox, DataLabel, AltText, Layout, Formula, TypedValue];

    /// <inheritdoc />
    public override string ToString() => Token;
}

/// <summary>
/// Text an operation found in a part of the document it does not change and left as it was, which
/// a <c>TEXT_NOT_REPLACED</c> names: <c>details</c> names the operation by <c>id</c> and
/// <c>op</c> and the kind of part in <c>part</c>, <c>location</c> the place. The warning is about
/// the output: a product hands the text to the SDK runner (<see cref="Operations.IOperationModel{TOp}.Unreplaced"/>)
/// with a check of whether what held it still holds a match, and the runner asks that check after
/// the last operation of the batch and warns only when it does. So a later operation of the batch
/// that changed or removed that text leaves no warning, and text a later operation wrote is not
/// named. Only the runner reads the warning, so no product warns about it another way.
/// </summary>
public sealed class UnreplacedText
{
    private readonly Func<bool> _remains;

    private UnreplacedText(Warning warning, Func<bool> remains)
    {
        Warning = warning;
        _remains = remains;
    }

    /// <summary>The warning, for the runner to report while the text remains.</summary>
    internal Warning Warning { get; }

    /// <summary>The text that <paramref name="operation"/> found in <paramref name="part"/> and left as it was.</summary>
    /// <param name="part">The kind of part that holds the text.</param>
    /// <param name="operation">The operation, with the id the batch gave it.</param>
    /// <param name="op">The op name of the operation, such as replace_text.</param>
    /// <param name="message">Where the operation found the text and that it left it, never repeating the text.</param>
    /// <param name="hint">How to change that text, such as the operation that does.</param>
    /// <param name="location">The address of the part, or of the unit that holds several.</param>
    /// <param name="remains">
    /// Whether the document as it is now still holds a match where the operation found one: read
    /// only from the objects that held a match then, and false for one no longer in the document.
    /// </param>
    /// <param name="docs">The topic of <c>aspose-cli docs</c> with background, or null.</param>
    public static UnreplacedText Found(TextPart part, BoundedOperation operation, string op, string message, string hint, string location, Func<bool> remains, string? docs = null)
    {
        ArgumentNullException.ThrowIfNull(part);
        string id = operation?.Id ?? throw new ArgumentException("The operation has no id; warn about operations of a prepared batch.", nameof(operation));
        ArgumentException.ThrowIfNullOrWhiteSpace(op);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ArgumentException.ThrowIfNullOrWhiteSpace(hint);
        ArgumentException.ThrowIfNullOrWhiteSpace(location);
        ArgumentNullException.ThrowIfNull(remains);
        return new UnreplacedText(new Warning(WarningCodes.TextNotReplaced, message)
        {
            Hint = hint,
            Location = location,
            Docs = docs,
            Details = new JsonObject { ["id"] = id, ["op"] = op, ["part"] = part.Token },
        }, remains);
    }

    /// <summary>Whether the text is still there; a check that fails cannot tell that it is gone, so the text counts as there.</summary>
    internal bool Remains()
    {
        try
        {
            return _remains();
        }
        catch (Exception exception) when (exception is not (OperationCanceledException or OutOfMemoryException))
        {
            return true;
        }
    }
}
