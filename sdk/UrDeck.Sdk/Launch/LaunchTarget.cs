// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Patrick Bigler

namespace UrDeck.Sdk.Launch;

/// <summary>What a launch target text is.</summary>
public enum LaunchTargetKind
{
    /// <summary>Nothing that can be started: empty, a relative path, or a malformed web address.</summary>
    Invalid,

    /// <summary>A rooted path to a file, a folder, an executable or a <c>.lnk</c>, or a bare name Windows resolves itself.</summary>
    File,

    /// <summary>An <c>http</c> or <c>https</c> address.</summary>
    Web,

    /// <summary>A <c>shell:</c> item, for example a packaged application under <c>shell:AppsFolder</c>.</summary>
    Shell,

    /// <summary>An address with any other scheme, handed to the application registered for it.</summary>
    Uri,
}

/// <summary>
/// A target text as the host's launch and icon services understand it. <see cref="Parse"/> is the one classification the
/// widget and the host share, so a widget can tell a valid target from an invalid one without starting anything.
/// </summary>
/// <param name="Kind">What the target is.</param>
/// <param name="Text">The target with surrounding space removed and, for a file target, environment variables expanded.</param>
/// <param name="DisplayName">
/// A short name for the target: the host of a web address without a leading <c>www.</c>, the file name of a file target,
/// else the text.
/// </param>
public readonly record struct LaunchTarget(LaunchTargetKind Kind, string Text, string DisplayName)
{
    public bool IsValid => Kind != LaunchTargetKind.Invalid;

    /// <summary>Classifies <paramref name="target"/>. Touches neither the disk nor the network and never throws.</summary>
    public static LaunchTarget Parse(string? target)
    {
        string text = target?.Trim() ?? "";
        if (text.Length == 0)
            return new LaunchTarget(LaunchTargetKind.Invalid, "", "");

        int schemeLength = SchemeLength(text);
        if (schemeLength > 0)
            return ParseAddress(text, text[..schemeLength]);

        return ParseFile(Environment.ExpandEnvironmentVariables(text));
    }

    /// <summary>
    /// The length of a URI scheme at the start of the text, or 0. A scheme has two or more characters, so that the drive
    /// letter of <c>C:\...</c> is not one.
    /// </summary>
    private static int SchemeLength(string text)
    {
        if (!char.IsAsciiLetter(text[0]))
            return 0;
        for (int i = 1; i < text.Length; i++)
        {
            char c = text[i];
            if (c == ':')
                return i >= 2 ? i : 0;
            if (!char.IsAsciiLetterOrDigit(c) && c != '+' && c != '-' && c != '.')
                return 0;
        }

        return 0;
    }

    private static LaunchTarget ParseAddress(string text, string scheme)
    {
        if (scheme.Equals("http", StringComparison.OrdinalIgnoreCase) || scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
        {
            if (!text.AsSpan(scheme.Length).StartsWith("://", StringComparison.Ordinal)
                || text.Any(char.IsWhiteSpace)
                || !System.Uri.TryCreate(text, UriKind.Absolute, out var uri)
                || uri.Host.Length == 0)
            {
                return new LaunchTarget(LaunchTargetKind.Invalid, text, text);
            }

            string host = uri.Host;
            if (host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) && host.Length > 4)
                host = host[4..];
            return new LaunchTarget(LaunchTargetKind.Web, text, host);
        }

        var kind = scheme.Equals("shell", StringComparison.OrdinalIgnoreCase) ? LaunchTargetKind.Shell : LaunchTargetKind.Uri;
        return new LaunchTarget(kind, text, text);
    }

    private static LaunchTarget ParseFile(string text)
    {
        bool hasSeparator = text.Contains('\\') || text.Contains('/');
        if (hasSeparator ? !IsRooted(text) : text.Contains(':'))
            return new LaunchTarget(LaunchTargetKind.Invalid, text, text);

        string name = text.TrimEnd('\\', '/');
        int cut = name.LastIndexOfAny(['\\', '/']);
        if (cut >= 0)
            name = name[(cut + 1)..];
        return new LaunchTarget(LaunchTargetKind.File, text, name.Length > 0 ? name : text);
    }

    /// <summary>A drive path (<c>C:\...</c>) or a network path (<c>\\server\share</c>). Decided from the text alone.</summary>
    private static bool IsRooted(string text) =>
        (text.Length >= 3 && char.IsAsciiLetter(text[0]) && text[1] == ':' && text[2] is '\\' or '/')
        || text.StartsWith(@"\\", StringComparison.Ordinal);
}
