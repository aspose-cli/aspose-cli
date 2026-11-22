using System.Text.Json.Nodes;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Formats;
using Aspose.Cli.Sdk.IO.Output;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;

namespace Aspose.Cli.Sdk.Tables;

/// <summary>The fields every object table edit takes; a product's request derives from it.</summary>
public abstract record TableEditRequest
{
    /// <summary>The document to edit.</summary>
    public required string Input { get; init; }

    /// <summary>The checked operation batch.</summary>
    public required TableBatch Batch { get; init; }

    /// <summary>Where the edited document is published; its format is the input's.</summary>
    public required EditOutput Output { get; init; }

    /// <summary>The password that opens the input.</summary>
    public Secret? Password { get; init; }

    /// <summary>The password the output opens with (<c>--encrypt</c>), or null.</summary>
    public Secret? EncryptPassword { get; init; }

    /// <summary>
    /// The option that gave <see cref="EncryptPassword"/> (<c>--encrypt</c>, <c>--encrypt-env</c>
    /// or <c>--encrypt-stdin</c>), which the refusal of an input format that cannot carry a
    /// password names; null without one.
    /// </summary>
    public string? EncryptOption { get; init; }
}

/// <summary>What an object table edit reports; a product's result states each field.</summary>
public sealed record TableEditOutcome
{
    /// <summary>The edited document.</summary>
    public required InputInfo Input { get; init; }

    /// <summary>The written document.</summary>
    public required OutputInfo Output { get; init; }

    /// <summary>What each operation left, in batch order.</summary>
    public required IReadOnlyList<TableOpReport> Ops { get; init; }

    /// <summary>The inventory counts that changed, each as [before, after].</summary>
    public required IReadOnlyDictionary<string, IReadOnlyList<long>> Counts { get; init; }

    /// <summary>The engine's warnings, or null when it reported none.</summary>
    public IReadOnlyList<EngineWarning>? EngineWarnings { get; init; }

    /// <summary>The CLI's own warnings about the documents, such as left-out external resources, or null.</summary>
    public IReadOnlyList<Warning>? Warnings { get; init; }

    /// <summary>The license mode when the engine ran without a license.</summary>
    public LicenseInfo? License { get; init; }
}

/// <summary>
/// The object table edit: opens the input, applies the batch in order, saves to a staged file,
/// opens that file again with the same engine call and reads each operation's address from it,
/// counts the inventory before and after, closes the input and publishes. Any failure writes
/// nothing; an engine exception is reported as it is, with the operation's position and address.
/// </summary>
public static class TableEdit
{
    /// <summary>Runs one edit.</summary>
    public static TableEditOutcome Run<TDocument>(OutputPipeline<TDocument> outputs, ITableDocuments<TDocument> documents, TableEditRequest request)
        where TDocument : class
    {
        ArgumentNullException.ThrowIfNull(outputs);
        ArgumentNullException.ThrowIfNull(documents);
        ArgumentNullException.ThrowIfNull(request);
        return TableErrors.Guard(documents.Table, "Editing the document", () => RunCore(outputs, documents, request));
    }

    private static TableEditOutcome RunCore<TDocument>(OutputPipeline<TDocument> outputs, ITableDocuments<TDocument> documents, TableEditRequest request)
        where TDocument : class
    {
        // The license is applied before the engine opens anything.
        LicenseState state = outputs.License;
        DocumentTable table = documents.Table;
        var warnings = new EngineWarningLog();
        FileWritePrecondition precondition = FileWritePrecondition.Capture(request.Input);
        IOpenedDocument<TDocument> opened = documents.Open(request.Input, request.Password, warnings);
        var context = new TableActionContext<TDocument>(documents, warnings, request.EncryptPassword is not null);
        bool closed = false;
        try
        {
            InputInfo input = precondition.Admit(opened.FormatId, opened.Protection);
            ResolvedOutput output = InTheInputsFormat(request.Output, documents.Formats, opened.FormatId, request.EncryptOption);

            IReadOnlyDictionary<string, long> before = table.Count(opened.Document);
            IReadOnlyList<Applied> applied = Apply(table, opened.Document, request.Batch, context);
            // An action that encrypted or decrypted the document decides the output's protection.
            OutputProtection protection = context.Protection ?? OutputProtection.For(output.Format, request.EncryptPassword, opened.Protection, request.Password);
            IReadOnlyList<Warning> written = [];
            EditPublication<Report> publication = outputs.PublishEdit(
                output,
                precondition,
                protection.Encrypted,
                temp =>
                {
                    warnings.During = "save";
                    Engine(table, "Saving the document", () => documents.Save(opened, temp, opened.FormatId, protection, warnings));
                },
                candidate =>
                {
                    warnings.During = "reopen";
                    using IOpenedDocument<TDocument> reopened = documents.Reopen(candidate, protection.Password, warnings);
                    // Reading the saved file back is the engine's read: an exception of a row's member is reported as the engine's.
                    return new Report(ReadBack(table, reopened.Document, applied), Changed(before, table.Count(reopened.Document)));
                },
                "table-edit",
                beforeCommit: () =>
                {
                    // The input is closed before the output replaces it, so an in-place edit
                    // leaves no handle of its own on the file it replaced; what the document
                    // policy left out while it was open is known by now.
                    written = [.. opened.Warnings, .. context.Warnings];
                    closed = true;
                    context.Close();
                    opened.Dispose();
                });
            return new TableEditOutcome
            {
                Input = input,
                Output = publication.Output,
                Ops = publication.Verification!.Ops,
                Counts = publication.Verification.Counts,
                EngineWarnings = warnings.Warnings.Count == 0 ? null : warnings.Warnings,
                Warnings = written.Count == 0 ? null : written,
                License = EnvelopeParts.License(state),
            };
        }
        finally
        {
            if (!closed)
            {
                context.Close();
                opened.Dispose();
            }
        }
    }

    /// <summary>Runs an engine call outside any operation, such as the save; what the engine throws is reported as it is.</summary>
    private static void Engine(DocumentTable table, string doing, Action call)
    {
        try
        {
            call();
        }
        catch (Exception exception) when (exception is not CliException and not OperationCanceledException && TableErrors.FromEngine(exception, table.Engine))
        {
            throw TableErrors.EngineDuring(doing, exception);
        }
    }

    /// <summary>
    /// The output in the input's format, the one an edit writes, decided once the input is open:
    /// the output's extension must be one of that format's, and the refusal lists only those; an
    /// output password needs a format that can carry one.
    /// </summary>
    private static ResolvedOutput InTheInputsFormat(EditOutput output, IReadOnlyList<FormatDescriptor> formats, string formatId, string? encryptOption)
    {
        FormatDescriptor format = formats.FirstOrDefault(candidate => string.Equals(candidate.Id, formatId, StringComparison.OrdinalIgnoreCase))
            ?? throw CliErrors.OptionInvalid(output.InPlace ? "--in-place" : "--out", $"the input is {formatId}, a format edit does not write");
        string extension = Path.GetExtension(output.Path);
        if (!format.Extensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            string name = Path.GetFileName(output.Path);
            string has = extension.Length > 1 ? $"has the {extension} extension" : "has no extension";
            string problem = $"{has}, and an edit writes the format of its input, {format.Id}";
            throw output.InPlace
                ? CliErrors.OutputExtensionInvalid("--in-place", output.Path, $"The input '{name}', which --in-place replaces,",
                    problem, format.Extensions, new JsonObject { ["format"] = format.Id })
                : CliErrors.OutputExtensionInvalid("--out", output.Path, $"--out '{name}'", problem, format.Extensions, new JsonObject { ["format"] = format.Id });
        }

        if (encryptOption is not null && !format.Protectable)
        {
            throw CliErrors.Unprotectable(encryptOption, format.Id, [.. formats.Where(static candidate => candidate.Protectable).Select(static candidate => candidate.Id)]);
        }

        return new ResolvedOutput(format, output.Path, output.Overwrite, output.InPlace);
    }

    /// <summary>Applies a batch to a document in memory, in order, stopping at the first failure.</summary>
    internal static IReadOnlyList<Applied> Apply(DocumentTable table, object document, TableBatch batch, object context)
    {
        var applied = new List<Applied>(batch.Count);
        foreach (TableOp op in batch.Ops)
        {
            string? at = null;
            string? row = null;
            try
            {
                applied.Add(op switch
                {
                    SetOp set => Set(table, document, set, ref at, ref row),
                    AddOp add => Add(table, document, add, ref at, ref row),
                    DeleteOp delete => Delete(table, document, delete, ref at),
                    MoveOp move => Move(table, document, move, ref at),
                    DoOp action => Do(table, document, action, context),
                    _ => throw new InvalidOperationException($"Unknown operation {op.GetType().Name}."),
                });
            }
            catch (CliException error) when (error.Details?["index"] is null)
            {
                throw TableErrors.At(op.Index, at, error);
            }
            catch (Exception exception) when (exception is not CliException and not OperationCanceledException && TableErrors.FromEngine(exception, table.Engine))
            {
                throw TableErrors.Engine(op.Index, at, row, exception);
            }
        }

        return applied;
    }

    private static Applied Set(DocumentTable table, object document, SetOp op, ref string? at, ref string? row)
    {
        at = op.Address.Text;
        Located located = AddressResolver.Resolve(table, document, op.Address);
        at = located.Address;
        Write(located.Item, located.Kind, op.Props, located.Address, op.Index, ref row);
        // A row the set wrote may be the object's key, such as a worksheet's name: its address is taken again.
        at = Settled(table, document, located.Placement, located.ParentAddress, located.Parent, located.Item, located.Address);
        return new Applied(op.Index, at, Action: null, Returned: null);
    }

    private static Applied Add(DocumentTable table, object document, AddOp op, ref string? at, ref string? row)
    {
        object parent = document;
        string parentAddress = table.Root.Name;
        ObjectKind parentKind = table.Root;
        if (op.In is { } address)
        {
            at = address.Text;
            Located located = AddressResolver.Resolve(table, document, address);
            (parent, parentAddress, parentKind) = (located.Item, located.Address, located.Kind);
        }

        at = parentAddress;
        if (!op.Adder.Target.IsInstanceOfType(parent))
        {
            string[] takes = [.. table.Adders
                .Where(candidate => candidate.Placement.Parent == parentKind && candidate.Target.IsInstanceOfType(parent))
                .Select(static candidate => candidate.Name)
                .Distinct(StringComparer.Ordinal)];
            throw TableErrors.NotTheParent(op.Index, parentAddress, parentKind.Name, op.Adder.Name, op.Adder.Target.Name, takes);
        }

        // The objects the arguments address, such as the sibling a new object goes before, are found first.
        object created = op.Adder.Create(parent, op.Arguments.Resolve(table, document));
        at = AddressResolver.Below(table, parentAddress, op.Adder.Placement, parent, created);
        Write(created, op.Adder.Placement.Kind, op.Props, at, op.Index, ref row);
        at = Settled(table, document, op.Adder.Placement, parentAddress, parent, created, at);
        return new Applied(op.Index, at, Action: null, Returned: null);
    }

    private static Applied Delete(DocumentTable table, object document, DeleteOp op, ref string? at)
    {
        at = op.Address.Text;
        Located located = AddressResolver.Resolve(table, document, op.Address);
        at = located.Address;
        located.Placement!.Delete!(located.Parent!, located.Item);
        return new Applied(op.Index, located.Address, Action: null, Returned: null);
    }

    private static Applied Move(DocumentTable table, object document, MoveOp op, ref string? at)
    {
        at = op.Address.Text;
        Located located = AddressResolver.Resolve(table, document, op.Address);
        at = located.Address;
        located.Placement!.Move!(located.Parent!, located.Item, op.To);
        at = Settled(table, document, located.Placement, located.ParentAddress, located.Parent, located.Item, located.Address);
        return new Applied(op.Index, at, Action: null, Returned: null);
    }

    /// <summary>
    /// The address of an object after an operation set, added or moved it, taken again from its
    /// key, and looked up in the document: an address that names more than one object is
    /// <c>ADDRESS_AMBIGUOUS</c>, as an engine that lets two objects share a name can leave it.
    /// </summary>
    private static string Settled(DocumentTable table, object document, Placement? placement, string? parentAddress, object? parent, object item, string address)
    {
        if (placement is null)
        {
            return address;
        }

        string settled = AddressResolver.Below(table, parentAddress!, placement, parent!, item);
        TableAddress parsed;
        try
        {
            parsed = TableAddress.Parse(table, settled);
        }
        catch (TableValueException unwritable)
        {
            throw TableErrors.DoesNotFit($"after the operation the object's address would be '{settled}', which no address of the table can be: {unwritable.Message}");
        }

        _ = AddressResolver.Resolve(table, document, parsed);
        return settled;
    }

    private static Applied Do(DocumentTable table, object document, DoOp op, object context)
    {
        // The objects the arguments address are found first, so a missing one is reported as the address's.
        Arguments arguments = op.Arguments.Resolve(table, document);
        ActionResult result = op.Action.Run(document, arguments, context);
        return new Applied(op.Index, result.At, op.Action.Name, result.Returned);
    }

    private static void Write(object item, ObjectKind kind, IReadOnlyList<PropValue> props, string at, int index, ref string? row)
    {
        foreach (PropValue prop in props)
        {
            TableRow? target = kind.RowsOf(item).FirstOrDefault(candidate => candidate.Name == prop.Name && candidate.Settable);
            if (target is null)
            {
                string[] settable = [.. kind.RowsOf(item).Where(static candidate => candidate.Settable).Select(static candidate => candidate.Name)];
                throw TableErrors.Invalid(index, OpsInvalidReasons.UnknownField, $"props.{prop.Name}",
                    $"{at} has no settable row '{prop.Name}'", at, settable);
            }

            row = prop.Name;
            try
            {
                target.Write(item, prop.Value);
            }
            catch (CliException absent) when (absent.Code == ErrorCodes.OpsInvalid && absent.Details?["field"] is null)
            {
                throw TableErrors.Invalid(index, OpsInvalidReasons.InvalidValue, $"props.{prop.Name}", $"{at}, row '{prop.Name}': {absent.Message}", at);
            }
        }

        row = null;
    }

    /// <summary>Reads each operation's address from the saved document.</summary>
    internal static IReadOnlyList<TableOpReport> ReadBack(DocumentTable table, object document, IReadOnlyList<Applied> applied)
    {
        var reports = new List<TableOpReport>(applied.Count);
        foreach (Applied op in applied)
        {
            if (op.At is null)
            {
                reports.Add(new TableOpReport { Do = op.Action, Returned = op.Returned });
                continue;
            }

            Located? located;
            try
            {
                located = AddressResolver.Resolve(table, document, TableAddress.Parse(table, op.At));
            }
            catch (CliException error) when (error.Code == ErrorCodes.AddressNotFound)
            {
                located = null;
            }
            catch (CliException error) when (error.Code == ErrorCodes.AddressAmbiguous)
            {
                // The saved file holds more than one object the address names, as an engine that
                // lets two names differ only in case can leave it: there is no one object to read.
                throw TableErrors.At(op.Index, op.At, error);
            }

            JsonObject? now;
            try
            {
                now = located?.Kind.Read(located.Item);
            }
            catch (RowReadException failure)
            {
                throw TableErrors.RowRead(table, op.Index, op.At, failure);
            }

            reports.Add(new TableOpReport
            {
                At = op.At,
                Do = op.Action,
                Exists = located is not null,
                Now = now,
                Returned = op.Returned,
            });
        }

        return reports;
    }

    /// <summary>The counts that differ, each as [before, after], in the order of <paramref name="after"/> then the ones only before has.</summary>
    internal static IReadOnlyDictionary<string, IReadOnlyList<long>> Changed(IReadOnlyDictionary<string, long> before, IReadOnlyDictionary<string, long> after)
    {
        var changed = new Dictionary<string, IReadOnlyList<long>>(StringComparer.Ordinal);
        foreach (string key in after.Keys.Concat(before.Keys.Where(key => !after.ContainsKey(key))))
        {
            long was = before.GetValueOrDefault(key);
            long now = after.GetValueOrDefault(key);
            if (was != now)
            {
                changed[key] = [was, now];
            }
        }

        return changed;
    }

    /// <summary>One applied operation: its position, the address it left, and an action's name and return value.</summary>
    internal sealed record Applied(int Index, string? At, string? Action, JsonNode? Returned);

    private sealed record Report(IReadOnlyList<TableOpReport> Ops, IReadOnlyDictionary<string, IReadOnlyList<long>> Counts);
}
