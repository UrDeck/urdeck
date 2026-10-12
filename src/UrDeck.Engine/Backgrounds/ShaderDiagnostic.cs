// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

namespace UrDeck.Engine.Backgrounds;

public enum ShaderSeverity
{
    Info,
    Warning,
    Error,
}

/// <summary>One finding about a background folder. The code keeps its meaning in later versions.</summary>
public sealed record ShaderDiagnostic(string Code, ShaderSeverity Severity, string Message, string File)
{
    public override string ToString() => $"{Code} {Message} ({File})";
}

/// <summary>The diagnostic codes, fixed so the log, the documentation, the tests and a later tool agree.</summary>
public static class ShaderCodes
{
    public const string ManifestMissing = "SHADER001";
    public const string ManifestNotJson = "SHADER002";
    public const string ManifestVersion = "SHADER003";
    public const string UnknownKey = "SHADER004";
    public const string WrongType = "SHADER005";
    public const string EntryMissing = "SHADER010";
    public const string EntryLeavesFolder = "SHADER011";
    public const string CompileFailed = "SHADER020";
    public const string ChildShader = "SHADER021";
    public const string UnsupportedUniform = "SHADER022";
    public const string BadDefault = "SHADER023";
    public const string AnnotatedNotDeclared = "SHADER030";
    public const string DeclaredNotAnnotated = "SHADER031";
    public const string HostKeyAnnotated = "SHADER032";
    public const string ColorKindMismatch = "SHADER033";
    public const string UnknownKind = "SHADER034";
    public const string RangeProblem = "SHADER035";
    public const string NoTitle = "SHADER040";
    public const string NoDescription = "SHADER041";
    public const string NoParameterDescription = "SHADER042";
    public const string NoSamples = "SHADER050";
    public const string SampleMissing = "SHADER051";
    public const string SampleLeavesFolder = "SHADER052";
    public const string UnknownSampleType = "SHADER053";
    public const string LayoutKindDisagree = "SHADER060";

    /// <summary>The errors that also stop the background from loading (the others leave it shown).</summary>
    public static bool StopsLoading(string code) =>
        code is ManifestMissing or ManifestNotJson or ManifestVersion or EntryMissing or EntryLeavesFolder or CompileFailed or ChildShader;
}
