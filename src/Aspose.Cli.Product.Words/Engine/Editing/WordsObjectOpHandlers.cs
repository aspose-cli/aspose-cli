using System.Data;
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
using Aspose.Words.Drawing;
using Aspose.Words.Fields;
using Aspose.Words.Lists;
using Aspose.Words.Replacing;
using Aspose.Words.Tables;
using SkiaSharp;

using static Aspose.Cli.Product.Words.Engine.Editing.WordsMutationSupport;

namespace Aspose.Cli.Product.Words.Engine.Editing;

/// <summary>Owns embedded objects, fields, metadata, protection and merge mutations.</summary>
internal static class WordsObjectOpHandlers
{
    internal static long InsertImage(Document document, Node anchor, InsertImageOp op, InputResourceScope inputs)
    {
        if (!File.Exists(op.Path))
        {
            throw CliErrors.FileNotFound(op.Path);
        }

        var paragraph = new Paragraph(document);
        Node cursor = anchor;
        InsertRelative(anchor, ref cursor, paragraph, op.Position);
        var builder = new DocumentBuilder(document);
        builder.MoveTo(paragraph);
        Shape shape = builder.InsertImage(inputs.OpenFile(op.Path));
        if (op.Width is not null)
        {
            shape.Width = op.Width.Value;
        }

        if (op.Height is not null)
        {
            shape.Height = op.Height.Value;
        }
        if (!op.Inline)
        {
            shape.WrapType = WrapType.Square;
        }

        return 1;
    }

    internal static long InsertToc(Document document, Node anchor, InsertTocOp op)
    {
        Paragraph paragraph = InsertBuilderParagraph(document, anchor, op.Position);
        var builder = new DocumentBuilder(document);
        builder.MoveTo(paragraph);
        builder.InsertTableOfContents($"\\o \"1-{op.MaxLevel}\" \\h \\z \\u");
        document.UpdateFields();
        document.UpdatePageLayout();
        return 1;
    }

    internal static long InsertBookmark(Document document, Node anchor, InsertBookmarkOp op)
    {
        if (document.Range.Bookmarks[op.Name] is not null)
        {
            throw Invalid($"bookmark '{op.Name}' already exists");
        }

        if (anchor is not Paragraph paragraph)
        {
            throw Invalid("insert_bookmark must target a paragraph block");
        }

        paragraph.PrependChild(new BookmarkStart(document, op.Name));
        paragraph.AppendChild(new BookmarkEnd(document, op.Name));
        return 1;
    }

    internal static long InsertHyperlink(Document document, Node anchor, InsertHyperlinkOp op)
    {
        Paragraph paragraph = InsertBuilderParagraph(document, anchor, op.Position);
        var builder = new DocumentBuilder(document);
        builder.MoveTo(paragraph);
        builder.InsertHyperlink(op.Text, op.Url, isBookmark: false);
        return 1;
    }

    internal static long InsertField(Document document, Node anchor, InsertFieldOp op)
    {
        Paragraph paragraph = InsertBuilderParagraph(document, anchor, op.Position);
        var builder = new DocumentBuilder(document);
        builder.MoveTo(paragraph);
        builder.InsertField(op.Code);
        return 1;
    }

    internal static long SetProperties(Document document, SetPropertiesOp op)
    {
        if (op.Title is not null)
        {
            document.BuiltInDocumentProperties.Title = op.Title;
        }

        if (op.Author is not null)
        {
            document.BuiltInDocumentProperties.Author = op.Author;
        }

        if (op.Subject is not null)
        {
            document.BuiltInDocumentProperties.Subject = op.Subject;
        }

        if (op.Keywords is not null)
        {
            document.BuiltInDocumentProperties.Keywords = op.Keywords;
        }

        foreach ((string name, string? value) in op.Custom ?? new Dictionary<string, string?>())
        {
            document.CustomDocumentProperties.Remove(name);
            if (value is not null)
            {
                document.CustomDocumentProperties.Add(name, value);
            }
        }

        return 1;
    }

    internal static long AddWatermark(Document document, AddWatermarkOp op)
    {
        if (op.Text is not null)
        {
            var options = new TextWatermarkOptions();
            if (op.Color is not null)
            {
                options.Color = ParseColor(op.Color);
            }

            if (op.Opacity is not null)
            {
                options.IsSemitrasparent = op.Opacity.Value < 1;
            }

            document.Watermark.SetText(op.Text, options);
        }
        else
        {
            using SKBitmap bitmap = SKBitmap.Decode(op.ImagePath!)
                ?? throw Invalid($"watermark image '{op.ImagePath}' could not be decoded");
            var options = new ImageWatermarkOptions();
            if (op.Opacity is not null)
            {
                options.IsWashout = op.Opacity.Value < 1;
            }

            document.Watermark.SetImage(bitmap, options);
        }

        return 1;
    }

    internal static long RemoveWatermark(Document document)
    {
        document.Watermark.Remove();
        return 1;
    }

    internal static long Protect(Document document, ProtectOp op, string? password)
    {
        ProtectionType type = op.Mode switch
        {
            "readOnly" => ProtectionType.ReadOnly,
            "forms" => ProtectionType.AllowOnlyFormFields,
            "comments" => ProtectionType.AllowOnlyComments,
            "trackedChanges" => ProtectionType.AllowOnlyRevisions,
            _ => throw Invalid($"unknown protection mode '{op.Mode}'"),
        };
        document.Protect(type, password ?? string.Empty);
        return 1;
    }

    internal static long Unprotect(Document document, string? password)
    {
        if (password is null)
        {
            document.Unprotect();
            return 1;
        }

        bool removed = document.Unprotect(password);
        if (!removed)
        {
            throw new CliException(
                WordsDiagnostics.DocumentProtected,
                "The supplied protection password is invalid.",
                hint: "Provide the correct environment variable named by passwordEnv.");
        }

        return 1;
    }

    internal static long ChangeRevisions(Document document, string? author, bool accept)
    {
        Revision[] revisions = document.Revisions.Cast<Revision>()
            .Where(revision => author is null || string.Equals(revision.Author, author, StringComparison.Ordinal))
            .ToArray();
        foreach (Revision revision in revisions)
        {
            if (accept)
            {
                revision.Accept();
            }
            else
            {
                revision.Reject();
            }
        }

        return revisions.LongLength;
    }

    internal static long AddComment(Document document, Node anchor, AddCommentOp op)
    {
        if (anchor is not Paragraph paragraph)
        {
            throw Invalid("add_comment must target a paragraph block");
        }

        string initials = string.Concat(op.Author.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(static part => char.ToUpperInvariant(part[0])));
        var comment = new Comment(document, op.Author, initials, new DateTime(2000, 1, 1));
        comment.AppendChild(new Paragraph(document));
        comment.FirstParagraph!.AppendChild(new Run(document, op.Text));
        var start = new CommentRangeStart(document, comment.Id);
        var end = new CommentRangeEnd(document, comment.Id);
        paragraph.PrependChild(start);
        paragraph.AppendChild(end);
        paragraph.AppendChild(comment);
        return 1;
    }

    internal static long RemoveComments(Document document, RemoveCommentsOp op)
    {
        Comment[] comments = document.GetChildNodes(NodeType.Comment, true).Cast<Comment>()
            .Where(comment => op.Author is null || string.Equals(comment.Author, op.Author, StringComparison.Ordinal))
            .ToArray();
        foreach (Comment comment in comments)
        {
            int id = comment.Id;
            comment.Remove();
            foreach (Node node in document.GetChildNodes(NodeType.CommentRangeStart, true).Cast<CommentRangeStart>()
                         .Where(start => start.Id == id).Cast<Node>()
                         .Concat(document.GetChildNodes(NodeType.CommentRangeEnd, true).Cast<CommentRangeEnd>().Where(end => end.Id == id)))
            {
                node.Remove();
            }
        }

        return comments.LongLength;
    }

    internal static long MailMerge(
        Document document,
        MailMergeOp op,
        InputSource inputs)
    {
        IReadOnlyList<IReadOnlyDictionary<string, string?>> rows =
            op.Inline ?? ReadMergeRows(op.Path!, inputs);
        if (rows.Count == 0)
        {
            throw MergeInvalid("merge data has no rows");
        }

        if (op.Regions)
        {
            ExecuteRegionMerge(document, rows);
            return rows.Count;
        }

        Document template = document.Clone();
        ExecuteMergeRow(document, rows[0]);
        for (int index = 1; index < rows.Count; index++)
        {
            Document letter = template.Clone();
            ExecuteMergeRow(letter, rows[index]);
            document.AppendDocument(letter, ImportFormatMode.KeepSourceFormatting);
        }

        return rows.Count;
    }

    internal static void ExecuteMergeRow(Document document, IReadOnlyDictionary<string, string?> row) =>
        document.MailMerge.Execute(row.Keys.ToArray(), row.Values.Cast<object?>().ToArray());

    internal static void ExecuteRegionMerge(
        Document document,
        IReadOnlyList<IReadOnlyDictionary<string, string?>> rows)
    {
        string? marker = document.Range.Fields.Cast<Field>()
            .OfType<FieldMergeField>()
            .Select(static field => field.FieldName)
            .FirstOrDefault(static name => name.StartsWith("TableStart:", StringComparison.OrdinalIgnoreCase));
        string? regionName = marker is null ? null : marker["TableStart:".Length..];
        if (string.IsNullOrWhiteSpace(regionName))
        {
            throw MergeInvalid("regions was requested but the template has no TableStart merge field");
        }

        string[] columns = rows.SelectMany(static row => row.Keys)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var table = new DataTable(regionName);
        foreach (string column in columns)
        {
            table.Columns.Add(column, typeof(string));
        }

        foreach (IReadOnlyDictionary<string, string?> row in rows)
        {
            DataRow data = table.NewRow();
            foreach (string column in columns)
            {
                data[column] = row.TryGetValue(column, out string? value)
                    ? value ?? (object)DBNull.Value
                    : DBNull.Value;
            }

            table.Rows.Add(data);
        }

        document.MailMerge.ExecuteWithRegions(table);
    }

    internal static long UpdateFields(Document document, UpdateFieldsOp op)
    {
        if (op.What == "toc")
        {
            foreach (FieldToc toc in document.Range.Fields.Cast<Field>().OfType<FieldToc>())
            {
                toc.Update();
            }
        }
        else
        {
            document.NormalizeFieldTypes();
            document.UpdateFields();
        }

        document.UpdatePageLayout();
        return document.Range.Fields.Count;
    }

    internal static IReadOnlyList<IReadOnlyDictionary<string, string?>> ReadMergeRows(
        string path,
        InputSource inputs)
    {
        if (!File.Exists(path))
        {
            throw CliErrors.FileNotFound(path);
        }

        if (Path.GetExtension(path).Equals(".csv", StringComparison.OrdinalIgnoreCase))
        {
            string[][] lines = inputs.ReadTextFile(path)
                .Split(["\r\n", "\n", "\r"], StringSplitOptions.RemoveEmptyEntries)
                .Select(static line => line.Split(',').Select(static cell => cell.Trim()).ToArray()).ToArray();
            if (lines.Length < 2)
            {
                throw MergeInvalid("CSV needs a header and at least one data row");
            }

            return lines.Skip(1).Select(row => (IReadOnlyDictionary<string, string?>)lines[0]
                .Select((name, index) => (name, value: index < row.Length ? row[index] : null))
                .ToDictionary(static pair => pair.name, static pair => pair.value, StringComparer.Ordinal)).ToArray();
        }

        try
        {
            using JsonDocument json = JsonDocument.Parse(inputs.ReadTextFile(path));
            if (json.RootElement.ValueKind != JsonValueKind.Array)
            {
                throw MergeInvalid("JSON merge data must be an array of objects");
            }

            return json.RootElement.EnumerateArray().Select(ReadMergeObject).ToArray();
        }
        catch (JsonException exception)
        {
            throw MergeInvalid(exception.Message);
        }
    }

    internal static IReadOnlyDictionary<string, string?> ReadMergeObject(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw MergeInvalid("each merge row must be an object");
        }

        return element.EnumerateObject().ToDictionary(
            static property => property.Name,
            static property => property.Value.ValueKind == JsonValueKind.Null ? null : property.Value.ToString(),
            StringComparer.Ordinal);
    }
}

