namespace SquadDash.Tests;

[TestFixture]
internal sealed class TintKeysTests {

    [Test]
    public void TintKeys_ContainsExpectedSurfaceKeys() {
        Assert.Multiple(() => {
            Assert.That(TintKeys.All, Does.Contain("AppSurface"));
            Assert.That(TintKeys.All, Does.Contain("TranscriptSurface"));
            Assert.That(TintKeys.All, Does.Contain("InputSurface"));
            Assert.That(TintKeys.All, Does.Contain("PanelBorder"));
            Assert.That(TintKeys.All, Does.Contain("BodyText"));
            Assert.That(TintKeys.All, Does.Contain("ScrollBarThumbBrush"));
        });
    }

    [Test]
    public void TintKeys_ExcludesSemanticStatusColors() {
        Assert.Multiple(() => {
            Assert.That(TintKeys.All, Does.Not.Contain("TaskPriorityMid"),
                "TaskPriorityMid is amber = mid-priority status; shifting it would collide with TaskPriorityLow");
            Assert.That(TintKeys.All, Does.Not.Contain("ScreenshotAnchorUnnamed"),
                "ScreenshotAnchorUnnamed is amber = semantic annotation color");
        });
    }

    [Test]
    public void TintKeys_ExcludesSearchHighlightKeys() {
        Assert.Multiple(() => {
            Assert.That(TintKeys.All, Does.Not.Contain("SearchHighlight"));
            Assert.That(TintKeys.All, Does.Not.Contain("SearchHighlightCurrent"));
            Assert.That(TintKeys.All, Does.Not.Contain("SearchHighlightText"));
        });
    }

    [Test]
    public void TintKeys_CountMatchesKnownTotal() {
        // 29 surfaces + 2 chrome + 17 borders + 20 text + 4 scrollbar + 2 editor = 74
        // A lower count would indicate a duplicate in the source literal (silently dropped by HashSet)
        // or an accidental deletion.
        Assert.That(TintKeys.All.Count, Is.EqualTo(74));
    }
}
