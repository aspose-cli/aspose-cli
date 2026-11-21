using Aspose.Cli.Product.Words.Engine.Mapping;
using Aspose.Cli.Sdk.IO.Output;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;
using Aspose.Cli.Sdk.Tables;
using Aspose.Words;

namespace Aspose.Cli.Product.Words.Engine;

/// <summary><c>words convert</c>: Document.Save in the target format, with the engine's options for it.</summary>
internal static class WordsConvert
{
    internal static WordsConvertResult Run(WordsSession session, WordsConvertRequest request)
    {
        LicenseState state = session.Outputs.License;
        var log = new EngineWarningLog();
        using LoadedDocument loaded = session.Loader.OpenAny(request.Input, request.Password, request.TextInput, log);
        Document document = loaded.Document;
        IReadOnlyList<int>? pages = request.Pages?.Resolve(document.PageCount);
        OutputProtection protection = OutputProtection.For(request.Output.Format, request.EncryptPassword, loaded.Protection, request.Password);
        PublishedFile written = session.Outputs.Write(request.Output.Path, request.Output.Format, request.Output.Overwrite, temp =>
        {
            log.During = "save";
            document.Save(temp, WordsEngineFormats.Options(request.Output.Format.Id, protection.Password, pages));
            loaded.Resources.ThrowIfFailed();
        }, encrypted: protection.Encrypted);
        return new WordsConvertResult
        {
            Input = FileObjects.Input(request.Input, loaded.FormatId, loaded.Protection.Encrypted),
            Outputs = [new(FileObjects.Output(written, request.Output.Format, encrypted: protection.Encrypted))],
            Pages = request.Pages?.Text,
            EngineWarnings = log.Warnings.Count == 0 ? null : log.Warnings,
            License = EnvelopeParts.License(state),
            Warnings = loaded.Warnings.Count == 0 ? null : loaded.Warnings,
        };
    }
}
