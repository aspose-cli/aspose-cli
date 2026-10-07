using System.CommandLine;
using Aspose.Cli.Host.Invocation;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Host.Commands;

/// <summary>
/// Lists schema ids as a normal result envelope. An explicit schema id,
/// including <c>--operation</c>, remains a raw JSON Schema document; this is
/// the same deliberate raw-document contract used by <c>docs</c>.
/// </summary>
internal static class SchemaCommand
{
    public static Command Create(
        CommandExecutor executor,
        HostSchemaCatalog schemas,
        GlobalOptions globals)
    {
        var idArgument = new Argument<string?>("id")
        {
            Description = "Schema id to print; omit it to return the available ids.",
            Arity = ArgumentArity.ZeroOrOne,
        }.WithInput(InputKind.None);
        var operation = new Option<string?>("--operation")
        {
            Description = "Narrow an ops schema to one exact operation id.",
        }.WithInput(InputKind.None);

        var schema = new Command(
            "schema",
            "List schema ids as an envelope, or print one raw JSON Schema document when an id is supplied.");
        schema.Arguments.Add(idArgument);
        schema.Options.Add(operation);

        schema.SetAction(parseResult =>
        {
            string? id = parseResult.GetValue(idArgument);
            string? operationId = parseResult.GetValue(operation);
            IReadOnlyList<string> available = schemas.Ids;
            if (string.IsNullOrEmpty(id))
            {
                return executor.Run(
                    parseResult,
                    globals,
                    _ => operationId is not null
                        ? throw CliErrors.OptionInvalid(
                            "--operation",
                            "requires a schema id",
                            "Pass an ops schema id before --operation.")
                        : new SchemaListResult
                    {
                        Schemas = available,
                    });
            }

            return executor.RunRaw(
                parseResult,
                globals,
                () => ReadRaw(schemas, id, operationId, available));
        });

        return schema;
    }

    private static string ReadRaw(
        HostSchemaCatalog schemas,
        string id,
        string? operationId,
        IReadOnlyList<string> available)
    {
        if (operationId is not null)
        {
            if (!schemas.TryRead(id, out _))
            {
                throw UnknownId(id, available);
            }

            IReadOnlyList<string> operations = schemas.GetOperations(id);
            return schemas.TryReadOperation(id, operationId, out string? selected)
                ? selected
                : throw CliErrors.OptionInvalid(
                    "--operation",
                    operations.Count == 0
                        ? $"schema '{id}' has no indexed operations"
                        : $"unknown operation '{operationId}' for schema '{id}'",
                    operations.Count == 0
                        ? "Use --operation only with a product ops schema."
                        : "Choose an operation advertised by capabilities.",
                    Mistake.Of(operationId, operations));
        }

        return schemas.TryRead(id, out string? document)
            ? document
            : throw UnknownId(id, available);
    }

    private static CliException UnknownId(string id, IReadOnlyList<string> available) => CliErrors.OptionInvalid(
        "schema id",
        $"unknown schema id '{id}'",
        "Choose one of the available schema ids.",
        Mistake.Of(id, available));
}
