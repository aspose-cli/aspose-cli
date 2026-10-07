namespace Aspose.Cli.TestKit.Scenarios;

/// <summary>
/// The license mode scenarios and their fixtures run in. A run is licensed when
/// <c>ASPOSE_CLI_TEST_LICENSE_PATH</c> names a license (<see cref="TestLicense"/>): the license is
/// then copied into each workspace as its project license, <c>.aspose/license.lic</c>, because
/// the CLI environment strips every other license source. Otherwise the CLI runs in evaluation
/// mode.
/// </summary>
public static class ScenarioLicense
{
    /// <summary>The workspace directory that holds the project license; never compared or reported as written.</summary>
    public const string ProjectDirectory = ".aspose";

    /// <summary>Whether this run's CLI invocations are licensed.</summary>
    public static bool Licensed => TestLicense.Path is not null;

    /// <summary><c>licensed</c> or <c>evaluation</c>.</summary>
    public static string Mode => Licensed ? "licensed" : "evaluation";

    /// <summary>Copies the run's license into a workspace as its project license, when the run has one.</summary>
    public static void Project(string workspace)
    {
        if (TestLicense.Path is not { } license)
        {
            return;
        }
        string directory = Path.Combine(workspace, ProjectDirectory);
        Directory.CreateDirectory(directory);
        File.Copy(license, Path.Combine(directory, "license.lic"), overwrite: true);
    }
}
