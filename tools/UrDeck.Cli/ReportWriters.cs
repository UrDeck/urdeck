// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using UrDeck.Engine.Backgrounds;
using UrDeck.Engine.Themes;

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

    public static void Write(string format, IReadOnlyList<SubjectReport> reports, TextWriter output)
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

    public static string Severity(DiagnosticSeverity severity) => severity switch
    {
        DiagnosticSeverity.Error => "error",
        DiagnosticSeverity.Warning => "warning",
        _ => "info",
    };

    /// <summary>One finding as the text format prints it: <c>path: severity CODE: message</c>.</summary>
    public static string TextLine(string path, Diagnostic diagnostic) =>
        $"{path}: {Severity(diagnostic.Severity)} {diagnostic.Code}: {OneLine(diagnostic.Message)}";

    private static void Text(IReadOnlyList<SubjectReport> reports, TextWriter output)
    {
        foreach (var report in reports)
            foreach (var diagnostic in report.Diagnostics)
                output.Write(TextLine(report.FileOf(diagnostic), diagnostic) + "\n");

        output.Write(Summary(reports) + "\n");
    }

    private static void Markdown(IReadOnlyList<SubjectReport> reports, TextWriter output)
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

    private static void Json(IReadOnlyList<SubjectReport> reports, TextWriter output)
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
            json.WriteStartArray(ArrayName(reports));
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
                    if (diagnostic.Key != null)
                        json.WriteString("key", diagnostic.Key);
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

    private static void Sarif(IReadOnlyList<SubjectReport> reports, TextWriter output)
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
                        DiagnosticSeverity.Error => "error",
                        DiagnosticSeverity.Warning => "warning",
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

    private static string Summary(IReadOnlyList<SubjectReport> reports)
    {
        var counts = Counts.Of(reports);
        return $"{Plural(reports.Count, Noun(reports))} checked: {Plural(counts.Errors, "error")}, {Plural(counts.Warnings, "warning")}, "
               + $"{Plural(counts.Infos, "remark")}.";
    }

    private static string Plural(int count, string noun) => count == 1 ? $"1 {noun}" : $"{count} {noun}s";

    // A run is about one kind of subject; the first report names it.
    private static string Noun(IReadOnlyList<SubjectReport> reports) => reports[0].Kind == SubjectKind.Theme ? "theme" : "background";

    private static string ArrayName(IReadOnlyList<SubjectReport> reports) => reports[0].Kind == SubjectKind.Theme ? "themes" : "backgrounds";

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
        [ThemeCodes.FileUnusable] = "theme.json is missing, is not valid JSON or holds no object.",
        [ThemeCodes.UnknownKey] = "A key the deck does not read.",
        [ThemeCodes.WrongType] = "A value of the wrong JSON type.",
        [ThemeCodes.OutOfRange] = "A number outside its range.",
        [ThemeCodes.NotOneOf] = "A value that is not one of the names allowed.",
        [ThemeCodes.BadColor] = "A colour that is not #RRGGBB or #AARRGGBB.",
        [ThemeCodes.BadList] = "A list outside its length limits or with an invalid element.",
        [ThemeCodes.MissingMember] = "A group that needs a member it does not have.",
        [ThemeCodes.NewerFormat] = "The format is newer than the deck reads.",
        [ThemeCodes.FontFile] = "A font file is missing, has the wrong extension or leaves the theme's folder.",
        [ThemeCodes.FontFileSize] = "A font file is over 3 MB or has no licence file beside it.",
        [ThemeCodes.FontFamily] = "A font is named by family, so it is used only where it is installed.",
        [ThemeCodes.FontWeight] = "A weight the font does not have.",
        [ThemeCodes.UnknownBackground] = "The default background does not exist.",
        [ThemeCodes.UnknownPairing] = "A paired background does not exist.",
        [ThemeCodes.DefaultNotPaired] = "The default background is not in the pairing list.",
        [ThemeCodes.StillPicture] = "The still background picture is missing, leaves the folder or is too large.",
        [ThemeCodes.UnknownIconSet] = "The icon set does not exist.",
        [ThemeCodes.BorderWithoutWidth] = "A border style, gradient or animation with a border width of 0.",
        [ThemeCodes.SpinWithoutConic] = "A spinning border without a conic gradient.",
        [ThemeCodes.PatternDense] = "A pattern whose marks are closer than three pixels.",
        [ThemeCodes.CornerMarksReach] = "Corner marks reaching past the gap.",
        [ThemeCodes.EffectReach] = "A shadow or glow reaching further than the gap.",
        [ThemeCodes.Hairline] = "A line that resolves under one pixel.",
        [ThemeCodes.CompactCapsule] = "A compact circle on a card that is not square.",
        [ThemeCodes.ContrastText] = "The text colour has too little contrast on the card fill.",
        [ThemeCodes.ContrastTint] = "The text colour has too little contrast on a card tint.",
        [ThemeCodes.ContrastMuted] = "The muted text colour has too little contrast on the card fill.",
        [ThemeCodes.NoMetadata] = "The theme's meta lacks a name, a description or a mode.",
        [ThemeCodes.Animations] = "More than one continuous animation is on.",
    };

    public static string Describe(string code) => Descriptions.GetValueOrDefault(code, code);
}
