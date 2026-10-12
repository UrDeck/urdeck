// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using UrDeck.Engine.Backgrounds;

namespace UrDeck.Cli;

/// <summary>The four ways validate prints its findings. Each writes the whole output stream.</summary>
internal static partial class ReportWriters
{
    // Messages quote names with ' and the like: readable, not HTML-safe, which a log has no need of.
    private static readonly JsonWriterOptions JsonOptions = new()
    {
        Indented = true,
        NewLine = "\n",
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static void Write(string format, IReadOnlyList<BackgroundReport> reports, TextWriter output)
    {
        switch (format)
        {
            case "text":
                Text(reports, output);
                break;
            case "markdown":
                Markdown(reports, output);
                break;
            case "json":
                Json(reports, output);
                break;
            case "sarif":
                Sarif(reports, output);
                break;
            default:
                throw new ToolFailureException($"unknown format '{format}'; use text, markdown, json or sarif.");
        }
    }

    public static string Severity(ShaderSeverity severity) => severity switch
    {
        ShaderSeverity.Error => "error",
        ShaderSeverity.Warning => "warning",
        _ => "info",
    };

    /// <summary>One finding as the text format prints it: <c>path: severity CODE: message</c>.</summary>
    public static string TextLine(string path, ShaderDiagnostic diagnostic) =>
        $"{path}: {Severity(diagnostic.Severity)} {diagnostic.Code}: {OneLine(diagnostic.Message)}";

    private static void Text(IReadOnlyList<BackgroundReport> reports, TextWriter output)
    {
        foreach (var report in reports)
            foreach (var diagnostic in report.Diagnostics)
                output.Write(TextLine(report.FileOf(diagnostic), diagnostic) + "\n");

        output.Write(Summary(reports) + "\n");
    }

    private static void Markdown(IReadOnlyList<BackgroundReport> reports, TextWriter output)
    {
        foreach (var report in reports)
        {
            output.Write($"## {report.Name}\n\n");
            if (report.Diagnostics.Count == 0)
            {
                output.Write("No findings.\n\n");
                continue;
            }

            output.Write("| Severity | Code | File | Message |\n| --- | --- | --- | --- |\n");
            foreach (var diagnostic in report.Diagnostics)
            {
                output.Write($"| {Severity(diagnostic.Severity)} | {diagnostic.Code} | {Cell(report.FileOf(diagnostic))} | {Cell(diagnostic.Message)} |\n");
            }

            output.Write("\n");
        }

        output.Write(Summary(reports) + "\n");
    }

    private static void Json(IReadOnlyList<BackgroundReport> reports, TextWriter output)
    {
        using var stream = new MemoryStream();
        using (var json = new Utf8JsonWriter(stream, JsonOptions))
        {
            json.WriteStartObject();
            json.WriteNumber("version", 1);
            json.WriteStartObject("tool");
            json.WriteString("name", ToolInfo.Name);
            json.WriteString("version", ToolInfo.Version);
            json.WriteEndObject();
            json.WriteStartArray("backgrounds");
            foreach (var report in reports)
            {
                json.WriteStartObject();
                json.WriteString("name", report.Name);
                json.WriteString("folder", report.Folder);
                json.WriteStartArray("diagnostics");
                foreach (var diagnostic in report.Diagnostics)
                {
                    json.WriteStartObject();
                    json.WriteString("code", diagnostic.Code);
                    json.WriteString("severity", Severity(diagnostic.Severity));
                    json.WriteString("message", diagnostic.Message);
                    json.WriteString("file", report.FileOf(diagnostic));
                    json.WriteEndObject();
                }

                json.WriteEndArray();
                json.WriteEndObject();
            }

            json.WriteEndArray();
            var counts = Counts.Of(reports);
            json.WriteStartObject("summary");
            json.WriteNumber("errors", counts.Errors);
            json.WriteNumber("warnings", counts.Warnings);
            json.WriteNumber("infos", counts.Infos);
            json.WriteEndObject();
            json.WriteEndObject();
        }

        output.Write(Encoding.UTF8.GetString(stream.ToArray()) + "\n");
    }

    private static void Sarif(IReadOnlyList<BackgroundReport> reports, TextWriter output)
    {
        var codes = reports.SelectMany(r => r.Diagnostics).Select(d => d.Code).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();

        using var stream = new MemoryStream();
        using (var json = new Utf8JsonWriter(stream, JsonOptions))
        {
            json.WriteStartObject();
            json.WriteString("$schema", "https://json.schemastore.org/sarif-2.1.0.json");
            json.WriteString("version", "2.1.0");
            json.WriteStartArray("runs");
            json.WriteStartObject();
            json.WriteStartObject("tool");
            json.WriteStartObject("driver");
            json.WriteString("name", ToolInfo.Name);
            json.WriteString("version", ToolInfo.Version);
            json.WriteString("informationUri", ToolInfo.InformationUri);
            json.WriteStartArray("rules");
            foreach (string code in codes)
            {
                json.WriteStartObject();
                json.WriteString("id", code);
                json.WriteStartObject("shortDescription");
                json.WriteString("text", RuleCatalog.Describe(code));
                json.WriteEndObject();
                json.WriteEndObject();
            }

            json.WriteEndArray();
            json.WriteEndObject();
            json.WriteEndObject();
            json.WriteStartArray("results");
            foreach (var report in reports)
            {
                foreach (var diagnostic in report.Diagnostics)
                {
                    json.WriteStartObject();
                    json.WriteString("ruleId", diagnostic.Code);
                    json.WriteString("level", diagnostic.Severity switch
                    {
                        ShaderSeverity.Error => "error",
                        ShaderSeverity.Warning => "warning",
                        _ => "note",
                    });
                    json.WriteStartObject("message");
                    json.WriteString("text", diagnostic.Message);
                    json.WriteEndObject();
                    json.WriteStartArray("locations");
                    json.WriteStartObject();
                    json.WriteStartObject("physicalLocation");
                    json.WriteStartObject("artifactLocation");
                    json.WriteString("uri", ArtifactUri(report.FileOf(diagnostic)));
                    json.WriteEndObject();
                    json.WriteEndObject();
                    json.WriteEndObject();
                    json.WriteEndArray();
                    json.WriteEndObject();
                }
            }

            json.WriteEndArray();
            json.WriteEndObject();
            json.WriteEndArray();
            json.WriteEndObject();
        }

        output.Write(Encoding.UTF8.GetString(stream.ToArray()) + "\n");
    }

    private static string Summary(IReadOnlyList<BackgroundReport> reports)
    {
        var counts = Counts.Of(reports);
        return $"{Plural(reports.Count, "background")} checked: {Plural(counts.Errors, "error")}, {Plural(counts.Warnings, "warning")}, "
               + $"{Plural(counts.Infos, "remark")}.";
    }

    private static string Plural(int count, string noun) => count == 1 ? $"1 {noun}" : $"{count} {noun}s";

    // A path as a SARIF uri: a relative one stays relative (escaped), an absolute one becomes a file uri.
    private static string ArtifactUri(string path) =>
        path.StartsWith('/') || (path.Length > 1 && path[1] == ':')
            ? new Uri(path).AbsoluteUri
            : string.Join('/', path.Split('/').Select(Uri.EscapeDataString));

    private static string OneLine(string text) => LineBreaks().Replace(text, " ").Trim();

    private static string Cell(string text) => OneLine(text).Replace("|", "\\|", StringComparison.Ordinal);

    [GeneratedRegex(@"\s*\r?\n\s*")]
    private static partial Regex LineBreaks();
}

/// <summary>A short description for each lint code, for the rules of a SARIF log.</summary>
internal static class RuleCatalog
{
    private static readonly Dictionary<string, string> Descriptions = new(StringComparer.Ordinal)
    {
        [ShaderCodes.ManifestMissing] = "manifest.json is missing.",
        [ShaderCodes.ManifestNotJson] = "The manifest is not valid JSON.",
        [ShaderCodes.ManifestVersion] = "The manifest version is missing or not supported.",
        [ShaderCodes.UnknownKey] = "An unknown key in the manifest.",
        [ShaderCodes.WrongType] = "A manifest value has the wrong JSON type.",
        [ShaderCodes.FolderName] = "The folder's name is not a valid background name.",
        [ShaderCodes.EntryMissing] = "The entry file does not exist.",
        [ShaderCodes.EntryLeavesFolder] = "The entry is absolute or leaves the folder.",
        [ShaderCodes.CompileFailed] = "The shader does not compile.",
        [ShaderCodes.ChildShader] = "The shader declares a uniform shader.",
        [ShaderCodes.UnsupportedUniform] = "A uniform has a type that cannot be set.",
        [ShaderCodes.BadDefault] = "A default value cannot be read.",
        [ShaderCodes.AnnotatedNotDeclared] = "The manifest annotates a parameter the shader does not declare.",
        [ShaderCodes.DeclaredNotAnnotated] = "The shader declares a parameter the manifest does not annotate.",
        [ShaderCodes.HostKeyAnnotated] = "The manifest annotates a name that belongs to the product.",
        [ShaderCodes.ColorKindMismatch] = "A colour parameter needs three or four components.",
        [ShaderCodes.UnknownKind] = "An unknown parameter kind.",
        [ShaderCodes.RangeProblem] = "A parameter's range or default is wrong.",
        [ShaderCodes.NoTitle] = "The manifest has no title.",
        [ShaderCodes.NoDescription] = "The manifest has no description.",
        [ShaderCodes.NoParameterDescription] = "A parameter has no description.",
        [ShaderCodes.NoSamples] = "The manifest lists no samples.",
        [ShaderCodes.SampleMissing] = "A sample file does not exist.",
        [ShaderCodes.SampleLeavesFolder] = "A sample file is absolute or leaves the folder.",
        [ShaderCodes.UnknownSampleType] = "An unknown sample type.",
        [ShaderCodes.LayoutKindDisagree] = "The shader and the manifest disagree about a parameter's kind.",
        [ShaderCodes.BlankRender] = "The shader drew nothing.",
    };

    public static string Describe(string code) => Descriptions.GetValueOrDefault(code, code);
}
