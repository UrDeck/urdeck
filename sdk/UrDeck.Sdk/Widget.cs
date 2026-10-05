// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Patrick Bigler

using System.Reflection;

namespace UrDeck.Sdk;

/// <summary>
/// Base class for widgets. Metadata defaults come from the class attributes, so a typical widget
/// only overrides <see cref="Render"/> (and optionally <see cref="UpdateAsync"/>).
/// </summary>
public abstract class Widget<TConfig> : IWidget<TConfig> where TConfig : WidgetConfig, new()
{
    private WidgetAttribute? WidgetMeta => GetType().GetCustomAttribute<WidgetAttribute>();

    public virtual string Name => WidgetMeta?.Name ?? GetType().Name;
    public virtual string Description => WidgetMeta?.Description ?? "";
    public virtual string Category =>
        GetType().GetCustomAttribute<CategoryAttribute>()?.Name ?? WidgetMeta?.Category ?? "General";

    public virtual System.Drawing.Size[] SupportedSizes =>
        GetType().GetCustomAttributes<WidgetSizeAttribute>()
            .Select(s => new System.Drawing.Size(s.Width, s.Height))
            .ToArray();

    public virtual TConfig DefaultConfig => new TConfig();
    public Type ConfigType => typeof(TConfig);

    public TConfig Config { get; private set; } = new TConfig();
    WidgetConfig IWidget.Config => Config;

    public void Configure(WidgetConfig config)
    {
        Config = config as TConfig
            ?? throw new ArgumentException($"Expected {typeof(TConfig).Name} but got {config.GetType().Name}.", nameof(config));
        OnConfigured();
    }

    /// <summary>Called after <see cref="Config"/> changes.</summary>
    protected virtual void OnConfigured() { }

    public virtual Task UpdateAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc cref="IWidget.NeedsRender"/>
    public virtual bool NeedsRender(DateTime now) => true;

    /// <inheritdoc cref="IWidget.IsAnimating"/>
    public virtual bool IsAnimating => false;

    public abstract void Render(WidgetRenderContext context);
}
