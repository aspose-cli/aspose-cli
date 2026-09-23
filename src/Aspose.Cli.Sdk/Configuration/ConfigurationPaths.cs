using System.Text;
using System.Text.Json;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Sdk.Configuration;

/// <summary>Resolves private configuration owned by this independent application.</summary>
public static class ConfigurationPaths
{
    public const string EnvironmentVariableName = DistributionInfo.EnvironmentVariablePrefix + "CONFIG_DIR";
    private const string OwnerFileName = ".aspose-cli-config.json";

    public static string UserDirectory()
    {
        string? configured = Environment.GetEnvironmentVariable(EnvironmentVariableName);
        if (!string.IsNullOrWhiteSpace(configured) && !Path.IsPathFullyQualified(configured))
        {
            throw CliErrors.OptionInvalid(EnvironmentVariableName, "configuration path is not absolute", "Use an absolute directory dedicated to this CLI.");
        }
        string root = Path.GetFullPath(string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData,
                Environment.SpecialFolderOption.DoNotVerify), DistributionInfo.ConfigurationDirectoryName)
            : configured);
        ValidateExistingOwner(Path.Combine(root, OwnerFileName));
        return root;
    }

    public static string EnsureUserDirectory()
    {
        string selected = UserDirectory();
        string marker = Path.Combine(selected, OwnerFileName);
        // Claiming makes the directory private, which rewrites its permissions. The default
        // location is named for this CLI; a directory chosen through the environment is claimed
        // only when it is new, empty or already marked, never when it holds someone else's files.
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(EnvironmentVariableName))
            && Directory.Exists(selected)
            && !File.Exists(marker)
            && Directory.EnumerateFileSystemEntries(selected).Any())
        {
            throw Conflict();
        }
        string root = PrivateUserStorage.EnsureDirectory(selected);
        try
        {
            using FileStream file = new(marker, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            byte[] value = Encoding.UTF8.GetBytes("{\"schemaVersion\":1,\"productId\":\"" + DistributionInfo.Id + "\"}");
            file.Write(value);
            file.Flush(flushToDisk: true);
        }
        catch (IOException) when (File.Exists(marker))
        {
            ValidateExistingOwner(marker);
        }
        return root;
    }

    private static void ValidateExistingOwner(string marker)
    {
        if (!File.Exists(marker))
        {
            return;
        }
        try
        {
            PrivateUserStorage.ValidateFile(marker);
            using FileStream file = new(marker, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (file.Length > 1024)
            {
                throw Conflict();
            }
            using JsonDocument owner = JsonDocument.Parse(file);
            if (owner.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw Conflict();
            }
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (JsonProperty property in owner.RootElement.EnumerateObject())
            {
                if (!names.Add(property.Name))
                {
                    throw Conflict();
                }
            }
            if (!owner.RootElement.TryGetProperty("schemaVersion", out JsonElement version)
                || version.ValueKind != JsonValueKind.Number
                || !version.TryGetInt32(out int schemaVersion)
                || schemaVersion != 1
                || !owner.RootElement.TryGetProperty("productId", out JsonElement id)
                || id.ValueKind != JsonValueKind.String
                || id.GetString() != DistributionInfo.Id)
            {
                throw Conflict();
            }
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            throw Conflict();
        }
    }

    private static CliException Conflict() => CliErrors.OptionInvalid(
        EnvironmentVariableName, "configuration ownership could not be verified for this application",
        "Choose a different configuration directory. Existing files were preserved.");
}
