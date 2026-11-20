using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Aspose.Cli.Sdk.Tables;
using Aspose.Words;
using Aspose.Words.Fields;
using Aspose.Words.Markup;
using Aspose.Words.Replacing;
using Aspose.Words.Tables;
using WordRow = Aspose.Words.Tables.Row;

namespace Aspose.Cli.Product.Words.Engine.Mapping;

/// <summary>
/// The Words actions: one name for one engine call, whose parameters the caller states. The object
/// a call is made on is named by its address. An action that makes an object returns its address,
/// which the report reads back from the saved file.
/// </summary>
internal static partial class WordsActions
{
    internal static void Declare(DocumentTable<Document> table)
    {
        DeclareText(table);
        DeclareUpdates(table);
        DeclareRevisions(table);
        DeclareDocument(table);
        DeclareNodes(table);
    }

    private static void DeclareText(DocumentTable<Document> table)
    {
        ObjectKind[] ranges =
        [
            WordsTable.SectionKind, WordsTable.BodyKind, WordsTable.HeaderFooterKind, WordsTable.ParagraphKind, WordsTable.TableKind,
            WordsTable.RowKind, WordsTable.CellKind, WordsTable.CommentKind, WordsTable.FootnoteKind, WordsTable.SdtKind,
        ];
        IReadOnlyList<Argument> options =
        [
            Argument.Address("node", ranges, "The object whose Node.Range the call replaces in. Default: Document.Range, the whole document.", required: false),
            Argument.Optional("matchCase", ValueKinds.Boolean, "FindReplaceOptions.MatchCase."),
            Argument.Optional("findWholeWordsOnly", ValueKinds.Boolean, "FindReplaceOptions.FindWholeWordsOnly."),
            Argument.Optional("ignoreDeleted", ValueKinds.Boolean, "FindReplaceOptions.IgnoreDeleted."),
            Argument.Optional("ignoreInserted", ValueKinds.Boolean, "FindReplaceOptions.IgnoreInserted."),
            Argument.Optional("ignoreFields", ValueKinds.Boolean, "FindReplaceOptions.IgnoreFields."),
            Argument.Optional("ignoreFieldCodes", ValueKinds.Boolean, "FindReplaceOptions.IgnoreFieldCodes."),
            Argument.Optional("ignoreFootnotes", ValueKinds.Boolean, "FindReplaceOptions.IgnoreFootnotes."),
        ];
        table.Action(new TableAction<Document>
        {
            Name = "replace",
            Description = "Range.Replace(pattern, replacement, FindReplaceOptions) on the node's range: every match of the text, as the engine matches it. Returns the number of replacements.",
            Arguments = [Argument.Of("pattern", ValueKinds.Text, "The text to find."), Argument.Of("replacement", ValueKinds.Text, "The text it is replaced with."), .. options],
            Run = static (d, a, _) => Replaced(RangeOf(d, a).Replace(a.Get<string>("pattern"), a.Get<string>("replacement"), FindReplace(a))),
        });
        table.Action(new TableAction<Document>
        {
            Name = "replace_regex",
            Description = "Range.Replace(Regex pattern, replacement, FindReplaceOptions) on the node's range: every match of the .NET regular expression; $1 in the replacement is a group when FindReplaceOptions.UseSubstitutions is true. Returns the number of replacements.",
            Arguments =
            [
                Argument.Of("pattern", ValueKinds.Text, "The .NET regular expression."),
                Argument.Of("replacement", ValueKinds.Text, "The text each match is replaced with."),
                Argument.Optional("useSubstitutions", ValueKinds.Boolean, "FindReplaceOptions.UseSubstitutions."),
                .. options,
            ],
            Run = static (d, a, _) =>
            {
                FindReplaceOptions find = FindReplace(a);
                if (a.Has("useSubstitutions"))
                {
                    find.UseSubstitutions = a.Get<bool>("useSubstitutions");
                }

                return Replaced(RangeOf(d, a).Replace(Pattern(a.Get<string>("pattern")), a.Get<string>("replacement"), find));
            },
        });
    }

    private static void DeclareUpdates(DocumentTable<Document> table)
    {
        table.Action(Call("update_fields", "Document.UpdateFields(): updates the result of every field; neither opening nor saving a document updates them. Returns nothing.", static d => d.UpdateFields()));
        table.Action(Call("update_page_layout", "Document.UpdatePageLayout(): lays the document out again, which the page numbers of a later update_fields use. Returns nothing.", static d => d.UpdatePageLayout()));
        table.Action(Call("update_list_labels", "Document.UpdateListLabels(): numbers every list item again, which listLabel.labelString reads. Returns nothing.", static d => d.UpdateListLabels()));
        table.Action(Call("update_word_count", "Document.UpdateWordCount(): counts the words, characters and paragraphs into the built-in document properties. Returns nothing.", static d => d.UpdateWordCount()));
        table.Action(new TableAction<Document>
        {
            Name = "update_field",
            Description = "Field.Update(): updates the field's result. Returns the field's address.",
            Arguments = [Argument.Address("field", WordsTable.FieldKind, "The field.")],
            Run = static (_, a, _) =>
            {
                a.Get<Field>("field").Update();
                return new ActionResult(null, a.AddressOf("field"));
            },
        });
        table.Action(new TableAction<Document>
        {
            Name = "update_toc_page_numbers",
            Description = "FieldToc.UpdatePageNumbers(): updates the page numbers of a table of contents. Returns the engine's bool, whether it updated them, and the field's address.",
            Arguments = [Argument.Address("field", WordsTable.FieldKind, "The TOC field.")],
            Run = static (_, a, _) =>
            {
                FieldToc toc = a.Get<Field>("field") as FieldToc
                    ?? throw TableErrors.NotFound("TOC field", a.AddressOf("field"), []);
                return new ActionResult(JsonValue.Create(toc.UpdatePageNumbers()), a.AddressOf("field"));
            },
        });
    }

    private static void DeclareRevisions(DocumentTable<Document> table)
    {
        table.Action(new TableAction<Document>
        {
            Name = "start_track_revisions",
            Description = "Document.StartTrackRevisions(author, dateTime), or StartTrackRevisions(author) without dateTime, whose revisions take the time they are made: the operations after it in the batch are recorded as revisions. Returns nothing.",
            Arguments =
            [
                Argument.Of("author", ValueKinds.Text, "The author of the revisions."),
                Argument.Optional("dateTime", WordsValues.DateTime, "The date and time of the revisions."),
            ],
            Run = static (d, a, _) =>
            {
                if (a.Has("dateTime"))
                {
                    d.StartTrackRevisions(a.Get<string>("author"), a.Get<DateTime>("dateTime"));
                }
                else
                {
                    d.StartTrackRevisions(a.Get<string>("author"));
                }

                return new ActionResult(null);
            },
        });
        table.Action(Call("stop_track_revisions", "Document.StopTrackRevisions(): the operations after it are not recorded as revisions. Returns nothing.", static d => d.StopTrackRevisions()));
        table.Action(Call("accept_all_revisions", "Document.Revisions.AcceptAll(). Returns nothing.", static d => d.Revisions.AcceptAll()));
        table.Action(Call("reject_all_revisions", "Document.Revisions.RejectAll(). Returns nothing.", static d => d.Revisions.RejectAll()));
        table.Action(new TableAction<Document>
        {
            Name = "accept_revision",
            Description = "Revision.Accept(): the revisions after it in Document.Revisions move up a position. Returns nothing.",
            Arguments = [Argument.Address("revision", WordsTable.RevisionKind, "The revision.")],
            Run = static (_, a, _) =>
            {
                a.Get<Revision>("revision").Accept();
                return new ActionResult(null);
            },
        });
        table.Action(new TableAction<Document>
        {
            Name = "reject_revision",
            Description = "Revision.Reject(): the revisions after it in Document.Revisions move up a position. Returns nothing.",
            Arguments = [Argument.Address("revision", WordsTable.RevisionKind, "The revision.")],
            Run = static (_, a, _) =>
            {
                a.Get<Revision>("revision").Reject();
                return new ActionResult(null);
            },
        });
    }

    private static void DeclareNodes(DocumentTable<Document> table)
    {
        ObjectKind[] parents =
        [
            WordsTable.BodyKind, WordsTable.HeaderFooterKind, WordsTable.CellKind, WordsTable.CommentKind, WordsTable.FootnoteKind,
            WordsTable.ShapeKind, WordsTable.SdtKind, WordsTable.ParagraphKind, WordsTable.TableKind, WordsTable.RowKind,
        ];
        table.Action(new TableAction<Document>
        {
            Name = "clone",
            Description = "Node.Clone(isCloneChildren) of a node, then CompositeNode.InsertBefore(clone, before) on parent; the clone keeps the node's formatting, and with its children their bookmarks and comments too. Returns the clone's address.",
            Arguments =
            [
                Argument.Address("node", [WordsTable.ParagraphKind, WordsTable.TableKind, WordsTable.RowKind, WordsTable.CellKind, WordsTable.RunKind], "The node to clone."),
                Argument.Address("parent", parents, "The node the clone goes into."),
                Argument.Address("before", [WordsTable.ParagraphKind, WordsTable.TableKind, WordsTable.SdtKind, WordsTable.RowKind, WordsTable.CellKind, WordsTable.RunKind, WordsTable.ShapeKind],
                    "The child of parent the clone goes before; not given, it goes last (InsertBefore's refChild is null).", required: false),
                Argument.Of("isCloneChildren", ValueKinds.Boolean, "Whether the clone holds copies of the node's children, as Node.Clone takes it."),
            ],
            Run = static (_, a, _) =>
            {
                CompositeNode parent = a.Get<CompositeNode>("parent");
                Node clone = a.Get<Node>("node").Clone(a.Get<bool>("isCloneChildren"));
                parent.InsertBefore(clone, a.Has("before") ? a.Get<Node>("before") : null);
                return new ActionResult(null, $"{a.AddressOf("parent")}/{Segment(clone)}/{parent.GetChildNodes(clone.NodeType, false).IndexOf(clone)}");
            },
        });
        table.Action(new TableAction<Document>
        {
            Name = "remove_self_only",
            Description = "StructuredDocumentTag.RemoveSelfOnly(): removes the content control and keeps its content in its place. Returns nothing.",
            Arguments = [Argument.Address("sdt", WordsTable.SdtKind, "The content control.")],
            Run = static (_, a, _) =>
            {
                a.Get<StructuredDocumentTag>("sdt").RemoveSelfOnly();
                return new ActionResult(null);
            },
        });
        table.Action(new TableAction<Document>
        {
            Name = "remove_list_numbers",
            Description = "ListFormat.RemoveNumbers() of a paragraph: it is no list item any more. Returns the paragraph's address.",
            Arguments = [Argument.Address("paragraph", WordsTable.ParagraphKind, "The paragraph.")],
            Run = static (_, a, _) =>
            {
                a.Get<Paragraph>("paragraph").ListFormat.RemoveNumbers();
                return new ActionResult(null, a.AddressOf("paragraph"));
            },
        });
        table.Action(new TableAction<Document>
        {
            Name = "insert_break",
            Description = "DocumentBuilder.MoveTo(paragraph), which puts the cursor at the paragraph's end, then DocumentBuilder.InsertBreak(breakType); a section break starts a new section with an empty paragraph. Returns nothing.",
            Arguments =
            [
                Argument.Address("paragraph", WordsTable.ParagraphKind, "The paragraph at whose end the break goes."),
                Argument.Of("breakType", ValueKinds.Enum<BreakType>(), "The engine's BreakType."),
            ],
            Run = static (d, a, _) =>
            {
                var builder = new DocumentBuilder(d);
                builder.MoveTo(a.Get<Paragraph>("paragraph"));
                builder.InsertBreak(a.Get<BreakType>("breakType"));
                return new ActionResult(null);
            },
        });
        table.Action(new TableAction<Document>
        {
            Name = "set_column_count",
            Description = "Section.PageSetup.TextColumns.SetCount(count). Returns the section's address.",
            Arguments =
            [
                Argument.Address("section", WordsTable.SectionKind, "The section."),
                Argument.Of("count", ValueKinds.Integer, "The number of text columns."),
            ],
            Run = static (_, a, _) =>
            {
                a.Get<Section>("section").PageSetup.TextColumns.SetCount(a.Get<int>("count"));
                return new ActionResult(null, a.AddressOf("section"));
            },
        });
    }

    private static TableAction<Document> Call(string name, string description, Action<Document> call) => new()
    {
        Name = name,
        Description = description,
        Run = (d, _, _) =>
        {
            call(d);
            return new ActionResult(null);
        },
    };

    private static ActionResult Replaced(int count) => new(JsonValue.Create(count));

    private static Aspose.Words.Range RangeOf(Document document, Arguments arguments) =>
        arguments.Has("node") ? arguments.Get<Node>("node").Range : document.Range;

    private static FindReplaceOptions FindReplace(Arguments a)
    {
        var options = new FindReplaceOptions();
        if (a.Has("matchCase"))
        {
            options.MatchCase = a.Get<bool>("matchCase");
        }

        if (a.Has("findWholeWordsOnly"))
        {
            options.FindWholeWordsOnly = a.Get<bool>("findWholeWordsOnly");
        }

        if (a.Has("ignoreDeleted"))
        {
            options.IgnoreDeleted = a.Get<bool>("ignoreDeleted");
        }

        if (a.Has("ignoreInserted"))
        {
            options.IgnoreInserted = a.Get<bool>("ignoreInserted");
        }

        if (a.Has("ignoreFields"))
        {
            options.IgnoreFields = a.Get<bool>("ignoreFields");
        }

        if (a.Has("ignoreFieldCodes"))
        {
            options.IgnoreFieldCodes = a.Get<bool>("ignoreFieldCodes");
        }

        if (a.Has("ignoreFootnotes"))
        {
            options.IgnoreFootnotes = a.Get<bool>("ignoreFootnotes");
        }

        return options;
    }

    private static Regex Pattern(string pattern)
    {
        try
        {
            return new Regex(pattern);
        }
        catch (ArgumentException invalid)
        {
            throw TableErrors.InvalidArguments($"pattern is no .NET regular expression: {invalid.Message}");
        }
    }

    private static string Segment(Node node) => node switch
    {
        Paragraph => "paragraph",
        Table => "table",
        WordRow => "row",
        Cell => "cell",
        Run => "run",
        StructuredDocumentTag => "sdt",
        _ => throw new InvalidOperationException($"A {node.NodeType} node was cloned, which clone does not take."),
    };
}
