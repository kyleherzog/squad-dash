using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SquadDash;

public sealed record InboxMessage
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("subject")]
    public string Subject { get; init; } = string.Empty;

    [JsonPropertyName("from")]
    public string From { get; init; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; init; }

    [JsonPropertyName("read")]
    public bool Read { get; set; }

    [JsonPropertyName("body")]
    public string Body { get; init; } = string.Empty;

    [JsonPropertyName("attachments")]
    public IReadOnlyList<InboxAttachment> Attachments { get; init; } = [];
}

public sealed record InboxAttachment
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    [JsonPropertyName("label")]
    public string Label { get; init; } = string.Empty;

    /// <summary>Relative file path (for file/markdown attachment types).</summary>
    [JsonPropertyName("path")]
    public string? Path { get; init; }

    /// <summary>URL (for url attachment type).</summary>
    [JsonPropertyName("href")]
    public string? Href { get; init; }

    /// <summary>Task ID (for task-ref attachment type).</summary>
    [JsonPropertyName("taskId")]
    public string? TaskId { get; init; }

    /// <summary>Inline text or markdown content.</summary>
    [JsonPropertyName("content")]
    public string? Content { get; init; }
}
