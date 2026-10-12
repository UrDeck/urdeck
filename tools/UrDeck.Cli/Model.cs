// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Engine.Backgrounds;

namespace UrDeck.Cli;

internal sealed record ValidateOptions(string? Folder, string? All, string Format, bool FailOnWarnings);

internal sealed record RenderOptions(
    string Folder, string Output, string Size, float Time, string? Theme, string? Colors, IReadOnlyList<string> Param, bool FailOnWarnings);

/// <summary>One background and what the lint found in it. <see cref="FullPath"/> is the folder on disk, <see cref="Folder"/> how it is printed.</summary>
internal sealed record BackgroundReport(string Name, string FullPath, IReadOnlyList<ShaderDiagnostic> Diagnostics)
{
    public string Folder => PathDisplay.Of(FullPath);

    public string FileOf(ShaderDiagnostic diagnostic) => PathDisplay.In(FullPath, diagnostic.File);
}

internal sealed record Counts(int Errors, int Warnings, int Infos)
{
    public static Counts Of(IEnumerable<BackgroundReport> reports)
    {
        var all = reports.SelectMany(r => r.Diagnostics).ToList();
        return new Counts(
            all.Count(d => d.Severity == ShaderSeverity.Error),
            all.Count(d => d.Severity == ShaderSeverity.Warning),
            all.Count(d => d.Severity == ShaderSeverity.Info));
    }
}
