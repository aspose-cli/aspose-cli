using System.Globalization;
using Aspose.Cli.Host.Invocation;
using Aspose.Cli.Host.LocalServices;

namespace Aspose.Cli.Host.App;

/// <summary>Small bounded application log; callers pass path-free messages.</summary>
internal sealed class AppLog
{
    private const long MaxBytes = 512 * 1024;
    private readonly object _gate = new();
    private readonly string _path;

    public AppLog(string path)
    {
        _path = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
    }

    public void Write(string message)
    {
        string oneLine = DiagnosticRedactor.Redact(
            message.ReplaceLineEndings(" ").Trim());
        lock (_gate)
        {
            try
            {
                string timestamp = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
                UserTextFile.AppendLine(
                    _path,
                    $"{timestamp} {oneLine}",
                    MaxBytes);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            try
            {
                File.Delete(_path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
        }
    }
}
