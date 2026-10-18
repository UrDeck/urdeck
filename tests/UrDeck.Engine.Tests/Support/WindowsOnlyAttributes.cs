// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Runtime.CompilerServices;
using Xunit;

namespace UrDeck.Engine.Tests.Support;

/// <summary>
/// A test that needs Windows (the shell, COM, the system provider, named kernel objects). It runs on Windows and shows as
/// skipped, with this reason, elsewhere. Pair it with <c>[SupportedOSPlatform("windows")]</c> on the class when the code
/// under test is marked that way, so CA1416 stays on.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
internal sealed class WindowsFactAttribute : FactAttribute
{
    public WindowsFactAttribute([CallerFilePath] string? sourceFilePath = null, [CallerLineNumber] int sourceLineNumber = -1)
        : base(sourceFilePath, sourceLineNumber)
    {
        if (!OperatingSystem.IsWindows())
            Skip = WindowsOnly.Reason;
    }
}

/// <summary>A <c>[Theory]</c> that needs Windows; see <see cref="WindowsFactAttribute"/>.</summary>
[AttributeUsage(AttributeTargets.Method)]
internal sealed class WindowsTheoryAttribute : TheoryAttribute
{
    public WindowsTheoryAttribute([CallerFilePath] string? sourceFilePath = null, [CallerLineNumber] int sourceLineNumber = -1)
        : base(sourceFilePath, sourceLineNumber)
    {
        if (!OperatingSystem.IsWindows())
            Skip = WindowsOnly.Reason;
    }
}

/// <summary>
/// A test whose expectation is exact down to the glyph pixels or advances, which only the Windows rasterizer meets: on Linux
/// FreeType hints the advances (a line shrunk to fit ends up narrower than asked, because the width is not linear in the
/// size), and an antialiased edge pixel can equal the muted colour (white at 70 %) exactly. Skipped elsewhere; the rest of
/// the text tests run on both.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
internal sealed class WindowsGlyphFactAttribute : FactAttribute
{
    public WindowsGlyphFactAttribute([CallerFilePath] string? sourceFilePath = null, [CallerLineNumber] int sourceLineNumber = -1)
        : base(sourceFilePath, sourceLineNumber)
    {
        if (!OperatingSystem.IsWindows())
            Skip = WindowsOnly.GlyphReason;
    }
}

/// <summary>A <c>[Theory]</c> that expects pixel-exact text; see <see cref="WindowsGlyphFactAttribute"/>.</summary>
[AttributeUsage(AttributeTargets.Method)]
internal sealed class WindowsGlyphTheoryAttribute : TheoryAttribute
{
    public WindowsGlyphTheoryAttribute([CallerFilePath] string? sourceFilePath = null, [CallerLineNumber] int sourceLineNumber = -1)
        : base(sourceFilePath, sourceLineNumber)
    {
        if (!OperatingSystem.IsWindows())
            Skip = WindowsOnly.GlyphReason;
    }
}

internal static class WindowsOnly
{
    public const string GlyphReason =
        "Windows only: it expects pixel-exact text; FreeType hints advances and its antialiased edges can equal a flat colour.";

    public const string Reason = "Windows only: the code under test uses the Windows shell, registry or kernel objects.";
}
