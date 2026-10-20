// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Engine.Backgrounds;
using UrDeck.Engine.Themes;

namespace UrDeck.Cli;

internal sealed record ValidateOptions(string? Folder, string? All, string Format, bool FailOnWarnings);

internal sealed record InitOptions(string Path, string? Output);

internal sealed record RenderOptions(
    string Folder, string Output, string Size, float Time, string? Theme, string? Colors, IReadOnlyList<string> Param, bool FailOnWarnings, bool UpdateManifest = false);

/// <summary>What a report is about. The kind names the array of the JSON output and the noun of the summary.</summary>
internal enum SubjectKind
{
    Background,
    Theme,
}

internal enum DiagnosticSeverity
{
    Info,
    Warning,
    Error,
}

/// <summary>One finding as the writers print it, whatever it is about. A <see cref="Key"/> is the path of a value in a theme.</summary>
internal sealed record Diagnostic(string Code, DiagnosticSeverity Severity, string Message, string File, string? Key = null)
{
    public static Diagnostic From(ShaderDiagnostic diagnostic) => new(diagnostic.Code, diagnostic.Severity switch
    {
        ShaderSeverity.Error => DiagnosticSeverity.Error,
        ShaderSeverity.Warning => DiagnosticSeverity.Warning,
        _ => DiagnosticSeverity.Info,
    }, diagnostic.Message, diagnostic.File);

    public static Diagnostic From(ThemeDiagnostic diagnostic) => new(diagnostic.Code, diagnostic.Severity switch
    {
        ThemeSeverity.Error => DiagnosticSeverity.Error,
        ThemeSeverity.Warning => DiagnosticSeverity.Warning,
        _ => DiagnosticSeverity.Info,
    }, diagnostic.Message, diagnostic.File, diagnostic.Key);
}

/// <summary>
/// One subject (a background or a theme) and what the lint found in it. <see cref="FullPath"/> is the folder on disk, or the
/// <see cref="Display"/> text for a subject with none; <see cref="Folder"/> is how it is printed.
/// </summary>
internal sealed record SubjectReport(SubjectKind Kind, string Name, string FullPath, IReadOnlyList<Diagnostic> Diagnostics, string? Display = null)
{
    /// <summary>The folder as it is printed: relative to the working directory, or the given display text (<c>builtin:carbon</c>).</summary>
    public string Folder => Display ?? PathDisplay.Of(FullPath);

    public string FileOf(Diagnostic diagnostic) =>
        Display == null ? PathDisplay.In(FullPath, diagnostic.File) : $"{Display}/{diagnostic.File}";

    public static SubjectReport Background(string name, string fullPath, IEnumerable<ShaderDiagnostic> diagnostics) =>
        new(SubjectKind.Background, name, fullPath, diagnostics.Select(Diagnostic.From).ToList());

    public static SubjectReport Theme(string name, string fullPath, IEnumerable<ThemeDiagnostic> diagnostics, string? display = null) =>
        new(SubjectKind.Theme, name, fullPath, diagnostics.Select(Diagnostic.From).ToList(), display);
}

internal sealed record Counts(int Errors, int Warnings, int Infos)
{
    public static Counts Of(IEnumerable<SubjectReport> reports)
    {
        var all = reports.SelectMany(r => r.Diagnostics).ToList();
        return new Counts(
            all.Count(d => d.Severity == DiagnosticSeverity.Error),
            all.Count(d => d.Severity == DiagnosticSeverity.Warning),
            all.Count(d => d.Severity == DiagnosticSeverity.Info));
    }
}
