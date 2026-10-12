// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

namespace UrDeck.Engine.Backgrounds;

/// <summary>
/// Checks a background folder without showing it. The host runs it when a background loads and a later command line
/// tool calls it, so both report the same findings. Needs no window, palette or size: the shader is compiled and thrown away.
/// </summary>
public static class ShaderLint
{
    /// <summary>
    /// Every diagnostic of the background in <paramref name="source"/>, in a stable order: the manifest, the entry, the
    /// shader, the parameters, the description, the samples. <paramref name="name"/> is the folder's name, checked first (the default entry is
    /// <c>&lt;name&gt;.sksl</c>). An empty list is a clean background.
    /// </summary>
    public static IReadOnlyList<ShaderDiagnostic> Check(IBackgroundSource source, string name)
    {
        var found = new List<ShaderDiagnostic>();

        if (!BackgroundSettings.IsValidName(name))
        {
            found.Add(new ShaderDiagnostic(ShaderCodes.FolderName, ShaderSeverity.Error,
                $"the folder name '{name}' is not a valid background name; a name may only contain letters, digits, '-' and '_'.", ""));
        }

        string? manifestText = source.ReadText(BackgroundManifest.FileName);
        if (manifestText == null)
        {
            found.Add(new ShaderDiagnostic(ShaderCodes.ManifestMissing, ShaderSeverity.Error,
                $"{BackgroundManifest.FileName} is missing; a background needs one (\"manifest\": 1).", BackgroundManifest.FileName));
            return found;
        }

        var read = BackgroundManifestReader.Read(manifestText);
        found.AddRange(read.Diagnostics);
        if (read.Manifest == null)
            return found;

        var manifest = read.Manifest;
        ShaderDeclarations? declarations = CheckEntry(source, name, manifest, found);
        if (declarations != null)
            CheckParameters(declarations, manifest, found);

        CheckDescription(manifest, found);
        CheckSamples(source, manifest, found);
        return found;
    }

    private static ShaderDeclarations? CheckEntry(IBackgroundSource source, string name, BackgroundManifest manifest, List<ShaderDiagnostic> found)
    {
        string entry = manifest.Entry ?? name + ShaderStore.FileExtension;
        string? path = BackgroundPaths.Normalize(entry);
        if (path == null)
        {
            found.Add(Make(ShaderCodes.EntryLeavesFolder, ShaderSeverity.Error,
                $"entry '{entry}' must name a file inside the folder (no absolute path, no '..').", BackgroundManifest.FileName));
            return null;
        }

        string? text = source.ReadText(path);
        if (text == null)
        {
            found.Add(Make(ShaderCodes.EntryMissing, ShaderSeverity.Error, $"the entry file '{path}' does not exist.", path));
            return null;
        }

        var declarations = ShaderDeclarations.Parse(text, name);
        bool child = declarations.HasChildShader;
        if (child)
        {
            found.Add(Make(ShaderCodes.ChildShader, ShaderSeverity.Error,
                "a 'uniform shader' (a child shader) cannot be used; there is nothing to give it.", path));
        }
        else
        {
            using var effect = ShaderProgram.Compile(text, out string? failure);
            if (effect == null)
                found.Add(Make(ShaderCodes.CompileFailed, ShaderSeverity.Error, failure!, path));
            else if (effect.Children.Count > 0)
                found.Add(Make(ShaderCodes.ChildShader, ShaderSeverity.Error, "a child shader cannot be used; there is nothing to give it.", path));
        }

        foreach (var diagnostic in declarations.Diagnostics)
            found.Add(diagnostic with { File = path });
        return declarations;
    }

    private static void CheckParameters(ShaderDeclarations declarations, BackgroundManifest manifest, List<ShaderDiagnostic> found)
    {
        var parameters = BackgroundParameter.Join(declarations, manifest);
        var declared = new HashSet<string>(parameters.Select(p => p.Name), StringComparer.Ordinal);
        string file = BackgroundManifest.FileName;

        foreach (var note in manifest.Parameters.Values)
        {
            if (HostKeys.IsHostKey(note.Name))
            {
                found.Add(Make(ShaderCodes.HostKeyAnnotated, ShaderSeverity.Error,
                    $"'{note.Name}' belongs to the product and cannot be a parameter.", file));
            }
            else if (!declared.Contains(note.Name))
            {
                found.Add(Make(ShaderCodes.AnnotatedNotDeclared, ShaderSeverity.Warning,
                    $"the manifest annotates '{note.Name}', which the shader does not declare as a uniform.", file));
            }
        }

        foreach (var parameter in parameters)
        {
            manifest.Parameters.TryGetValue(parameter.Name, out var note);
            if (note == null)
            {
                found.Add(Make(ShaderCodes.DeclaredNotAnnotated, ShaderSeverity.Warning,
                    $"the shader declares '{parameter.Name}' and the manifest does not annotate it.", file));
                continue;
            }

            if (!parameter.Hidden && string.IsNullOrWhiteSpace(note.Description))
            {
                found.Add(Make(ShaderCodes.NoParameterDescription, ShaderSeverity.Warning,
                    $"parameter '{parameter.Name}' has no description.", file));
            }

            if (parameter.Kind == ParameterKind.Color && parameter.Components is not (3 or 4))
            {
                found.Add(Make(ShaderCodes.ColorKindMismatch, ShaderSeverity.Warning,
                    $"'{parameter.Name}' is a colour but has {parameter.Components} component(s); a colour needs three or four. The kind is ignored.", file));
            }

            if (note.Kind == ParameterKind.Number && declarations.Uniforms.First(u => u.Name == parameter.Name).IsColor)
            {
                found.Add(Make(ShaderCodes.LayoutKindDisagree, ShaderSeverity.Info,
                    $"'{parameter.Name}' is layout(color) in the shader and kind number in the manifest; the manifest wins.", file));
            }

            CheckRange(parameter, found);
        }
    }

    private static void CheckRange(BackgroundParameter parameter, List<ShaderDiagnostic> found)
    {
        if (parameter.Min is { } min && parameter.Max is { } max && min > max)
        {
            found.Add(Make(ShaderCodes.RangeProblem, ShaderSeverity.Warning,
                $"'{parameter.Name}' has min {min} above max {max}.", BackgroundManifest.FileName));
            return;
        }

        if (parameter.Default is { } value && !parameter.InRange(value))
        {
            found.Add(Make(ShaderCodes.RangeProblem, ShaderSeverity.Warning,
                $"the default of '{parameter.Name}' is outside the range {parameter.DescribeRange()}.", BackgroundManifest.FileName));
        }
    }

    private static void CheckDescription(BackgroundManifest manifest, List<ShaderDiagnostic> found)
    {
        if (string.IsNullOrWhiteSpace(manifest.Title))
            found.Add(Make(ShaderCodes.NoTitle, ShaderSeverity.Warning, "the manifest has no title.", BackgroundManifest.FileName));
        if (string.IsNullOrWhiteSpace(manifest.Description))
            found.Add(Make(ShaderCodes.NoDescription, ShaderSeverity.Warning, "the manifest has no description.", BackgroundManifest.FileName));
    }

    private static void CheckSamples(IBackgroundSource source, BackgroundManifest manifest, List<ShaderDiagnostic> found)
    {
        if (manifest.Samples.Count == 0)
        {
            found.Add(Make(ShaderCodes.NoSamples, ShaderSeverity.Warning, "the manifest lists no samples.", BackgroundManifest.FileName));
            return;
        }

        foreach (var sample in manifest.Samples)
        {
            if (string.IsNullOrWhiteSpace(sample.File))
            {
                found.Add(Make(ShaderCodes.SampleMissing, ShaderSeverity.Error, "a sample has no file.", BackgroundManifest.FileName));
            }
            else if (BackgroundPaths.Normalize(sample.File) == null)
            {
                found.Add(Make(ShaderCodes.SampleLeavesFolder, ShaderSeverity.Error,
                    $"sample '{sample.File}' must name a file inside the folder (no absolute path, no '..').", BackgroundManifest.FileName));
            }
            else if (!source.Exists(sample.File))
            {
                found.Add(Make(ShaderCodes.SampleMissing, ShaderSeverity.Error, $"the sample file '{sample.File}' does not exist.", sample.File));
            }
        }
    }

    private static ShaderDiagnostic Make(string code, ShaderSeverity severity, string message, string file) => new(code, severity, message, file);
}
