using System.Data;
using Aspose.Cli.Product.Words.Engine.Mapping;
using Aspose.Cli.Sdk.Errors;
using Aspose.Words;
using Aspose.Words.Drawing;
using Aspose.Words.Fields;
using SkiaSharp;

using static Aspose.Cli.Product.Words.Engine.Editing.WordsMutationSupport;

namespace Aspose.Cli.Product.Words.Engine.Editing;

// Embedded objects, fields, metadata, protection, revisions, comments and merges.
internal sealed partial class WordsMutationHandlers
{
    public ValueTuple Apply(InsertImageOp operation)
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
        Shape shape = builder.InsertImage(OperationImages.Read(_operationInputs, operation.Path));
        // The engine keeps an inserted image's aspect ratio, so the height would rescale the
        // width given before it; with both given, the image takes exactly those.
        if (operation.Width is not null && operation.Height is not null)
        {
            shape.AspectRatioLocked = false;
        }

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

        return default;
    }

    public ValueTuple Apply(InsertTocOp operation)
    {
        Paragraph paragraph = InsertBuilderParagraph(_document, Anchor, operation.Position);
        var builder = new DocumentBuilder(_document);
        builder.MoveTo(paragraph);
        var toc = (FieldToc)builder.InsertTableOfContents($"\\o \"1-{operation.MaxLevel}\" \\h \\z \\u");
        UpdateTocs(_document, [toc]);
        return default;
    }

    public ValueTuple Apply(InsertBookmarkOp operation)
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
        return default;
    }

    public ValueTuple Apply(InsertHyperlinkOp operation)
    {
        Paragraph paragraph = InsertBuilderParagraph(_document, Anchor, operation.Position);
        var builder = new DocumentBuilder(_document);
        builder.MoveTo(paragraph);
        builder.InsertHyperlink(operation.Text, operation.Url, isBookmark: false);
        return default;
    }

    public ValueTuple Apply(InsertFieldOp operation)
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

        return default;
    }

    public ValueTuple Apply(SetPropertiesOp operation)
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

        return default;
    }

    public ValueTuple Apply(SetWatermarkOp operation)
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
            return default;
        }

        // The image is an admitted, size-bounded input, checked to decode before the engine
        // embeds it. The engine decodes it into a bitmap it keeps, whose pixels are charged to the
        // memory budget from the header.
        byte[] image = OperationImages.Read(_inputs, operation.Path!);
        using (SKCodec? codec = SKCodec.Create(new SKMemoryStream(image)))
        {
            if (codec is not null)
            {
                _loader.ResourceBudgets.Consume(
                    ResourceBudgetKinds.MemoryBufferBytes,
                    (long)codec.Info.Width * codec.Info.Height * 4,
                    "bytes",
                    "image-decode");
            }
        }

        _document.Watermark.SetImage(new MemoryStream(image, writable: false), new ImageWatermarkOptions { IsWashout = operation.Faded });
        return default;
    }

    public ValueTuple Apply(DeleteWatermarkOp operation)
    {
        _document.Watermark.Remove();
        return default;
    }

    public ValueTuple Apply(ProtectDocumentOp operation)
    {
        string? password = OperationSecrets.Resolve(_secrets, operation.PasswordEnv)?.Reveal();
        _document.Protect(WordsProtection.FromMode(operation.Protection), password ?? string.Empty);
        return default;
    }

    public ValueTuple Apply(UnprotectDocumentOp operation)
    {
        string? password = OperationSecrets.Resolve(_secrets, operation.PasswordEnv)?.Reveal();
        if (password is null)
        {
            _document.Unprotect();
            return default;
        }

        bool removed = _document.Unprotect(password);
        if (!removed)
        {
            throw CliErrors.ProtectionPassword(passwordGiven: true, "the document", "passwordEnv");
        }

        return default;
    }

    public ValueTuple Apply(AcceptRevisionsOp operation)
    {
        ChangeRevisions(operation.Author, accept: true);
        return default;
    }

    public ValueTuple Apply(RejectRevisionsOp operation)
    {
        ChangeRevisions(operation.Author, accept: false);
        return default;
    }

    /// <summary>
    /// Accepts or rejects the numbered changes the batch resolved, one author's revisions or
    /// every revision. Numbered changes are checked first: when an earlier decision in the batch
    /// took some of their revisions, the operation is refused before it changes anything.
    /// </summary>
    private void ChangeRevisions(string? author, bool accept)
    {
        if (_resolved.Revisions is { } changes)
        {
            IRevisionCriteria listed = RevisionChange.Criteria(changes);
            int expected = changes.Sum(static change => change.Members.Count);
            if (_document.Revisions.Count(listed.IsMatch) != expected)
            {
                throw new OperationInvalidException(
                    "it lists revisions an earlier operation in the batch already decided",
                    "Decide each revision once: list it in only one accept_revisions or reject_revisions, after no decision by author or of every revision.");
            }

            int decided = accept ? _document.Revisions.Accept(listed) : _document.Revisions.Reject(listed);
            if (decided != expected)
            {
                throw new EngineOpException(
                    $"The engine decided {decided} of the {expected} revisions the listed changes consist of.",
                    new InvalidOperationException("Revision decision count mismatch."));
            }

            return;
        }

        if (author is not null)
        {
            var byAuthor = new AuthorCriteria(author);
            _ = accept ? _document.Revisions.Accept(byAuthor) : _document.Revisions.Reject(byAuthor);
            return;
        }

        if (accept)
        {
            _document.Revisions.AcceptAll();
        }
        else
        {
            _document.Revisions.RejectAll();
        }
    }

    private sealed class AuthorCriteria(string author) : IRevisionCriteria
    {
        public bool IsMatch(Revision revision) =>
            string.Equals(revision?.Author, author, StringComparison.Ordinal);
    }

    public ValueTuple Apply(AddCommentOp operation)
    {
        if (Anchor is not Paragraph paragraph)
        {
            throw Invalid("add_comment must target a paragraph block");
        }

        string initials = string.Concat(operation.Author.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(static part => char.ToUpperInvariant(part[0])));
        Untracked(() =>
        {
            var comment = new Comment(_document, operation.Author, initials, DateTime.UtcNow);
            comment.AppendChild(new Paragraph(_document));
            comment.FirstParagraph!.AppendChild(new Run(_document, operation.Text));
            var start = new CommentRangeStart(_document, comment.Id);
            var end = new CommentRangeEnd(_document, comment.Id);
            paragraph.PrependChild(start);
            paragraph.AppendChild(end);
            paragraph.AppendChild(comment);
        });
        return default;
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

    public ValueTuple Apply(DeleteCommentsOp operation)
    {
        Comment[] all = [.. _document.GetChildNodes(NodeType.Comment, true).Cast<Comment>()];
        Comment[] comments = all
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

        return default;
    }

    public ValueTuple Apply(MailMergeOp operation)
    {
        Warning? gaps = WordsMailMerge.Merge(
            _document,
            WordsMergeData.Rows(operation.Items, operation.Path, _inputs),
            operation.Regions,
            _loader,
            _outputs);
        if (gaps is not null)
        {
            _warnings.Add(gaps);
        }

        return default;
    }

    public ValueTuple Apply(RefreshFieldsOp operation)
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

        return default;
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
}
