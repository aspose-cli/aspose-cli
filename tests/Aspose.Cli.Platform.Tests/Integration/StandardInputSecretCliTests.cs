using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;
using Aspose.Cli.TestKit;
using Aspose.Cli.TestKit.Scenarios;
using Xunit;

namespace Aspose.Cli.Platform.Tests.Integration;

/// <summary>
/// A password on stdin is the UTF-8 text of the bytes piped to the CLI, as every value on stdin is,
/// whatever code page the console reads: a password encrypts and opens a file alike from stdin and
/// from an environment variable, and bytes that are not UTF-8 are refused before anything is written.
/// The tests write the bytes themselves, and the CLI starts as on a machine whose console reads
/// stdin as Latin-1, so a CLI that read the password through the console would read another one.
/// </summary>
public sealed class StandardInputSecretCliTests
{
    // Pa55- and the Chinese for "password".
    private const string Password = "Pa55-密码";
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);
    private static readonly Dictionary<string, string?> PasswordVariable = new() { ["PW"] = Password };

    [Theory]
    [InlineData("cells", "enc.xlsx")]
    [InlineData("words", "enc.docx")]
    [InlineData("slides", "enc.pptx")]
    [InlineData("pdf", "enc.pdf")]
    public void PasswordOnStdin_IsThePasswordAnEnvironmentVariableHolds(string product, string file)
    {
        using var workspace = new TempWorkspace();
        ScenarioLicense.Project(workspace.Path);
        File.WriteAllText(workspace.File("text.txt"), "Hello\n");
        string[] source = product == "pdf" ? ["--from-text", "text.txt"] : [];

        Succeeds(Piped(workspace, Utf8.GetBytes(Password + "\n"), [product, "create", file, .. source, "--encrypt-stdin", "--output-format", "json"]));
        Succeeds(workspace.RunWithEnv(PasswordVariable, product, "inspect", file, "--password-env", "PW", "--output-format", "json"));

        workspace.RunWithEnv(PasswordVariable, [product, "create", "env-" + file, .. source, "--encrypt-env", "PW", "--output-format", "json"]).Succeeded();
        Succeeds(Piped(workspace, Utf8.GetBytes(Password + "\n"), [product, "inspect", "env-" + file, "--password-stdin", "--output-format", "json"]));
    }

    /// <summary>The first line is the password, after a UTF-8 byte order mark and before LF or CRLF, also in the worker of --timeout.</summary>
    [Theory]
    [InlineData("Pa55-word\n", "Pa55-word", false)]
    [InlineData("Pa55-word\r\n", "Pa55-word", false)]
    [InlineData("Pa55-word", "Pa55-word", false)]
    [InlineData("﻿" + Password + "\n", Password, false)]
    [InlineData(Password + "\r\n", Password, true)]
    public void PasswordOnStdin_IsItsFirstLine(string input, string password, bool timeout)
    {
        using var workspace = new TempWorkspace();
        ScenarioLicense.Project(workspace.Path);
        workspace.RunWithEnv(new Dictionary<string, string?> { ["PW"] = password }, "cells", "create", "enc.xlsx", "--encrypt-env", "PW", "--output-format", "json").Succeeded();
        string[] worker = timeout ? ["--timeout", "60"] : [];

        Succeeds(Piped(workspace, Utf8.GetBytes(input), ["cells", "inspect", "enc.xlsx", "--password-stdin", .. worker, "--output-format", "json"]));
    }

    /// <summary>
    /// Bytes that are not UTF-8, here the GBK encoding of the Chinese for "password", are refused
    /// as the option's, with the -env option as the other way, and the error repeats none of them;
    /// an empty stdin still says no password was given.
    /// </summary>
    [Fact]
    public void PasswordOnStdin_ThatIsNotUtf8_IsRefusedBeforeAnythingIsWritten()
    {
        using var workspace = new TempWorkspace();
        ScenarioLicense.Project(workspace.Path);

        CliResult refused = Piped(workspace, [0xC3, 0xDC, 0xC2, 0xEB, 0x0A], ["cells", "create", "gbk.xlsx", "--encrypt-stdin", "--output-format", "json"]);

        Assert.True(refused.ExitCode == 2, refused.StdOut + refused.StdErr);
        JsonNode error = JsonNode.Parse(refused.StdErr)!["error"]!;
        Assert.Equal(
            ("OPTION_INVALID", "Invalid use of --encrypt-stdin: stdin is read as UTF-8 text, and the password on it is not", "--encrypt-stdin"),
            (error["code"]!.GetValue<string>(), error["message"]!.GetValue<string>(), error["details"]!["option"]!.GetValue<string>()));
        Assert.Contains("--encrypt-env", error["hint"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.False(File.Exists(workspace.File("gbk.xlsx")));

        CliResult empty = Piped(workspace, [], ["cells", "create", "empty.xlsx", "--encrypt-stdin", "--output-format", "json"]);
        Assert.Contains("no password was provided on stdin", JsonNode.Parse(empty.StdErr)!["error"]!["message"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    private static void Succeeds(CliResult result) => Assert.True(result.ExitCode == 0, result.StdOut + result.StdErr);

    /// <summary>Runs the CLI with exactly <paramref name="input"/> on stdin, its console reading stdin as Latin-1.</summary>
    private static CliResult Piped(TempWorkspace workspace, byte[] input, string[] args)
    {
        ProcessStartInfo start = workspace.StartInfo(new Dictionary<string, string?>
        {
            [CliWorkerHost.HookVariable] = typeof(TempWorkspace).Assembly.Location,
            [CliWorkerHost.ConsoleInputVariable] = "iso-8859-1",
        });
        start.RedirectStandardInput = start.RedirectStandardOutput = start.RedirectStandardError = true;
        start.StandardOutputEncoding = start.StandardErrorEncoding = Encoding.UTF8;
        foreach (string argument in args)
        {
            start.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(start)!;
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        process.StandardInput.BaseStream.Write(input);
        process.StandardInput.Close();
        Assert.True(process.WaitForExit(120_000), string.Join(' ', args));
        return new CliResult(process.ExitCode, stdout.Result, stderr.Result);
    }
}
