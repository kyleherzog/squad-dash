using NUnit.Framework;
using System;

namespace SquadDash.Tests;

[TestFixture]
internal sealed class InboxMessageSelectedTextAttachmentTests
{
    [Test]
    public void AttachmentBlock_ContainsMessageMetadata()
    {
        // Arrange
        var message = new InboxMessage
        {
            Id = "test-123",
            Subject = "Test Subject",
            From = "TestAgent",
            Timestamp = new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.Zero),
            Body = "This is a longer message body with multiple sentences. Some of which are quite important."
        };
        
        var selectedText = "Some of which are quite important.";
        
        // Build what the attachment should look like
        var expectedContent = $@"From: TestAgent
Subject: Test Subject
Date: 1/15/2024 10:30 AM

Selected excerpt:
---
{selectedText}";

        var block = AttachmentBlockFormatter.BuildTypedAttachmentBlock(
            "inbox-excerpt",
            $"Excerpt from: {message.Subject}",
            expectedContent);

        // Act & Assert - verify the block contains all necessary metadata
        Assert.That(block, Does.Contain("inbox-excerpt"));
        Assert.That(block, Does.Contain("Excerpt from: Test Subject"));
        Assert.That(block, Does.Contain("From: TestAgent"));
        Assert.That(block, Does.Contain("Subject: Test Subject"));
        Assert.That(block, Does.Contain("Selected excerpt:"));
        Assert.That(block, Does.Contain(selectedText));
    }

    [Test]
    public void AttachmentBlock_PreservesSpecialCharactersInSelection()
    {
        // Arrange
        var message = new InboxMessage
        {
            Id = "test-456",
            Subject = "Code Review",
            From = "Reviewer",
            Timestamp = DateTimeOffset.Now,
            Body = "The code has issues: </attachment> and other XML-like tags <foo>"
        };
        
        var selectedText = "</attachment> and other XML-like tags <foo>";

        var content = $@"From: {message.From}
Subject: {message.Subject}
Date: {message.Timestamp:g}

Selected excerpt:
---
{selectedText}";

        // Act
        var block = AttachmentBlockFormatter.BuildTypedAttachmentBlock(
            "inbox-excerpt",
            $"Excerpt from: {message.Subject}",
            content);

        // Assert - verify escaping works
        var extracted = AttachmentBlockFormatter.ExtractAttachmentContent(block);
        Assert.That(extracted, Does.Contain(selectedText));
    }

    [Test]
    public void AttachmentTitle_IncludesMessageSubject()
    {
        // Arrange
        var subject = "Important Update";
        var selectedText = "Key excerpt";
        
        var content = $@"From: Agent
Subject: {subject}
Date: 1/1/2024 12:00 PM

Selected excerpt:
---
{selectedText}";

        // Act
        var block = AttachmentBlockFormatter.BuildTypedAttachmentBlock(
            "inbox-excerpt",
            $"Excerpt from: {subject}",
            content);

        // Assert
        Assert.That(block, Does.Contain($"title=\"Excerpt from: {subject}\""));
    }

    [Test]
    public void AttachmentBlock_CanBeExtractedAndParsed()
    {
        // Arrange
        var excerptText = "This is the selected text from the message.";
        var content = $@"From: TestAgent
Subject: Test Subject
Date: 1/15/2024 10:30 AM

Selected excerpt:
---
{excerptText}";

        // Act
        var block = AttachmentBlockFormatter.BuildTypedAttachmentBlock(
            "inbox-excerpt",
            "Excerpt from: Test Subject",
            content);
        
        var extractedContent = AttachmentBlockFormatter.ExtractAttachmentContent(block);

        // Assert - verify the excerpt marker and content are present in extracted content
        Assert.That(extractedContent, Does.Contain("Selected excerpt:"));
        Assert.That(extractedContent, Does.Contain("---"));
        Assert.That(extractedContent, Does.Contain(excerptText));
    }

    [Test]
    public void AttachmentBlock_WithMultilineExcerpt_PreservesFormatting()
    {
        // Arrange
        var excerptText = @"This is a multiline excerpt.
It has several lines.
And preserves formatting.";

        var content = $@"From: TestAgent
Subject: Test Subject
Date: 1/15/2024 10:30 AM

Selected excerpt:
---
{excerptText}";

        // Act
        var block = AttachmentBlockFormatter.BuildTypedAttachmentBlock(
            "inbox-excerpt",
            "Excerpt from: Test Subject",
            content);
        
        var extractedContent = AttachmentBlockFormatter.ExtractAttachmentContent(block);

        // Assert - verify multiline content is preserved
        Assert.That(extractedContent, Does.Contain(excerptText));
        var lines = excerptText.Split('\n');
        foreach (var line in lines)
        {
            Assert.That(extractedContent, Does.Contain(line.TrimEnd()));
        }
    }
}
