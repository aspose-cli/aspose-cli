using Aspose.Cli.Product.Slides.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Aspose.Slides;

namespace Aspose.Cli.Product.Slides.Engine.Mapping;

internal sealed class SlidesPresentationLoader(
    ResourceBudgetLedger resourceBudgets)
{
    public LoadedPresentation Open(string path, string? password)
    {
        InputSizeGuard.Ensure(
            resourceBudgets,
            path,
            InputSizeGuard.ResolveMaxBytes(Environment.GetEnvironmentVariable));

        try
        {
            IPresentationInfo info = PresentationFactory.Instance.GetPresentationInfo(path);
            string format = FormatId(info.LoadFormat);
            if (!SlidesFormats.LoadIds.Contains(format, StringComparer.Ordinal))
            {
                throw Invalid(path, $"detected format is {info.LoadFormat}");
            }

            if (info.IsPasswordProtected)
            {
                if (string.IsNullOrEmpty(password))
                {
                    throw CliErrors.PasswordRequired(path);
                }

                if (!info.CheckPassword(password))
                {
                    throw CliErrors.PasswordInvalid(path);
                }
            }

            var options = new LoadOptions { Password = password };
            var presentation = new Presentation(path, options);
            try
            {
                resourceBudgets.EnsureWithin(
                    SlidesBudgetDomains.Slides,
                    presentation.Slides.Count,
                    "items",
                    "post-load");
                resourceBudgets.EnsureWithin(
                    SlidesBudgetDomains.Shapes,
                    presentation.Slides.Sum(
                        static slide => (long)slide.Shapes.Count),
                    "items",
                    "projection");
            }
            catch
            {
                presentation.Dispose();
                throw;
            }
            return new LoadedPresentation(presentation, format);
        }
        catch (CliException)
        {
            throw;
        }
        catch (InvalidPasswordException)
        {
            throw string.IsNullOrEmpty(password)
                ? CliErrors.PasswordRequired(path)
                : CliErrors.PasswordInvalid(path);
        }
        catch (FileNotFoundException)
        {
            throw CliErrors.FileNotFound(path);
        }
        catch (UnauthorizedAccessException)
        {
            throw CliErrors.FileAccessDenied(path);
        }
        catch (IOException exception)
        {
            if (!File.Exists(path))
            {
                throw CliErrors.FileNotFound(path);
            }

            if (FileAccessProbe.CanOpenForRead(path))
            {
                throw Invalid(path, exception.Message, exception);
            }

            throw CliErrors.FileLocked(path);
        }
        catch (Exception exception) when (
            exception.GetType().Assembly.GetName().Name?.StartsWith("Aspose.Slides", StringComparison.Ordinal) == true
            || exception is ArgumentException or InvalidOperationException)
        {
            throw Invalid(path, exception.Message, exception);
        }
    }

    private static string FormatId(LoadFormat format) => format switch
    {
        LoadFormat.Ppt => "ppt",
        LoadFormat.Pps => "pps",
        LoadFormat.Pptx => "pptx",
        LoadFormat.Ppsx => "ppsx",
        LoadFormat.Odp => "odp",
        LoadFormat.Potx => "potx",
        LoadFormat.Pptm => "pptm",
        LoadFormat.Ppsm => "ppsm",
        LoadFormat.Potm => "potm",
        LoadFormat.Otp => "otp",
        LoadFormat.Ppt95 => "ppt",
        LoadFormat.Pot => "pot",
        LoadFormat.Fodp => "fodp",
        _ => "unknown",
    };

    private static CliException Invalid(string path, string reason, Exception? inner = null) => new(
        ErrorCodes.FileCorrupt,
        $"Input is not a valid supported presentation: {path} ({reason}).",
        hint: $"Use one of: {string.Join(", ", SlidesFormats.LoadIds)}. Verify the file opens in a presentation editor and is not merely renamed.",
        innerException: inner);
}

internal sealed record LoadedPresentation(Presentation Presentation, string FormatId) : IDisposable
{
    public void Dispose() => Presentation.Dispose();
}
