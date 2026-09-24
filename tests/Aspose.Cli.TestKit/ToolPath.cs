using Xunit;

namespace Aspose.Cli.TestKit;

/// <summary>Resolves the external tools a test drives the way a shell resolves a command.</summary>
public static class ToolPath
{
    /// <summary>The first <paramref name="name"/> executable in a PATH directory, or null.</summary>
    public static string? Find(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        string file = OperatingSystem.IsWindows() ? name + ".exe" : name;
        return (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(directory => Path.GetFullPath(Path.Combine(directory.Trim('"'), file)))
            .FirstOrDefault(File.Exists);
    }

    /// <summary>Resolves <paramref name="name"/>, or skips the test when PATH does not provide it.</summary>
    public static string Require(string name)
    {
        string? path = Find(name);
        Assert.SkipWhen(path is null, $"Requires {name} on PATH.");
        return path!;
    }
}
