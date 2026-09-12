using System.Drawing;
using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Aspose.Cli.Product.Slides.Contracts;
using Aspose.Cli.Product.Slides.Engine.Mapping;
using Aspose.Cli.Sdk.Addressing;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Text;
using Aspose.Slides;
using Aspose.Slides.Charts;
using Aspose.Slides.SlideShow;
using static Aspose.Cli.Product.Slides.Engine.SlidesEngineSupport;
using static Aspose.Cli.Product.Slides.Engine.SlidesMutationSupport;

namespace Aspose.Cli.Product.Slides.Engine;

/// <summary>Applies title, body, text, and notes operations.</summary>
internal static class SlidesContentHandlers
{
    internal static long SetText(ISlide slide, IShape shape, string text, ISet<uint> touched)
    {
        if (shape is not IAutoShape { TextFrame: not null } auto)
        {
            throw new InvalidOperationException($"Shape {shape.OfficeInteropShapeId} has no editable text frame.");
        }

        auto.TextFrame.Text = text;
        touched.Add(slide.SlideId);
        return 1;
    }

    internal static long ReplaceText(
        Presentation presentation,
        SlidesReplaceTextOp op,
        ISet<uint> touched)
    {
        Regex? regex = op.Regex ? SafeRegex.Create(op.Find, op.MatchCase) : null;
        long count = 0;
        foreach (ISlide slide in presentation.Slides)
        {
            bool changed = false;
            if (op.Scope is "shapes" or "all")
            {
                foreach (IAutoShape shape in slide.Shapes.OfType<IAutoShape>())
                {
                    if (shape.TextFrame is null)
                    {
                        continue;
                    }
                    (string value, int replacements) = Replace(shape.TextFrame.Text, op, regex);
                    if (replacements > 0)
                    {
                        shape.TextFrame.Text = value;
                        count += replacements;
                        changed = true;
                    }
                }
            }

            if (op.Scope is "notes" or "all")
            {
                ITextFrame? notes = slide.NotesSlideManager.NotesSlide?.NotesTextFrame;
                if (notes is not null)
                {
                    (string value, int replacements) = Replace(notes.Text, op, regex);
                    if (replacements > 0)
                    {
                        notes.Text = value;
                        count += replacements;
                        changed = true;
                    }
                }
            }

            if (changed)
            {
                touched.Add(slide.SlideId);
            }
        }

        return count;
    }

    internal static (string Value, int Count) Replace(
        string input,
        SlidesReplaceTextOp op,
        Regex? regex)
    {
        if (regex is not null)
        {
            int regexCount = regex.Matches(input).Count;
            return (regex.Replace(input, op.Replace), regexCount);
        }

        StringComparison comparison = op.MatchCase
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;
        int count = 0;
        int offset = 0;
        var result = new System.Text.StringBuilder(input.Length);
        while (true)
        {
            int found = input.IndexOf(op.Find, offset, comparison);
            if (found < 0)
            {
                result.Append(input, offset, input.Length - offset);
                break;
            }

            result.Append(input, offset, found - offset);
            result.Append(op.Replace);
            offset = found + op.Find.Length;
            count++;
        }

        return (result.ToString(), count);
    }

    internal static long SetNotes(ISlide slide, string text, ISet<uint> touched)
    {
        INotesSlide notes = slide.NotesSlideManager.NotesSlide
            ?? slide.NotesSlideManager.AddNotesSlide();
        notes.NotesTextFrame!.Text = text;
        touched.Add(slide.SlideId);
        return 1;
    }

}

