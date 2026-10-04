using System.Data;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Aspose.Cli.Product.Words.Contracts;
using Aspose.Cli.Product.Words.Engine.Mapping;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Aspose.Words;
using Aspose.Words.Drawing;
using Aspose.Words.Fields;
using Aspose.Words.MailMerging;
using Aspose.Words.Replacing;
using Aspose.Words.Tables;
using SkiaSharp;

using static Aspose.Cli.Product.Words.Engine.Editing.WordsMutationSupport;

namespace Aspose.Cli.Product.Words.Engine.Editing;

// Embedded objects, fields, metadata, protection, revisions, comments and merges.
internal sealed partial class WordsMutationHandlers
{
    public long Apply(InsertImageOp operation)
    {
        if (!File.Exists(operation.Path))
        {
            throw CliErrors.FileNotFound(operation.Path);
        }

        var paragraph = new Paragraph(_document);
        Node cursor = Anchor;
        InsertRelative(Anchor, ref cursor, paragraph, operation.Position);
        var builder = new DocumentBuilder(_document);
        builder.MoveTo(paragraph);
        Shape shape = builder.InsertImage(_operationInputs.OpenFile(operation.Path));
        if (operation.Width is not null)
        {
            shape.Width = operation.Width.Value;
        }

        if (operation.Height is not null)
        {
            shape.Height = operation.Height.Value;
        }
        if (!operation.Inline)
        {
            shape.WrapType = WrapType.Square;
        }

        return 1;
    }

    public long Apply(InsertTocOp operation)
    {
        Paragraph paragraph = InsertBuilderParagraph(_document, Anchor, operation.Position);
        var builder = new DocumentBuilder(_document);
        builder.MoveTo(paragraph);
        var toc = (FieldToc)builder.InsertTableOfContents($"\\o \"1-{operation.MaxLevel}\" \\h \\z \\u");
        UpdateTocs(_document, [toc]);
        return 1;
    }

    public long Apply(InsertBookmarkOp operation)
    {
        if (_document.Range.Bookmarks[operation.Name] is not null)
        {
            throw Invalid($"bookmark '{operation.Name}' already exists");
        }

        if (Anchor is not Paragraph paragraph)
        {
            throw Invalid("insert_bookmark must target a paragraph block");
        }

        paragraph.PrependChild(new BookmarkStart(_document, operation.Name));
        paragraph.AppendChild(new BookmarkEnd(_document, operation.Name));
        return 1;
    }

    public long Apply(InsertHyperlinkOp operation)
    {
        Paragraph paragraph = InsertBuilderParagraph(_document, Anchor, operation.Position);
        var builder = new DocumentBuilder(_document);
        builder.MoveTo(paragraph);
        builder.InsertHyperlink(operation.Text, operation.Url, isBookmark: false);
        return 1;
    }

    public long Apply(InsertFieldOp operation)
    {
        Paragraph paragraph = InsertBuilderParagraph(_document, Anchor, operation.Position);
        var builder = new DocumentBuilder(_document);
        builder.MoveTo(paragraph);
        Field field = builder.InsertField(operation.Code);
        // WORDS-PAGE-FIELD-LAYOUT: a page field inserted after the document was laid out, as
        // every loaded document is, gets no result until the layout is rebuilt; the edit
        // updates it once after the batch, from a single fresh layout.
        if (field.Result.Length == 0 && field.Type is FieldType.FieldPage or FieldType.FieldNumPages
                or FieldType.FieldSectionPages or FieldType.FieldPageRef)
        {
            _pageFields.Add(field);
        }

        return 1;
    }

    public long Apply(SetPropertiesOp operation)
    {
        if (operation.Title is not null)
        {
            _document.BuiltInDocumentProperties.Title = operation.Title;
        }

        if (operation.Author is not null)
        {
            _document.BuiltInDocumentProperties.Author = operation.Author;
        }

        if (operation.Subject is not null)
        {
            _document.BuiltInDocumentProperties.Subject = operation.Subject;
        }

        if (operation.Keywords is not null)
        {
            _document.BuiltInDocumentProperties.Keywords = operation.Keywords;
        }

        foreach ((string name, string? value) in operation.Custom ?? new Dictionary<string, string?>())
        {
            _document.CustomDocumentProperties.Remove(name);
            if (value is not null)
            {
                _document.CustomDocumentProperties.Add(name, value);
            }
        }

        return 1;
    }

    public long Apply(AddWatermarkOp operation)
    {
        if (operation.Text is not null)
        {
            var options = new TextWatermarkOptions { IsSemitrasparent = operation.Faded };
            if (operation.Color is not null)
            {
                options.Color = ParseColor(operation.Color);
            }

            // A watermark has one font for all its text, and the SDK's default font has no East
            // Asian glyphs. Only a document set up for Chinese, Japanese or Korean surely names
            // an East Asian default font; another may name a Latin one there.
            Aspose.Words.Font defaults = _document.Styles.DefaultFont;
            string? documentFont = WordsFonts.IsEastAsianLanguage(defaults.LocaleIdFarEast) && !string.IsNullOrEmpty(defaults.NameFarEast)
                ? defaults.NameFarEast
                : null;
            string? font = operation.Font ?? (WordsFonts.HasEastAsian(operation.Text)
                ? documentFont ?? WordsFonts.EastAsianFallback
                : null);
            if (font is not null)
            {
                options.FontFamily = font;
            }

            _document.Watermark.SetText(operation.Text, options);
            return 1;
        }

        // The image is an admitted, size-bounded input; its decoded pixels are charged to the
        // memory budget from the header before any pixel buffer is allocated.
        byte[] encoded = _inputs.ReadAllBytes(operation.ImagePath!);
        using SKCodec codec = SKCodec.Create(new SKMemoryStream(encoded))
            ?? throw Invalid($"watermark image '{operation.ImagePath}' is not a supported image");
        _loader.ResourceBudgets.Consume(
            ResourceBudgetKinds.MemoryBufferBytes,
            (long)codec.Info.Width * codec.Info.Height * 4,
            "bytes",
            "image-decode");
        using SKBitmap bitmap = SKBitmap.Decode(codec)
            ?? throw Invalid($"watermark image '{operation.ImagePath}' could not be decoded");
        _document.Watermark.SetImage(bitmap, new ImageWatermarkOptions { IsWashout = operation.Faded });
        return 1;
    }

    public long Apply(RemoveWatermarkOp operation)
    {
        _document.Watermark.Remove();
        return 1;
    }

    public long Apply(ProtectOp operation)
    {
        string? password = OperationSecrets.Resolve(_secrets, operation.PasswordEnv);
        _document.Protect(WordsProtection.FromMode(operation.Mode), password ?? string.Empty);
        return 1;
    }

    public long Apply(UnprotectOp operation)
    {
        string? password = OperationSecrets.Resolve(_secrets, operation.PasswordEnv);
        if (password is null)
        {
            _document.Unprotect();
            return 1;
        }

        bool removed = _document.Unprotect(password);
        if (!removed)
        {
            throw new CliException(
                WordsDiagnostics.DocumentProtected,
                "The supplied protection password is invalid.",
                hint: "Provide the correct environment variable named by passwordEnv.");
        }

        return 1;
    }

    public long Apply(AcceptRevisionsOp operation) => ChangeRevisions(operation.Author, accept: true);

    public long Apply(RejectRevisionsOp operation) => ChangeRevisions(operation.Author, accept: false);

    private long ChangeRevisions(string? author, bool accept)
    {
        if (author is not null)
        {
            var byAuthor = new AuthorCriteria(author);
            return accept ? _document.Revisions.Accept(byAuthor) : _document.Revisions.Reject(byAuthor);
        }

        int count = _document.Revisions.Count;
        if (accept)
        {
            _document.Revisions.AcceptAll();
        }
        else
        {
            _document.Revisions.RejectAll();
        }

        return count;
    }

    private sealed class AuthorCriteria(string author) : IRevisionCriteria
    {
        public bool IsMatch(Revision revision) =>
            string.Equals(revision?.Author, author, StringComparison.Ordinal);
    }

    public long Apply(AddCommentOp operation)
    {
        if (Anchor is not Paragraph paragraph)
        {
            throw Invalid("add_comment must target a paragraph block");
        }

        string initials = string.Concat(operation.Author.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(static part => char.ToUpperInvariant(part[0])));
        Untracked(() =>
        {
            var comment = new Comment(_document, operation.Author, initials, DateTime.Now);
            comment.AppendChild(new Paragraph(_document));
            comment.FirstParagraph!.AppendChild(new Run(_document, operation.Text));
            var start = new CommentRangeStart(_document, comment.Id);
            var end = new CommentRangeEnd(_document, comment.Id);
            paragraph.PrependChild(start);
            paragraph.AppendChild(end);
            paragraph.AppendChild(comment);
        });
        return 1;
    }

    /// <summary>
    /// Changes comments outside revision tracking: a comment is a review annotation of its own,
    /// not a tracked change. Under tracking the SDK would record an added comment's text as
    /// insertions and leave a removed comment in place.
    /// </summary>
    private void Untracked(Action change)
    {
        _tracking?.Stop();
        try
        {
            change();
        }
        finally
        {
            _tracking?.Start();
        }
    }

    public long Apply(RemoveCommentsOp operation)
    {
        Comment[] comments = _document.GetChildNodes(NodeType.Comment, true).Cast<Comment>()
            .Where(comment => operation.Author is null || string.Equals(comment.Author, operation.Author, StringComparison.Ordinal))
            .ToArray();
        // Collect every node first: removing from a live node collection while enumerating it skips nodes.
        var ids = comments.Select(static comment => comment.Id).ToHashSet();
        Node[] anchors = _document.GetChildNodes(NodeType.CommentRangeStart, true).Cast<CommentRangeStart>()
            .Where(start => ids.Contains(start.Id)).Cast<Node>()
            .Concat(_document.GetChildNodes(NodeType.CommentRangeEnd, true).Cast<CommentRangeEnd>()
                .Where(end => ids.Contains(end.Id)))
            .ToArray();
        Untracked(() =>
        {
            foreach (Node node in comments.Concat(anchors))
            {
                node.Remove();
            }
        });

        return comments.LongLength;
    }

    public long Apply(MailMergeOp operation)
    {
        IReadOnlyList<IReadOnlyDictionary<string, string?>> rows = operation.Inline is { } inline
            ? MergeRows(inline)
            : ReadMergeRows(operation.Path!, _inputs);
        if (rows.Count == 0)
        {
            throw MergeDataInvalid(
                "mail_merge needs at least one row",
                "Supply a JSON array with at least one object, or a CSV file with a data row after its header.");
        }

        if (operation.Regions)
        {
            string region = SingleRegion(_document);
            _loader.EnsureNodeCapacity(_document, rows.Count * RegionNodeCount(_document, region));
            Warning? regionGaps = WordsMergeGaps.Find(WordsMergeGaps.TemplateFields(_document, region), rows);
            ExecuteRegionMerge(_document, region, rows);
            AddWarning(regionGaps);
            return rows.Count;
        }

        // Every further row appends one copy of the whole template.
        _loader.EnsureNodeCapacity(_document, (rows.Count - 1L) * _document.GetChildNodes(NodeType.Any, true).Count);
        Warning? gaps = WordsMergeGaps.Find(WordsMergeGaps.TemplateFields(_document, region: null), rows);
        Document template = _document.Clone();
        ExecuteMergeRow(_document, rows[0]);
        for (int index = 1; index < rows.Count; index++)
        {
            Document letter = template.Clone();
            ExecuteMergeRow(letter, rows[index]);
            // Under evaluation a letter starts with a banner: the template's own, or the one
            // the evaluation merge writes into it.
            if (_loaded.Evaluation)
            {
                WordsEvaluation.RemoveLeadingBanners(letter);
            }

            _document.AppendDocument(letter, ImportFormatMode.KeepSourceFormatting);
        }

        AddWarning(gaps);
        return rows.Count;
    }

    // Discloses a merge's blank fields once the merge succeeded.
    private void AddWarning(Warning? warning)
    {
        if (warning is not null)
        {
            _warnings.Add(warning);
        }
    }

    // A template field the row has no key for merges as blank text, as a null value does.
    internal static void ExecuteMergeRow(Document document, IReadOnlyDictionary<string, string?> row)
    {
        document.MailMerge.CleanupOptions = MailMergeCleanupOptions.RemoveUnusedFields;
        document.MailMerge.Execute(row.Keys.ToArray(), row.Values.Cast<object?>().ToArray());
    }

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

        // A region field no record has a key for merges as blank text; fields outside the region stay.
        document.MailMerge.CleanupOptions = MailMergeCleanupOptions.RemoveUnusedFields;
        document.MailMerge.ExecuteWithRegions(table);
    }

    public long Apply(UpdateFieldsOp operation)
    {
        if (operation.What == "toc")
        {
            // Updating a TOC adds its own hyperlink and PAGEREF fields, so snapshot the TOCs first.
            UpdateTocs(_document, _document.Range.Fields.Cast<Field>().OfType<FieldToc>().ToArray());
        }
        else
        {
            _document.NormalizeFieldTypes();
            _document.UpdateFields();
            _document.UpdatePageLayout();
        }

        return _document.Range.Fields.Count;
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

    /// <summary>
    /// Reads merge rows, by column or property name, from a JSON array of flat objects or a CSV
    /// file with a header row; a header row alone is no rows. CSV cannot tell an empty value
    /// from a missing one, so an empty cell is null, as a cell past the end of a short row is.
    /// </summary>
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
            if (lines.Count == 0)
            {
                throw MergeDataInvalid("the CSV has no header row", MergeDataShape);
            }

            return lines.Skip(1).Select(row => (IReadOnlyDictionary<string, string?>)lines[0]
                .Select((name, index) => (name, value: index < row.Length && row[index].Length > 0 ? row[index] : null))
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
            static property => MergeValue(property.Value),
            StringComparer.Ordinal);
    }

    /// <summary>Reads inline rows as the rows of a JSON data file.</summary>
    internal static IReadOnlyList<IReadOnlyDictionary<string, string?>> MergeRows(
        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows) =>
        [.. rows.Select(static row => (IReadOnlyDictionary<string, string?>)row.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value is JsonElement element ? MergeValue(element) : (string?)pair.Value,
            StringComparer.Ordinal))];

    // A string merges as itself and a number or Boolean as its JSON text; null is no value.
    private static string? MergeValue(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Null => null,
        JsonValueKind.String => value.GetString(),
        JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => value.GetRawText(),
        _ => throw MergeDataInvalid("a merge value must be a string, number, Boolean or null", MergeDataShape),
    };
}

