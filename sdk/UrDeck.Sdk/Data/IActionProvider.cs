// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Patrick Bigler

namespace UrDeck.Sdk.Data;

/// <summary>
/// A provider that can also be asked to do something (skip a track, toggle a switch). Implementing it beside
/// <see cref="IDataProvider"/> is the declaration; a provider that does not is never asked. The same class implements both,
/// and the engine creates it once per run.
/// <para>
/// <see cref="InvokeAsync"/> can run while <see cref="IDataProvider.SampleAsync"/> runs, so guard any state the two share.
/// It is never called before <see cref="IDataProvider.Start"/> or after <see cref="IDataProvider.Shutdown"/>, and the
/// engine runs the actions of one provider one at a time, in the order they were asked.
/// </para>
/// </summary>
public interface IActionProvider
{
    /// <summary>The catalog: every action the provider offers. Called once; must not need <see cref="IDataProvider.Start"/>.</summary>
    IReadOnlyList<ActionDescriptor> DescribeActions();

    /// <summary>
    /// Performs the action at <paramref name="path"/> (for example <c>control/next</c>). The engine only checks that the
    /// provider is running; whether the path is one of the provider's actions is for the provider, which returns
    /// <see cref="ActionResult.Failed"/> with a message when it is not. <paramref name="argument"/> is null or the one value
    /// the caller passed. The token is cancelled after the provider's <c>actionTimeoutMs</c> (10 seconds unless set); a provider that ignores it holds one thread.
    /// </summary>
    Task<ActionResult> InvokeAsync(string path, ReadingValue? argument, CancellationToken cancellationToken);
}

/// <summary>One entry of a provider's action catalog.</summary>
/// <param name="Path">The action's path, the part of its id after <c>provider:</c>.</param>
/// <param name="Label">A short label for lists.</param>
/// <param name="Name">A full name for documentation and editors.</param>
/// <param name="TakesArgument">Whether the action uses the optional argument (a volume, a brightness).</param>
public sealed record ActionDescriptor(string Path, string Label, string Name, bool TakesArgument = false);

/// <summary>How an action ended: done, or failed with a message.</summary>
public readonly record struct ActionResult
{
    private ActionResult(bool succeeded, string message)
    {
        Succeeded = succeeded;
        Message = message;
    }

    /// <summary>Whether the provider did what was asked.</summary>
    public bool Succeeded { get; }

    /// <summary>Why it failed; empty when it is done and nothing needs saying.</summary>
    public string Message { get; }

    /// <summary>The action is done.</summary>
    public static ActionResult Done { get; } = new(true, "");

    /// <summary>The action is done and there is something to say.</summary>
    public static ActionResult DoneWith(string message) => new(true, message ?? "");

    /// <summary>The action failed for <paramref name="message"/>.</summary>
    public static ActionResult Failed(string message) => new(false, string.IsNullOrEmpty(message) ? "failed" : message);

    public override string ToString() => Succeeded ? (Message.Length == 0 ? "done" : $"done: {Message}") : $"failed: {Message}";
}

/// <summary>
/// The host's action service: asks a provider to do something on a widget's behalf. Safe on any thread, including the UI
/// thread (<see cref="Input.ITapTarget.OnTap"/>).
/// </summary>
public interface IActionInvoker
{
    /// <summary>
    /// Asks the provider named by <paramref name="id"/> (<c>provider:path</c>) to perform the action. Returns at once; the
    /// task completes when the action ends, on a worker thread, never the UI thread. Never throws and the task never faults:
    /// an unknown provider, one that is not running or offers no actions, a timeout, a full queue and a throwing provider all
    /// end as <see cref="ActionResult.Failed"/>. A widget may ignore the task; the effect shows in the next reading.
    /// </summary>
    Task<ActionResult> Invoke(string id, ReadingValue? argument = null);
}

/// <summary>The invoker of a widget that was never attached, and of a host that offers none: every action fails.</summary>
internal sealed class NullActionInvoker : IActionInvoker
{
    public static readonly NullActionInvoker Instance = new();

    private static readonly Task<ActionResult> NoActions = Task.FromResult(ActionResult.Failed("the host offers no actions"));

    public Task<ActionResult> Invoke(string id, ReadingValue? argument = null) => NoActions;
}
