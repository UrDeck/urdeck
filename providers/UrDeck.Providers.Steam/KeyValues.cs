// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Globalization;
using System.Text;

namespace UrDeck.Providers.Steam;

/// <summary>
/// A node of Valve's text KeyValues format (VDF, ACF): a block of keys, each holding a text or a nested block. Keys are
/// compared without regard to case, as Steam does; a key that appears twice keeps the last value.
/// </summary>
internal sealed class KeyValues
{
    private const int MaxDepth = 32;

    private readonly Dictionary<string, KeyValues> _children = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The text of a leaf; null for a block.</summary>
    public string? Text { get; private init; }

    public bool IsBlock => Text is null;

    public IEnumerable<KeyValuePair<string, KeyValues>> Children => _children;

    /// <summary>The child under <paramref name="key"/>, or null.</summary>
    public KeyValues? this[string key] => _children.GetValueOrDefault(key);

    /// <summary>The text under <paramref name="path"/> (keys separated by <c>/</c>), or null when it is missing or a block.</summary>
    public string? GetText(string path) => Find(path)?.Text;

    /// <summary>The number under <paramref name="path"/>, or <paramref name="fallback"/> when missing or not a whole number.</summary>
    public long GetLong(string path, long fallback = 0) =>
        long.TryParse(GetText(path), NumberStyles.Integer, CultureInfo.InvariantCulture, out long value) ? value : fallback;

    /// <summary>The node under <paramref name="path"/> (keys separated by <c>/</c>), or null.</summary>
    public KeyValues? Find(string path)
    {
        var node = this;
        foreach (string part in path.Split('/'))
        {
            node = node._children.GetValueOrDefault(part);
            if (node is null)
                return null;
        }

        return node;
    }

    /// <summary>
    /// Reads <paramref name="text"/>: a sequence of <c>"key" "value"</c> and <c>"key" { ... }</c> entries with <c>//</c> comments.
    /// Strict: anything else, an unterminated string or block, or a stray closing brace throws <see cref="FormatException"/>,
    /// because Steam writes these files while they are read and a truncated one must not read as a smaller, valid one.
    /// The result is the top level, as a block.
    /// </summary>
    public static KeyValues Parse(string text)
    {
        var reader = new Reader(text);
        var root = new KeyValues();
        reader.ReadEntries(root, topLevel: true, 0);
        return root;
    }

    /// <summary>Parse that returns false instead of throwing.</summary>
    public static bool TryParse(string text, out KeyValues result)
    {
        try
        {
            result = Parse(text);
            return true;
        }
        catch (FormatException)
        {
            result = new KeyValues();
            return false;
        }
    }

    private enum Kind
    {
        End,
        Text,
        Open,
        Close,
    }

    private sealed class Reader(string text)
    {
        private int _pos;

        public void ReadEntries(KeyValues into, bool topLevel, int depth)
        {
            if (depth > MaxDepth)
                throw new FormatException("nested too deeply");
            while (true)
            {
                var (kind, key) = Next();
                if (kind == Kind.End)
                {
                    if (topLevel)
                        return;
                    throw new FormatException("a block is not closed");
                }

                if (kind == Kind.Close)
                {
                    if (!topLevel)
                        return;
                    throw new FormatException("a closing brace without a block");
                }

                if (kind != Kind.Text)
                    throw new FormatException("a key was expected");

                var (valueKind, value) = Next();
                switch (valueKind)
                {
                    case Kind.Text:
                        into._children[key] = new KeyValues { Text = value };
                        break;
                    case Kind.Open:
                        var block = new KeyValues();
                        ReadEntries(block, topLevel: false, depth + 1);
                        into._children[key] = block;
                        break;
                    default:
                        throw new FormatException("a value was expected");
                }
            }
        }

        private (Kind Kind, string Value) Next()
        {
            SkipSpaceAndComments();
            if (_pos >= text.Length)
                return (Kind.End, "");
            char c = text[_pos];
            switch (c)
            {
                case '{':
                    _pos++;
                    return (Kind.Open, "");
                case '}':
                    _pos++;
                    return (Kind.Close, "");
                case '"':
                    return (Kind.Text, ReadQuoted());
                default:
                    throw new FormatException("unquoted text");
            }
        }

        private void SkipSpaceAndComments()
        {
            while (_pos < text.Length)
            {
                char c = text[_pos];
                if (char.IsWhiteSpace(c))
                {
                    _pos++;
                }
                else if (c == '/' && _pos + 1 < text.Length && text[_pos + 1] == '/')
                {
                    while (_pos < text.Length && text[_pos] != '\n')
                        _pos++;
                }
                else
                {
                    return;
                }
            }
        }

        private string ReadQuoted()
        {
            _pos++; // the opening quote
            var sb = new StringBuilder();
            while (_pos < text.Length)
            {
                char c = text[_pos++];
                if (c == '"')
                    return sb.ToString();
                if (c == '\\' && _pos < text.Length)
                {
                    char e = text[_pos++];
                    sb.Append(e switch { 'n' => '\n', 't' => '\t', _ => e });
                }
                else
                {
                    sb.Append(c);
                }
            }

            throw new FormatException("a string is not closed");
        }
    }
}
