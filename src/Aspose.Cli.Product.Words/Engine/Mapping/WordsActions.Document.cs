using System.Data;
using System.Text.Json.Nodes;
using Aspose.Cli.Sdk.Tables;
using Aspose.Words;
using Aspose.Words.MailMerging;

namespace Aspose.Cli.Product.Words.Engine.Mapping;

internal static partial class WordsActions
{
    private static void DeclareDocument(DocumentTable<Document> table)
    {
        table.Action(new TableAction<Document>
        {
            Name = "protect",
            Description = "Document.Protect(protectionType, password), or Document.Protect(protectionType) without passwordEnv. Returns nothing.",
            Arguments =
            [
                Argument.Of("protectionType", ValueKinds.Enum<ProtectionType>(), "The engine's ProtectionType."),
                Argument.Optional("passwordEnv", ValueKinds.SecretVariable, "The environment variable that holds the password."),
            ],
            Run = static (d, a, _) =>
            {
                if (a.Has("passwordEnv"))
                {
                    d.Protect(a.Get<ProtectionType>("protectionType"), a.Get<Secret>("passwordEnv").Reveal());
                }
                else
                {
                    d.Protect(a.Get<ProtectionType>("protectionType"));
                }

                return new ActionResult(null);
            },
        });
        table.Action(new TableAction<Document>
        {
            Name = "unprotect",
            Description = "Document.Unprotect(password), or Document.Unprotect() without passwordEnv. Returns the engine's bool: false when the password is not the document's.",
            Arguments = [Argument.Optional("passwordEnv", ValueKinds.SecretVariable, "The environment variable that holds the password.")],
            Run = static (d, a, _) =>
            {
                if (a.Has("passwordEnv"))
                {
                    return new ActionResult(JsonValue.Create(d.Unprotect(a.Get<Secret>("passwordEnv").Reveal())));
                }

                d.Unprotect();
                return new ActionResult(null);
            },
        });
        table.Action(new TableAction<Document>
        {
            Name = "set_text_watermark",
            Description = "Document.Watermark.SetText(text, TextWatermarkOptions), with the options given and the engine's defaults for the rest. Returns nothing.",
            Arguments =
            [
                Argument.Of("text", ValueKinds.Text, "The watermark's text."),
                Argument.Optional("fontFamily", ValueKinds.Text, "TextWatermarkOptions.FontFamily."),
                Argument.Optional("fontSize", ValueKinds.Single, "TextWatermarkOptions.FontSize, in points; 0 sizes it to the page."),
                Argument.Optional("color", ValueKinds.Color, "TextWatermarkOptions.Color."),
                Argument.Optional("isSemitrasparent", ValueKinds.Boolean, "TextWatermarkOptions.IsSemitrasparent, as the engine spells it."),
                Argument.Optional("layout", ValueKinds.Enum<WatermarkLayout>(), "TextWatermarkOptions.Layout."),
            ],
            Run = static (d, a, _) =>
            {
                var options = new TextWatermarkOptions();
                if (a.Has("fontFamily"))
                {
                    options.FontFamily = a.Get<string>("fontFamily");
                }

                if (a.Has("fontSize"))
                {
                    options.FontSize = a.Get<float>("fontSize");
                }

                if (a.Has("color"))
                {
                    options.Color = a.Get<System.Drawing.Color>("color");
                }

                if (a.Has("isSemitrasparent"))
                {
                    options.IsSemitrasparent = a.Get<bool>("isSemitrasparent");
                }

                if (a.Has("layout"))
                {
                    options.Layout = a.Get<WatermarkLayout>("layout");
                }

                d.Watermark.SetText(a.Get<string>("text"), options);
                return new ActionResult(null);
            },
        });
        table.Action(new TableAction<Document>
        {
            Name = "set_image_watermark",
            Description = "Document.Watermark.SetImage(stream, ImageWatermarkOptions), with the options given and the engine's defaults for the rest. Returns nothing.",
            Arguments =
            [
                Argument.Of("image", ValueKinds.Image, "The image file the watermark shows."),
                Argument.Optional("scale", ValueKinds.Number, "ImageWatermarkOptions.Scale; 0 sizes it to the page."),
                Argument.Optional("isWashout", ValueKinds.Boolean, "ImageWatermarkOptions.IsWashout."),
            ],
            Run = static (d, a, _) =>
            {
                var options = new ImageWatermarkOptions();
                if (a.Has("scale"))
                {
                    options.Scale = a.Get<double>("scale");
                }

                if (a.Has("isWashout"))
                {
                    options.IsWashout = a.Get<bool>("isWashout");
                }

                using var image = new MemoryStream(a.Get<byte[]>("image"), writable: false);
                d.Watermark.SetImage(image, options);
                return new ActionResult(null);
            },
        });
        table.Action(Call("remove_watermark", "Document.Watermark.Remove(). Returns nothing.", static d => d.Watermark.Remove()));
        table.Action(new TableAction<Document>
        {
            Name = "append_document",
            Description = "Document.AppendDocument(source, importFormatMode): the source document's sections are added after the last. Returns nothing.",
            Arguments =
            [
                Argument.Of("file", ValueKinds.File, "The document to append, in a format edit reads."),
                Argument.Of("importFormatMode", ValueKinds.Enum<ImportFormatMode>(), "The engine's ImportFormatMode."),
                Argument.Optional("passwordEnv", ValueKinds.SecretVariable, "The environment variable that holds the password of that document."),
            ],
            Run = static (d, a, context) =>
            {
                Document source = context.Open(a.Get<string>("file"), a.Has("passwordEnv") ? a.Get<Secret>("passwordEnv") : null).Document;
                d.AppendDocument(source, a.Get<ImportFormatMode>("importFormatMode"));
                return new ActionResult(null);
            },
        });
        IReadOnlyList<Argument> merge =
        [
            Argument.Of("fieldNames", ValueKinds.ListOf(ValueKinds.Text), "The names of the data's columns, which MERGEFIELD fields name."),
            Argument.Of("rows", WordsValues.TextRows, "The data: one list of strings per record, in the order of fieldNames."),
            Argument.Optional("cleanupOptions", ValueKinds.ListOf(ValueKinds.Enum<MailMergeCleanupOptions>()), "MailMerge.CleanupOptions, the members of the engine's MailMergeCleanupOptions to combine."),
        ];
        table.Action(new TableAction<Document>
        {
            Name = "mail_merge",
            Description = "MailMerge.Execute(DataTable) over a table of strings made of fieldNames and rows, after MailMerge.CleanupOptions is set when given: the engine repeats the whole document once per record. Returns nothing.",
            Arguments = merge,
            Run = static (d, a, _) =>
            {
                using DataTable data = Data(a, "Data");
                Cleanup(d, a);
                d.MailMerge.Execute(data);
                return new ActionResult(null);
            },
        });
        table.Action(new TableAction<Document>
        {
            Name = "mail_merge_regions",
            Description = "MailMerge.ExecuteWithRegions(DataTable) over a table of strings named tableName, made of fieldNames and rows, after MailMerge.CleanupOptions is set when given: the region TableStart:tableName to TableEnd:tableName repeats once per record. Returns nothing.",
            Arguments = [Argument.Of("tableName", ValueKinds.Text, "The region's name, DataTable.TableName."), .. merge],
            Run = static (d, a, _) =>
            {
                using DataTable data = Data(a, a.Get<string>("tableName"));
                Cleanup(d, a);
                d.MailMerge.ExecuteWithRegions(data);
                return new ActionResult(null);
            },
        });
    }

    /// <summary>The records a merge names, as the engine's DataTable of string columns.</summary>
    private static DataTable Data(Arguments arguments, string name)
    {
        IReadOnlyList<string?> names = arguments.Get<IReadOnlyList<string?>>("fieldNames");
        var data = new DataTable(name) { Locale = System.Globalization.CultureInfo.InvariantCulture };
        foreach (string? column in names)
        {
            data.Columns.Add(column ?? throw TableErrors.InvalidArguments("fieldNames holds null; each is a column's name"), typeof(string));
        }

        int index = 0;
        foreach (IReadOnlyList<string?> row in arguments.Get<IReadOnlyList<IReadOnlyList<string?>>>("rows"))
        {
            if (row.Count != names.Count)
            {
                throw TableErrors.InvalidArguments($"rows[{index}] holds {row.Count} values and fieldNames {names.Count}; a record has a value per field name");
            }

            data.Rows.Add([.. row]);
            index++;
        }

        return data;
    }

    private static void Cleanup(Document document, Arguments arguments)
    {
        if (arguments.Has("cleanupOptions"))
        {
            document.MailMerge.CleanupOptions = arguments.Get<IReadOnlyList<MailMergeCleanupOptions>>("cleanupOptions")
                .Aggregate(MailMergeCleanupOptions.None, static (all, option) => all | option);
        }
    }
}
