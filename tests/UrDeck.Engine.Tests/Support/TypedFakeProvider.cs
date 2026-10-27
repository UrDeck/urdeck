// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using UrDeck.Engine.Data;
using UrDeck.Sdk.Data;

namespace UrDeck.Engine.Tests.Support;

/// <summary>The settings of <see cref="TypedFakeProvider"/>: an address, a token issued for it, a number and a switch.</summary>
internal sealed class TypedSettings : ProviderSettings
{
    [Setting("Address", Help = "Where the service is.", Required = true)]
    public string? Url { get; set; }

    [Setting("Token", Help = "Issued by the service.", Required = true)]
    [Secret(ContextSetting = "url")]
    public SecretValue? Token { get; set; }

    [Setting("Retries", Help = "How often to try again.")]
    public int Retries { get; set; } = 3;

    [Setting("Secure")]
    public bool Secure { get; set; } = true;
}

/// <summary>A provider with declared settings and one action; it records the settings of each start.</summary>
internal sealed class TypedFakeProvider : DataProvider<TypedSettings>, IActionProvider
{
    private readonly object _gate = new();
    private readonly List<TypedSettings> _starts = [];
    private int _stops;

    public IReadOnlyList<TypedSettings> Starts
    {
        get
        {
            lock (_gate)
                return _starts.ToArray();
        }
    }

    public int Stops => Volatile.Read(ref _stops);

    public IReadingSink? Sink { get; private set; }

    public Func<string, CancellationToken, Task<ActionResult>>? Handler { get; set; }

    public override IReadOnlyList<ReadingDescriptor> Describe() =>
        [new ReadingDescriptor("a", ReadingKind.Number, "A", "Reading a")];

    public override void Start(IReadingSink sink, TypedSettings settings)
    {
        Sink = sink;
        lock (_gate)
            _starts.Add(settings);
    }

    public override void SetDemand(IReadOnlyCollection<string> paths)
    {
    }

    public override Task SampleAsync(CancellationToken cancellationToken)
    {
        Sink?.Publish("a", 1);
        return Task.CompletedTask;
    }

    public override void Shutdown() => Interlocked.Increment(ref _stops);

    /// <summary>What the connection test does with the settings it is given; null: it succeeds with a fixed line.</summary>
    public Func<TypedSettings, CancellationToken, Task<string>>? Validate { get; set; }

    public override bool OffersConnectionTest => true;

    public override Task<string> ValidateAsync(TypedSettings settings, CancellationToken cancellationToken) =>
        Validate == null ? Task.FromResult("Connected") : Validate(settings, cancellationToken);

    public IReadOnlyList<ActionDescriptor> DescribeActions() => [new ActionDescriptor("scene/toggle", "Toggle", "Toggle a scene")];

    public Task<ActionResult> InvokeAsync(string path, ReadingValue? argument, CancellationToken cancellationToken) =>
        Handler == null ? Task.FromResult(ActionResult.Done) : Handler(path, cancellationToken);

    public ProviderDescriptor Descriptor(string id = "typed") =>
        new(id, "Typed", typeof(TypedFakeProvider), 1000, 250, "tests")
        {
            Factory = () => this,
            MissingSettingMessage = "Set up Typed in Settings",
        };
}
