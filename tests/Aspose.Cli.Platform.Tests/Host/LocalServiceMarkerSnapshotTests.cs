using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Aspose.Cli.Host.LocalServices;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Host.Tests;

public sealed class LocalServiceMarkerSnapshotTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PairedFiles_AreOneSnapshotDuringConcurrentReadsAndWrites(bool pauseReader)
    {
        using var directory = new TempDirectory();
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        bool armed = false;
        JsonTypeInfo<State> markerType = TypeInfo();
        Action<object> pause = _ =>
        {
            if (!armed) { return; }
            entered.Set();
            Assert.True(release.Wait(TimeSpan.FromSeconds(5)), "The concurrent operation was not released.");
        };
        if (pauseReader) { markerType.OnDeserialized = pause; }
        else { markerType.OnSerializing = pause; }
        var store = new LocalServiceMarkerFiles<State, State>(
            directory.File("marker.json"), directory.File("secrets.json"), markerType, TypeInfo());
        store.Write(new State(1), new State(1));
        armed = true;
        Task? first = null;
        Task? second = null;
        (State Marker, State Secrets)? observed = null;
        try
        {
            first = pauseReader
                ? Task.Run(() => { observed = store.Read(); })
                : Task.Run(() => store.Write(new State(2), new State(2)));
            Assert.True(entered.Wait(TimeSpan.FromSeconds(5)), "The first file operation did not reach its boundary.");
            second = pauseReader
                ? Task.Run(() => store.Write(new State(2), new State(2)))
                : Task.Run(() => { observed = store.Read(); });
            await Task.Delay(150);
            Assert.False(second.IsCompleted,
                "The paired-file operation escaped the shared resource lock.");
            release.Set();
            await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(10));
            Assert.NotNull(observed);
            int expected = pauseReader ? 1 : 2;
            Assert.Equal(expected, observed.Value.Marker.Revision);
            Assert.Equal(expected, observed.Value.Secrets.Revision);
        }
        finally
        {
            release.Set();
            if (first is not null) { await first.WaitAsync(TimeSpan.FromSeconds(10)); }
            if (second is not null) { await second.WaitAsync(TimeSpan.FromSeconds(10)); }
        }
    }

    private static JsonTypeInfo<State> TypeInfo() =>
        (JsonTypeInfo<State>)new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        }.GetTypeInfo(typeof(State));

    private sealed record State(int Revision);
}
