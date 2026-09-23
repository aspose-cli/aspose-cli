using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aspose.Cli.Sdk;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.IO;

namespace Aspose.Cli.Host.Updating;

/// <summary>
/// Outcome of the last detached installer run for one installation. The CLI records the
/// handoff as pending; install.ps1 then records running, succeeded or failed and writes its
/// log beside the status file. Later update commands report a failed or unfinished run.
/// </summary>
internal static class UpdateStatus
{
    internal const string FailedWarningCode = "UPDATE_FAILED";
    internal const string InProgressWarningCode = "UPDATE_IN_PROGRESS";
    private const int MaximumBytes = 64 * 1024;

    /// <summary>The status file of the installation rooted at <paramref name="installRoot"/>.</summary>
    internal static string PathFor(string installRoot)
    {
        string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(installRoot));
        string key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(root.ToUpperInvariant())))
            .ToLowerInvariant()[..16];
        return Path.Combine(PrivateUserStorage.TemporaryRoot(), "updates", "status-" + key + ".json");
    }

    /// <summary>Records a started installer; the installer overwrites the state it owns.</summary>
    internal static void WritePending(string path, int installerProcessId, string currentVersion, string? targetVersion)
    {
        var status = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["state"] = "pending",
            ["installerProcessId"] = installerProcessId,
            ["currentVersion"] = currentVersion,
            ["targetVersion"] = targetVersion,
            ["message"] = null,
            ["log"] = Path.ChangeExtension(path, ".log"),
            ["updatedAt"] = DateTimeOffset.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
        };
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (FileStream stream = PrivateUserStorage.CreateFile(temporary))
            {
                stream.Write(Encoding.UTF8.GetBytes(status.ToJsonString()));
            }
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    /// <summary>A warning about the last installer run, or null when it succeeded or never ran.</summary>
    internal static Warning? ReadWarning(string path)
    {
        JsonObject? status = Read(path);
        if (status is null)
        {
            return null;
        }
        string? state = Text(status, "state");
        string? target = Text(status, "targetVersion");
        string? log = Text(status, "log");
        string update = target is null ? "The last update" : $"The last update to {target}";
        string hint = (log is null ? string.Empty : $"See the installer log at '{log}'. ")
            + $"Close programs that use this installation, including AI agents that started '{DistributionInfo.CommandName} mcp serve', then run update install again.";
        switch (state)
        {
            case "failed":
                string reason = Text(status, "message") ?? "the installer reported no reason";
                return new Warning { Code = FailedWarningCode, Message = $"{update} failed: {reason}", Hint = hint };
            case "pending" or "running":
                int? processId = status["installerProcessId"] is JsonValue value && value.TryGetValue(out int id) ? id : null;
                if (processId is int alive && IsInstallerRunning(alive))
                {
                    return new Warning
                    {
                        Code = InProgressWarningCode,
                        Message = $"{update} is still being installed by process {alive}.",
                        Hint = $"Wait for it to finish; '{DistributionInfo.CommandName} --version' then reports the installed version.",
                    };
                }
                return new Warning { Code = FailedWarningCode, Message = $"{update} stopped before its installer reported a result.", Hint = hint };
            default:
                return null;
        }
    }

    private static JsonObject? Read(string path)
    {
        try
        {
            var file = new FileInfo(path);
            if (!file.Exists || file.Length > MaximumBytes)
            {
                return null;
            }
            // Windows PowerShell may write a byte order mark.
            string text = File.ReadAllText(path, Encoding.UTF8).TrimStart('﻿');
            return JsonNode.Parse(text) as JsonObject;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    private static string? Text(JsonObject status, string name) =>
        status[name] is JsonValue value && value.TryGetValue(out string? text) && !string.IsNullOrWhiteSpace(text)
            ? text
            : null;

    private static bool IsInstallerRunning(int processId)
    {
        try
        {
            using Process process = Process.GetProcessById(processId);
            return !process.HasExited
                && string.Equals(process.ProcessName, "powershell", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }
}
