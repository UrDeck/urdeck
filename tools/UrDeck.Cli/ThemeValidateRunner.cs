// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Engine.Themes;

namespace UrDeck.Cli;

internal sealed record ThemeValidateOptions(
    string? Folder, string? All, bool Builtin, IReadOnlyList<string> BackgroundDirs, string Format, bool FailOnWarnings);

/// <summary>
/// <c>theme validate</c>: the engine's theme lint on one folder, on every folder in a directory, or on the built-in themes as
/// the product ships them, printed in one format.
/// </summary>
internal static class ThemeValidateRunner
{
    public static int Run(ThemeValidateOptions options, TextWriter output)
    {
        int given = (options.Folder != null ? 1 : 0) + (options.All != null ? 1 : 0) + (options.Builtin ? 1 : 0);
        if (given > 1)
            throw new ToolFailureException("give one of a folder, --all <directory> or --builtin, not more than one.");
        if (given == 0)
            throw new ToolFailureException("give the folder to check, --all <directory> or --builtin.");
        if (options.Folder is { } argument && argument.StartsWith('-'))
            throw new ToolFailureException($"unknown option '{argument}'.");

        var lint = new ThemeLintOptions(options.BackgroundDirs.Select(BackgroundDirectory).ToList());

        using var log = new LogCapture();
        List<SubjectReport> reports;
        if (options.Builtin)
            reports = ThemeStore.BuiltInNames.Order(StringComparer.Ordinal).Select(name => CheckBuiltIn(name, lint)).ToList();
        else if (options.All != null)
            reports = Themes(options.All).Select(folder => CheckFolder(folder, lint)).ToList();
        else
            reports = [CheckFolder(FullFolder(options.Folder!), lint)];

        var counts = Counts.Of(reports);
        using var buffer = new StringWriter { NewLine = "\n" };
        ReportWriters.Write(options.Format, reports, buffer);
        output.Write(buffer.ToString());

        bool fails = counts.Errors > 0 || (options.FailOnWarnings && counts.Warnings > 0);
        return fails ? ExitCodes.Findings : ExitCodes.Ok;
    }

    private static SubjectReport CheckBuiltIn(string name, ThemeLintOptions lint) =>
        SubjectReport.Theme(name, $"builtin:{name}", ThemeLint.Check(new EmbeddedThemeSource(name), lint), display: $"builtin:{name}");

    private static SubjectReport CheckFolder(string folder, ThemeLintOptions lint)
    {
        var source = new DirectoryThemeSource(folder);
        try
        {
            return SubjectReport.Theme(source.Name, folder, ThemeLint.Check(source, lint));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new ToolFailureException($"the folder {PathDisplay.Of(folder)} could not be read: {ex.Message}");
        }
    }

    private static string FullFolder(string path)
    {
        string full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        if (!Directory.Exists(full))
            throw new ToolFailureException($"the folder {PathDisplay.Of(path)} does not exist.");
        return full;
    }

    private static string BackgroundDirectory(string path)
    {
        string full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        if (!Directory.Exists(full))
            throw new ToolFailureException($"the background directory {PathDisplay.Of(path)} does not exist.");
        return full;
    }

    /// <summary>The folders directly inside the directory in ordinal name order; hidden ones and files are not themes.</summary>
    private static List<string> Themes(string directory)
    {
        string full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
        if (!Directory.Exists(full))
            throw new ToolFailureException($"the directory {PathDisplay.Of(directory)} does not exist.");

        List<string> folders;
        try
        {
            folders = Directory.EnumerateDirectories(full)
                .Where(f => !Path.GetFileName(f).StartsWith('.') && !new DirectoryInfo(f).Attributes.HasFlag(FileAttributes.Hidden))
                .OrderBy(Path.GetFileName, StringComparer.Ordinal)
                .ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new ToolFailureException($"the directory {PathDisplay.Of(directory)} could not be read: {ex.Message}");
        }

        if (folders.Count == 0)
            throw new ToolFailureException($"no themes were found in {PathDisplay.Of(directory)}.");
        return folders;
    }
}
