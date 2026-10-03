using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Sdk.Tests.Licensing;

public sealed class LicenseResolverTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly Dictionary<string, string?> _environment = [];

    public void Dispose() => _temp.Dispose();

    private string? GetEnv(string name) => _environment.GetValueOrDefault(name);

    private LicenseResolution Resolve(string? flagPath = null) =>
        LicenseResolver.Resolve(flagPath, "cells", GetEnv, _temp.File("work"), _temp.File("user-config"));

    private LicenseResolution ResolveProduct(string productId, string? flagPath = null) =>
        LicenseResolver.Resolve(
            flagPath,
            productId,
            GetEnv,
            _temp.File("work"),
            _temp.File("user-config"));

    private string CreateFile(string relativePath)
    {
        string path = _temp.File(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "<License/>");
        return path;
    }

    public LicenseResolverTests()
    {
        Directory.CreateDirectory(_temp.File("work"));
        Directory.CreateDirectory(_temp.File("user-config"));
    }

    [Fact]
    public void PendingInstallUsesValidatedContentAndReportsTheFinalConfigurationPath()
    {
        string snapshot = CreateFile("validated.lic");
        string config = _temp.File("user-config");
        string final = LicenseResolver.UserLicensePath(config, "cells");
        var changes = new UserLicenseChanges([KeyValuePair.Create<string, string?>("cells", snapshot)]);
        LicenseResolution result = LicenseResolver.Resolve(null, "cells", GetEnv, _temp.File("work"), config, changes);
        Assert.Equal(LicenseSourceKind.ProductUserFile, result.Kind);
        Assert.Equal(final, result.Path);
        Assert.Equal(snapshot, result.ContentPath);
        Assert.True(changes.IsProductInstalled(config, "cells"));
        Assert.False(File.Exists(final));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PendingRemovalSkipsOnlyTheSelectedUserSources(bool shared)
    {
        CreateFile("user-config/licenses/cells.lic");
        string sharedPath = CreateFile("user-config/license.lic");
        var changes = new UserLicenseChanges([KeyValuePair.Create<string, string?>("cells", null)], shared);
        string config = _temp.File("user-config");
        LicenseResolution result = LicenseResolver.Resolve(null, "cells", GetEnv, _temp.File("work"), config, changes);
        Assert.Equal(shared ? LicenseSourceKind.None : LicenseSourceKind.UserFile, result.Kind);
        Assert.False(changes.IsProductInstalled(config, "cells"));
        Assert.Equal(!shared, changes.IsSharedInstalled(config));
        Assert.True(File.Exists(sharedPath));
    }

    [Fact]
    public void PendingUserInstallCannotHideAnInvalidExplicitSource()
    {
        string snapshot = CreateFile("validated.lic");
        var changes = new UserLicenseChanges([KeyValuePair.Create<string, string?>("cells", snapshot)]);
        CliException error = Assert.Throws<CliException>(() => LicenseResolver.Resolve(_temp.File("missing.lic"),
            "cells", GetEnv, _temp.File("work"), _temp.File("user-config"), changes));
        Assert.Equal(ErrorCodes.LicenseFileNotFound, error.Code);
    }

    [Fact]
    public void Resolve_NothingConfigured_ReturnsNone()
    {
        LicenseResolution resolution = Resolve();

        Assert.Equal(LicenseResolution.None, resolution);
        Assert.False(resolution.IsConfigured);
        Assert.Null(resolution.SourceLabel);
    }

    [Fact]
    public void Resolve_FlagBeatsEverything()
    {
        string flagFile = CreateFile("flag.lic");
        _environment[LicenseResolver.EnvBase64Name] = "AAAA";
        _environment[LicenseResolver.EnvPathName] = CreateFile("env.lic");

        LicenseResolution resolution = Resolve(flagFile);

        Assert.Equal(LicenseSourceKind.Flag, resolution.Kind);
        Assert.Equal(flagFile, resolution.Path);
        Assert.Equal("flag", resolution.SourceLabel);
    }

    [Fact]
    public void Resolve_FlagPointingAtMissingFile_Throws()
    {
        CliException exception = Assert.Throws<CliException>(() => Resolve(_temp.File("missing.lic")));

        Assert.Equal(ErrorCodes.LicenseFileNotFound, exception.Code);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Resolve_SourcePointingAtADirectory_SaysItIsADirectory(bool environment)
    {
        string directory = _temp.File("work");
        CreateFile("user-config/license.lic");
        if (environment)
        {
            _environment[LicenseResolver.EnvPathName] = directory;
        }

        CliException exception = Assert.Throws<CliException>(() => Resolve(environment ? null : directory));

        Assert.Equal(ErrorCodes.LicenseFileNotFound, exception.Code);
        Assert.Contains("is a directory, not a license file", exception.Message, StringComparison.Ordinal);
        Assert.Equal(directory, exception.Details!["path"]!.GetValue<string>());
        Assert.Equal(
            environment ? "env:" + LicenseResolver.EnvPathName : "--license",
            exception.Details["source"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Resolve_AnEmptyFlagNeverFallsBackToOtherSources(string flag)
    {
        CreateFile("user-config/license.lic");

        Assert.ThrowsAny<ArgumentException>(() => Resolve(flag));
    }

    [Fact]
    public void Resolve_Base64BeatsEnvPath()
    {
        _environment[LicenseResolver.EnvBase64Name] = "AAAA";
        _environment[LicenseResolver.EnvPathName] = CreateFile("env.lic");

        LicenseResolution resolution = Resolve();

        Assert.Equal(LicenseSourceKind.EnvBase64, resolution.Kind);
        Assert.Null(resolution.Path);
    }

    [Fact]
    public void Resolve_EnvPathIsHonored()
    {
        string primary = CreateFile("primary.lic");
        _environment[LicenseResolver.EnvPathName] = primary;

        LicenseResolution resolution = Resolve();

        Assert.Equal(LicenseSourceKind.EnvPath, resolution.Kind);
        Assert.Equal(primary, resolution.Path);
    }

    [Fact]
    public void Resolve_EnvPathPointingAtMissingFile_Throws()
    {
        _environment[LicenseResolver.EnvPathName] = _temp.File("missing.lic");

        CliException exception = Assert.Throws<CliException>(() => Resolve());

        Assert.Equal(ErrorCodes.LicenseFileNotFound, exception.Code);
    }

    [Fact]
    public void Resolve_ProjectFileBeatsUserFile()
    {
        string projectFile = CreateFile(Path.Combine("work", ".aspose", "license.lic"));
        CreateFile(Path.Combine("user-config", "license.lic"));

        LicenseResolution resolution = Resolve();

        Assert.Equal(LicenseSourceKind.ProjectFile, resolution.Kind);
        Assert.Equal(projectFile, resolution.Path);
    }

    [Fact]
    public void Resolve_UserFile_IsTheLastResort()
    {
        string userFile = CreateFile(Path.Combine("user-config", "license.lic"));

        LicenseResolution resolution = Resolve();

        Assert.Equal(LicenseSourceKind.UserFile, resolution.Kind);
        Assert.Equal(userFile, resolution.Path);
    }

    [Fact]
    public void ResolveProduct_ProductEnvironmentBeatsCommonEnvironment()
    {
        string productFile = CreateFile("words.lic");
        _environment[LicenseResolver.ProductEnvPathName("words")] = productFile;
        _environment[LicenseResolver.EnvBase64Name] = "AAAA";

        LicenseResolution resolution = ResolveProduct("words");

        Assert.Equal(LicenseSourceKind.ProductEnvPath, resolution.Kind);
        Assert.Equal("words", resolution.ProductId);
        Assert.Equal("env:ASPOSE_WORDS_LICENSE_PATH", resolution.SourceLabel);
        Assert.Equal(productFile, resolution.Path);
    }

    [Fact]
    public void ResolveProduct_PdfSpecificEnvironmentBeatsSharedTotalEnvironment()
    {
        string productFile = CreateFile("pdf.lic");
        _environment[LicenseResolver.ProductEnvPathName("pdf")] = productFile;
        _environment[LicenseResolver.EnvPathName] = CreateFile("total.lic");

        LicenseResolution resolution = ResolveProduct("pdf");

        Assert.Equal(LicenseSourceKind.ProductEnvPath, resolution.Kind);
        Assert.Equal("pdf", resolution.ProductId);
        Assert.Equal("env:ASPOSE_PDF_LICENSE_PATH", resolution.SourceLabel);
        Assert.Equal(productFile, resolution.Path);
    }

    [Fact]
    public void ResolveProduct_PdfFallsBackToSharedTotalEnvironment()
    {
        string sharedFile = CreateFile("total-only.lic");
        _environment[LicenseResolver.EnvPathName] = sharedFile;

        LicenseResolution resolution = ResolveProduct("pdf");

        Assert.Equal(LicenseSourceKind.EnvPath, resolution.Kind);
        Assert.Null(resolution.ProductId);
        Assert.Equal("env:ASPOSE_LICENSE_PATH", resolution.SourceLabel);
        Assert.Equal(sharedFile, resolution.Path);
    }

    [Fact]
    public void ResolveProduct_CommonEnvironmentBeatsProductProjectFile()
    {
        _environment[LicenseResolver.EnvPathName] = CreateFile("common.lic");
        CreateFile(Path.Combine("work", ".aspose", "licenses", "words.lic"));

        LicenseResolution resolution = ResolveProduct("words");

        Assert.Equal(LicenseSourceKind.EnvPath, resolution.Kind);
        Assert.Equal("env:ASPOSE_LICENSE_PATH", resolution.SourceLabel);
    }

    [Fact]
    public void ResolveProduct_ProductProjectFileBeatsCommonProjectFile()
    {
        string productFile = CreateFile(
            Path.Combine("work", ".aspose", "licenses", "words.lic"));
        CreateFile(Path.Combine("work", ".aspose", "license.lic"));

        LicenseResolution resolution = ResolveProduct("words");

        Assert.Equal(LicenseSourceKind.ProductProjectFile, resolution.Kind);
        Assert.Equal("project:words", resolution.SourceLabel);
        Assert.Equal(productFile, resolution.Path);
    }

    [Fact]
    public void ResolveProduct_ProductUserFileBeatsSharedUserFile()
    {
        string productFile = CreateFile(
            Path.Combine("user-config", "licenses", "words.lic"));
        CreateFile(Path.Combine("user-config", "license.lic"));

        LicenseResolution resolution = ResolveProduct("words");

        Assert.Equal(LicenseSourceKind.ProductUserFile, resolution.Kind);
        Assert.Equal("user:words", resolution.SourceLabel);
        Assert.Equal(productFile, resolution.Path);
    }

    [Fact]
    public void ResolveProduct_OtherProductsLicenseIsIgnored()
    {
        CreateFile(Path.Combine("user-config", "licenses", "cells.lic"));

        LicenseResolution resolution = ResolveProduct("words");

        Assert.Equal(LicenseResolution.None, resolution);
    }

    [Fact]
    public void ProductEnvironmentNames_NormalizeProductId()
    {
        Assert.Equal(
            "ASPOSE_WORDS_LICENSE_B64",
            LicenseResolver.ProductEnvBase64Name(" Words "));
        Assert.Equal(
            "ASPOSE_WORDS_LICENSE_PATH",
            LicenseResolver.ProductEnvPathName("WORDS"));
    }
}
