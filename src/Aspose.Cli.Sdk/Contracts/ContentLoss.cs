using System.Text.Json.Nodes;

namespace Aspose.Cli.Sdk.Contracts;

/// <summary>
/// A kind of content an output can leave out: the closed set of kebab-case plural nouns a
/// <c>LOSSY_CONVERSION</c> names in <c>details.content</c>, so a caller can branch on what was
/// lost. Only the members here exist; a product picks from them and cannot make another.
/// </summary>
public sealed class ContentKind
{
    private ContentKind(string token, string description)
    {
        Token = token;
        Description = description;
    }

    /// <summary>The token <c>details.content</c> holds for this kind.</summary>
    public string Token { get; }

    /// <summary>What the output leaves out, as the error-payload schema publishes it for <see cref="Token"/>.</summary>
    public string Description { get; }

    /// <summary>Embedded files.</summary>
    public static ContentKind Attachments { get; } = new("attachments", "Files embedded in the document, such as the attachments of a PDF.");

    /// <summary>Bookmarks.</summary>
    public static ContentKind Bookmarks { get; } = new("bookmarks", "The bookmarks of the document's outline.");

    /// <summary>Charts.</summary>
    public static ContentKind Charts { get; } = new("charts", "Charts as charts with their data: the output holds only a picture of them, or none.");

    /// <summary>Comments.</summary>
    public static ContentKind Comments { get; } = new("comments", "Comments as comments: the output writes their text into the body, draws them in balloons beside its pages, or leaves them out.");

    /// <summary>Editing restrictions.</summary>
    public static ContentKind EditingRestrictions { get; } = new("editing-restrictions", "Editing restrictions, such as a read-only protection of the document.");

    /// <summary>Form fields.</summary>
    public static ContentKind FormFields { get; } = new("form-fields", "Form fields: inputs the output has no field for.");

    /// <summary>Form field values.</summary>
    public static ContentKind FormValues { get; } = new("form-values", "The values of form fields.");

    /// <summary>Headers and footers.</summary>
    public static ContentKind HeadersFooters { get; } = new("headers-footers", "Headers and footers.");

    /// <summary>Pictures.</summary>
    public static ContentKind Pictures { get; } = new("pictures", "Pictures.");

    /// <summary>Portfolio presentations.</summary>
    public static ContentKind Portfolios { get; } = new("portfolios", "The presentation of a PDF portfolio, such as its file list layout; its files stay plain attachments.");

    /// <summary>Revisions.</summary>
    public static ContentKind Revisions { get; } = new("revisions", "Tracked changes as revisions a reader can accept or reject.");

    /// <summary>Sections.</summary>
    public static ContentKind Sections { get; } = new("sections", "Sections, such as the sections of a deck.");

    /// <summary>Other drawings.</summary>
    public static ContentKind Shapes { get; } = new("shapes", "Drawings other than charts and pictures, such as shapes, text boxes and groups.");

    /// <summary>Speaker notes.</summary>
    public static ContentKind SpeakerNotes { get; } = new("speaker-notes", "Speaker notes.");

    /// <summary>Table styles.</summary>
    public static ContentKind TableStyles { get; } = new("table-styles", "Table styles: the fills, borders and text formatting a table's style gives it.");

    /// <summary>Slide transitions.</summary>
    public static ContentKind Transitions { get; } = new("transitions", "Slide transitions.");

    /// <summary>Every kind, in the order of their tokens.</summary>
    public static IReadOnlyList<ContentKind> All { get; } =
    [
        Attachments,
        Bookmarks,
        Charts,
        Comments,
        EditingRestrictions,
        FormFields,
        FormValues,
        HeadersFooters,
        Pictures,
        Portfolios,
        Revisions,
        Sections,
        Shapes,
        SpeakerNotes,
        TableStyles,
        Transitions,
    ];

    /// <inheritdoc />
    public override string ToString() => Token;
}

/// <summary>
/// Builds the <c>LOSSY_CONVERSION</c> that names what an output leaves out: the only source of
/// <c>details.content</c>, which lists the kinds in the order given. A warning that only says a
/// format may not keep every feature names no kind and is built without it.
/// </summary>
public static class ContentLoss
{
    /// <summary>The warning that the output leaves out content of the kinds in <paramref name="content"/>.</summary>
    /// <param name="content">The kinds the output leaves out, at least one, each once, in the order the message names them.</param>
    /// <param name="message">What the output leaves out, where and how much.</param>
    /// <param name="hint">How to read or keep what was left out.</param>
    /// <param name="location">The address of the one item the loss concerns, or null.</param>
    /// <param name="affectsCompleteness">Whether the loss leaves the output incomplete (<see cref="Warning.AffectsCompleteness"/>).</param>
    public static Warning Warning(IReadOnlyCollection<ContentKind> content, string message, string hint, string? location = null, bool affectsCompleteness = false)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ArgumentException.ThrowIfNullOrWhiteSpace(hint);
        if (content.Count == 0)
        {
            throw new ArgumentException("A content loss names at least one kind of content.", nameof(content));
        }

        if (content.Any(static kind => kind is null) || content.Distinct().Count() != content.Count)
        {
            throw new ArgumentException("A content loss names each kind of content once.", nameof(content));
        }

        return new Warning(WarningCodes.LossyConversion, message)
        {
            Hint = hint,
            Location = location,
            AffectsCompleteness = affectsCompleteness,
            Details = new JsonObject { ["content"] = new JsonArray([.. content.Select(static kind => (JsonNode?)kind.Token)]) },
        };
    }
}
