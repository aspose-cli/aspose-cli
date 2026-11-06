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

    /// <summary>Every part.</summary>
    public static IReadOnlyList<TextPart> All { get; } = [HeaderFooter, Comment, ChartTitle, ChartData, AxisTitle, TextBox];

    /// <inheritdoc />
    public override string ToString() => Token;
}

/// <summary>
/// Builds the <c>TEXT_NOT_REPLACED</c> of an operation that found its text in a part of the
/// document it does not change and left it as it was: <c>details</c> names the operation by
/// <c>id</c> and <c>op</c> and the kind of part in <c>part</c>, <c>location</c> the place.
/// </summary>
public static class UnreplacedText
{
    /// <summary>The warning that <paramref name="operation"/> left the text it found in <paramref name="part"/>.</summary>
    /// <param name="part">The kind of part that holds the text.</param>
    /// <param name="operation">The operation, with the id the batch gave it.</param>
    /// <param name="op">The op name of the operation, such as replace_text.</param>
    /// <param name="message">Where the operation found the text and that it left it, never repeating the text.</param>
    /// <param name="hint">How to change that text, such as the operation that does.</param>
    /// <param name="location">The address of the part, or of the unit that holds several.</param>
    /// <param name="docs">The topic of <c>aspose-cli docs</c> with background, or null.</param>
    public static Warning Warning(TextPart part, BoundedOperation operation, string op, string message, string hint, string location, string? docs = null)
    {
        ArgumentNullException.ThrowIfNull(part);
        string id = operation?.Id ?? throw new ArgumentException("The operation has no id; warn about operations of a prepared batch.", nameof(operation));
        ArgumentException.ThrowIfNullOrWhiteSpace(op);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ArgumentException.ThrowIfNullOrWhiteSpace(hint);
        ArgumentException.ThrowIfNullOrWhiteSpace(location);
        return new Warning(WarningCodes.TextNotReplaced, message)
        {
            Hint = hint,
            Location = location,
            Docs = docs,
            Details = new JsonObject { ["id"] = id, ["op"] = op, ["part"] = part.Token },
        };
    }
}
