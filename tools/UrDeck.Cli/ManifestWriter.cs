// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace UrDeck.Cli;

/// <summary>What a rewrite of a manifest would lose, as the person reads it ("a comment", "the key 'x' twice"); empty when nothing.</summary>
internal sealed record ManifestInspection(bool IsObject, IReadOnlyList<string> Losses);

/// <summary>
/// Writes <c>manifest.json</c>: a <see cref="JsonNode"/> tree as indented JSON (two spaces, LF, a final newline, text left
/// readable), through a temporary file and a rename. It also says whether a manifest can be rewritten without loss. The engine
/// only reads manifests; writing lives here.
/// </summary>
internal static class ManifestWriter
{
    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Indented = true,
        IndentSize = 2,
        NewLine = "\n",
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    // The reader of the engine accepts comments and trailing commas; so does this.
    private static readonly JsonDocumentOptions Lenient = new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

    /// <summary>Parses the manifest as the engine's reader does. Duplicate keys must have been refused by <see cref="Inspect"/> first.</summary>
    public static JsonNode? Parse(string text) => JsonNode.Parse(text, documentOptions: Lenient);

    public static string Serialize(JsonNode node)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, WriterOptions))
            node.WriteTo(writer);

        return Encoding.UTF8.GetString(stream.ToArray()) + "\n";
    }

    /// <summary>Writes <paramref name="path"/> through a temporary file in the same folder, so a failure leaves the old file whole.</summary>
    public static void Write(string path, JsonNode node) => WriteText(path, Serialize(node));

    public static void WriteText(string path, string text)
    {
        string temporary = path + ".tmp";
        try
        {
            File.WriteAllText(temporary, text, new UTF8Encoding(false));
            File.Move(temporary, path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            try
            {
                File.Delete(temporary);
            }
            catch (Exception cleanup) when (cleanup is IOException or UnauthorizedAccessException)
            {
            }

            throw new ToolFailureException($"{PathDisplay.Of(path)} cannot be written: {ex.Message}");
        }
    }

    /// <summary>
    /// Finds what a rewrite would lose: a comment and a trailing comma (which plain JSON cannot hold) and a key that occurs
    /// twice in one object (which the tree cannot hold). Text that is not a JSON object is <c>IsObject</c> false and left for the
    /// reader to report.
    /// </summary>
    public static ManifestInspection Inspect(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(text, Lenient);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return new ManifestInspection(false, []);
        }
        catch (JsonException)
        {
            return new ManifestInspection(false, []);
        }

        var losses = new List<string>();
        if (HasComment(text))
            losses.Add("a comment");
        if (HasTrailingComma(text))
            losses.Add("a trailing comma");
        foreach (string key in DuplicateKeys(text))
            losses.Add($"the key '{key}' twice in one object");

        return new ManifestInspection(true, losses);
    }

    private static bool HasComment(string text)
    {
        var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(text), new JsonReaderOptions { CommentHandling = JsonCommentHandling.Allow, AllowTrailingCommas = true });
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.Comment)
                return true;
        }

        return false;
    }

    private static bool HasTrailingComma(string text)
    {
        try
        {
            using var strict = JsonDocument.Parse(text, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip });
            return false;
        }
        catch (JsonException)
        {
            return true;
        }
    }

    private static List<string> DuplicateKeys(string text)
    {
        var found = new List<string>();
        var scopes = new Stack<HashSet<string>>();
        var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(text), new JsonReaderOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        while (reader.Read())
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.StartObject:
                    scopes.Push(new HashSet<string>(StringComparer.Ordinal));
                    break;
                case JsonTokenType.EndObject:
                    scopes.Pop();
                    break;
                case JsonTokenType.PropertyName:
                    string name = reader.GetString()!;
                    if (!scopes.Peek().Add(name) && !found.Contains(name))
                        found.Add(name);
                    break;
            }
        }

        return found;
    }
}
