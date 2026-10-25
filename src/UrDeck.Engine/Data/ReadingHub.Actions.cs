// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Engine.Diagnostics;
using UrDeck.Sdk.Data;

namespace UrDeck.Engine.Data;

// Actions: a widget asks a provider to do something. One queue per provider, worked through one action at a time on the
// thread pool, beside (not inside) the sampling loop. Every failure is a value; see the provider-actions spec.
public sealed partial class ReadingHub
{
    private static readonly TimeSpan ActionLimit = TimeSpan.FromSeconds(10);

    private const int MaxWaitingActions = 8;

    /// <inheritdoc />
    public Task<ActionResult> Invoke(string id, ReadingValue? argument = null)
    {
        long asked = _time.GetTimestamp();
        if (!ReadingId.TryParse(id, out string providerId, out string path, out string? error))
            return Failed(id, error!, asked);

        PendingAction pending;
        lock (_gate)
        {
            if (_disposed)
                return Failed(id, "the host is shutting down", asked);
            if (_providers.Get(providerId) == null)
                return Failed(id, $"no data provider '{providerId}' is registered", asked);
            if (!_runtimes.TryGetValue(providerId, out var rt) || !rt.Started || rt.Instance == null)
                return Failed(id, $"provider '{providerId}' is not running", asked);
            if (rt.Instance is not IActionProvider)
                return Failed(id, $"provider '{providerId}' offers no actions", asked);
            if (rt.Actions.Count >= MaxWaitingActions)
                return Failed(id, $"provider '{providerId}' is busy", asked);

            pending = new PendingAction(id, path, argument, asked,
                new TaskCompletionSource<ActionResult>(TaskCreationOptions.RunContinuationsAsynchronously));
            rt.Actions.Enqueue(pending);
            if (!rt.ActionPumpRunning)
            {
                rt.ActionPumpRunning = true;
                rt.ActionCts ??= new CancellationTokenSource();
                int generation = rt.Generation;
                var token = rt.ActionCts.Token;
                Task.Run(() => PumpActionsAsync(rt, generation, token));
            }
        }

        return pending.Completion.Task;
    }

    /// <summary>
    /// The actions a provider offers, creating the instance if needed but not starting it. Null when the provider is unknown,
    /// cannot describe itself, or offers no actions (it does not implement <see cref="IActionProvider"/>).
    /// </summary>
    public IReadOnlyList<ActionDescriptor>? GetActionCatalog(string providerId)
    {
        ProviderRuntime rt;
        lock (_gate)
        {
            if (_disposed)
                return null;
            var descriptor = _providers.Get(providerId);
            if (descriptor == null)
                return null;
            rt = GetOrCreateRuntime(descriptor);
            if (rt.ActionCatalog != null)
                return rt.ActionCatalog.ToList();
        }

        var instance = EnsureInstance(rt, out _);
        if (instance is not IActionProvider actions)
            return null;

        List<ActionDescriptor> catalog;
        try
        {
            catalog = (actions.DescribeActions() ?? [])
                .Where(a => a != null && !string.IsNullOrEmpty(a.Path))
                .GroupBy(a => a.Path, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();
        }
        catch (Exception ex)
        {
            UrDeckLog.Error($"Provider '{providerId}' could not describe its actions: {ex.GetBaseException().Message}", ex);
            return null;
        }

        lock (_gate)
        {
            if (_disposed || !_runtimes.TryGetValue(providerId, out var current) || !ReferenceEquals(current, rt))
                return catalog;
            rt.ActionCatalog ??= catalog;
            return rt.ActionCatalog.ToList();
        }
    }

    private Task<ActionResult> Failed(string id, string message, long asked)
    {
        LogAction(id, ActionResult.Failed(message), asked);
        return Task.FromResult(ActionResult.Failed(message));
    }

    private void LogAction(string id, ActionResult result, long asked)
    {
        long ms = (long)_time.GetElapsedTime(asked).TotalMilliseconds;
        if (result.Succeeded)
            UrDeckLog.Info(result.Message.Length == 0 ? $"Action '{id}': done ({ms} ms)" : $"Action '{id}': done: {result.Message} ({ms} ms)");
        else
            UrDeckLog.Warn($"Action '{id}': failed: {result.Message} ({ms} ms)");
    }

    /// <summary>Under the gate: takes every waiting action of the run that is ending.</summary>
    private static List<PendingAction> DrainActions(ProviderRuntime rt)
    {
        var dropped = rt.Actions.ToList();
        rt.Actions.Clear();
        return dropped;
    }

    private void FailActions(string providerId, List<PendingAction> actions, string reason)
    {
        foreach (var action in actions)
        {
            var result = ActionResult.Failed($"provider '{providerId}': {reason}");
            LogAction(action.Id, result, action.Asked);
            action.Completion.TrySetResult(result);
        }
    }

    private async Task PumpActionsAsync(ProviderRuntime rt, int generation, CancellationToken stop)
    {
        while (true)
        {
            PendingAction? action;
            IActionProvider? provider;
            lock (_gate)
            {
                if (rt.Generation != generation)
                    return;
                if (!rt.Actions.TryDequeue(out action))
                {
                    rt.ActionPumpRunning = false;
                    return;
                }

                provider = rt.Instance as IActionProvider;
            }

            var result = provider == null
                ? ActionResult.Failed($"provider '{rt.Descriptor.Id}' offers no actions")
                : await RunActionAsync(provider, action, stop);
            LogAction(action.Id, result, action.Asked);
            action.Completion.TrySetResult(result);
        }
    }

    private async Task<ActionResult> RunActionAsync(IActionProvider provider, PendingAction action, CancellationToken stop)
    {
        using var actionCts = CancellationTokenSource.CreateLinkedTokenSource(stop);
        using var timeoutCts = new CancellationTokenSource();

        // On its own pool thread: a provider that blocks before returning its task must not take the queue's timeout with it.
        var run = Task.Run(() => provider.InvokeAsync(action.Path, action.Argument, actionCts.Token), CancellationToken.None);
        var timeout = Task.Delay(ActionLimit, _time, timeoutCts.Token);
        var first = await Task.WhenAny(run, timeout);

        if (first == run)
        {
            await timeoutCts.CancelAsync();
            try
            {
                return await run;
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested)
            {
                return ActionResult.Failed("the provider stopped");
            }
            catch (Exception ex)
            {
                return ActionResult.Failed(ex.GetBaseException().Message);
            }
        }

        await actionCts.CancelAsync();
        _ = run.ContinueWith(t => _ = t.Exception, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
        return ActionResult.Failed($"did not finish within {ActionLimit.TotalSeconds:0.#} s");
    }
}
