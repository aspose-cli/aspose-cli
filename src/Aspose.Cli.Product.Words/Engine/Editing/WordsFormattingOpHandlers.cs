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

// Character formatting and styles.
internal sealed partial class WordsOpHandlers
{
    public long Apply(FormatTextOp operation)
    {
        long count = 0;
        foreach (Run run in Nodes.SelectMany(Descendants<Run>))
        {
            if (operation.Bold is not null)
            {
                run.Font.Bold = operation.Bold.Value;
            }

            if (operation.Italic is not null)
            {
                run.Font.Italic = operation.Italic.Value;
            }

            if (operation.Underline is not null)
            {
                run.Font.Underline = operation.Underline.Value ? Underline.Single : Underline.None;
            }

            if (operation.Size is not null)
            {
                run.Font.Size = operation.Size.Value;
            }

            if (operation.Font is not null)
            {
                run.Font.Name = operation.Font;
            }

            if (operation.Color is not null)
            {
                run.Font.Color = ParseColor(operation.Color);
            }

            if (operation.Highlight is not null)
            {
                run.Font.HighlightColor = ParseColor(operation.Highlight);
            }

            count++;
        }

        return count;
    }

    public long Apply(SetStyleOp operation)
    {
        Style? style = _document.Styles[operation.Style];
        if (style is null)
        {
            throw StyleNotFound(operation.Style);
        }

        long count = 0;
        foreach (Paragraph paragraph in Nodes.SelectMany(Paragraphs))
        {
            paragraph.ParagraphFormat.Style = style;
            count++;
        }

        return count;
    }

    public long Apply(DefineStyleOp operation)
    {
        Style style = _document.Styles[operation.Name] ?? _document.Styles.Add(StyleType.Paragraph, operation.Name);
        if (operation.BasedOn is not null)
        {
            style.BaseStyleName = operation.BasedOn;
        }

        if (operation.Font is not null)
        {
            style.Font.Name = operation.Font;
        }

        if (operation.Size is not null)
        {
            style.Font.Size = operation.Size.Value;
        }

        if (operation.Bold is not null)
        {
            style.Font.Bold = operation.Bold.Value;
        }

        if (operation.Color is not null)
        {
            style.Font.Color = ParseColor(operation.Color);
        }

        if (operation.SpaceBefore is not null)
        {
            style.ParagraphFormat.SpaceBefore = operation.SpaceBefore.Value;
        }

        if (operation.SpaceAfter is not null)
        {
            style.ParagraphFormat.SpaceAfter = operation.SpaceAfter.Value;
        }

        return 1;
    }

    public long Apply(SetDefaultFontOp operation)
    {
        foreach (Style style in _document.Styles)
        {
            if (style.Type is StyleType.Paragraph or StyleType.Character)
            {
                style.Font.Name = operation.Font;
                if (operation.Size is not null)
                {
                    style.Font.Size = operation.Size.Value;
                }
            }
        }

        return 1;
    }
}

