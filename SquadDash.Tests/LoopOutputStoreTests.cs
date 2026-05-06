using System.Collections.Generic;
using System.Linq;

namespace SquadDash.Tests;

[TestFixture]
internal sealed class LoopOutputStoreTests {
    private static string LogsDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SquadDash", "loop-logs");

    private HashSet<string> _preExistingFiles = null!;

    [SetUp]
    public void SetUp() {
        _preExistingFiles = Directory.Exists(LogsDir)
            ? [.. Directory.GetFiles(LogsDir)]
            : [];
    }

    [TearDown]
    public void TearDown() {
        if (!Directory.Exists(LogsDir)) return;
        foreach (var file in Directory.GetFiles(LogsDir)) {
            if (!_preExistingFiles.Contains(file)) {
                try { File.Delete(file); } catch { }
            }
        }
    }

    private string[] GetNewFiles() {
        if (!Directory.Exists(LogsDir)) return [];
        return [.. Directory.GetFiles(LogsDir).Where(f => !_preExistingFiles.Contains(f))];
    }

    [Test]
    public void SaveLog_WhitespaceContent_DoesNotCreateFile() {
        LoopOutputStore.SaveLog("   \t\n  ");

        Assert.That(GetNewFiles(), Is.Empty);
    }

    [Test]
    public void SaveLog_ValidContent_CreatesLogFile() {
        LoopOutputStore.SaveLog("loop output content");

        Assert.That(GetNewFiles(), Has.Length.EqualTo(1));
    }

    [Test]
    public void SaveLog_ValidContent_FileContainsExpectedContent() {
        const string content = "loop output: step 1 complete";

        LoopOutputStore.SaveLog(content);

        var created = GetNewFiles();
        Assert.That(created, Has.Length.EqualTo(1));
        Assert.That(File.ReadAllText(created[0]), Is.EqualTo(content));
    }

    [Test]
    public void SaveLog_CalledTwice_CreatesTwoDistinctFiles() {
        LoopOutputStore.SaveLog("first call");
        LoopOutputStore.SaveLog("second call");

        Assert.That(GetNewFiles(), Has.Length.EqualTo(2));
    }

    [Test]
    public void SaveLog_CalledTwice_SecondFileHasHigherNumber() {
        LoopOutputStore.SaveLog("first call");
        var afterFirst = GetNewFiles();

        LoopOutputStore.SaveLog("second call");
        var afterSecond = GetNewFiles();

        var firstFile = afterFirst.Single();
        var secondFile = afterSecond.Except(afterFirst).Single();

        var firstNumber = int.Parse(Path.GetFileNameWithoutExtension(firstFile).Split('-').Last());
        var secondNumber = int.Parse(Path.GetFileNameWithoutExtension(secondFile).Split('-').Last());

        Assert.That(secondNumber, Is.GreaterThan(firstNumber));
    }
}
