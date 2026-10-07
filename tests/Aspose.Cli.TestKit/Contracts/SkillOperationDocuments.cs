using System.Text;
using System.Text.RegularExpressions;

namespace Aspose.Cli.TestKit;

/// <summary>One operation document a Skill shows, with the file and line it starts on.</summary>
public sealed record SkillOperationDocument(string Source, int Line, string Json)
{
    public string Location => $"{Path.GetRelativePath(RepositoryPaths.Root, Source)}:{Line}";
}

/// <summary>
/// Finds every operation document a Skill directory shows, so the product contract tests can
/// validate each against the generated schema. The rules:
/// <list type="bullet">
/// <item>A <c>*.json</c> file or a fenced <c>json</c> block in Markdown is an operation
/// document when its text holds <c>"ops"</c>, and must then parse. Other <c>json</c> blocks show
/// output fragments (such as one member of a result) and are not read.</item>
/// <item>An <c>--ops</c> argument in a fenced code block (prose and inline code show the syntax
/// with placeholders such as <c>'{...}'</c> and are not read) whose value
/// is quoted (<c>'...'</c> or <c>"..."</c>, possibly over several lines) or a PowerShell here-string
/// (<c>@'...'@</c>, <c>@"..."@</c>) and starts with <c>{</c> or <c>[</c> is an operation document,
/// unquoted with the quoting rules of the enclosing fence (PowerShell for a <c>powershell</c> or
/// <c>pwsh</c> fence, POSIX shell otherwise).</item>
/// <item>An <c>--ops</c> value naming a <c>*.json</c> file is a reference. In a Markdown file
/// under <c>examples/</c>, whose commands run next to their files, the file must exist beside
/// it; elsewhere such names stand for the reader's own file. <c>-</c> (stdin), variables and
/// placeholders are not read.</item>
/// </list>
/// </summary>
public static class SkillOperationDocuments
{
    private static readonly Regex OpsArgument = new(@"--ops[ \t]+", RegexOptions.CultureInvariant);

    /// <summary>Collects the documents under <paramref name="skills"/> and the problems found reading them.</summary>
    public static (IReadOnlyList<SkillOperationDocument> Documents, IReadOnlyList<string> Problems) Collect(string skills)
    {
        var documents = new List<SkillOperationDocument>();
        var problems = new List<string>();
        foreach (string file in Directory.EnumerateFiles(skills, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            if (file.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                string json = File.ReadAllText(file);
                if (json.Contains("\"ops\"", StringComparison.Ordinal))
                {
                    documents.Add(new SkillOperationDocument(file, 1, json));
                }
            }
            else if (file.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            {
                bool example = Path.GetRelativePath(skills, file)
                    .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    .Contains("examples", StringComparer.Ordinal);
                ReadMarkdown(file, File.ReadAllText(file).ReplaceLineEndings("\n"), example, documents, problems);
            }
        }

        return (documents, problems);
    }

    private static void ReadMarkdown(
        string file,
        string markdown,
        bool example,
        List<SkillOperationDocument> documents,
        List<string> problems)
    {
        string[] lines = markdown.Split('\n');
        var lineStarts = new int[lines.Length];
        var fences = new string?[lines.Length];
        string? fence = null;
        for (int index = 0, offset = 0; index < lines.Length; offset += lines[index].Length + 1, index++)
        {
            lineStarts[index] = offset;
            string trimmed = lines[index].TrimStart();
            if (trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                if (fence is null)
                {
                    fence = trimmed[3..].Trim().Split(' ', 2)[0];
                    if (fence == "json")
                    {
                        AddFencedJson(file, lines, index, documents);
                    }
                }
                else
                {
                    fence = null;
                }

                continue;
            }

            fences[index] = fence;
        }

        foreach (Match match in OpsArgument.Matches(markdown))
        {
            int start = match.Index + match.Length;
            int line = Array.BinarySearch(lineStarts, match.Index);
            line = line >= 0 ? line : ~line - 1;
            if (fences[line] is null)
            {
                continue;
            }

            string location = $"{Path.GetRelativePath(RepositoryPaths.Root, file)}:{line + 1}";
            bool powerShell = fences[line] is "powershell" or "pwsh" or "ps1";
            string? value = ReadValue(markdown, start, powerShell, out bool quoted, out string? error);
            if (error is not null)
            {
                problems.Add($"{location}: --ops {error}");
                continue;
            }

            string trimmedValue = value!.Trim();
            if (quoted && (trimmedValue.StartsWith('{') || trimmedValue.StartsWith('[')))
            {
                documents.Add(new SkillOperationDocument(file, line + 1, value));
            }
            else if (trimmedValue.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                && example
                && !File.Exists(Path.Combine(Path.GetDirectoryName(file)!, trimmedValue)))
            {
                problems.Add($"{location}: --ops names '{trimmedValue}', which is not beside this example.");
            }
        }
    }

    private static void AddFencedJson(string file, string[] lines, int fenceLine, List<SkillOperationDocument> documents)
    {
        var block = new StringBuilder();
        for (int index = fenceLine + 1; index < lines.Length && !lines[index].TrimStart().StartsWith("```", StringComparison.Ordinal); index++)
        {
            block.Append(lines[index]).Append('\n');
        }

        string text = block.ToString();
        if (text.Contains("\"ops\"", StringComparison.Ordinal))
        {
            documents.Add(new SkillOperationDocument(file, fenceLine + 2, text));
        }
    }

    /// <summary>Reads one shell word starting at <paramref name="start"/> and removes its quoting.</summary>
    private static string? ReadValue(string text, int start, bool powerShell, out bool quoted, out string? error)
    {
        quoted = false;
        error = null;
        if (start >= text.Length)
        {
            return string.Empty;
        }

        char first = text[start];
        if (first == '@' && start + 2 < text.Length && text[start + 1] is '\'' or '"' && text[start + 2] == '\n')
        {
            quoted = true;
            string terminator = "\n" + text[start + 1] + "@";
            int end = text.IndexOf(terminator, start + 2, StringComparison.Ordinal);
            if (end < 0)
            {
                error = "here-string is not terminated";
                return null;
            }

            return text[(start + 3)..end];
        }

        if (first is '\'' or '"')
        {
            quoted = true;
            var value = new StringBuilder();
            for (int index = start + 1; index < text.Length; index++)
            {
                char current = text[index];
                char next = index + 1 < text.Length ? text[index + 1] : '\0';
                if (first == '\'')
                {
                    if (current != '\'')
                    {
                        value.Append(current);
                    }
                    else if (powerShell && next == '\'')
                    {
                        value.Append('\'');
                        index++;
                    }
                    else if (!powerShell && string.CompareOrdinal(text, index, "'\\''", 0, 4) == 0)
                    {
                        value.Append('\'');
                        index += 3;
                    }
                    else
                    {
                        return value.ToString();
                    }
                }
                else if (powerShell)
                {
                    if (current == '`' && next != '\0')
                    {
                        value.Append(next);
                        index++;
                    }
                    else if (current == '"' && next == '"')
                    {
                        value.Append('"');
                        index++;
                    }
                    else if (current == '"')
                    {
                        return value.ToString();
                    }
                    else
                    {
                        value.Append(current);
                    }
                }
                else
                {
                    if (current == '\\' && next is '"' or '\\' or '$' or '`')
                    {
                        value.Append(next);
                        index++;
                    }
                    else if (current == '\\' && next == '\n')
                    {
                        index++;
                    }
                    else if (current == '"')
                    {
                        return value.ToString();
                    }
                    else
                    {
                        value.Append(current);
                    }
                }
            }

            error = "value is not terminated";
            return null;
        }

        int stop = start;
        while (stop < text.Length && !char.IsWhiteSpace(text[stop]) && text[stop] is not ('`' or ')' or '|' or ';'))
        {
            stop++;
        }

        return text[start..stop];
    }
}
