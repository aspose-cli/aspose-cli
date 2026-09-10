using System.CommandLine;
using Aspose.Cli.Host.Invocation;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Host.Skills;

namespace Aspose.Cli.Host.Commands;

/// <summary>The <c>aspose-cli skill</c> command group: discover and install bundled Agent Skills.</summary>
internal static class SkillCommandGroup
{
    private const string Codex = "codex";
    private const string ClaudeCode = "claude-code";
    private const string OpenCode = "opencode";

    public static Command Create(
        CommandExecutor executor,
        SkillCatalog catalog,
        GlobalOptions globals)
    {
        var skill = new Command("skill", "List or install Agent Skills bundled with this binary.");
        skill.Subcommands.Add(CreateList(executor, catalog, globals));
        skill.Subcommands.Add(CreateInstall(executor, catalog, globals));
        return skill;
    }

    private static Command CreateList(
        CommandExecutor executor,
        SkillCatalog catalog,
        GlobalOptions globals)
    {
        var list = new Command("list", "List bundled Agent Skills and supported hosts.");
        list.SetAction(parseResult => executor.RunLightweight(parseResult, globals, (_, _) => new SkillListResult
        {
            Skills = catalog.All.Select(static package => new SkillPackageInfo
            {
                Name = package.Name,
                Description = package.Description,
                Hosts = [Codex, ClaudeCode, OpenCode],
            }).ToArray(),
        }));
        return list;
    }

    private static Command CreateInstall(
        CommandExecutor executor,
        SkillCatalog catalog,
        GlobalOptions globals)
    {
        var skillArgument = new Argument<string>("skill")
        {
            Description = $"Bundled skill to install: {string.Join(", ", catalog.All.Select(static package => package.Name))}.",
            Arity = ArgumentArity.ExactlyOne,
        };
        var hostOption = new Option<string?>("--host")
        {
            Description = "Agent host whose standard skills directory should be used.",
        };
        hostOption.AcceptOnlyFromAmong(Codex, ClaudeCode, OpenCode);
        var scopeOption = new Option<string>("--scope")
        {
            Description = "Install for the current project or the current user.",
            DefaultValueFactory = _ => "project",
        };
        scopeOption.AcceptOnlyFromAmong("project", "user");
        var dirOption = new Option<string?>("--dir")
        {
            Description = "Project root used with --scope project. Default: current working directory.",
        };
        var targetOption = new Option<string?>("--target")
        {
            Description = "Advanced: explicit parent directory; the selected Skill folder is created there.",
        };

        var install = new Command("install", "Install a bundled Agent Skill for Codex, Claude Code, or OpenCode.");
        install.Arguments.Add(skillArgument);
        install.Options.Add(hostOption);
        install.Options.Add(scopeOption);
        install.Options.Add(dirOption);
        install.Options.Add(targetOption);

        install.SetAction(parseResult => executor.Run(parseResult, globals, context =>
        {
            string packageName = parseResult.GetRequiredValue(skillArgument);
            BundledSkill? package = catalog.All.FirstOrDefault(candidate => candidate.Name == packageName);
            if (package is null)
            {
                throw CliErrors.OptionInvalid("skill", $"unknown bundled skill '{packageName}'", $"Run 'aspose-cli skill list'; available: {string.Join(", ", catalog.All.Select(static item => item.Name))}.");
            }

            string? explicitTarget = parseResult.GetValue(targetOption);
            string? host = parseResult.GetValue(hostOption);
            if (explicitTarget is null && host is null)
            {
                throw CliErrors.OptionInvalid("--host", "a host is required unless --target is used", "Choose codex, claude-code, or opencode.");
            }

            if (explicitTarget is not null && host is not null)
            {
                throw CliErrors.OptionInvalid("--target", "cannot be combined with --host", "Use the host convention or an explicit target, not both.");
            }

            string parent = explicitTarget is not null
                ? context.Paths.ResolveOutput(explicitTarget)
                : ResolveHostParent(
                    host!,
                    parseResult.GetValue(scopeOption)!,
                    parseResult.GetValue(dirOption),
                    context.Paths.BaseDirectory);
            return package.InstallInto(
                context.ResourceBudgets,
                Path.Combine(parent, package.Name));
        }));

        return install;
    }

    internal static string ResolveHostParent(string host, string scope, string? projectDirectory, string baseDirectory)
    {
        bool user = string.Equals(scope, "user", StringComparison.Ordinal);
        string root = user
            ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile, Environment.SpecialFolderOption.DoNotVerify)
            : Path.GetFullPath(projectDirectory ?? baseDirectory, baseDirectory);
        return (host, user) switch
        {
            (Codex, false) => Path.Combine(root, ".agents", "skills"),
            (Codex, true) => Path.Combine(root, ".agents", "skills"),
            (ClaudeCode, false) => Path.Combine(root, ".claude", "skills"),
            (ClaudeCode, true) => Path.Combine(root, ".claude", "skills"),
            (OpenCode, false) => Path.Combine(root, ".opencode", "skills"),
            (OpenCode, true) => Path.Combine(root, ".config", "opencode", "skills"),
            _ => throw CliErrors.OptionInvalid("--host", $"unsupported host '{host}'", "Choose codex, claude-code, or opencode."),
        };
    }
}
