using System.Reflection;

namespace Aspose.Cli.Sdk.Extensibility;

/// <summary>
/// The name and description an embedded Agent Skill declares in the front matter of its
/// <c>skill/&lt;name&gt;/SKILL.md</c> resource. Product and platform Skills are read the same way.
/// </summary>
public sealed record SkillFrontMatter(string Name, string Description)
{
    /// <summary>
    /// Reads the Skill <paramref name="skillName"/> embedded in <paramref name="assembly"/>, or
    /// returns null when the assembly ships none; the declared name must equal
    /// <paramref name="skillName"/> and the description must not be empty.
    /// </summary>
    public static SkillFrontMatter? Read(Assembly assembly, string skillName)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentException.ThrowIfNullOrWhiteSpace(skillName);
        using Stream? stream = assembly.GetManifestResourceStream($"skill/{skillName}/SKILL.md");
        if (stream is null)
        {
            return null;
        }

        using var reader = new StreamReader(stream);
        if (!string.Equals(reader.ReadLine(), "---", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Skill '{skillName}' must start with YAML front matter.");
        }

        string? name = null;
        string? description = null;
        for (string? line = reader.ReadLine(); line is not null && line != "---"; line = reader.ReadLine())
        {
            int separator = line.IndexOf(':');
            if (separator < 1)
            {
                continue;
            }

            string key = line[..separator].Trim();
            string value = line[(separator + 1)..].Trim();
            if (key == "name")
            {
                name = value;
            }
            else if (key == "description")
            {
                description = value;
            }
        }

        if (!string.Equals(name, skillName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Skill name must be '{skillName}', not '{name}'.");
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            throw new InvalidOperationException($"Skill '{skillName}' front matter has no description.");
        }

        return new SkillFrontMatter(skillName, description);
    }
}
