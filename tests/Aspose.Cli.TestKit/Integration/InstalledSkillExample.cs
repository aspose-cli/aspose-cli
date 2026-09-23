using System.CommandLine.Parsing;
using System.Text.RegularExpressions;
using System.Text.Json.Nodes;
using Xunit;

namespace Aspose.Cli.TestKit;

/// <summary>Runs the command blocks of an installed example in an isolated writable Skill copy.</summary>
public static class InstalledSkillExample
{
    public static InstalledSkillExampleResult Run(TempWorkspace workspace, string skill, string example)
    {
        string target = workspace.File("installed");
        CliResult install = workspace.Run("skill", "install", skill, "--target", target, "--output", "json");
        Assert.True(install.ExitCode == 0, install.StdErr);
        string directory = System.IO.Path.Combine(target, skill, "examples", example);
        string markdown = System.IO.File.ReadAllText(System.IO.Path.Combine(directory, "README.md"));
        var outputs = new List<JsonNode>();
        foreach (Match block in Regex.Matches(markdown,
            @"(?ms)^" + "\x60{3}powershell" + @"\r?\n(?<body>.*?)^" + "\x60{3}" + @"[ \t]*\r?$"))
        {
            foreach (string line in block.Groups["body"].Value.Split('\n'))
            {
                string command = line.Trim();
                if (command.Length == 0 || command.StartsWith('#')) { continue; }
                string[] tokens = CommandLineParser.SplitCommandLine(command).ToArray();
                Assert.True(tokens.Length > 1 && tokens[0] == "aspose-cli",
                    $"The executable example contains an unsupported command: {command}");
                CliResult result = workspace.Run(["--workdir", directory, .. tokens.Skip(1)]);
                Assert.True(result.ExitCode == 0,
                    $"{skill}/{example}: {command}\n{result.StdErr}\n{result.StdOut}");
                outputs.Add(JsonNode.Parse(result.StdOut)!);
            }
        }
        Assert.True(outputs.Count > 0, $"No executable command block found for {skill}/{example}.");
        return new InstalledSkillExampleResult(directory, outputs);
    }
}

public sealed record InstalledSkillExampleResult(string Directory, IReadOnlyList<JsonNode> Outputs);
