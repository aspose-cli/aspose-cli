using System.CommandLine;
using System.CommandLine.Parsing;
using System.Text.Json.Nodes;
using Aspose.Cli.Sdk;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO.Paths;

namespace Aspose.Cli.Host.Invocation;

/// <summary>
/// Explains a <c>FILE_CORRUPT</c> from a product command where generic routing reports the same
/// file as <c>FORMAT_UNSUPPORTED</c>: the product's error then takes that code and keeps its
/// message and details, which speak in the product's terms. A command with several inputs is
/// explained only when the product names the failing file in <c>details.path</c>.
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

        // Only a document the command takes as an argument is compared with generic routing;
        // a file an option names, such as an image of 'pdf create --from-images', is no
        // document a product routes, so a damaged one stays a corrupt input.
        return IsDocumentArgument(path, parseResult, globals) && RoutedAsUnsupported(path, catalog)
            ? CliErrors.FormatUnsupported(error)
            : exception;
    }

    /// <summary>
    /// Whether generic routing refuses the file as <c>FORMAT_UNSUPPORTED</c>: content no product
    /// recognizes under an extension no product owns as an input, or a format the CLI only
    /// writes, such as <c>.xps</c>. A product command then refuses the file with one code; under an extension a product owns as an input, routing too reports the
    /// product's damaged file.
    /// </summary>
    private static bool RoutedAsUnsupported(string path, ProductCatalog catalog)
    {
        try
        {
            _ = new ProductFileRouter(catalog).ResolveAsync(new FileRouteRequest(path)).AsTask().GetAwaiter().GetResult();
            return false;
        }
        catch (CliException routed)
        {
            return routed.Code == ErrorCodes.FormatUnsupported;
        }
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

    /// <summary>Whether a positional file argument of the command names <paramref name="path"/>.</summary>
    private static bool IsDocumentArgument(string path, ParseResult parseResult, GlobalValues globals)
    {
        var paths = new PathResolver(globals.WorkDir ?? Directory.GetCurrentDirectory());
        return parseResult.DeclaredParameters()
            .Where(static parameter => parameter.Symbol is Argument && parameter.Metadata.InputKind == InputKind.File)
            .SelectMany(static parameter => parameter.TextValues())
            .Where(static value => !string.IsNullOrWhiteSpace(value) && value != "-")
            .Any(value => FilePaths.Same(paths.ResolveOutput(value), path));
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
            .Select(value => paths.ResolveOutput(value))
            .Distinct(FilePaths.Comparer)
            .ToArray();
        return inputs.Length == 1 && File.Exists(inputs[0]) ? inputs[0] : null;
    }
}
