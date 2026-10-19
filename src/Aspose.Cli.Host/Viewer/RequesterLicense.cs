using Aspose.Cli.Host.LocalServices;
using Aspose.Cli.Sdk;

namespace Aspose.Cli.Host.Viewer;

/// <summary>
/// Where the command that opens a document finds its license: its working directory, which
/// holds a project license file, and the license variables of its environment, a path or
/// license content carried as secrets. The service outlives the shell that started it, so a
/// render resolves the license from this alone, never from the service's own environment.
/// </summary>
internal sealed record RequesterLicense
{
    public required string WorkDirectory { get; init; }

    /// <summary>The set license variables by name.</summary>
    public IReadOnlyDictionary<string, WireSecret> Variables { get; init; } = new Dictionary<string, WireSecret>();

    /// <summary>Reads the license variables a command sees.</summary>
    public static RequesterLicense Capture(
        string workDirectory, IEnumerable<string> names, Func<string, string?> readEnvironment)
    {
        var variables = new Dictionary<string, WireSecret>(StringComparer.OrdinalIgnoreCase);
        foreach (string name in names)
        {
            if (readEnvironment(name) is { Length: > 0 } value) { variables[name] = new WireSecret(value); }
        }
        return new RequesterLicense { WorkDirectory = Path.GetFullPath(workDirectory), Variables = variables };
    }

    /// <summary>The value of one license variable, or null when the command did not set it.</summary>
    public string? Read(string name) =>
        Variables.FirstOrDefault(pair => string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase)).Value?.Reveal();

    /// <summary>Whether two commands resolve their license from the same sources.</summary>
    public static bool Same(RequesterLicense? left, RequesterLicense? right) =>
        left is null || right is null
            ? left is null && right is null
            : FilePaths.Same(left.WorkDirectory, right.WorkDirectory)
                && left.Variables.Count == right.Variables.Count
                && left.Variables.All(pair => right.Read(pair.Key) is { } value && value == pair.Value.Reveal());
}
