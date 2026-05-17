using System;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using SquadDash.Screenshots;

namespace SquadDash.Tests;

[TestFixture]
public class ScreenshotRefreshRunnerTests
{
    private string _tempDir = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static ScreenshotRefreshRunner MakeRunner(
        ScreenshotDefinitionRegistry registry, string screenshotsDir) =>
        new(registry, new UiActionReplayRegistry(), new FixtureLoaderRegistry(), screenshotsDir);

    private static async Task<ScreenshotDefinitionRegistry> EmptyRegistryAsync(string dir) =>
        await ScreenshotDefinitionRegistry.LoadAsync(dir);

    // ── Tests ──────────────────────────────────────────────────────────────────

    [Test]
    public async Task RunAsync_ModeNone_CompletesWithoutRaisingCapture()
    {
        var registry = await EmptyRegistryAsync(_tempDir);
        var runner   = MakeRunner(registry, _tempDir);
        var captured = false;
        runner.CaptureRequested += (_, _) => captured = true;

        await runner.RunAsync(ScreenshotRefreshOptions.None);

        Assert.That(captured, Is.False, "CaptureRequested should not fire when Mode is None.");
    }

    [Test]
    public async Task RunAsync_AllMode_EmptyRegistry_CompletesWithoutRaisingCapture()
    {
        var registry = await EmptyRegistryAsync(_tempDir);
        var runner   = MakeRunner(registry, _tempDir);
        var captured = false;
        runner.CaptureRequested += (_, _) => captured = true;

        await runner.RunAsync(new ScreenshotRefreshOptions(ScreenshotRefreshMode.All, null));

        Assert.That(captured, Is.False,
            "CaptureRequested should not fire when the definition registry is empty.");
    }

    [Test]
    public async Task RunAsync_NamedMode_UnknownDefinition_CompletesWithoutRaisingCapture()
    {
        var registry = await EmptyRegistryAsync(_tempDir);
        var runner   = MakeRunner(registry, _tempDir);
        var captured = false;
        runner.CaptureRequested += (_, _) => captured = true;

        await runner.RunAsync(
            new ScreenshotRefreshOptions(ScreenshotRefreshMode.Named, "nonexistent-definition"));

        Assert.That(captured, Is.False,
            "CaptureRequested should not fire when the named definition is not registered.");
    }
}
