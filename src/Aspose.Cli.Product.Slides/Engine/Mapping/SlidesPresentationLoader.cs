using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO.Budgets;
using Aspose.Cli.Sdk.IO.Files;
using Aspose.Cli.Sdk.IO.Input;
using Aspose.Cli.Sdk.Results;
using Aspose.Cli.Sdk.Tables;
using Aspose.Slides;

namespace Aspose.Cli.Product.Slides.Engine.Mapping;

/// <summary>
/// Opens presentations as Aspose.Slides loads them, under the document resource policy and the
/// invocation's budgets, passing every warning the engine reports to the invocation's log.
/// </summary>
internal sealed class SlidesPresentationLoader
{
    private readonly ResourceBudgetLedger _resourceBudgets;
    private readonly OutputPipeline _outputs;

    /// <param name="resourceBudgets">The invocation's resource budgets.</param>
    /// <param name="outputs">The write pipeline, which learns whether each opened input is encrypted.</param>
    internal SlidesPresentationLoader(ResourceBudgetLedger resourceBudgets, OutputPipeline outputs)
    {
        _resourceBudgets = resourceBudgets ?? throw new ArgumentNullException(nameof(resourceBudgets));
        _outputs = outputs ?? throw new ArgumentNullException(nameof(outputs));
    }

    /// <summary>
    /// How a presentation input that does not load is reported: Aspose.Slides raises
    /// <see cref="InvalidPasswordException"/> for a missing or wrong password, and its own
    /// exceptions, <see cref="ArgumentException"/> or <see cref="InvalidOperationException"/>
    /// for bytes it cannot read.
    /// </summary>
    internal static readonly InputLoading Loading = new(
        "supported presentation",
        Classify);

    private static LoadFailureKind Classify(Exception exception) => exception switch
    {
        InvalidPasswordException => LoadFailureKind.Password,
        ArgumentException or InvalidOperationException => LoadFailureKind.Corrupt,
        _ when exception.GetType().Assembly.GetName().Name?.StartsWith("Aspose.Slides", StringComparison.Ordinal) == true
            => LoadFailureKind.Corrupt,
        _ => LoadFailureKind.Other,
    };

    /// <summary>Opens a user input with the password its options give, if any.</summary>
    public LoadedPresentation OpenTable(string path, Secret? password, EngineWarningLog warnings) =>
        Open(path, PasswordSource.Option(password), warnings);

    /// <summary>Opens a user input, such as a template, with the password <paramref name="password"/> gives.</summary>
    public LoadedPresentation Open(string path, PasswordSource password, EngineWarningLog warnings) =>
        InputOpening.Open(_outputs, _resourceBudgets, path, () => OpenCore(path, password, warnings));

    /// <summary>Opens an output this invocation just saved, to read it back; its size is bounded by publication.</summary>
    internal LoadedPresentation ReopenTable(string path, Secret? password, EngineWarningLog warnings) =>
        InputOpening.OpenPublished(() => OpenCore(path, PasswordSource.Option(password), warnings));

    private LoadedPresentation OpenCore(string path, PasswordSource passwordSource, EngineWarningLog warnings)
    {
        string? password = passwordSource.Password?.Reveal();
        FileStream? source = null;
        try
        {
            // One read that lets another process replace the file, as an in-place edit publishes;
            // the presentation may keep reading media from it while it is open.
            source = InputFiles.OpenRead(path);
            IPresentationInfo info;
            try
            {
                info = PresentationFactory.Instance.GetPresentationInfo(source);
            }
            catch (Exception exception) when (Classify(exception) == LoadFailureKind.Corrupt)
            {
                // The engine cannot tell the format of what it read, which a package cut short
                // or damaged gives: say so rather than repeat its "Unknown file format".
                throw Loading.Unreadable(path, "its content is incomplete or damaged, so no presentation format can be read from it", exception);
            }

            // Content the engine recognizes as no format at all, such as text or random bytes,
            // is named so rather than by the engine's "Unknown".
            if (info.LoadFormat == LoadFormat.Unknown)
            {
                throw Loading.Unreadable(path, "its content is not in any supported presentation format");
            }

            source.Position = 0;
            string format = SlidesEngineFormats.IdOf(info.LoadFormat);
            if (!SlidesFormats.LoadIds.Contains(format, StringComparer.Ordinal))
            {
                throw Loading.Unloadable(path, info.LoadFormat.ToString());
            }

            if (info.IsPasswordProtected && (password is null || !info.CheckPassword(password)))
            {
                throw InputLoading.PasswordRefused(path, passwordSource);
            }

            // Engine code creates presentations only here: without a resource policy, rendering
            // or saving fetches linked media from any address.
            var resources = SlidesResourcePolicy.Beside(path, _resourceBudgets);
            Presentation presentation;
            try
            {
                presentation = new Presentation(source, new LoadOptions
                {
                    Password = password,
                    ResourceLoadingCallback = resources,
                    WarningCallback = new SlidesWarnings(warnings),
                });
            }
            catch
            {
                resources.Dispose();
                throw;
            }

            try
            {
                _resourceBudgets.EnsureWithin(SlidesBudgetDomains.Slides, presentation.Slides.Count, "items", "post-load");
                _resourceBudgets.EnsureWithin(
                    SlidesBudgetDomains.Shapes,
                    presentation.Slides.Sum(static slide => (long)slide.Shapes.Count),
                    "items",
                    "projection");
            }
            catch
            {
                presentation.Dispose();
                resources.Dispose();
                throw;
            }

            Stream held = source;
            source = null;
            return new LoadedPresentation(presentation, format, resources)
            {
                Encrypted = presentation.ProtectionManager.IsEncrypted,
                Source = held,
            };
        }
        catch (Exception exception) when (exception is not CliException and not OperationCanceledException)
        {
            throw Loading.Failure(exception, path, passwordSource);
        }
        finally
        {
            source?.Dispose();
        }
    }
}

/// <summary>A presentation Aspose.Slides opened, with the resource policy it loads under and the file it reads from.</summary>
internal sealed record LoadedPresentation(Presentation Presentation, string FormatId, SlidesResourcePolicy Resources)
    : IOpenedDocument<Presentation>
{
    /// <inheritdoc />
    public Presentation Document => Presentation;

    /// <inheritdoc />
    public IReadOnlyList<Warning> Warnings => Resources.Warning is { } omitted ? [omitted] : [];

    /// <summary>Whether the file is encrypted, as it was opened; a later encryption does not change it.</summary>
    public bool Encrypted { get; init; } = Presentation.ProtectionManager.IsEncrypted;

    /// <inheritdoc />
    public InputProtection Protection => new(Encrypted);

    /// <summary>The input file the presentation was read from, held until it is disposed.</summary>
    public Stream? Source { get; init; }

    public void Dispose()
    {
        try { Presentation.Dispose(); }
        finally
        {
            Resources.Dispose();
            Source?.Dispose();
        }
    }
}
