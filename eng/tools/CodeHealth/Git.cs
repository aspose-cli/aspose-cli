using System.Diagnostics;
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
        string output = Run(directory, ["rev-parse", "--show-toplevel"]);
        return Path.GetFullPath(output.Trim());
    }

    /// <summary>
    /// The <c>.cs</c> files under <paramref name="directory"/> as committed in <paramref name="revision"/>:
    /// every blob of the tree, whatever the export attributes that <c>git archive</c> would apply.
    /// </summary>
    public IReadOnlyList<(string Path, string Text)> ReadTree(string revision, string directory)
    {
        // Resolve first, so a bad revision reports git's own message rather than a tree error.
        string commit = Run(root, ["rev-parse", "--verify", "--end-of-options", revision + "^{commit}"]).Trim();
        List<(string Path, string Blob)> blobs = [];
        foreach (string entry in Run(root, ["-c", "core.quotePath=false", "ls-tree", "-r", "-z", "--full-tree", commit, "--", directory])
            .Split('\0', StringSplitOptions.RemoveEmptyEntries))
        {
            // <mode> SP <type> SP <object> TAB <path>; a symbolic link (mode 120000) is not source.
            int tab = entry.IndexOf('\t', StringComparison.Ordinal);
            string[] header = entry[..tab].Split(' ');
            string path = entry[(tab + 1)..];
            if (header[1] == "blob" && header[0] != "120000" && path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            {
                blobs.Add((path, header[2]));
            }
        }
        return [.. blobs.Zip(ReadBlobs([.. blobs.Select(blob => blob.Blob)]), (blob, text) => (blob.Path, text))];
    }

    /// <summary>The <c>.cs</c> files under <paramref name="directory"/> that git tracks, repository-relative with <c>/</c>.</summary>
    public IReadOnlyList<string> TrackedFiles(string directory) =>
        [.. Run(root, ["-c", "core.quotePath=false", "ls-files", "-z", "--full-name", "--", directory])
            .Split('\0', StringSplitOptions.RemoveEmptyEntries)
            .Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))];

    /// <summary>The UTF-8 text of each blob, in order, read by one <c>git cat-file --batch</c>.</summary>
    private List<string> ReadBlobs(IReadOnlyList<string> objects)
    {
        List<string> texts = [];
        if (objects.Count == 0)
        {
            return texts;
        }
        ProcessStartInfo start = new("git")
        {
            WorkingDirectory = root,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.ArgumentList.Add("cat-file");
        start.ArgumentList.Add("--batch");
        using Process process = Process.Start(start) ?? throw new InvalidOperationException("git could not be started.");
        Task<string> error = process.StandardError.ReadToEndAsync();
        // Writing on its own task keeps a full output pipe from blocking the requests.
        Task requests = Task.Run(() =>
        {
            foreach (string id in objects)
            {
                process.StandardInput.Write(id + "\n");
            }
            process.StandardInput.Close();
        });
        Stream output = process.StandardOutput.BaseStream;
        UTF8Encoding utf8 = new(false);
        foreach (string id in objects)
        {
            // <object> SP <type> SP <size> LF <contents> LF
            string[] header = ReadLine(output).Split(' ');
            if (header.Length != 3 || header[1] != "blob")
            {
                throw new InvalidOperationException($"git cat-file could not read {id}: {string.Join(' ', header)}");
            }
            byte[] content = new byte[int.Parse(header[2], System.Globalization.CultureInfo.InvariantCulture)];
            output.ReadExactly(content);
            output.ReadByte();
            texts.Add(utf8.GetString(content).TrimStart('﻿'));
        }
        requests.Wait();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"git cat-file --batch failed: {error.Result.Trim()}");
        }
        return texts;
    }

    private static string ReadLine(Stream stream)
    {
        List<byte> line = [];
        for (int value = stream.ReadByte(); value != '\n'; value = stream.ReadByte())
        {
            if (value < 0)
            {
                throw new InvalidOperationException("git cat-file ended early.");
            }
            line.Add((byte)value);
        }
        return Encoding.UTF8.GetString([.. line]);
    }

    /// <summary>
    /// The history of every file under <paramref name="directory"/> changed in <paramref name="range"/>
    /// (any <c>git log</c> revision range), by path as it was in each commit; merges are skipped.
    /// </summary>
    public IReadOnlyDictionary<string, FileHistory> History(string range, string directory)
    {
        string output = Run(root,
            ["-c", "core.quotePath=false", "log", "--no-merges", "--no-renames", "--name-only", "--format=%x00%s", range, "--", directory]);
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

    private static string Run(string directory, IReadOnlyList<string> arguments)
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
        string output = process.StandardOutput.ReadToEnd();
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
