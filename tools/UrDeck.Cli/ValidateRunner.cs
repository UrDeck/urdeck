// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Engine.Backgrounds;

namespace UrDeck.Cli;

/// <summary><c>shader validate</c>: the engine's lint on one folder or on every folder in a directory, printed in one format.</summary>
internal static class ValidateRunner
{
    public static int Run(ValidateOptions options, TextWriter output, TextWriter error)
    {
        if (options.Folder != null && options.All != null)
            throw new ToolFailureException("give a folder or --all <directory>, not both.");
        if (options.Folder == null && options.All == null)
            throw new ToolFailureException("give the folder to check, or --all <directory>.");
        if (options.Folder is { } folder && folder.StartsWith('-'))
            throw new ToolFailureException($"unknown option '{folder}'.");

        using var log = new LogCapture();
        var folders = options.All != null ? Backgrounds(options.All) : [FullFolder(options.Folder!)];
        var reports = folders.Select(Check).ToList();

        var counts = Counts.Of(reports);
        using var buffer = new StringWriter { NewLine = "\n" };
        ReportWriters.Write(options.Format, reports, buffer);
        output.Write(buffer.ToString());

        bool fails = counts.Errors > 0 || (options.FailOnWarnings && counts.Warnings > 0);
        return fails ? ExitCodes.Findings : ExitCodes.Ok;
    }

    private static BackgroundReport Check(string folder)
    {
        string name = Path.GetFileName(folder);
        try
        {
            return new BackgroundReport(name, folder, ShaderLint.Check(new DirectoryBackgroundSource(folder), name));
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

    /// <summary>The folders directly inside the directory in ordinal name order; hidden ones and files are not backgrounds.</summary>
    private static List<string> Backgrounds(string directory)
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
            throw new ToolFailureException($"no backgrounds were found in {PathDisplay.Of(directory)}.");
        return folders;
    }
}
