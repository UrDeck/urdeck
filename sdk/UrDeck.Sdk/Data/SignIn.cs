// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Patrick Bigler

namespace UrDeck.Sdk.Data;

/// <summary>
/// What the user has to do to approve a sign-in: scan a QR code, or type a code at an address. The host draws a QR code on this
/// PC from <see cref="QrText"/>; the text is a credential, so it is never sent anywhere, copied or logged. Make one with
/// <see cref="Qr"/> or <see cref="CodeAndAddress"/>.
/// </summary>
public sealed record SignInChallenge
{
    private SignInChallenge()
    {
    }

    /// <summary>The text to draw as a QR code; null for a code-and-address challenge.</summary>
    public string? QrText { get; private init; }

    /// <summary>The code the user types at <see cref="Address"/>; null for a QR challenge.</summary>
    public string? Code { get; private init; }

    /// <summary>The address the user opens to type <see cref="Code"/>; null for a QR challenge.</summary>
    public string? Address { get; private init; }

    /// <summary>One line saying what to do ("Scan with the Steam app: Steam Guard, then the QR button").</summary>
    public string Instructions { get; private init; } = "";

    /// <summary>When the challenge stops working.</summary>
    public DateTimeOffset ExpiresAt { get; private init; }

    /// <summary>A challenge the host draws as a QR code.</summary>
    public static SignInChallenge Qr(string qrText, string instructions, DateTimeOffset expiresAt) =>
        new() { QrText = qrText, Instructions = instructions, ExpiresAt = expiresAt };

    /// <summary>A challenge shown as a code with an address to open.</summary>
    public static SignInChallenge CodeAndAddress(string code, string address, string instructions, DateTimeOffset expiresAt) =>
        new() { Code = code, Address = address, Instructions = instructions, ExpiresAt = expiresAt };

    /// <summary>Never shows the challenge's content.</summary>
    private bool PrintMembers(System.Text.StringBuilder builder)
    {
        builder.Append(QrText != null ? "QR challenge" : "code challenge");
        return true;
    }
}

/// <summary>Where a sign-in reports what to show. Thread-safe.</summary>
public interface ISignInProgress
{
    /// <summary>Shows <paramref name="challenge"/>; calling it again replaces the shown one (the service rotated it).</summary>
    void Show(SignInChallenge challenge);

    /// <summary>Shows one line of progress ("Scanned; approve on your phone"), replacing the previous one.</summary>
    void Status(string text);
}

/// <summary>How a sign-in ended: approved with an account and secrets, or failed with a reason. Make one with <see cref="Success"/> or <see cref="Failed"/>.</summary>
public sealed class SignInResult
{
    private SignInResult(bool approved, string text, IReadOnlyDictionary<string, SecretValue> secrets)
    {
        Approved = approved;
        Text = text;
        Secrets = secrets;
    }

    /// <summary>Whether the user approved.</summary>
    public bool Approved { get; }

    /// <summary>The account name when approved; empty otherwise.</summary>
    public string Account => Approved ? Text : "";

    /// <summary>The reason when failed; empty otherwise.</summary>
    public string Reason => Approved ? "" : Text;

    /// <summary>The values of the provider's secrets set by sign-in, by setting key; empty when failed.</summary>
    public IReadOnlyDictionary<string, SecretValue> Secrets { get; }

    private string Text { get; }

    /// <summary>
    /// The user approved. <paramref name="secrets"/> holds a value for each secret of the provider declared set by sign-in (by
    /// its key); any other key is refused by the host and the sign-in fails.
    /// </summary>
    public static SignInResult Success(string account, IReadOnlyDictionary<string, SecretValue> secrets) =>
        new(true, account ?? "", secrets);

    /// <summary>The sign-in ended without credentials; <paramref name="reason"/> is one line shown to the user (declined, expired, no network).</summary>
    public static SignInResult Failed(string reason) =>
        new(false, reason ?? "", new Dictionary<string, SecretValue>());

    /// <summary>Never shows the secrets or the account.</summary>
    public override string ToString() => Approved ? "sign-in approved" : "sign-in failed";
}

/// <summary>The optional sign-in of a provider; implemented by <see cref="DataProvider{TSettings}"/>.</summary>
public interface ISignIn
{
    /// <summary>Whether the provider offers a sign-in.</summary>
    bool Offered { get; }

    /// <summary>One line saying what connecting gives, shown beside Connect.</summary>
    string Description { get; }

    /// <summary>Where the user ends a session the provider could not end (shown after a disconnect that could not tell the service).</summary>
    string SignOutHint { get; }

    /// <summary>Runs the sign-in with unsaved settings; see <see cref="DataProvider{TSettings}.SignInAsync"/>.</summary>
    Task<SignInResult> SignInAsync(ProviderSettings settings, ISignInProgress progress, CancellationToken cancellationToken);

    /// <summary>Ends the session at the service; see <see cref="DataProvider{TSettings}.SignOutAsync"/>.</summary>
    Task SignOutAsync(ProviderSettings settings, CancellationToken cancellationToken);
}
