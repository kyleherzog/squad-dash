using System.Threading;
using System.Windows.Controls;
using System.Windows.Documents;

namespace SquadDash.Tests;

// DirectRevise cannot be tested end-to-end here because:
//  1. It requires RevisionHighlightAdorner, RevisionPendingIndicator, RevisionWorkingOverlay,
//     RevisionResultWindow, and WindowPlacementHelper — none of which are compiled into the
//     test project, and several access Application.Current.Resources (unavailable headlessly).
//  2. Its async Task is fire-and-forget (assigned to '_'), leaving no handle to await before
//     asserting a result.
//  3. The adorner and overlay APIs require a fully rendered visual tree, not merely an
//     instantiated but unshown Window.
//
// The tests below instead verify the \r\n normalisation invariant that DirectRevise
// depends on: TextRange.Text emits \r\n between paragraphs, while RichTextBoxExtensions
// (GetSubstring / GetPlainText) always return \n-only text.  The fix in DirectRevise —
//   .Replace("\r\n", "\n")
// before comparing currentText to originalText — is correct precisely because the two
// sources disagree on line endings for multi-paragraph selections.

[TestFixture]
[Apartment(ApartmentState.STA)]
internal sealed class MarkdownRevisionExecutorTests
{
    [Test]
    public void TextRange_Text_ContainsCrLf_BetweenParagraphs()
    {
        // TextRange.Text is the source DirectRevise reads AFTER the AI call returns.
        WpfTestContext.Run(() =>
        {
            var rtb   = new RichTextBox();
            rtb.SetPlainText("line one\nline two");

            var start = rtb.GetTextPointerAt(0);
            var end   = rtb.GetTextPointerAt(rtb.GetTextLength());
            var raw   = new TextRange(start, end).Text;

            Assert.That(raw, Does.Contain("\r\n"));
        });
    }

    [Test]
    public void GetSubstring_ReturnsLfOnly_ForMultiParagraphContent()
    {
        // GetSubstring is the source DirectRevise uses for originalText at call time.
        WpfTestContext.Run(() =>
        {
            var rtb  = new RichTextBox();
            rtb.SetPlainText("line one\nline two");

            var text = rtb.GetSubstring(0, rtb.GetTextLength());

            Assert.That(text, Does.Not.Contain("\r"));
        });
    }

    [Test]
    public void WithoutNormalization_TextRange_DoesNotMatch_GetSubstring()
    {
        // Demonstrates WHY the .Replace("\r\n", "\n") fix in DirectRevise is necessary.
        // Before the fix, MainWindow's path compared raw TextRange.Text (with \r\n) against
        // GetSubstring (with \n only), so multi-line selections never matched and the revised
        // text was always shown in RevisionResultWindow instead of applied in-place.
        WpfTestContext.Run(() =>
        {
            var rtb = new RichTextBox();
            rtb.SetPlainText("first line\nsecond line");

            var start = rtb.GetTextPointerAt(0);
            var end   = rtb.GetTextPointerAt(rtb.GetTextLength());

            var rawTextRangeText   = new TextRange(start, end).Text;
            var getSubstringResult = rtb.GetSubstring(0, rtb.GetTextLength());

            Assert.That(rawTextRangeText, Is.Not.EqualTo(getSubstringResult),
                "Without normalisation the two sources disagree on line endings.");
        });
    }

    [Test]
    public void WithNormalization_TextRange_Matches_GetSubstring()
    {
        // Demonstrates that .Replace("\r\n", "\n") — as applied in DirectRevise — makes the
        // comparison reliable, enabling direct in-place replacement of the revised text.
        WpfTestContext.Run(() =>
        {
            var rtb = new RichTextBox();
            rtb.SetPlainText("first line\nsecond line");

            var start = rtb.GetTextPointerAt(0);
            var end   = rtb.GetTextPointerAt(rtb.GetTextLength());

            var normalised      = new TextRange(start, end).Text.Replace("\r\n", "\n");
            var getSubstringResult = rtb.GetSubstring(0, rtb.GetTextLength());

            Assert.That(normalised, Is.EqualTo(getSubstringResult));
        });
    }
}
