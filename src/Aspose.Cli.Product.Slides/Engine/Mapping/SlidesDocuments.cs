using Aspose.Cli.Sdk.Formats;
using Aspose.Cli.Sdk.IO.Output;
using Aspose.Cli.Sdk.Tables;
using Aspose.Slides;
using Aspose.Slides.Warnings;

namespace Aspose.Cli.Product.Slides.Engine.Mapping;

/// <summary>
/// How the object table opens and saves a presentation: the loader's open, and a save in the
/// format the presentation was read in, with the password protection decides. Every warning the
/// engine reports through its callback goes to the invocation's log as it was reported.
/// </summary>
internal sealed class SlidesDocuments(SlidesPresentationLoader loader) : ITableDocuments<Presentation>
{
    /// <inheritdoc />
    public DocumentTable<Presentation> Table => SlidesTable.Definition;

    /// <inheritdoc />
    public IReadOnlyList<FormatDescriptor> Formats => SlidesFormats.Editable;

    /// <inheritdoc />
    public IOpenedDocument<Presentation> Open(string path, Secret? password, EngineWarningLog warnings) =>
        loader.OpenTable(path, password, warnings);

    /// <inheritdoc />
    public IOpenedDocument<Presentation> Reopen(string path, Secret? password, EngineWarningLog warnings) =>
        loader.ReopenTable(path, password, warnings);

    /// <inheritdoc />
    public void Save(IOpenedDocument<Presentation> document, string path, string format, OutputProtection protection, EngineWarningLog warnings)
    {
        Presentation presentation = document.Document;
        SlidesOutputProtection.Protect(presentation, protection);
        SlidesExport.Save(presentation, path, SlidesEngineFormats.SaveFormatOf(format), warnings);
        ((LoadedPresentation)document).Resources.ThrowIfFailed();
    }
}

/// <summary>Passes each warning Aspose.Slides reports to the log, as it reported it, and lets the engine go on.</summary>
internal sealed class SlidesWarnings(EngineWarningLog log) : IWarningCallback
{
    public ReturnAction Warning(IWarningInfo warning)
    {
        log.Add(Enum.GetName(warning.WarningType) ?? warning.WarningType.ToString(), warning.Description);
        return ReturnAction.Continue;
    }
}
