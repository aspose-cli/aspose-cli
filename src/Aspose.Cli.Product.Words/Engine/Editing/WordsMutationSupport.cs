using System.Data;
using System.Drawing;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Aspose.Cli.Product.Words.Contracts;
using Aspose.Cli.Product.Words.Engine.Mapping;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Text;
using Aspose.Words;
using Aspose.Words.Fields;
using Aspose.Words.Lists;
using Aspose.Words.Replacing;
using Aspose.Words.Tables;
using SkiaSharp;

namespace Aspose.Cli.Product.Words.Engine.Editing;

/// <summary>Provides stateless SDK adaptation helpers shared by Words operation families.</summary>
internal static class WordsMutationSupport
{
    internal static void ApplyPageSetup(Section section, PageSetupInput setup)
    {
        if (setup.Size is not null)
        {
            section.PageSetup.PaperSize = setup.Size.ToLowerInvariant() switch
            {
                "a3" => PaperSize.A3,
                "a4" => PaperSize.A4,
                "a5" => PaperSize.A5,
                "letter" => PaperSize.Letter,
                "legal" => PaperSize.Legal,
                _ => throw Invalid($"unknown paper size '{setup.Size}'"),
            };
        }

        if (setup.Orientation is not null)
        {
            section.PageSetup.Orientation = setup.Orientation == "landscape" ? Orientation.Landscape : Orientation.Portrait;
        }

        if (setup.Margins is not null)
        {
            section.PageSetup.TopMargin = setup.Margins.Top ?? section.PageSetup.TopMargin;
            section.PageSetup.RightMargin = setup.Margins.Right ?? section.PageSetup.RightMargin;
            section.PageSetup.BottomMargin = setup.Margins.Bottom ?? section.PageSetup.BottomMargin;
            section.PageSetup.LeftMargin = setup.Margins.Left ?? section.PageSetup.LeftMargin;
        }

        if (setup.ColumnCount is int columns)
        {
            section.PageSetup.TextColumns.SetCount(columns);
        }
    }

    internal static void ApplyParagraphStyle(Document document, Paragraph paragraph, string styleName)
    {
        paragraph.ParagraphFormat.Style = GetStyle(document, styleName);
    }

    internal static Paragraph InsertBuilderParagraph(Document document, Node anchor, string position)
    {
        var paragraph = new Paragraph(document);
        Node cursor = anchor;
        InsertRelative(anchor, ref cursor, paragraph, position);
        return paragraph;
    }

    internal static void InsertRelative(Node anchor, ref Node cursor, Node inserted, string position)
    {
        if (position == "before")
        {
            anchor.ParentNode!.InsertBefore(inserted, anchor);
        }
        else
        {
            cursor.ParentNode!.InsertAfter(inserted, cursor);
            cursor = inserted;
        }
    }

    internal static IEnumerable<Paragraph> Paragraphs(Node node) =>
        node is Paragraph paragraph ? [paragraph] : Descendants<Paragraph>(node);

    internal static IEnumerable<T> Descendants<T>(Node node)
        where T : Node =>
        node is CompositeNode composite
            ? composite.GetChildNodes(NodeType.Any, true).Cast<Node>().OfType<T>()
            : [];

    internal static HeaderFooterType HeaderFooterTypeOf(string kind, bool isHeader) => (kind, isHeader) switch
    {
        ("primary", true) => HeaderFooterType.HeaderPrimary,
        ("first", true) => HeaderFooterType.HeaderFirst,
        ("even", true) => HeaderFooterType.HeaderEven,
        ("primary", false) => HeaderFooterType.FooterPrimary,
        ("first", false) => HeaderFooterType.FooterFirst,
        ("even", false) => HeaderFooterType.FooterEven,
        _ => throw Invalid($"unknown header/footer kind '{kind}'"),
    };

    internal static ParagraphAlignment AlignmentOf(string alignment) => alignment switch
    {
        "left" => ParagraphAlignment.Left,
        "right" => ParagraphAlignment.Right,
        "center" => ParagraphAlignment.Center,
        _ => throw Invalid($"unknown alignment '{alignment}'"),
    };

    internal static Color ParseColor(string value)
    {
        try
        {
            return ColorTranslator.FromHtml(value);
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException)
        {
            throw Invalid($"invalid color '{value}'");
        }
    }

    /// <summary>A style of the document by name; a missing name is STYLE_NOT_FOUND listing the document's styles.</summary>
    internal static Style GetStyle(Document document, string name) =>
        document.Styles[name] ?? throw CliErrors.NotFound(
            ErrorCodes.StyleNotFound, "style", name, document.Styles.Select(static style => style.Name).ToArray());

    /// <summary>The hint for merge data whose shape is wrong.</summary>
    internal const string MergeDataShape =
        "Use a JSON array of flat objects, or a CSV file with a header row.";

    /// <summary>The merge data of mail_merge or repeat_table_row cannot be read as rows.</summary>
    internal static CliException MergeDataInvalid(string reason, string hint) => new(
        WordsDiagnostics.MergeDataInvalid,
        $"Merge data is invalid: {reason}.",
        hint: hint);

    /// <summary>The template's merge regions cannot take the flat merge rows.</summary>
    internal static CliException MergeRegionInvalid(string reason, string hint) => new(
        WordsDiagnostics.MergeDataInvalid,
        $"The mail merge template cannot be merged with regions: {reason}.",
        hint: hint);

    internal static OperationInvalidException Invalid(string reason) => new(reason);
}

