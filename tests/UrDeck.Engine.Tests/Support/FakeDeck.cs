// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Drawing;
using UrDeck.Engine.Display;

namespace UrDeck.Engine.Tests.Support;

/// <summary>The owner's three displays (see the display-recovery design), for selection and placement tests.</summary>
internal static class TestDisplays
{
    public const string PanelPath = @"\\?\DISPLAY#RTD1100#5&abf4c77&0&UID4353#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}";
    public const string PanelPathOtherOutput = @"\\?\DISPLAY#RTD1100#5&abf4c77&0&UID4360#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}";

    public static DisplayInfo Panel(string device = @"\\.\DISPLAY1", string path = PanelPath) =>
        new("HYTE Y70Touch", "RTD1100", path, device, new Rectangle(3840, -3573, 1100, 3840), 1, false);

    public static DisplayInfo Primary(string device = @"\\.\DISPLAY2") =>
        new("LG ULTRAGEAR", "GSM5CE2", @"\\?\DISPLAY#GSM5CE2#5&abf4c77&0&UID4356#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}", device,
            new Rectangle(0, 0, 3840, 2160), 1.5, true);

    public static DisplayInfo Side(string device = @"\\.\DISPLAY3") =>
        new("HP Z23n G2", "HPN347B", @"\\?\DISPLAY#HPN347B#5&abf4c77&0&UID4355#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}", device,
            new Rectangle(-1080, -12, 1080, 1920), 1, false);

    public static RememberedDisplay Remembered(DisplayInfo display, string? selector = null) =>
        new() { Name = display.Name, Model = display.Model, Path = display.Path, Selector = selector };
}

/// <summary>
/// A scripted window and its owner. A cover lands where it was asked to, with the scaling of the display there and a
/// layout that fills it, unless <see cref="OnCover"/> says what happens instead.
/// </summary>
internal sealed class FakeDeck : IDisplayEnvironment, IPlacementOwner
{
    public List<DisplayInfo> Displays { get; } = [];
    public Rectangle ContentBounds { get; set; }
    public double WindowScaling { get; set; } = 1;
    public SizeF LayoutSize { get; set; }
    public bool Visible { get; private set; }

    /// <summary>Replaces what a cover does to the window.</summary>
    public Action<Rectangle, bool>? OnCover { get; set; }

    /// <summary>Every call in order: cover, show, hide, rebuild, release, save.</summary>
    public List<string> Calls { get; } = [];
    public List<(Rectangle Bounds, bool Nudge)> Covers { get; } = [];
    public List<bool> Rebuilds { get; } = [];
    public List<RememberedDisplay> Saves { get; } = [];
    public List<(PlacementState State, string? Name)> States { get; } = [];
    public int Shows { get; private set; }
    public int Hides { get; private set; }
    public int Releases { get; private set; }

    public IReadOnlyList<DisplayInfo> GetDisplays() => Displays.ToList();

    public void Cover(Rectangle bounds, bool nudge)
    {
        Calls.Add("cover");
        Covers.Add((bounds, nudge));
        if (OnCover != null)
            OnCover(bounds, nudge);
        else
            Land(bounds);
    }

    public void Land(Rectangle bounds)
    {
        ContentBounds = bounds;
        if (Displays.FirstOrDefault(d => d.Bounds == bounds) is { } display)
            WindowScaling = display.Scaling;
        LayoutSize = new SizeF((float)(bounds.Width / WindowScaling), (float)(bounds.Height / WindowScaling));
    }

    public void Show()
    {
        Calls.Add("show");
        Shows++;
        Visible = true;
    }

    public void Hide()
    {
        Calls.Add("hide");
        Hides++;
        Visible = false;
    }

    public void RebuildPage(bool force)
    {
        Calls.Add("rebuild");
        Rebuilds.Add(force);
    }

    public void ReleasePage()
    {
        Calls.Add("release");
        Releases++;
    }

    public void SaveRememberedDisplay(RememberedDisplay display)
    {
        Calls.Add("save");
        Saves.Add(display);
    }

    public void StateChanged(PlacementState state, string? displayName) => States.Add((state, displayName));

    public void ClearRecords()
    {
        Calls.Clear();
        Covers.Clear();
        Rebuilds.Clear();
        Saves.Clear();
        States.Clear();
        Shows = Hides = Releases = 0;
    }
}
