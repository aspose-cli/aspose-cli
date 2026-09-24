using System.Collections.Frozen;
using System.Text.Json;
using Aspose.Cli.Host.App;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.TestKit;
using Xunit;

namespace Aspose.Cli.Host.Tests;

public sealed class AppPreferencesTransactionTests
{
    // Every mutation publishes through the store's single Commit path.
    [Fact]
    public void LockedFile_PreservesMemoryAndDiskUntilARetryCommits()
    {
        Requires.Windows();
        using var temp = new TempDirectory();
        string file = temp.File("settings.json");
        var store = new AppPreferencesStore(ActualCommandTree.Host.Catalog, file);
        store.Update("cells", "workbook", true);
        store.RecordRecent(temp.File("one.xlsx"), "cells", "workbook");
        AppPreferences before = store.Current;
        byte[] disk = File.ReadAllBytes(file);
        Action change = () => store.RecordRecent(temp.File("two.xlsx"), "cells", "sheets");
        using (var held = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Exception? error = Record.Exception(change);
            Assert.True(error is IOException or UnauthorizedAccessException, error?.ToString());
            Assert.Same(before, store.Current);
            Assert.Equal(disk, File.ReadAllBytes(file));
        }
        change();
        Assert.NotSame(before, store.Current);
        Assert.NotEqual(disk, File.ReadAllBytes(file));
        var reopened = new AppPreferencesStore(ActualCommandTree.Host.Catalog, file);
        Assert.Equal(JsonSerializer.Serialize(store.Current), JsonSerializer.Serialize(reopened.Current));
        Assert.Empty(Directory.EnumerateFiles(temp.Path, "*.private"));
    }

    [Fact]
    public void ReadOnlyDestination_PreservesTheCommittedSnapshot()
    {
        Requires.Windows();
        using var temp = new TempDirectory();
        string file = temp.File("settings.json");
        var store = new AppPreferencesStore(ActualCommandTree.Host.Catalog, file);
        store.Update("cells", "workbook", true);
        AppPreferences before = store.Current;
        byte[] disk = File.ReadAllBytes(file);
        FileAttributes original = File.GetAttributes(file);
        File.SetAttributes(file, original | FileAttributes.ReadOnly);
        try
        {
            Exception? error = Record.Exception(() => store.Update("cells", "sheets", false));
            Assert.True(error is IOException or UnauthorizedAccessException, error?.ToString());
            Assert.Same(before, store.Current);
            Assert.Equal(disk, File.ReadAllBytes(file));
        }
        finally { File.SetAttributes(file, original); }
        store.Update("cells", "sheets", false);
        Assert.Equal("sheets", store.Current.PreviewViews["cells"]);
        _ = PrivateUserStorage.ReadAllText(file);
    }

    [Fact]
    public async Task ConcurrentUpdates_DoNotLoseOtherPreferencesOrLeakMutableCollections()
    {
        using var temp = new TempDirectory();
        var store = new AppPreferencesStore(ActualCommandTree.Host.Catalog, temp.File("settings.json"));
        await Task.WhenAll(
            Task.Run(() => store.Update("cells", "sheets", true)),
            Task.Run(() => store.Update("words", "pages", true)),
            Task.Run(store.CompleteOnboarding));
        await Task.WhenAll(Enumerable.Range(0, 8).Select(index => Task.Run(() =>
            store.RecordRecent(temp.File($"{index}.xlsx"), "cells", "sheets"))));
        AppPreferences snapshot = store.Current;
        Assert.Equal("sheets", snapshot.PreviewViews["cells"]);
        Assert.Equal("pages", snapshot.PreviewViews["words"]);
        Assert.True(snapshot.OnboardingCompleted);
        Assert.Equal(8, snapshot.RecentFiles.Count);
        Assert.IsAssignableFrom<FrozenDictionary<string, string>>(snapshot.PreviewViews);
        var recent = Assert.IsAssignableFrom<IList<AppRecentFile>>(snapshot.RecentFiles);
        Assert.Throws<NotSupportedException>(() => recent.Clear());
        store.ClearRecent();
        Assert.Equal(8, snapshot.RecentFiles.Count);
        Assert.Empty(store.Current.RecentFiles);
    }
}
