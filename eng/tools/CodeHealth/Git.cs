using System.Diagnostics;
using System.Formats.Tar;
using System.Text;
using System.Text.RegularExpressions;

namespace Aspose.Cli.CodeHealth;

/// <summary>The change history of one file over the analyzed revision range.</summary>
/// <param name="Commits">The commits that changed the file.</param>
/// <param name="Fixes">Those of them whose subject is a <c>fix</c> commit.</param>
internal sealed record FileHistory(int Commits, int Fixes);

/// <summary>Read-only access to a Git repository: it never writes the index, refs or working tree.</summary>
internal sealed partial class Git(string root)
{
    /// <summary>The top-level directory of the repository that contains <paramref name="directory"/>.</summary>
    public static string FindRoot(string directory)
    {
        string output = Run(directory, ["rev-parse", "--show-toplevel"], null);
        return Path.GetFullPath(output.Trim());
    }

    /// <summary>The <c>.cs</c> files under <paramref name="directory"/> as committed in <paramref name="revision"/>.</summary>
    public IReadOnlyList<(string Path, string Text)> ReadTree(string revision, string directory)
    {
        // Resolve first, so a bad revision reports git's own message rather than an archive error.
        string commit = Run(root, ["rev-parse", "--verify", "--end-of-options", revision + "^{commit}"], null).Trim();
        List<(string Path, string Text)> files = [];
        Run(root, ["archive", "--format=tar", commit, "--", directory], stdout =>
        {
            using TarReader reader = new(stdout, leaveOpen: true);
            while (reader.GetNextEntry() is { } entry)
            {
                if (entry.EntryType is not (TarEntryType.RegularFile or TarEntryType.V7RegularFile)
                    || !entry.Name.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                    || entry.DataStream is null)
                {
                    continue;
                }
                using StreamReader text = new(entry.DataStream, Encoding.UTF8);
                files.Add((entry.Name, text.ReadToEnd()));
            }
        });
        return files;
    }

    /// <summary>
    /// The history of every file under <paramref name="directory"/> changed in <paramref name="range"/>
    /// (any <c>git log</c> revision range), by path as it was in each commit; merges are skipped.
    /// </summary>
    public IReadOnlyDictionary<string, FileHistory> History(string range, string directory)
    {
        string output = Run(root,
            ["-c", "core.quotePath=false", "log", "--no-merges", "--no-renames", "--name-only", "--format=%x00%s", range, "--", directory],
            null);
        Dictionary<string, FileHistory> history = new(StringComparer.Ordinal);
        bool fix = false;
        foreach (string line in output.Split('\n'))
        {
            string trimmed = line.TrimEnd('\r');
            if (trimmed.StartsWith('\0'))
            {
                fix = FixSubject().IsMatch(trimmed[1..]);
            }
            else if (trimmed.Length > 0)
            {
                FileHistory current = history.GetValueOrDefault(trimmed, new FileHistory(0, 0));
                history[trimmed] = new FileHistory(current.Commits + 1, current.Fixes + (fix ? 1 : 0));
            }
        }
        return history;
    }

    private static string Run(string directory, IReadOnlyList<string> arguments, Action<Stream>? readStdout)
    {
        ProcessStartInfo start = new("git")
        {
            WorkingDirectory = directory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            StandardOutputEncoding = Encoding.UTF8,
        };
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }
        using Process process = Process.Start(start) ?? throw new InvalidOperationException("git could not be started.");
        Task<string> error = process.StandardError.ReadToEndAsync();
        string output = "";
        if (readStdout is null)
        {
            output = process.StandardOutput.ReadToEnd();
        }
        else
        {
            readStdout(process.StandardOutput.BaseStream);
            process.StandardOutput.BaseStream.CopyTo(Stream.Null);
        }
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"git {string.Join(' ', arguments)} failed: {error.Result.Trim()}");
        }
        return output;
    }

    [GeneratedRegex(@"^fix(\([^)]*\))?!?:", RegexOptions.CultureInvariant)]
    private static partial Regex FixSubject();
}
