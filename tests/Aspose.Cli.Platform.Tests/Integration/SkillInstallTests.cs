using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.IntegrationTests;

/// <summary>Product-neutral black-box coverage for skill discovery and installation.</summary>
public sealed class SkillInstallTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();

    public void Dispose() => _workspace.Dispose();

    [Fact]
    public void SkillInstallationSupportsLongLocalPathsThroughTheRealExecutable()
    {
        string skill = FirstSkill();
        string target = Path.Combine(new string('a', 90), new string('b', 90), new string('c', 90));
        Assert.True(_workspace.File(target).Length > 260);
        for (int pass = 0; pass < 2; pass++)
        {
            CliResult installed = _workspace.Run(
                "skill", "install", skill, "--target", target, "--output-format", "json");
            Assert.True(installed.ExitCode == 0, installed.StdErr);
            Assert.True(File.Exists(_workspace.File(Path.Combine(target, skill, "SKILL.md"))));
        }
    }

    [Fact]
    public void BundledSkillResources_AreCompleteLinkedDocumentedAndUseKnownCommands()
    {
        DocumentationCommandValidator commands = DocumentationCommandValidator.Read(_workspace);

        CliResult listed = _workspace.Run("skill", "list", "--output-format", "json");
        Assert.Equal(0, listed.ExitCode);
        string[] skills = Parse(listed.StdOut)["skills"]!
            .AsArray()
            .Select(static skill => skill!["name"]!.GetValue<string>())
            .ToArray();
        Assert.NotEmpty(skills);

        foreach (string skill in skills)
        {
            string target = "contract-" + skill;
            CliResult installed = _workspace.Run(
                "skill", "install", skill, "--target", target,
                "--output-format", "json");
            Assert.True(installed.ExitCode == 0, installed.StdErr);

            string root = _workspace.File(Path.Combine(target, skill));
            string skillFile = Path.Combine(root, "SKILL.md");
            Assert.True(File.Exists(skillFile));
            Assert.True(File.Exists(Path.Combine(root, ".aspose-skill-manifest.json")));
            Assert.Equal(
                Parse(installed.StdOut)["fileCount"]!.GetValue<int>(),
                Directory.GetFiles(root, "*", SearchOption.AllDirectories).Length);
            string[] markdownFiles = Directory.GetFiles(
                root,
                "*.md",
                SearchOption.AllDirectories);
            Assert.Contains(
                markdownFiles,
                path => IsUnder(path, Path.Combine(root, "references")));
            if (skill != PlatformSkill)
            {
                // Product Skills teach through runnable examples; the platform Skill has none.
                Assert.Contains(
                    markdownFiles,
                    path => IsUnder(path, Path.Combine(root, "examples")));
            }

            // docs reads the resources the install extracted, so a document is its docs
            // topic exactly when docs lists the topic and no other document claims it.
            var topics = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string markdown in markdownFiles)
            {
                string content = File.ReadAllText(markdown);
                AssertLocalLinksResolve(root, markdown, content);
                commands.AssertMarkdown(markdown, content);

                string? topic = DocsTopic(skill, root, markdown);
                if (topic is null)
                {
                    continue;
                }

                Assert.True(commands.HasTopic(topic), $"docs does not list '{topic}' for {markdown}");
                Assert.True(
                    topics.TryAdd(topic, markdown),
                    $"{markdown} and {topics[topic]} share the docs topic '{topic}'");
            }

            // docs prints the document itself, without an envelope or other changes.
            CliResult docs = _workspace.Run("docs", DocsTopic(skill, root, skillFile)!);
            Assert.True(docs.ExitCode == 0, docs.StdErr);
            Assert.Equal(
                NormalizeNewLines(File.ReadAllText(skillFile)).TrimEnd(),
                NormalizeNewLines(docs.StdOut).TrimEnd());
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
            "--dir", "proj", "--output-format", "json");

        Assert.Equal(0, result.ExitCode);
        Assert.True(File.Exists(_workspace.File(
            Path.Combine("proj", hostDirectory, "skills", skill, "SKILL.md"))));
    }

    [Fact]
    public void SkillList_ReportsAllSupportedHosts()
    {
        CliResult result = _workspace.Run("skill", "list", "--output-format", "json");

        Assert.Equal(0, result.ExitCode);
        JsonArray skills = Parse(result.StdOut)["skills"]!.AsArray();
        Assert.All(skills, skill => Assert.Equal(3, skill!["hosts"]!.AsArray().Count));
    }

    [Fact]
    public void Install_CreatesNothingButTheSkillInTheSkillsRoot()
    {
        string skill = FirstSkill();
        string root = _workspace.File("skills-root");
        Assert.Equal(0, _workspace.Run("skill", "install", skill, "--target", "skills-root").ExitCode);
        var created = new System.Collections.Concurrent.ConcurrentQueue<string>();
        using var watcher = new FileSystemWatcher(root)
        {
            NotifyFilter = NotifyFilters.DirectoryName,
            IncludeSubdirectories = false,
        };
        watcher.Created += (_, e) => created.Enqueue(e.Name!);
        watcher.Renamed += (_, e) => created.Enqueue(e.Name!);
        watcher.EnableRaisingEvents = true;

        // A host loads every directory under its Skills root, so a stage or a backup of the
        // replaced Skill there reads as a duplicate Skill while the install runs or after a crash.
        CliResult replaced = _workspace.Run("skill", "install", skill, "--target", "skills-root");

        Assert.True(replaced.ExitCode == 0, replaced.StdErr);
        // The published Skill is the last directory the install creates in the root.
        DateTime until = DateTime.UtcNow.AddSeconds(30);
        while (!created.Contains(skill) && DateTime.UtcNow < until) { Thread.Sleep(25); }
        Assert.Equal([skill], created.Distinct());
        SkillsRootLayout.AssertOnlySkills(root, skill);
        SkillsRootLayout.AssertTransactionArtifactsBeside(root, ".aspose-publication-*", 0);
    }

    [Fact]
    public void Install_ThatFailsWhileReplacingTheSkill_LeavesTheInstalledSkill()
    {
        Requires.Windows();
        string skill = FirstSkill();
        Assert.Equal(0, _workspace.Run("skill", "install", skill, "--target", "skills-root").ExitCode);
        string root = _workspace.File("skills-root");
        string installed = Path.Combine(root, skill);
        string[] before = Tree(installed);

        // Another program holds a file of the installed Skill open without sharing its deletion,
        // so the install's publication cannot move the installed tree aside and fails there.
        CliResult failed;
        using (new FileStream(Path.Combine(installed, "SKILL.md"), FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            failed = _workspace.Run("skill", "install", skill, "--target", "skills-root", "--output-format", "json");
        }

        Assert.True(failed.ExitCode != 0, failed.StdOut);
        Assert.Equal("OUTPUT_UNWRITABLE", Parse(failed.StdErr)["error"]!["code"]!.GetValue<string>());
        Assert.Equal(before, Tree(installed));
        Assert.Equal([skill], Directory.EnumerateFileSystemEntries(root).Select(Path.GetFileName));
        Assert.Empty(Directory.EnumerateDirectories(_workspace.Path, ".aspose-publication-*"));
    }

    [Fact]
    public void Install_OverASkillWithAReadOnlyFile_SucceedsEachTime()
    {
        string skill = FirstSkill();
        Assert.Equal(0, _workspace.Run("skill", "install", skill, "--target", "skills-root").ExitCode);
        string installed = _workspace.File(Path.Combine("skills-root", skill));
        string readOnly = Path.Combine(installed, "SKILL.md");
        File.SetAttributes(readOnly, File.GetAttributes(readOnly) | FileAttributes.ReadOnly);
        string[] before = Tree(installed);
        try
        {
            // The replaced Skill is deleted with its read-only file, so nothing is left for the
            // next install to recover.
            for (int pass = 0; pass < 2; pass++)
            {
                CliResult replaced = _workspace.Run(
                    "skill", "install", skill, "--target", "skills-root", "--output-format", "json");
                Assert.True(replaced.ExitCode == 0, replaced.StdErr);
                Assert.Equal(before, Tree(installed));
            }
            Assert.Empty(Directory.EnumerateDirectories(_workspace.Path, ".aspose-publication-*"));
        }
        finally
        {
            foreach (string file in Directory.EnumerateFiles(_workspace.Path, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
            }
        }
    }

    /// <summary>Every file below a directory with its content hash.</summary>
    private static string[] Tree(string directory) =>
        Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(directory, path) + ":" + FileHashes.Sha256(path))
            .Order(StringComparer.Ordinal)
            .ToArray();

    [Fact]
    public void Install_WritesOwnershipAndRefusesAnUnrecognizedManifestWithoutOverwritingIt()
    {
        string skill = FirstSkill();
        string parent = "managed-skill";
        CliResult first = _workspace.Run(
            "skill", "install", skill, "--target", parent, "--output-format", "json");
        Assert.Equal(0, first.ExitCode);

        string root = _workspace.File(Path.Combine(parent, skill));
        string manifestPath = Path.Combine(root, ".aspose-skill-manifest.json");
        JsonObject manifest = Parse(File.ReadAllText(manifestPath)).AsObject();
        Assert.Equal(2, manifest["schemaVersion"]!.GetValue<int>());
        Assert.Equal("aspose-cli-skill", manifest["productId"]!.GetValue<string>());
        JsonArray files = manifest["files"]!.AsArray();
        Assert.NotEmpty(files);
        Assert.All(files, item =>
        {
            Assert.False(Path.IsPathRooted(item!["path"]!.GetValue<string>()));
            Assert.Equal(64, item["sha256"]!.GetValue<string>().Length);
        });

        // A manifest of another schema does not prove that this CLI owns the tree.
        var unrecognized = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["skill"] = skill,
            ["cliVersion"] = manifest["cliVersion"]!.GetValue<string>(),
            ["executable"] = manifest["executable"]!.GetValue<string>(),
            ["executableSha256"] = manifest["executableSha256"]!.GetValue<string>(),
            ["contentSha256"] = manifest["contentSha256"]!.GetValue<string>(),
        };
        File.WriteAllText(manifestPath, unrecognized.ToJsonString());

        CliResult refused = _workspace.Run(
            "skill", "install", skill, "--target", parent, "--output-format", "json");
        Assert.NotEqual(0, refused.ExitCode);
        Assert.Equal(unrecognized.ToJsonString(), File.ReadAllText(manifestPath));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Install_RefusesModifiedOrCustomManagedSkillWithoutOverwriting(bool addUnknownFile)
    {
        string skill = FirstSkill();
        string parent = "custom-skill-" + addUnknownFile;
        CliResult first = _workspace.Run(
            "skill", "install", skill, "--target", parent, "--output-format", "json");
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
            "skill", "install", skill, "--target", parent, "--output-format", "json");
        Assert.Equal(5, second.ExitCode);
        Assert.Equal("OUTPUT_EXISTS", Parse(second.StdErr)["error"]!["code"]!.GetValue<string>());
        Assert.Equal(before, File.ReadAllText(changed));
    }

    [Fact]
    public void Install_RejectsAReparsePointInTheTargetAncestorChain()
    {
        Requires.Windows();

        string skill = FirstSkill();
        string actual = _workspace.File("junction-target");
        string junction = _workspace.File("junction-parent");
        Directory.CreateDirectory(actual);
        FileSystemLinks.CreateDirectoryLink(junction, actual);

        try
        {
            Assert.True(
                (File.GetAttributes(junction) & FileAttributes.ReparsePoint) != 0);
            CliResult result = _workspace.Run(
                "skill", "install", skill, "--target", "junction-parent",
                "--output-format", "json");

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
            "skill", "install", "--target", "installed", "--output-format", "json");

        Assert.Equal(2, result.ExitCode);
        Assert.Equal(string.Empty, result.StdOut);
        Assert.Equal("USAGE_ERROR", Parse(result.StdErr)["error"]!["code"]!.GetValue<string>());
        Assert.False(Directory.Exists(_workspace.File("installed")));
    }

    [Fact]
    public void Install_RefusesADirectoryForTheUsersOwnSkills()
    {
        CliResult result = _workspace.Run(
            "skill", "install", FirstSkill(), "--host", "codex", "--user", "--dir", "project", "--dry-run", "--output-format", "json");

        Assert.Equal(2, result.ExitCode);
        JsonNode error = Parse(result.StdErr)["error"]!;
        Assert.Equal("OPTION_INVALID", error["code"]!.GetValue<string>());
        Assert.Equal("--dir", error["details"]!["option"]!.GetValue<string>());
    }

    private string FirstSkill()
    {
        CliResult result = _workspace.Run("skill", "list", "--output-format", "json");
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

    private const string PlatformSkill = "aspose-cli-platform";

    /// <summary>
    /// The docs topic of a Skill document: the platform Skill's documents are unprefixed, a
    /// product Skill's are prefixed with the product id.
    /// </summary>
    private static string? DocsTopic(
        string skill,
        string root,
        string markdown)
    {
        string prefix = skill == PlatformSkill ? string.Empty : skill["aspose-cli-".Length..] + "/";
        string relative = Path.GetRelativePath(root, markdown)
            .Replace(Path.DirectorySeparatorChar, '/');
        if (string.Equals(relative, "SKILL.md", StringComparison.Ordinal))
        {
            return prefix + "overview";
        }
        if (relative.StartsWith("references/", StringComparison.Ordinal))
        {
            return prefix + relative[
                "references/".Length..(relative.Length - ".md".Length)];
        }
        if (relative.StartsWith("examples/", StringComparison.Ordinal)
            && relative.EndsWith("/README.md", StringComparison.Ordinal))
        {
            return prefix + relative[
                "examples/".Length..(relative.Length - "/README.md".Length)];
        }
        return null;
    }

    private static string NormalizeNewLines(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal);

    private static JsonNode Parse(string json) =>
        JsonNode.Parse(json) ?? throw new InvalidOperationException("Output was not JSON:\n" + json);
}
