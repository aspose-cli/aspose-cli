using System.CommandLine;
using Aspose.Cli.Host.Invocation;
using Aspose.Cli.Sdk.Contracts;

namespace Aspose.Cli.Host.Commands;

/// <summary>
/// <c>aspose-cli schema [id]</c>: without an id the catalog of products, commands and document ids;
/// with one the document it names. A JSON Schema, or a part of an object table's schema, is
/// printed raw; any other document is a result envelope.
/// </summary>
internal static class SchemaCommand
{
    public static Command Create(
        CommandExecutor executor,
        Lazy<SchemaDirectory> directory,
        GlobalOptions globals)
    {
        ArgumentNullException.ThrowIfNull(directory);
        var idArgument = new Argument<string?>("id")
        {
            Description = "Id of the document to print: one the catalog lists, or a part of an object table's schema that its directory names; omit it for the catalog.",
            Arity = ArgumentArity.ZeroOrOne,
        }.WithInput(InputKind.None);
        var schema = new Command(
            "schema",
            "Print the catalog of products, commands and document ids, or the document an id names.");
        schema.Arguments.Add(idArgument);
        schema.SetAction(parseResult =>
        {
            string? id = parseResult.GetValue(idArgument);
            if (string.IsNullOrEmpty(id))
            {
                return executor.RunLightweight(parseResult, globals, (_, _) => directory.Value.Catalog);
            }

            return directory.Value.TryGetDocument(id, out ResultEnvelope? document)
                ? executor.RunLightweight(parseResult, globals, (_, _) => document!)
                : executor.RunRaw(parseResult, globals, () => directory.Value.ReadSchema(id), json: true);
        });

        return schema;
    }
}
