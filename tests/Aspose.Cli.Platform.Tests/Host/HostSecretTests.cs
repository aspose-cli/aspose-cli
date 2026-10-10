using System.Text.Json;
using System.Text.Json.Nodes;
using Aspose.Cli.Host.LocalServices;
using Aspose.Cli.Host.ViewerService;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.TestKit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Aspose.Cli.Host.Tests;

/// <summary>
/// A document password the Host carries from the CLI to the viewer service and on to its render
/// worker is redacted wherever a request is printed, yet still reaches the other process over
/// the local frames. The requests are built from their wire JSON, the form the peer receives.
/// </summary>
public sealed class HostSecretTests : IDisposable
{
    private const string Sentinel = "S3cr3t-sentinel";
    private const string PasswordVariable = "ASPOSE_CLI_TEST_PREVIEW_PASSWORD";
    private static readonly string HostRoot = Path.Combine(RepositoryPaths.Root, "src", "Aspose.Cli.Host");
    private readonly TempWorkspace _workspace = new();

    public void Dispose() => _workspace.Dispose();

    [Fact]
    public void Requests_DoNotPrintThePassword()
    {
        Assert.DoesNotContain(Sentinel, OpenRequest(Sentinel).ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(Sentinel, RenderRequest(Sentinel).ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(Sentinel, Options(Sentinel).ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void ViewerOpenRequest_CarriesThePasswordToTheService()
    {
        string payload = JsonSerializer.Serialize(OpenRequest(Sentinel), ViewerServiceJsonContext.Default.ViewerOpenRequest);
        ViewerOpenRequest received = JsonSerializer.Deserialize(payload, ViewerServiceJsonContext.Default.ViewerOpenRequest)!;

        Assert.Equal(Sentinel, received.Password?.ToSecret().Reveal());
    }

    [Fact]
    public async Task RenderWorkerRequest_CarriesThePasswordToTheWorker()
    {
        using var frames = new MemoryStream();
        await ProcessPipeMessages.WriteAsync(frames, RenderRequest(Sentinel), CancellationToken.None);
        frames.Position = 0;
        RenderWorkerRequest received = await ProcessPipeMessages.ReadAsync<RenderWorkerRequest>(frames, CancellationToken.None);

        Assert.Equal(Sentinel, received.Password?.ToSecret().Reveal());
    }

    [Category(TestCategory.Slow)]
    [Fact]
    public void RenderWorker_OpensAnEncryptedDocumentWithThePassword()
    {
        File.WriteAllText(_workspace.File("content.txt"), "Encrypted preview fixture");
        CliResult created = _workspace.RunWithEnv(
            new Dictionary<string, string?> { [PasswordVariable] = Sentinel },
            "words", "create", "secret.docx", "--text", "content.txt", "--encrypt-env", PasswordVariable, "--output", "json");
        Assert.True(created.ExitCode == 0, created.StdErr);
        using var supervisor = new RenderWorkerSupervisor(() => _workspace.StartInfo(), TimeSpan.FromMinutes(2));

        RenderWorkerResponse locked = supervisor.Render(RenderRequest(null, _workspace.File("secret.docx"), Output()));
        RenderWorkerResponse opened = supervisor.Render(RenderRequest(Sentinel, _workspace.File("secret.docx"), Output()));

        Assert.False(locked.Ok);
        Assert.Equal(ErrorCodes.PasswordRequired.Name, locked.Code);
        Assert.True(opened.Ok, $"{opened.Code} {opened.Message}");
    }

    [Fact]
    public void LiveDocument_ReusesOnlyTheSamePassword()
    {
        File.WriteAllText(_workspace.File("watched.txt"), "fixture");
        using var supervisor = new RenderWorkerSupervisor(() => _workspace.StartInfo(), TimeSpan.FromMinutes(2));
        using var document = new LiveDocument(
            "doc", _workspace.File("watched.txt"), _workspace.File("root"), supervisor, Options(Sentinel),
            LocalServiceResourceLimits.Resolve());

        Assert.True(document.Matches(Options(Sentinel)));
        Assert.False(document.Matches(Options("other-password")));
        Assert.False(document.Matches(Options(null)));
    }

    [Fact]
    public void HostSource_HoldsNoPasswordAsAString()
    {
        string[] violations = [.. Directory.GetFiles(HostRoot, "*.cs", SearchOption.AllDirectories)
            .Where(static path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .SelectMany(static path => CSharpSyntaxTree.ParseText(File.ReadAllText(path), path: path).GetRoot().DescendantNodes()
                .SelectMany(static node => node switch
                {
                    PropertyDeclarationSyntax property => [(node, property.Type, property.Identifier.ValueText)],
                    FieldDeclarationSyntax field => field.Declaration.Variables
                        .Select(variable => ((SyntaxNode)variable, field.Declaration.Type, variable.Identifier.ValueText)),
                    LocalDeclarationStatementSyntax local => local.Declaration.Variables
                        .Select(variable => ((SyntaxNode)variable, local.Declaration.Type, variable.Identifier.ValueText)),
                    ParameterSyntax { Type: { } type } parameter => [(node, type, parameter.Identifier.ValueText)],
                    _ => Array.Empty<(SyntaxNode, TypeSyntax, string)>(),
                })
                .Where(static member => IsString(member.Item2) && member.Item3.EndsWith("Password", StringComparison.OrdinalIgnoreCase))
                .Select(member => $"{Path.GetRelativePath(HostRoot, path)}:{member.Item1.GetLocation().GetLineSpan().StartLinePosition.Line + 1}: string {member.Item3}"))];

        Assert.True(violations.Length == 0,
            "The Host carries a password as a redacting secret type, never as a string:" + Environment.NewLine
            + string.Join(Environment.NewLine, violations));
    }

    private static bool IsString(TypeSyntax type) => type switch
    {
        PredefinedTypeSyntax predefined => predefined.Keyword.IsKind(SyntaxKind.StringKeyword),
        NullableTypeSyntax nullable => IsString(nullable.ElementType),
        _ => false,
    };

    private string Output() =>
        Directory.CreateDirectory(_workspace.File(Path.Combine("out", Guid.NewGuid().ToString("N")))).FullName;

    private static ViewerOpenRequest OpenRequest(string? password) =>
        Wire<ViewerOpenRequest>(new JsonObject { ["file"] = "secret.docx", ["password"] = password });

    private static RenderWorkerRequest RenderRequest(string? password, string source = "secret.docx", string output = "out") =>
        Wire<RenderWorkerRequest>(new JsonObject
        {
            ["id"] = 1,
            ["source"] = source,
            ["output"] = output,
            ["maxPartCount"] = 8,
            ["timeoutMs"] = 120_000,
            ["password"] = password,
        });

    private static LiveDocumentOptions Options(string? password) =>
        Wire<LiveDocumentOptions>(new JsonObject { ["password"] = password });

    /// <summary>Reads a request from the JSON the peer process receives.</summary>
    private static T Wire<T>(JsonObject json) => json.Deserialize<T>(JsonSerializerOptions.Web)!;
}
