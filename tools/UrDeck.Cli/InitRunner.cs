// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Text.Json.Nodes;
using UrDeck.Engine.Backgrounds;

namespace UrDeck.Cli;

/// <summary>
/// <c>shader init</c>: a background folder with a manifest from a shader. Every refusal is raised before anything is created;
/// the manifest is written before the shader is moved, so a failure half-way leaves a loose file where it was.
/// </summary>
internal static class InitRunner
{
    private const string ShaderExtension = ".sksl";

    public static int Run(InitOptions options, TextWriter output, TextWriter error)
    {
        string path = Path.TrimEndingDirectorySeparator(Path.GetFullPath(options.Path));
        bool isFolder = Directory.Exists(path);
        if (!isFolder && !File.Exists(path))
            throw new ToolFailureException($"{PathDisplay.Of(options.Path)} does not exist; give a .sksl file or a folder that holds one.");

        string? outputDirectory = null;
        if (options.Output != null)
        {
            if (isFolder)
                throw new ToolFailureException("--output applies to a shader file; a folder is worked on in place.");

            outputDirectory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(options.Output));
            if (!Directory.Exists(outputDirectory))
                throw new ToolFailureException($"the output directory {PathDisplay.Of(outputDirectory)} does not exist.");
        }

        string folder;
        string name;
        string? loose = null;
        if (isFolder)
        {
            folder = path;
            name = Path.GetFileName(folder);
        }
        else
        {
            if (!string.Equals(Path.GetExtension(path), ShaderExtension, StringComparison.OrdinalIgnoreCase))
                throw new ToolFailureException($"{PathDisplay.Of(path)} is not a {ShaderExtension} file.");

            loose = path;
            name = Path.GetFileNameWithoutExtension(path);
            folder = Path.Combine(outputDirectory ?? Path.GetDirectoryName(path)!, name);
        }

        if (!BackgroundSettings.IsValidName(name))
        {
            throw new ToolFailureException(
                $"'{name}' is not a valid background name; a name may only contain letters, digits, '-' and '_'.");
        }

        string shaderInFolder = Path.Combine(folder, name + ShaderExtension);
        string manifestPath = Path.Combine(folder, BackgroundManifest.FileName);
        if (File.Exists(manifestPath))
            throw new ToolFailureException($"{PathDisplay.Of(manifestPath)} exists; init never overwrites a manifest.");

        if (File.Exists(folder))
            throw new ToolFailureException($"{PathDisplay.Of(folder)} exists and is a file.");

        bool createFolder = !Directory.Exists(folder);
        if (loose == null)
        {
            if (!File.Exists(shaderInFolder))
                throw new ToolFailureException($"{PathDisplay.Of(folder)} holds no {name + ShaderExtension}; init needs the shader in a folder of the same name.");
        }
        else if (File.Exists(shaderInFolder))
        {
            throw new ToolFailureException($"{PathDisplay.Of(shaderInFolder)} exists; nothing was moved. To make its manifest, run init on the folder.");
        }

        string text = ReadText(loose ?? shaderInFolder);
        var (manifest, skipped) = ManifestStub.Build(name, text);
        string serialized = ManifestWriter.Serialize(manifest);

        // Past this point something is written: the folder, then the manifest, then the shader moves in.
        try
        {
            if (createFolder)
                Directory.CreateDirectory(folder);
            ManifestWriter.WriteText(manifestPath, serialized);
            if (loose != null)
                File.Move(loose, shaderInFolder);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ToolFailureException)
        {
            Undo(createFolder, folder, manifestPath);
            throw new ToolFailureException($"{PathDisplay.Of(folder)} could not be made: {ex.Message}");
        }

        int count = ((JsonObject)manifest["parameters"]!).Count;
        if (loose != null)
            output.Write($"made {PathDisplay.Of(folder)}/ and moved {PathDisplay.Of(loose)} to {PathDisplay.Of(shaderInFolder)}\n");
        output.Write($"wrote {PathDisplay.Of(manifestPath)} with {count} parameter{(count == 1 ? "" : "s")}\n");
        foreach (string uniform in skipped)
            output.Write($"note: no entry for '{uniform}': a uniform of that type cannot be set (float, float2, float3 and float4 can)\n");
        output.Write($"next: urdeck-cli shader validate {PathDisplay.Of(folder)}\n");
        return ExitCodes.Ok;
    }

    private static string ReadText(string path)
    {
        try
        {
            return File.ReadAllText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new ToolFailureException($"{PathDisplay.Of(path)} cannot be read: {ex.Message}");
        }
    }

    /// <summary>Removes what this run made, as far as it can; never the shader.</summary>
    private static void Undo(bool createdFolder, string folder, string manifestPath)
    {
        try
        {
            File.Delete(manifestPath);
            if (createdFolder && Directory.Exists(folder) && !Directory.EnumerateFileSystemEntries(folder).Any())
                Directory.Delete(folder);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
