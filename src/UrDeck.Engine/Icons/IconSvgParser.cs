// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using SkiaSharp;
using UrDeck.Sdk;

namespace UrDeck.Engine.Icons;

/// <summary>The outcome of reading one icon file: the glyph, or the reason there is none (a clause that follows the file's name).</summary>
public sealed record IconParseResult(IconGlyph? Glyph, string? Reason);

/// <summary>
/// Reads an icon file of the icon-sets format: flat outline art in a 24 by 24 box made of a short list of elements. Only
/// geometry and <c>fill="currentColor"</c> are used; colours, widths and the rest are the theme's. No SVG library: anything
/// outside the whitelist is refused, which is also what makes a theme's own file safe to accept. The deck and the tool read
/// files through this one class, so they cannot disagree.
/// </summary>
public static class IconSvgParser
{
    public const int MaxBytes = 4096;

    private const int MaxElements = 64;
    private const int MaxDataLength = 2048;

    private static readonly HashSet<string> Containers = ["svg", "g"];
    private static readonly HashSet<string> Shapes = ["path", "circle", "ellipse", "line", "polyline", "polygon", "rect"];

    // transform moves art out of the box the file rules promise; style and links reach beyond the geometry.
    private static readonly HashSet<string> Forbidden = ["transform", "style", "href"];

    public static IconParseResult Parse(Stream stream)
    {
        using var buffer = new MemoryStream();
        byte[] chunk = new byte[1024];
        int read;
        while ((read = stream.Read(chunk, 0, chunk.Length)) > 0)
        {
            buffer.Write(chunk, 0, read);
            if (buffer.Length > MaxBytes)
                return Fail($"is over {MaxBytes / 1024} KB; simplify the icon");
        }

        return Parse(Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length));
    }

    public static IconParseResult Parse(string text)
    {
        if (Encoding.UTF8.GetByteCount(text) > MaxBytes)
            return Fail($"is over {MaxBytes / 1024} KB; simplify the icon");

        XElement root;
        try
        {
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, IgnoreComments = true, IgnoreProcessingInstructions = true };
            using var reader = XmlReader.Create(new StringReader(text), settings);
            root = XDocument.Load(reader).Root!;
        }
        catch (Exception ex) when (ex is XmlException or InvalidOperationException or NullReferenceException)
        {
            return Fail($"is not valid XML ({ex.Message.Split('\n')[0].TrimEnd('.', ' ')})");
        }

        if (root.Name.LocalName != "svg")
            return Fail($"has a root element '{root.Name.LocalName}', not 'svg'");
        if (!IsViewBox((string?)root.Attribute("viewBox")))
            return Fail("has no viewBox of \"0 0 24 24\"");

        var shapes = new List<IconShape>();
        int count = 0;
        string? problem = null;
        try
        {
            Walk(root, false, shapes, ref count, ref problem);
        }
        catch
        {
            foreach (var shape in shapes)
                shape.Path.Dispose();
            throw;
        }

        if (problem != null)
        {
            foreach (var shape in shapes)
                shape.Path.Dispose();
            return Fail(problem);
        }

        return shapes.Count == 0 ? Fail("draws no shape") : new IconParseResult(new IconGlyph(shapes), null);
    }

    private static IconParseResult Fail(string reason) => new(null, reason);

    private static bool IsViewBox(string? value)
    {
        if (value == null)
            return false;
        string[] parts = value.Split([' ', ',', '\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 4 && parts.Select(p => double.TryParse(p, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? v : double.NaN)
            .SequenceEqual([0d, 0d, 24d, 24d]);
    }

    private static void Walk(XElement element, bool inheritedFill, List<IconShape> shapes, ref int count, ref string? problem)
    {
        if (problem != null)
            return;
        string name = element.Name.LocalName;
        if (++count > MaxElements)
        {
            problem = $"has more than {MaxElements} elements; simplify the icon";
            return;
        }

        if (!Containers.Contains(name) && !Shapes.Contains(name))
        {
            problem = $"has an element '{name}', which is not allowed; use only path, circle, ellipse, line, polyline, polygon and rect";
            return;
        }

        foreach (var attribute in element.Attributes())
        {
            if (Forbidden.Contains(attribute.Name.LocalName))
            {
                problem = $"has a '{attribute.Name.LocalName}' attribute on '{name}', which is not allowed; draw the shape in place";
                return;
            }
        }

        string? fill = (string?)element.Attribute("fill");
        bool filled = fill == null ? inheritedFill : string.Equals(fill, "currentColor", StringComparison.OrdinalIgnoreCase);

        if (Containers.Contains(name))
        {
            foreach (var child in element.Elements())
                Walk(child, filled, shapes, ref count, ref problem);
            return;
        }

        if (element.HasElements)
        {
            problem = $"has elements inside '{name}', which is not allowed";
            return;
        }

        var path = Shape(element, name, ref problem);
        if (path != null)
            shapes.Add(new IconShape(path, filled));
    }

    private static SKPath? Shape(XElement e, string name, ref string? problem)
    {
        using var builder = new SKPathBuilder();
        switch (name)
        {
            case "path":
                {
                    string? data = (string?)e.Attribute("d");
                    if (string.IsNullOrWhiteSpace(data))
                    {
                        problem = "has a 'path' with no 'd'";
                        return null;
                    }

                    if (data.Length > MaxDataLength)
                    {
                        problem = $"has path data over {MaxDataLength} characters; simplify the icon";
                        return null;
                    }

                    var parsed = SKPath.ParseSvgPathData(data);
                    if (parsed == null)
                    {
                        problem = "has path data that cannot be read";
                        return null;
                    }

                    return parsed;
                }
            case "circle":
                {
                    if (!Number(e, "r", out float r) || r <= 0)
                        return Missing(name, "r", ref problem);
                    builder.AddCircle(Optional(e, "cx"), Optional(e, "cy"), r);
                    break;
                }
            case "ellipse":
                {
                    if (!Number(e, "rx", out float rx) || !Number(e, "ry", out float ry) || rx <= 0 || ry <= 0)
                        return Missing(name, "rx and ry", ref problem);
                    builder.AddOval(new SKRect(Optional(e, "cx") - rx, Optional(e, "cy") - ry, Optional(e, "cx") + rx, Optional(e, "cy") + ry));
                    break;
                }
            case "line":
                {
                    builder.MoveTo(Optional(e, "x1"), Optional(e, "y1"));
                    builder.LineTo(Optional(e, "x2"), Optional(e, "y2"));
                    break;
                }
            case "polyline":
            case "polygon":
                {
                    var points = Points((string?)e.Attribute("points"));
                    if (points == null || points.Count < 2)
                    {
                        problem = $"has a '{name}' whose points cannot be read";
                        return null;
                    }

                    builder.MoveTo(points[0]);
                    for (int i = 1; i < points.Count; i++)
                        builder.LineTo(points[i]);
                    if (name == "polygon")
                        builder.Close();
                    break;
                }
            default:
                {
                    if (!Number(e, "width", out float w) || !Number(e, "height", out float h) || w <= 0 || h <= 0)
                        return Missing(name, "width and height", ref problem);
                    var rect = SKRect.Create(Optional(e, "x"), Optional(e, "y"), w, h);
                    bool hasRx = Number(e, "rx", out float rx), hasRy = Number(e, "ry", out float ry);
                    if (!hasRx && !hasRy)
                    {
                        builder.AddRect(rect);
                    }
                    else
                    {
                        // SVG: a missing radius takes the other's value.
                        float radiusX = Math.Min(hasRx ? rx : ry, w / 2), radiusY = Math.Min(hasRy ? ry : rx, h / 2);
                        builder.AddRoundRect(new SKRoundRect(rect, radiusX, radiusY));
                    }

                    break;
                }
        }

        return builder.Detach();
    }

    private static SKPath? Missing(string element, string attribute, ref string? problem)
    {
        problem = $"has a '{element}' with no usable {attribute}";
        return null;
    }

    private static bool Number(XElement e, string attribute, out float value)
    {
        value = 0;
        string? text = (string?)e.Attribute(attribute);
        return text != null && float.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value) && float.IsFinite(value);
    }

    private static float Optional(XElement e, string attribute) => Number(e, attribute, out float value) ? value : 0;

    private static List<SKPoint>? Points(string? text)
    {
        if (text == null)
            return null;
        string[] numbers = text.Split([' ', ',', '\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries);
        if (numbers.Length % 2 != 0)
            return null;
        var points = new List<SKPoint>(numbers.Length / 2);
        for (int i = 0; i < numbers.Length; i += 2)
        {
            if (!float.TryParse(numbers[i], NumberStyles.Float, CultureInfo.InvariantCulture, out float x)
                || !float.TryParse(numbers[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y)
                || !float.IsFinite(x) || !float.IsFinite(y))
                return null;
            points.Add(new SKPoint(x, y));
        }

        return points;
    }
}
