using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Aspose.Cli.Tests;

/// <summary>
/// scripts/test.ps1 runs a test project when a repository file it lists as a RepositoryInput
/// changes. Documentation and repository metadata outside the projects reach no project
/// otherwise, so a test that reads one without listing it would not run when it changes.
/// </summary>
public sealed partial class RepositoryInputTests
{
    private static readonly string Root = RepositoryPaths.Root;

    /// <summary>
    /// Literals that name a documentation file but do not read the repository's, by test source
    /// and literal, with the reason; an entry that no longer matches a literal fails the check.
    /// </summary>
    private static readonly Dictionary<(string Source, string Literal), string> NotRead = new()
    {
        [("tests/Aspose.Cli.Tests/CodeHealth/CodeHealthReportTests.cs", ".gitignore")] =
            "Writes a .gitignore into the temporary git repository the test builds, not the repository's.",
        [("tests/Aspose.Cli.Tests/CodeHealth/CodeHealthReportTests.cs", ".gitattributes")] =
            "Writes a .gitattributes into the temporary git repository the test builds, not the repository's.",
    };

    /// <summary>
    /// Every documentation or metadata file that a test project's sources name, by its repository
    /// path or its file name, is one of the project's RepositoryInput items.
    /// </summary>
    /// <remarks>
    /// Only regular string literals that hold a whole repository path or file name are read. The
    /// check does not see a name in an interpolated, verbatim or raw string literal, a path built
    /// from partial segments (such as ".github" and "workflows" without the file name), a name
    /// computed at run time, or a file the TestKit reads on a project's behalf.
    /// </remarks>
    [Fact]
    public void EveryDocumentationFileATestNamesIsARepositoryInputOfItsProject()
    {
        string[] documents = [.. DocumentationFiles()];
        Assert.NotEmpty(documents);

        var undeclared = new List<string>();
        var excused = new HashSet<(string, string)>();
        foreach (string project in TestProjects())
        {
            string directory = Path.GetDirectoryName(project)!;
            HashSet<string> declared = [.. XDocument.Load(project).Descendants("RepositoryInput")
                .Select(item => Relative(Path.GetFullPath(Path.Combine(directory, (string)item.Attribute("Include")!))))];
            // This file names the kinds of documentation file, not a file it reads.
            foreach (string source in Sources(directory).Where(static path => Path.GetFileName(path) != nameof(RepositoryInputTests) + ".cs"))
            {
                foreach (string literal in Literals(File.ReadAllText(source)))
                {
                    string[] named = [.. documents.Where(document => Names(document, literal))];
                    if (named.Length != 0 && NotRead.ContainsKey((Relative(source), literal)))
                    {
                        excused.Add((Relative(source), literal));
                        continue;
                    }
                    if (named.Length != 0 && !named.Any(declared.Contains))
                    {
                        undeclared.Add($"{Relative(source)} names \"{literal}\" ({string.Join(" or ", named)}), which {Path.GetFileName(project)} does not list as a RepositoryInput.");
                    }
                }
            }
        }

        undeclared.AddRange(NotRead.Keys.Where(key => !excused.Contains(key))
            .Select(static key => $"The exemption of \"{key.Literal}\" in {key.Source} no longer matches a literal there; delete it."));
        Assert.True(undeclared.Count == 0, string.Join(Environment.NewLine, undeclared.Distinct(StringComparer.Ordinal)));
    }

    // The tracked files scripts/test.ps1 (Test-DocumentationPath) treats as documentation and
    // repository metadata: *.md, .github/, LICENSE*, .gitignore and .gitattributes outside src/
    // and tests/. git lists them, so build output and untracked files never count.
    private static IEnumerable<string> DocumentationFiles() =>
        TrackedFiles()
            .Where(static path => !path.StartsWith("src/", StringComparison.Ordinal) && !path.StartsWith("tests/", StringComparison.Ordinal))
            .Where(static path =>
            {
                string name = path.Split('/')[^1];
                return path.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
                    || path.StartsWith(".github/", StringComparison.OrdinalIgnoreCase)
                    || name.StartsWith("LICENSE", StringComparison.OrdinalIgnoreCase)
                    || name is ".gitignore" or ".gitattributes";
            })
            .Order(StringComparer.Ordinal);

    private static string[] TrackedFiles()
    {
        string? git = ToolPath.Find("git");
        Assert.True(git is not null, "This check lists the tracked files with git, which PATH does not provide.");
        var start = new ProcessStartInfo(git!)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (string argument in (string[])["-C", Root, "ls-files", "-z"])
        {
            start.ArgumentList.Add(argument);
        }
        using Process process = Process.Start(start)!;
        Task<string> error = process.StandardError.ReadToEndAsync();
        string output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, $"git ls-files failed: {error.Result}");
        return output.Split('\0', StringSplitOptions.RemoveEmptyEntries);
    }

    // Names are compared as written: a CLI word such as "license" is not the LICENSE file.
    private static bool Names(string document, string literal)
    {
        string value = literal.Replace(@"\\", "/", StringComparison.Ordinal).Replace('\\', '/').Trim('/');
        return value.Length != 0
            && (string.Equals(value, document, StringComparison.Ordinal)
                || string.Equals(value, document.Split('/')[^1], StringComparison.Ordinal));
    }

    private static IEnumerable<string> TestProjects() =>
        Directory.EnumerateFiles(Path.Combine(Root, "tests"), "*.csproj", SearchOption.AllDirectories)
            .Where(static path => !IsBuildOutput(path) && Path.GetFileNameWithoutExtension(path) != "Aspose.Cli.TestKit")
            .Order(StringComparer.Ordinal);

    private static IEnumerable<string> Sources(string directory) =>
        Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories).Where(static path => !IsBuildOutput(path));

    private static bool IsBuildOutput(string path) =>
        Relative(path).Split('/').Any(static segment => segment is "bin" or "obj");

    private static IEnumerable<string> Literals(string text) =>
        StringLiteral().Matches(text).Select(static match => match.Groups["value"].Value);

    private static string Relative(string path) => Path.GetRelativePath(Root, path).Replace('\\', '/');

    [GeneratedRegex(@"(?<![@$""])""(?<value>(?:[^""\\\r\n]|\\.)*)""")]
    private static partial Regex StringLiteral();
}
