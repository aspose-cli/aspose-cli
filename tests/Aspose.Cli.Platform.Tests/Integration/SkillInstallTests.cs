using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Diagnostics;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.IntegrationTests;

/// <summary>Product-neutral black-box coverage for skill discovery and installation.</summary>
public sealed class SkillInstallTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();

    public void Dispose() => _workspace.Dispose();

    [Fact]
    public void SkillListAndInstall_WorkForEveryCompiledProduct()
    {
        CliResult listed = _workspace.Run("skill", "list", "--output", "json");

        Assert.Equal(0, listed.ExitCode);
        JsonArray skills = Parse(listed.StdOut)["skills"]!.AsArray();
        Assert.NotEmpty(skills);
        foreach (string name in skills.Select(static item => item!["name"]!.GetValue<string>()))
        {
            string target = "installed-" + name;
            CliResult installed = _workspace.Run(
                "skill", "install", name, "--target", target, "--output", "json");
            Assert.True(installed.ExitCode == 0, installed.StdErr);
            Assert.True(File.Exists(_workspace.File(Path.Combine(target, name, "SKILL.md"))));
        }
    }

    [Fact]
    public void BundledSkillResources_AreCompleteLinkedDocumentedAndUseKnownCommands()
    {
        CliResult capabilitiesResult = _workspace.Run(
            "capabilities", "--output", "json");
        Assert.Equal(0, capabilitiesResult.ExitCode);
        string[] commandPaths = Parse(capabilitiesResult.StdOut)["commands"]!
            .AsArray()
            .Select(static command => command!["path"]!.GetValue<string>())
            .Where(static path => path.Contains(' '))
            .OrderByDescending(static path => path.Length)
            .ToArray();

        CliResult listed = _workspace.Run("skill", "list", "--output", "json");
        Assert.Equal(0, listed.ExitCode);
        string[] skills = Parse(listed.StdOut)["skills"]!
            .AsArray()
            .Select(static skill => skill!["name"]!.GetValue<string>())
            .ToArray();

        foreach (string skill in skills)
        {
            string target = "contract-" + skill;
            CliResult installed = _workspace.Run(
                "skill", "install", skill, "--target", target,
                "--output", "json");
            Assert.True(installed.ExitCode == 0, installed.StdErr);

            string root = _workspace.File(Path.Combine(target, skill));
            string[] markdownFiles = Directory.GetFiles(
                root,
                "*.md",
                SearchOption.AllDirectories);
            Assert.Contains(
                markdownFiles,
                path => IsUnder(path, Path.Combine(root, "references")));
            Assert.Contains(
                markdownFiles,
                path => IsUnder(path, Path.Combine(root, "examples")));

            foreach (string markdown in markdownFiles)
            {
                string content = File.ReadAllText(markdown);
                AssertLocalLinksResolve(root, markdown, content);
                AssertDocumentedCommandsExist(markdown, content, commandPaths);

                string? topic = DocsTopic(skill, root, markdown);
                if (topic is null)
                {
                    continue;
                }

                CliResult docs = _workspace.Run("docs", topic);
                Assert.True(docs.ExitCode == 0, docs.StdErr);
                Assert.Equal(
                    NormalizeNewLines(content).TrimEnd(),
                    NormalizeNewLines(docs.StdOut).TrimEnd());
            }
        }
    }

    [Theory]
    [InlineData("codex", ".agents")]
    [InlineData("claude-code", ".claude")]
    [InlineData("opencode", ".opencode")]
    public void ProjectScope_UsesTheHostConvention(string host, string hostDirectory)
    {
        string skill = FirstSkill();

        CliResult result = _workspace.Run(
            "skill", "install", skill, "--host", host,
            "--scope", "project", "--dir", "proj", "--output", "json");

        Assert.Equal(0, result.ExitCode);
        Assert.True(File.Exists(_workspace.File(
            Path.Combine("proj", hostDirectory, "skills", skill, "SKILL.md"))));
    }

    [Fact]
    public void SkillList_ReportsAllSupportedHosts()
    {
        CliResult result = _workspace.Run("skill", "list", "--output", "json");

        Assert.Equal(0, result.ExitCode);
        JsonArray skills = Parse(result.StdOut)["skills"]!.AsArray();
        Assert.All(skills, skill => Assert.Equal(3, skill!["hosts"]!.AsArray().Count));
    }

    [Fact]
    public void Install_WritesOwnershipAndRefusesUnsupportedOldManifests()
    {
        string skill = FirstSkill();
        string parent = "managed-skill";
        CliResult first = _workspace.Run(
            "skill", "install", skill, "--target", parent, "--output", "json");
        Assert.Equal(0, first.ExitCode);

        string root = _workspace.File(Path.Combine(parent, skill));
        string manifestPath = Path.Combine(root, ".aspose-skill-manifest.json");
        JsonObject version2 = Parse(File.ReadAllText(manifestPath)).AsObject();
        Assert.Equal(2, version2["schemaVersion"]!.GetValue<int>());
        Assert.Equal("aspose-cli-skill", version2["productId"]!.GetValue<string>());
        JsonArray files = version2["files"]!.AsArray();
        Assert.NotEmpty(files);
        Assert.All(files, item =>
        {
            Assert.False(Path.IsPathRooted(item!["path"]!.GetValue<string>()));
            Assert.Equal(64, item["sha256"]!.GetValue<string>().Length);
        });

        var version1 = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["skill"] = skill,
            ["cliVersion"] = version2["cliVersion"]!.GetValue<string>(),
            ["executable"] = version2["executable"]!.GetValue<string>(),
            ["executableSha256"] = version2["executableSha256"]!.GetValue<string>(),
            ["contentSha256"] = version2["contentSha256"]!.GetValue<string>(),
        };
        File.WriteAllText(manifestPath, version1.ToJsonString());

        CliResult migrated = _workspace.Run(
            "skill", "install", skill, "--target", parent, "--output", "json");
        Assert.NotEqual(0, migrated.ExitCode);
        Assert.Equal(1, Parse(File.ReadAllText(manifestPath))["schemaVersion"]!.GetValue<int>());
        Assert.Equal(version1.ToJsonString(), File.ReadAllText(manifestPath));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Install_RefusesModifiedOrCustomManagedSkillWithoutOverwriting(bool addUnknownFile)
    {
        string skill = FirstSkill();
        string parent = "custom-skill-" + addUnknownFile;
        CliResult first = _workspace.Run(
            "skill", "install", skill, "--target", parent, "--output", "json");
        Assert.Equal(0, first.ExitCode);

        string root = _workspace.File(Path.Combine(parent, skill));
        string changed = addUnknownFile
            ? Path.Combine(root, "CUSTOM.md")
            : Path.Combine(root, "SKILL.md");
        if (addUnknownFile)
        {
            File.WriteAllText(changed, "customer-owned");
        }
        else
        {
            File.AppendAllText(changed, "\ncustomer-owned");
        }
        string before = File.ReadAllText(changed);

        CliResult second = _workspace.Run(
            "skill", "install", skill, "--target", parent, "--output", "json");
        Assert.Equal(5, second.ExitCode);
        Assert.Equal("OUTPUT_EXISTS", Parse(second.StdErr)["error"]!["code"]!.GetValue<string>());
        Assert.Equal(before, File.ReadAllText(changed));
    }

    [Fact]
    public async Task Install_RejectsAReparsePointInTheTargetAncestorChain()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        string skill = FirstSkill();
        string actual = _workspace.File("junction-target");
        string junction = _workspace.File("junction-parent");
        Directory.CreateDirectory(actual);
        var start = new ProcessStartInfo("powershell.exe")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            ErrorDialog = false,
        };
        string createJunction =
            $"New-Item -ItemType Junction -Path '{junction.Replace("'", "''", StringComparison.Ordinal)}' "
            + $"-Target '{actual.Replace("'", "''", StringComparison.Ordinal)}' | Out-Null";
        foreach (string argument in new[]
        {
            "-NoLogo",
            "-NoProfile",
            "-NonInteractive",
            "-Command",
            createJunction,
        })
        {
            start.ArgumentList.Add(argument);
        }

        using (Process process = Process.Start(start)!)
        {
            string output = await process.StandardOutput.ReadToEndAsync();
            string error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15));
            Assert.True(process.ExitCode == 0, output + error);
        }

        try
        {
            Assert.True(
                (File.GetAttributes(junction) & FileAttributes.ReparsePoint) != 0);
            CliResult result = _workspace.Run(
                "skill", "install", skill, "--target", "junction-parent",
                "--output", "json");

            Assert.Equal(5, result.ExitCode);
            JsonNode error = Parse(result.StdErr)["error"]!;
            Assert.Equal("OUTPUT_EXISTS", error["code"]!.GetValue<string>());
            Assert.Contains(
                "reparse-point ancestor",
                error["message"]!.GetValue<string>(),
                StringComparison.OrdinalIgnoreCase);
            Assert.Empty(Directory.EnumerateFileSystemEntries(actual));
        }
        finally
        {
            if (Directory.Exists(junction)
                && (File.GetAttributes(junction) & FileAttributes.ReparsePoint) != 0)
            {
                Directory.Delete(junction);
            }
        }
    }

    [Fact]
    public void Install_RequiresAnExplicitSkill()
    {
        CliResult result = _workspace.Run(
            "skill", "install", "--target", "installed", "--output", "json");

        Assert.Equal(2, result.ExitCode);
        JsonNode error = Parse(result.StdErr)["error"]!;
        Assert.Equal("USAGE_ERROR", error["code"]!.GetValue<string>());
        string problem = Assert.Single(
            error["details"]!["errors"]!.AsArray())!.GetValue<string>();
        Assert.Contains("Required argument missing", problem, StringComparison.Ordinal);
        Assert.Contains("'install'", problem, StringComparison.Ordinal);
    }

    private string FirstSkill()
    {
        CliResult result = _workspace.Run("skill", "list", "--output", "json");
        Assert.Equal(0, result.ExitCode);
        return Parse(result.StdOut)["skills"]![0]!["name"]!.GetValue<string>();
    }

    private static bool IsUnder(string path, string directory)
    {
        string root = Path.GetFullPath(directory)
            .TrimEnd(Path.DirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        return Path.GetFullPath(path).StartsWith(
            root,
            StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertLocalLinksResolve(
        string skillRoot,
        string markdown,
        string content)
    {
        foreach (Match match in Regex.Matches(
            content,
            @"\[[^\]]+\]\((?<target>[^)]+)\)",
            RegexOptions.CultureInvariant))
        {
            string target = match.Groups["target"].Value;
            if (target.StartsWith('#')
                || target.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || target.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                || target.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            target = target.Split('#', 2)[0];
            string resolved = Path.GetFullPath(Path.Combine(
                Path.GetDirectoryName(markdown)!,
                target.Replace('/', Path.DirectorySeparatorChar)));
            Assert.True(
                IsUnder(resolved, skillRoot),
                $"Skill link escapes its package: {markdown} -> {target}");
            Assert.True(
                File.Exists(resolved) || Directory.Exists(resolved),
                $"Broken Skill link: {markdown} -> {target}");
        }
    }

    private static void AssertDocumentedCommandsExist(
        string markdown,
        string content,
        IReadOnlyList<string> commandPaths)
    {
        foreach (string line in NormalizeNewLines(content).Split('\n'))
        {
            string command = line.Trim();
            if (!command.StartsWith("aspose-cli ", StringComparison.Ordinal))
            {
                continue;
            }

            Assert.True(
                commandPaths.Any(path =>
                    string.Equals(command, path, StringComparison.Ordinal)
                    || command.StartsWith(path + " ", StringComparison.Ordinal)),
                $"Skill documents a command path absent from capabilities: {markdown}: {command}");
        }
    }

    private static string? DocsTopic(
        string skill,
        string root,
        string markdown)
    {
        string product = skill["aspose-cli-".Length..];
        string relative = Path.GetRelativePath(root, markdown)
            .Replace(Path.DirectorySeparatorChar, '/');
        if (string.Equals(relative, "SKILL.md", StringComparison.Ordinal))
        {
            return product + "/overview";
        }
        if (relative.StartsWith("references/", StringComparison.Ordinal))
        {
            return product + "/" + relative[
                "references/".Length..(relative.Length - ".md".Length)];
        }
        if (relative.StartsWith("examples/", StringComparison.Ordinal)
            && relative.EndsWith("/README.md", StringComparison.Ordinal))
        {
            return product + "/" + relative[
                "examples/".Length..(relative.Length - "/README.md".Length)];
        }
        return null;
    }

    private static string NormalizeNewLines(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal);

    private static JsonNode Parse(string json) =>
        JsonNode.Parse(json) ?? throw new InvalidOperationException("Output was not JSON:\n" + json);
}
