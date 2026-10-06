using System.CommandLine;
using System.CommandLine.Parsing;
using System.Text.Json.Nodes;
using Aspose.Cli.Sdk;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Extensibility.Commanding;
using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Host.Invocation;

/// <summary>
/// Explains a <c>FILE_CORRUPT</c> from a product command: when the file's content looks like a
/// format another product reads, such as a Word document renamed to <c>.pdf</c>, the error becomes
/// the <c>FORMAT_MISMATCH</c> that generic routing reports for the same file, names that product in
/// <c>details.detected</c>, and its hint points to that product's command. The product's own
/// message stays; content no product recognizes, or in a format this product reads, leaves the
/// error unchanged. A command with several inputs is explained only when the product names the
/// failing file in <c>details.path</c>.
/// </summary>
internal static class CorruptInputDetection
{
    public static Exception Explain(
        Exception exception,
        ParseResult parseResult,
        GlobalValues globals,
        ProductCatalog catalog)
    {
        if (exception is not CliException error || error.Code != ErrorCodes.FileCorrupt)
        {
            return exception;
        }

        CommandResult? productCommand = ProductCommand(parseResult.CommandResult);
        string? productId = productCommand?.Command.Policy().ProductId;
        if (productId is null || CorruptFile(error, parseResult, globals) is not { } path)
        {
            return exception;
        }

        // A format this product reads, such as a damaged PDF given to 'words convert', is the
        // product's own corrupt input, not a renamed file.
        ProductDefinition product = catalog.Get(productId);
        FileDetection[] detected = new ProductFileRouter(catalog).DetectAsync(path).AsTask().GetAwaiter().GetResult()
            .Where(detection => detection.ProductId != productId
                && (detection.FormatId is null || product.Formats.Named(FormatUse.Input, detection.FormatId) is null))
            .ToArray();
        if (detected.Length == 0)
        {
            return exception;
        }

        JsonObject details = error.Details?.DeepClone().AsObject() ?? [];
        details["path"] ??= path;
        details["declared"] = productId;
        details["detected"] = new JsonArray(detected.Select(static item => JsonValue.Create(item.ProductId)).ToArray());
        return new CliException(
            ErrorCodes.FormatMismatch,
            error.Message,
            hint: Hint(detected, productCommand!, parseResult.CommandResult, path),
            details: details,
            docs: error.Docs,
            innerException: error.InnerException);
    }

    private static string Hint(
        IReadOnlyList<FileDetection> detected,
        CommandResult productCommand,
        CommandResult command,
        string path)
    {
        string fileName = Path.GetFileName(path);
        if (detected.Count > 1)
        {
            return $"The content of '{fileName}' looks like a document of another product: "
                + $"{string.Join(", ", detected.Select(static item => item.ProductId))}. Use that product's commands, "
                + "or give the file its real extension.";
        }

        FileDetection match = detected[0];
        string format = match.FormatId is null ? match.ProductId : $"{match.ProductId} ({match.FormatId})";
        string? sameCommand = SameCommand(productCommand, command, match.ProductId);
        string next = sameCommand is null
            ? $"Use the '{DistributionInfo.CommandName} {match.ProductId}' commands for it"
            : $"Use '{sameCommand}' for it";
        return $"The content of '{fileName}' looks like a {format} document, not a file this command reads. "
            + $"{next}, or give the file its real extension.";
    }

    // The detected product's command at the same path, such as 'words inspect' for 'pdf inspect'.
    private static string? SameCommand(CommandResult productCommand, CommandResult command, string productId)
    {
        var names = new Stack<string>();
        for (CommandResult? current = command; current is not null && current != productCommand;
            current = current.Parent as CommandResult)
        {
            names.Push(current.Command.Name);
        }

        Command? target = (productCommand.Parent as CommandResult)?.Command.Subcommands
            .FirstOrDefault(candidate => candidate.Policy().ProductId == productId);
        foreach (string name in names)
        {
            target = target?.Subcommands.FirstOrDefault(candidate => candidate.Name == name);
        }
        return target is null
            ? null
            : string.Join(' ', [DistributionInfo.CommandName, productId, .. names]);
    }

    private static CommandResult? ProductCommand(CommandResult command)
    {
        for (CommandResult? current = command; current is not null; current = current.Parent as CommandResult)
        {
            if (current.Command.Policy().ProductId is not null)
            {
                return current;
            }
        }
        return null;
    }

    // The file the product named, or else the command's only input file.
    private static string? CorruptFile(CliException error, ParseResult parseResult, GlobalValues globals)
    {
        var paths = new PathResolver(globals.WorkDir ?? Directory.GetCurrentDirectory());
        if (error.Details?["path"] is JsonValue named
            && named.TryGetValue(out string? reported)
            && !string.IsNullOrWhiteSpace(reported))
        {
            // A relative path is the product's, so it is relative to --workdir like the inputs.
            string full = paths.ResolveOutput(reported);
            return File.Exists(full) ? full : null;
        }

        string[] inputs = parseResult.DeclaredParameters()
            .Where(static parameter => parameter.Metadata.InputKind == InputKind.File)
            .SelectMany(static parameter => parameter.TextValues())
            .Where(static value => !string.IsNullOrWhiteSpace(value) && value != "-")
            .Select(paths.ResolveOutput)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return inputs.Length == 1 && File.Exists(inputs[0]) ? inputs[0] : null;
    }
}
