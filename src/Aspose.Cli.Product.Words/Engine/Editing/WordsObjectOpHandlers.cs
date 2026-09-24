using System.Data;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Aspose.Cli.Product.Words.Contracts;
using Aspose.Cli.Product.Words.Engine.Mapping;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Aspose.Words;
using Aspose.Words.Drawing;
using Aspose.Words.Fields;
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
        var toc = (FieldToc)builder.InsertTableOfContents($"\\o \"1-{op.MaxLevel}\" \\h \\z \\u");
        UpdateTocs(document, [toc]);
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

    internal static long AddWatermark(
        Document document,
        AddWatermarkOp op,
        InputSource inputs,
        ResourceBudgetLedger budgets)
    {
        if (op.Text is not null)
        {
            var options = new TextWatermarkOptions { IsSemitrasparent = op.Faded };
            if (op.Color is not null)
            {
                options.Color = ParseColor(op.Color);
            }

            document.Watermark.SetText(op.Text, options);
            return 1;
        }

        // The image is an admitted, size-bounded input; its decoded pixels are charged to the
        // memory budget from the header before any pixel buffer is allocated.
        byte[] encoded = inputs.ReadAllBytes(op.ImagePath!);
        using SKCodec codec = SKCodec.Create(new SKMemoryStream(encoded))
            ?? throw Invalid($"watermark image '{op.ImagePath}' is not a supported image");
        budgets.Consume(
            ResourceBudgetKinds.MemoryBufferBytes,
            (long)codec.Info.Width * codec.Info.Height * 4,
            "bytes",
            "image-decode");
        using SKBitmap bitmap = SKBitmap.Decode(codec)
            ?? throw Invalid($"watermark image '{op.ImagePath}' could not be decoded");
        document.Watermark.SetImage(bitmap, new ImageWatermarkOptions { IsWashout = op.Faded });
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
        if (author is not null)
        {
            var byAuthor = new AuthorCriteria(author);
            return accept ? document.Revisions.Accept(byAuthor) : document.Revisions.Reject(byAuthor);
        }

        int count = document.Revisions.Count;
        if (accept)
        {
            document.Revisions.AcceptAll();
        }
        else
        {
            document.Revisions.RejectAll();
        }

        return count;
    }

    private sealed class AuthorCriteria(string author) : IRevisionCriteria
    {
        public bool IsMatch(Revision revision) =>
            string.Equals(revision?.Author, author, StringComparison.Ordinal);
    }

    internal static long AddComment(Document document, Node anchor, AddCommentOp op)
    {
        if (anchor is not Paragraph paragraph)
        {
            throw Invalid("add_comment must target a paragraph block");
        }

        string initials = string.Concat(op.Author.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(static part => char.ToUpperInvariant(part[0])));
        var comment = new Comment(document, op.Author, initials, DateTime.Now);
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
        // Collect every node first: removing from a live node collection while enumerating it skips nodes.
        var ids = comments.Select(static comment => comment.Id).ToHashSet();
        Node[] anchors = document.GetChildNodes(NodeType.CommentRangeStart, true).Cast<CommentRangeStart>()
            .Where(start => ids.Contains(start.Id)).Cast<Node>()
            .Concat(document.GetChildNodes(NodeType.CommentRangeEnd, true).Cast<CommentRangeEnd>()
                .Where(end => ids.Contains(end.Id)))
            .ToArray();
        foreach (Node node in comments.Concat(anchors))
        {
            node.Remove();
        }

        return comments.LongLength;
    }

    internal static long MailMerge(
        Document document,
        MailMergeOp op,
        InputSource inputs,
        WordsDocumentLoader loader)
    {
        IReadOnlyList<IReadOnlyDictionary<string, string?>> rows =
            op.Inline ?? ReadMergeRows(op.Path!, inputs);
        if (rows.Count == 0)
        {
            throw MergeDataInvalid("merge data has no rows", MergeDataShape);
        }

        if (op.Regions)
        {
            string region = SingleRegion(document);
            loader.EnsureNodeCapacity(document, rows.Count * RegionNodeCount(document, region));
            ExecuteRegionMerge(document, region, rows);
            return rows.Count;
        }

        // Every further row appends one copy of the whole template.
        loader.EnsureNodeCapacity(document, (rows.Count - 1L) * document.GetChildNodes(NodeType.Any, true).Count);
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

    /// <summary>
    /// The one merge region the flat rows feed. Several regions would need hierarchical data,
    /// so a template with more than one region name is rejected rather than half merged.
    /// </summary>
    private static string SingleRegion(Document document)
    {
        string[] regions = MergeFields(document)
            .Select(static field => field.FieldName)
            .Where(static name => name.StartsWith(TableStart, StringComparison.OrdinalIgnoreCase))
            .Select(static name => name[TableStart.Length..])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return regions switch
        {
            [] => throw MergeRegionInvalid(
                "it has no TableStart merge field",
                "Put TableStart:Name and TableEnd:Name merge fields around the content to repeat, or set regions to false to merge one copy of the document per row."),
            [var region] when !string.IsNullOrWhiteSpace(region) => region,
            [_] => throw MergeRegionInvalid(
                "its TableStart merge field has no region name",
                "Name the region in both fields, such as TableStart:Items and TableEnd:Items."),
            _ => throw MergeRegionInvalid(
                $"it has {regions.Length} merge regions ({string.Join(", ", regions)}) but flat merge rows feed exactly one",
                "Split the template so each copy has one region, and merge each copy with its own rows."),
        };
    }

    /// <summary>The nodes one region repetition adds: its table rows, or its body blocks.</summary>
    private static long RegionNodeCount(Document document, string region)
    {
        FieldMergeField[] fields = MergeFields(document).ToArray();
        FieldMergeField start = fields.First(field =>
            string.Equals(field.FieldName, TableStart + region, StringComparison.OrdinalIgnoreCase));
        FieldMergeField end = fields.FirstOrDefault(field =>
                string.Equals(field.FieldName, "TableEnd:" + region, StringComparison.OrdinalIgnoreCase))
            ?? throw MergeRegionInvalid(
                $"region '{region}' has no TableEnd:{region} merge field",
                $"Add a TableEnd:{region} merge field after the content the region repeats.");
        if (start.Start.GetAncestor(NodeType.Row) is Row first
            && end.End.GetAncestor(NodeType.Row) is Row last
            && ReferenceEquals(first.ParentTable, last.ParentTable))
        {
            return Span(first, last);
        }

        return Span(start.Start.GetAncestor(NodeType.Paragraph), end.End.GetAncestor(NodeType.Paragraph));

        static long Span(Node first, Node last)
        {
            long count = 0;
            for (Node? node = first; node is not null; node = node.NextSibling)
            {
                count += 1 + (node is CompositeNode composite ? composite.GetChildNodes(NodeType.Any, true).Count : 0);
                if (ReferenceEquals(node, last))
                {
                    break;
                }
            }

            return count;
        }
    }

    private const string TableStart = "TableStart:";

    private static IEnumerable<FieldMergeField> MergeFields(Document document) =>
        document.Range.Fields.Cast<Field>().OfType<FieldMergeField>();

    internal static void ExecuteRegionMerge(
        Document document,
        string regionName,
        IReadOnlyList<IReadOnlyDictionary<string, string?>> rows)
    {
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
            // Updating a TOC adds its own hyperlink and PAGEREF fields, so snapshot the TOCs first.
            UpdateTocs(document, document.Range.Fields.Cast<Field>().OfType<FieldToc>().ToArray());
        }
        else
        {
            document.NormalizeFieldTypes();
            document.UpdateFields();
            document.UpdatePageLayout();
        }

        return document.Range.Fields.Count;
    }

    /// <summary>
    /// Rebuilds the given tables of contents only, then fills their page numbers from a fresh
    /// layout. Other fields (DATE, INCLUDETEXT and the like) keep their stored results.
    /// </summary>
    private static void UpdateTocs(Document document, IReadOnlyList<FieldToc> tocs)
    {
        foreach (FieldToc toc in tocs)
        {
            toc.Update();
        }

        document.UpdatePageLayout();
        foreach (FieldToc toc in tocs)
        {
            toc.UpdatePageNumbers();
        }
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
            IReadOnlyList<string[]> lines = ReadCsv(inputs.ReadTextFile(path));
            if (lines.Count < 2)
            {
                throw MergeDataInvalid("CSV needs a header and at least one data row", MergeDataShape);
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
                throw MergeDataInvalid("JSON merge data must be an array of objects", MergeDataShape);
            }

            return json.RootElement.EnumerateArray().Select(ReadMergeObject).ToArray();
        }
        catch (JsonException exception)
        {
            throw MergeDataInvalid(exception.Message, "Fix the JSON syntax at the reported position; merge data is an array of flat objects.");
        }
    }

    /// <summary>
    /// Reads RFC 4180 CSV: quoted fields may hold commas, quotes (doubled) and line breaks.
    /// Unquoted fields are trimmed, and blank lines are skipped.
    /// </summary>
    internal static IReadOnlyList<string[]> ReadCsv(string text)
    {
        var records = new List<string[]>();
        var fields = new List<string>();
        var field = new StringBuilder();
        bool quoted = false;
        bool wasQuoted = false;

        void EndField()
        {
            fields.Add(wasQuoted ? field.ToString() : field.ToString().Trim());
            field.Clear();
            wasQuoted = false;
        }

        void EndRecord()
        {
            EndField();
            if (fields.Count > 1 || fields[0].Length > 0)
            {
                records.Add([.. fields]);
            }

            fields.Clear();
        }

        for (int index = 0; index < text.Length; index++)
        {
            char current = text[index];
            if (quoted)
            {
                if (current != '"')
                {
                    field.Append(current);
                }
                else if (index + 1 < text.Length && text[index + 1] == '"')
                {
                    field.Append('"');
                    index++;
                }
                else
                {
                    quoted = false;
                }
            }
            else if (current == '"' && field.ToString().Trim().Length == 0)
            {
                field.Clear();
                quoted = true;
                wasQuoted = true;
            }
            else if (current == ',')
            {
                EndField();
            }
            else if (current is '\r' or '\n')
            {
                if (current == '\r' && index + 1 < text.Length && text[index + 1] == '\n')
                {
                    index++;
                }

                EndRecord();
            }
            else
            {
                field.Append(current);
            }
        }

        if (quoted)
        {
            throw MergeDataInvalid("the CSV ends inside a quoted field", "Close every quoted CSV field, and double each quote inside one.");
        }

        if (field.Length > 0 || fields.Count > 0)
        {
            EndRecord();
        }

        return records;
    }

    internal static IReadOnlyDictionary<string, string?> ReadMergeObject(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw MergeDataInvalid("each merge row must be an object", MergeDataShape);
        }

        return element.EnumerateObject().ToDictionary(
            static property => property.Name,
            static property => property.Value.ValueKind == JsonValueKind.Null ? null : property.Value.ToString(),
            StringComparer.Ordinal);
    }
}

