using System.Drawing;
using System.Globalization;
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

using static Aspose.Cli.Product.Words.Engine.Editing.WordsMutationSupport;

namespace Aspose.Cli.Product.Words.Engine.Editing;

/// <summary>Owns text, paragraph and style mutations.</summary>
internal static class WordsFormattingOpHandlers
{
    internal static long FormatText(IReadOnlyList<Node> nodes, FormatTextOp op)
    {
        long count = 0;
        foreach (Run run in nodes.SelectMany(Descendants<Run>))
        {
            if (op.Bold is not null)
            {
                run.Font.Bold = op.Bold.Value;
            }

            if (op.Italic is not null)
            {
                run.Font.Italic = op.Italic.Value;
            }

            if (op.Underline is not null)
            {
                run.Font.Underline = op.Underline.Value ? Underline.Single : Underline.None;
            }

            if (op.Size is not null)
            {
                run.Font.Size = op.Size.Value;
            }

            if (op.Font is not null)
            {
                run.Font.Name = op.Font;
            }

            if (op.Color is not null)
            {
                run.Font.Color = ParseColor(op.Color);
            }

            if (op.Highlight is not null)
            {
                run.Font.HighlightColor = ParseColor(op.Highlight);
            }

            count++;
        }

        return count;
    }

    internal static long SetStyle(Document document, IReadOnlyList<Node> nodes, SetStyleOp op)
    {
        Style? style = document.Styles[op.Style];
        if (style is null)
        {
            throw StyleNotFound(op.Style);
        }

        long count = 0;
        foreach (Paragraph paragraph in nodes.SelectMany(Paragraphs))
        {
            paragraph.ParagraphFormat.Style = style;
            count++;
        }

        return count;
    }

    internal static long DefineStyle(Document document, DefineStyleOp op)
    {
        Style style = document.Styles[op.Name] ?? document.Styles.Add(StyleType.Paragraph, op.Name);
        if (op.BasedOn is not null)
        {
            style.BaseStyleName = op.BasedOn;
        }

        if (op.Font is not null)
        {
            style.Font.Name = op.Font;
        }

        if (op.Size is not null)
        {
            style.Font.Size = op.Size.Value;
        }

        if (op.Bold is not null)
        {
            style.Font.Bold = op.Bold.Value;
        }

        if (op.Color is not null)
        {
            style.Font.Color = ParseColor(op.Color);
        }

        if (op.SpaceBefore is not null)
        {
            style.ParagraphFormat.SpaceBefore = op.SpaceBefore.Value;
        }

        if (op.SpaceAfter is not null)
        {
            style.ParagraphFormat.SpaceAfter = op.SpaceAfter.Value;
        }

        return 1;
    }

    internal static long SetDefaultFont(Document document, SetDefaultFontOp op)
    {
        foreach (Style style in document.Styles)
        {
            if (style.Type is StyleType.Paragraph or StyleType.Character)
            {
                style.Font.Name = op.Font;
                if (op.Size is not null)
                {
                    style.Font.Size = op.Size.Value;
                }
            }
        }

        return 1;
    }
}

