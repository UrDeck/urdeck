// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using SkiaSharp;
using UrDeck.Engine.Backgrounds;
using UrDeck.Engine.Config;
using UrDeck.Engine.Deck;
using UrDeck.Engine.Plugin;
using UrDeck.Engine.Themes;
using UrDeck.Sdk;
using UrDeck.Sdk.Input;

namespace UrDeck.Engine.Tests.Support;

/// <summary>
/// A deck thread whose clocks only move when a test advances them: posted work runs in <see cref="RunPosted"/> (normal before
/// low, as the dispatcher does), timers fire in order during <see cref="Advance"/>.
/// </summary>
internal sealed class ManualScheduler : IDeckScheduler
{
    private static readonly DateTime Start = new(2026, 11, 7, 12, 0, 0, DateTimeKind.Local);
    private readonly object _gate = new();
    private readonly List<(Action Action, DeckPriority Priority)> _posted = [];
    private readonly List<ManualTimer> _timers = [];

    public DateTime Now => Start + Elapsed;

    public TimeSpan Elapsed { get; private set; }

    public int PostedCount
    {
        get
        {
            lock (_gate)
                return _posted.Count;
        }
    }

    public void Post(Action action, DeckPriority priority = DeckPriority.Normal)
    {
        lock (_gate)
            _posted.Add((action, priority));
    }

    public IDeckTimer CreateTimer(Action tick)
    {
        var timer = new ManualTimer(this, tick);
        _timers.Add(timer);
        return timer;
    }

    /// <summary>Runs posted work until none is left, the normal work first each time.</summary>
    public void RunPosted()
    {
        while (true)
        {
            Action next;
            lock (_gate)
            {
                if (_posted.Count == 0)
                    return;
                int i = _posted.FindIndex(p => p.Priority == DeckPriority.Normal);
                if (i < 0)
                    i = 0;
                next = _posted[i].Action;
                _posted.RemoveAt(i);
            }

            next();
        }
    }

    /// <summary>Moves both clocks on, firing every timer that falls due on the way (and the work it posts) in order.</summary>
    public void Advance(TimeSpan by)
    {
        var target = Elapsed + by;
        RunPosted();
        while (_timers.Where(t => t.IsRunning && t.Due <= target).OrderBy(t => t.Due).FirstOrDefault() is { } timer)
        {
            Elapsed = timer.Due;
            timer.Fire();
            RunPosted();
        }

        Elapsed = target;
    }

    private sealed class ManualTimer(ManualScheduler owner, Action tick) : IDeckTimer
    {
        private TimeSpan _interval;
        private bool _repeat;

        public bool IsRunning { get; private set; }

        public TimeSpan Due { get; private set; }

        public void Start(TimeSpan interval, bool repeat = false)
        {
            _interval = interval;
            _repeat = repeat;
            Due = owner.Elapsed + interval;
            IsRunning = true;
        }

        public void Cancel() => IsRunning = false;

        public void Fire()
        {
            if (_repeat)
                Due += _interval;
            else
                IsRunning = false;
            tick();
        }
    }
}

/// <summary>A frame source that only records who is registered; a test ticks it.</summary>
internal sealed class FakeFrames : IFrameSource
{
    public HashSet<IFrameClient> Clients { get; } = [];

    public void Add(IFrameClient client) => Clients.Add(client);

    public void Remove(IFrameClient client) => Clients.Remove(client);

    public void Tick()
    {
        foreach (var client in Clients.ToArray())
            client.Invalidate();
    }
}

/// <summary>The test widget types, registered in a registry of their own, and a plugin reload a test can raise.</summary>
internal sealed class FakeWidgets : IDeckWidgets
{
    public WidgetRegistry Registry { get; } = new();

    /// <summary>Every widget made, in order.</summary>
    public List<IWidget> Created { get; } = [];

    public FakeWidgets()
    {
        foreach (var type in new[] { typeof(ProbeWidget), typeof(TickWidget), typeof(TapWidget) })
            Registry.Register(WidgetDescriptor.TryCreate(type, out _)!);
    }

    public event Action? PluginsChanged;

    public WidgetDescriptor? GetDescriptor(string typeId) => Registry.GetDescriptor(typeId);

    public IReadOnlyList<string> GetRegisteredWidgetTypes() => Registry.GetRegisteredWidgetTypes();

    public IWidget? CreateWidget(WidgetConfig config, out WidgetServices? services)
    {
        var widget = Registry.CreateWidget(config, out services);
        if (widget != null)
            Created.Add(widget);
        return widget;
    }

    public void ReloadPlugins() => PluginsChanged?.Invoke();
}

/// <summary>Counts the users of each reading; a test raises changes.</summary>
internal sealed class FakeReadings : IDeckReadings
{
    public Dictionary<string, int> Users { get; } = new(StringComparer.OrdinalIgnoreCase);

    public event Action<IReadOnlyCollection<string>>? ReadingsChanged;

    public void Subscribe(IEnumerable<string> ids)
    {
        foreach (string id in ids)
            Users[id] = Users.GetValueOrDefault(id) + 1;
    }

    public void Unsubscribe(IEnumerable<string> ids)
    {
        foreach (string id in ids)
            Users[id] = Users.GetValueOrDefault(id) - 1;
    }

    public int UsersOf(string id) => Users.GetValueOrDefault(id);

    public void Change(params string[] ids) => ReadingsChanged?.Invoke(ids);
}

/// <summary>A configuration held in memory; <see cref="Reload"/> raises the change as the store does after a save.</summary>
internal sealed class FakeConfigSource(UrDeckConfig config) : IDeckConfigSource
{
    public UrDeckConfig Config { get; set; } = config;

    public event Action<UrDeckConfig>? ConfigChanged;

    public void Reload(UrDeckConfig? config = null)
    {
        if (config != null)
            Config = config;
        ConfigChanged?.Invoke(Config);
    }
}

/// <summary>
/// A presenter that shows nothing: it records what the session asked for, shows slots the way the window does (shown while
/// alive, hidden when dropped) and counts their paint requests. A settle completes when the test calls <see cref="Settle"/>.
/// </summary>
internal sealed class FakePresenter : IDeckPresenter, IPagerView
{
    private readonly Dictionary<WidgetSlot, int> _paints = [];

    public DeckSurface Surface { get; set; } = new(new System.Drawing.Size(400, 800), 1);

    public IPagerView Pages => this;

    public List<PageSlots> Alive { get; } = [];

    public PageSlots? DockShown { get; private set; }

    public IndicatorSetup? Indicator { get; private set; }

    public int Clears { get; private set; }

    public int Builds { get; private set; }

    public List<string?> BackgroundKeys { get; } = [];

    public Dictionary<PageSlots, double> Offsets { get; } = [];

    public Action? PendingSettle { get; private set; }

    public IReadOnlyList<PageMove> LastSlide { get; private set; } = [];

    public ShaderStore Shaders { get; } = new(Path.Combine(Path.GetTempPath(), "urdeck-no-backgrounds"));

    public void ShowBackground(BackgroundPlan plan, LoadedTheme theme, DeckSurface surface) =>
        BackgroundKeys.Add(plan.RequestKey(Shaders, Path.GetTempPath()) ?? "colour");

    public int PaintsOf(WidgetSlot slot) => _paints.GetValueOrDefault(slot);

    public void ResetPaints() => _paints.Clear();

    public void PageAdded(PageSlots page, double offset)
    {
        Builds++;
        Alive.Add(page);
        Offsets[page] = offset;
        Show(page);
    }

    public void PageRemoved(PageSlots page)
    {
        Alive.Remove(page);
        Offsets.Remove(page);
        Hide(page);
    }

    public void DockAdded(PageSlots dock)
    {
        DockShown = dock;
        Show(dock);
    }

    public void IndicatorAdded(IndicatorSetup indicator) => Indicator = indicator;

    public void Cleared()
    {
        Clears++;
        foreach (var page in Alive)
            Hide(page);
        if (DockShown != null)
            Hide(DockShown);
        Alive.Clear();
        Offsets.Clear();
        DockShown = null;
        Indicator = null;
    }

    public void SetOffset(PageSlots page, double offset) => Offsets[page] = offset;

    public void Slide(IReadOnlyList<PageMove> moves, TimeSpan duration, Action settled)
    {
        LastSlide = moves;
        PendingSettle = settled;
    }

    /// <summary>Ends the settle that is running, as the window's compositor batch does.</summary>
    public void Settle()
    {
        var settled = PendingSettle;
        PendingSettle = null;
        settled?.Invoke();
    }

    public void IndicatorFollow(double position) { }

    public void IndicatorSettle(double position) { }

    public int IndicatorHitTest(System.Drawing.PointF local, float cellPx) => -1;

    private void Show(PageSlots page)
    {
        foreach (var card in page.Cards)
        {
            var slot = card.Slot;
            slot.PaintRequested += () => _paints[slot] = _paints.GetValueOrDefault(slot) + 1;
            slot.Show();
        }
    }

    private static void Hide(PageSlots page)
    {
        foreach (var card in page.Cards)
            card.Slot.Hide();
    }
}

/// <summary>One session's world: its inputs, its presenter and the session, built on a scheduler that may be shared.</summary>
internal sealed class DeckRig : IDisposable
{
    public static readonly LoadedTheme Dark = new ThemeStore("").Load(ThemeStore.DefaultName);

    public DeckRig(UrDeckConfig config, ManualScheduler? scheduler = null, FakeWidgets? widgets = null, FakeReadings? readings = null, FakeConfigSource? source = null)
    {
        Scheduler = scheduler ?? new ManualScheduler();
        Widgets = widgets ?? new FakeWidgets();
        Readings = readings ?? new FakeReadings();
        Config = source ?? new FakeConfigSource(config);
        Session = new DeckSession(new DeckSessionInputs(Config, _ => Dark, Widgets, Readings, Scheduler, Frames), Presenter);
    }

    public ManualScheduler Scheduler { get; }

    public FakeWidgets Widgets { get; }

    public FakeReadings Readings { get; }

    public FakeConfigSource Config { get; }

    public FakeFrames Frames { get; } = new();

    public FakePresenter Presenter { get; } = new();

    public DeckSession Session { get; }

    /// <summary>Shows the deck and builds its page, as placement does once the window is on its display.</summary>
    public DeckRig Show()
    {
        Session.SetShown(true);
        Session.RequestPage(force: true);
        Scheduler.RunPosted();
        return this;
    }

    public IEnumerable<WidgetSlot> LiveSlots => Session.Pager.LivePages.SelectMany(p => p.Cards).Select(c => c.Slot);

    public static WidgetConfig Probe(int col, int row, string readings = "", int width = 1) =>
        new() { WidgetTypeId = "test.probe", Col = col, Row = row, Width = width, Height = 1, Parameters = { ["readings"] = readings } };

    public static UrDeckConfig Pages(params WidgetConfig[][] pages) => new()
    {
        Theme = ThemeStore.DefaultName,
        Pages = pages.Select((widgets, i) => new PageConfig { Id = $"p-{i + 1}", Name = $"P{i + 1}", Widgets = [.. widgets] }).ToList(),
    };

    public void Dispose() => Session.Dispose();
}

/// <summary>
/// A widget whose behaviour its parameters set: <c>readings</c> (comma-separated ids it subscribes to), <c>animating</c>
/// (true: it animates), <c>wakeMs</c> (it asks to be woken this long after each paint), <c>fill</c> (a colour it clears its
/// card to).
/// </summary>
[Widget("Probe", "d", Id = "test.probe")]
[WidgetSize(1, 1)]
[WidgetSize(2, 1)]
[WidgetSize(4, 1)]
[RefreshOnData]
internal sealed class ProbeWidget : Widget<WidgetConfig>, IDisposable
{
    public int Renders { get; private set; }

    public bool Disposed { get; private set; }

    public bool Animating { get; set; }

    private DateTime _lastRender;

    public override IReadOnlyCollection<string> Subscriptions =>
        Config.Parameters.TryGetValue("readings", out string? ids) && ids.Length > 0 ? ids.Split(',') : [];

    public override bool IsAnimating => Animating || Config.Parameters.GetValueOrDefault("animating") == "true";

    public override DateTime? NextAnimationAt =>
        Config.Parameters.TryGetValue("wakeMs", out string? ms) ? _lastRender.AddMilliseconds(double.Parse(ms, System.Globalization.CultureInfo.InvariantCulture)) : null;

    public override void Render(WidgetRenderContext context)
    {
        Renders++;
        _lastRender = context.Time;
        if (Config.Parameters.TryGetValue("fill", out string? fill))
            context.Canvas.Clear(SKColor.Parse(fill));
    }

    public void Dispose() => Disposed = true;
}

/// <summary>A widget refreshed every second, counting its updates.</summary>
[Widget("Tick", "d", Id = "test.tick")]
[WidgetSize(1, 1)]
[RefreshOnTick(1, TimeUnit.Seconds)]
internal sealed class TickWidget : Widget<WidgetConfig>
{
    public int Updates { get; private set; }

    public override Task UpdateAsync(CancellationToken cancellationToken)
    {
        Updates++;
        return Task.CompletedTask;
    }

    public override void Render(WidgetRenderContext context) { }
}

/// <summary>A widget that takes taps anywhere and records where.</summary>
[Widget("Tap", "d", Id = "test.tap")]
[WidgetSize(1, 1)]
[RefreshOnData]
internal sealed class TapWidget : Widget<WidgetConfig>, ITapTarget
{
    public List<SKPoint> Taps { get; } = [];

    public void OnTap(SKPoint point) => Taps.Add(point);

    public override void Render(WidgetRenderContext context) { }
}
