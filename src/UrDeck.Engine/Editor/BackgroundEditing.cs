// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Text.Json.Nodes;
using UrDeck.Engine.Backgrounds;
using UrDeck.Engine.Config;
using UrDeck.Engine.Motion;

namespace UrDeck.Engine.Editor;

/// <summary>
/// The Theme and background section's edits of the configuration document, each one undoable step: the theme, the background
/// (the theme's choice, none, a shader, a picture), Moving or Still, the dimming, the shader's parameters and the motion level.
/// A choice keeps what carries over: another shader keeps Still and the dimming, a picture keeps the dimming.
/// </summary>
public sealed class BackgroundEditing(ConfigDocument document)
{
    private const string Background = "/background";

    /// <summary>Sets <c>theme</c>. False when it is the theme already or the document is read-only.</summary>
    public bool SetTheme(string name)
    {
        if (string.Equals(document.Typed.Theme, name, StringComparison.Ordinal))
            return false;
        return document.Edit($"Choose the theme {name}", e => e.Set(Key(e, "", "theme"), name));
    }

    /// <summary>The theme's choice: the <c>background</c> key is removed (and with it the dimming and the options).</summary>
    public bool UseThemesBackground() =>
        document.Root.ContainsKey("background") && document.Edit("Use the theme's background", e => e.Remove(Key(e, "", "background")));

    /// <summary><c>{ "type": "none" }</c>.</summary>
    public bool UseNoBackground() =>
        document.Edit("Use no background", e => e.Set(Key(e, "", "background"), new JsonObject { ["type"] = "none" }));

    /// <summary>A shader by name; Still (<c>speed</c> 0) and the dimming of the current background carry over, the rest is the shader's own.</summary>
    public bool UseShader(string name)
    {
        var current = Current();
        var next = new JsonObject { ["type"] = "shader", ["name"] = name };
        if (current != null && IsShader(current) && NumberOf(Own(current, "speed")) == 0)
            next["speed"] = 0;
        Carry(current, next);
        return Replace(current, next, $"Use the background {name}");
    }

    /// <summary>A picture by path; the dimming carries over.</summary>
    public bool UsePicture(string path)
    {
        var current = Current();
        var next = new JsonObject { ["type"] = "image", ["path"] = path };
        Carry(current, next);
        return Replace(current, next, "Use a picture as the background");
    }

    /// <summary>Still (<c>speed</c> 0) or Moving (<c>speed</c> removed) for a shader background.</summary>
    public bool SetStill(bool still)
    {
        if (Current() is not { } current || !IsShader(current))
            return false;
        return document.Edit(still ? "Make the background still" : "Make the background move", e =>
        {
            string key = Key(e, Background, "speed");
            if (still)
                e.Set(key, 0);
            else
                e.Remove(key);
        });
    }

    /// <summary>The dimming behind the cards from 0 to 0.7; null removes <c>dim</c> (the theme's own scrim).</summary>
    public bool SetDim(double? dim)
    {
        if (Current() is not { } current || (!IsShader(current) && !IsImage(current)))
            return false;
        return document.Edit("Change the dimming", e =>
        {
            string key = Key(e, Background, "dim");
            if (dim is { } value)
                e.Set(key, Math.Round(Math.Clamp(value, 0, BackgroundSettings.MaxDim), 2));
            else
                e.Remove(key);
        }, mergeKey: "background#dim");
    }

    /// <summary>Removes every shader parameter from the background object: the shader's own values again.</summary>
    public bool ResetParameters()
    {
        if (Current() is not { } current || !IsShader(current))
            return false;
        var keys = current.Select(p => p.Key).Where(k => !BackgroundSettings.IsOwnKey(k)).ToList();
        if (keys.Count == 0)
            return false;
        return document.Edit("Reset the background's options", e =>
        {
            foreach (string key in keys)
                e.Remove(Key(e, Background, key));
        });
    }

    /// <summary>The deck-wide motion level: <c>full</c>, <c>reduced</c>, <c>off</c>, or null to follow Windows (the key removed).</summary>
    public bool SetMotionLevel(string? level)
    {
        if (level != null && MotionPolicy.Parse(level) is null)
            return false;
        return document.Edit("Change the motion level", e =>
        {
            string key = Key(e, "", "motion");
            if (level == null)
                e.Remove(key);
            else
                e.Set(key, level);
        });
    }

    /// <summary>The <c>background</c> object, or null for none or something that is not an object.</summary>
    private JsonObject? Current() => document.Root["background"] as JsonObject;

    private static bool IsShader(JsonObject background) => TypeOf(background) == "shader";

    private static bool IsImage(JsonObject background) => TypeOf(background) == "image";

    private static string? TypeOf(JsonObject background) =>
        Own(background, "type") is JsonValue { } value && value.TryGetValue(out string? text) ? text?.ToLowerInvariant() : null;

    /// <summary>
    /// The number a node holds, or null for anything else. A node read from the file holds a JSON element and one written by an edit
    /// holds the .NET number it was made from (an <c>int</c> for <c>0</c>); both are read, which <c>GetValue&lt;double&gt;</c> does not.
    /// </summary>
    public static double? NumberOf(JsonNode? node)
    {
        if (node is not JsonValue value)
            return null;
        if (value.TryGetValue(out double d))
            return d;
        if (value.TryGetValue(out int i))
            return i;
        if (value.TryGetValue(out long l))
            return l;
        if (value.TryGetValue(out decimal m))
            return (double)m;
        return value.TryGetValue(out System.Text.Json.JsonElement element) && element.ValueKind == System.Text.Json.JsonValueKind.Number && element.TryGetDouble(out double e)
            ? e
            : null;
    }

    private static JsonNode? Own(JsonObject background, string key) =>
        background.FirstOrDefault(p => string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase)).Value;

    private static void Carry(JsonObject? current, JsonObject next)
    {
        if (current != null && Own(current, "dim") is { } dim)
            next["dim"] = dim.DeepClone();
    }

    private bool Replace(JsonObject? current, JsonObject next, string label)
    {
        if (current != null && JsonNode.DeepEquals(current, next))
            return false;
        return document.Edit(label, e => e.Set(Key(e, "", "background"), next));
    }

    private static string Key(ConfigEditor e, string parent, string key) => $"{parent}/{JsonPointer.Escape(e.FindKey(parent, key) ?? key)}";
}
