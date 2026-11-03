// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using SkiaSharp;
using UrDeck.Engine.Themes;
using UrDeck.Sdk;
using UrDeck.Sdk.Components;
using Xunit;

namespace UrDeck.Engine.Tests;

/// <summary>The scrolling list, its follow and rotate modes and the item under a point, on a fake clock.</summary>
[Collection("TextLayoutCache")]
public sealed class TextListTests : IDisposable
{
    private static readonly DateTime T0 = new(2026, 11, 3, 12, 0, 0, DateTimeKind.Local);

    private readonly Theme _theme = ThemeResolver.Resolve(new ThemeStore("").Load(ThemeStore.DefaultName), 275);

    public void Dispose() => _theme.Dispose();

    private float Line => _theme.BodySize * TextBlock.LineSpacing;

    private float Gap => _theme.BodySize * 0.5f;

    /// <summary>A rectangle that shows <paramref name="items"/> single-line items and a little more than none of the next.</summary>
    private SKRect View(int items) => new(10, 10, 260, 10 + items * Line + (items - 1) * Gap + 0.5f);

    private static TextListItem Item(int n) => new($"message {n}");

    private static TextList Filled(int count, TextListMode mode = TextListMode.Manual, int capacity = 0)
    {
        var list = new TextList { Mode = mode, Capacity = capacity };
        list.SetItems(Enumerable.Range(0, count).Select(Item));
        return list;
    }

    private SKBitmap Draw(TextList list, SKRect rect, DateTime time, MotionLevel motion = MotionLevel.Full)
    {
        var bitmap = new SKBitmap(300, (int)rect.Bottom + 30);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Black);
        list.Draw(canvas, _theme, rect, time, motion);
        return bitmap;
    }

    /// <summary>The point in the middle of the item row <paramref name="row"/> (0 is the top) of a list at rest.</summary>
    private SKPoint Row(SKRect rect, int row) => new(rect.Left + 20, rect.Top + row * (Line + Gap) + Line / 2);

    // ---- Text List

    [Fact]
    public void More_items_than_fit_are_clipped_to_the_rectangle()
    {
        var rect = View(5);
        var list = Filled(20);
        using var bitmap = Draw(list, rect, T0);
        Assert.True(list.ContentHeight > rect.Height);
        Assert.True(list.MaxOffset > 0);
        bool inside = false;
        for (int y = 0; y < bitmap.Height; y++)
            for (int x = 0; x < bitmap.Width; x++)
            {
                bool lit = bitmap.GetPixel(x, y) != SKColors.Black;
                bool outside = x < rect.Left || x >= rect.Right || y < rect.Top || y >= rect.Bottom;
                Assert.False(lit && outside, $"pixel {x},{y} is drawn outside the rectangle");
                inside |= lit;
            }

        Assert.True(inside);
    }

    [Fact]
    public void An_offset_beyond_the_end_is_the_maximum()
    {
        var rect = View(5);
        var list = Filled(20);
        list.Offset = 1_000_000;
        Draw(list, rect, T0).Dispose();
        Assert.Equal(list.MaxOffset, list.Offset, 0.01);
        list.Offset = -50;
        Draw(list, rect, T0).Dispose();
        Assert.Equal(0, list.Offset);
    }

    [Fact]
    public void Few_items_are_drawn_from_the_top_with_no_offset()
    {
        var rect = View(5);
        var list = Filled(2);
        Draw(list, rect, T0).Dispose();
        Assert.Equal(0, list.MaxOffset);
        Assert.Equal(0, list.Offset);
        Assert.Equal(0, list.ItemAt(Row(rect, 0)));
        Assert.Equal(1, list.ItemAt(Row(rect, 1)));
    }

    [Fact]
    public void A_following_list_with_few_items_sits_at_the_bottom()
    {
        var rect = View(5);
        var list = Filled(2, TextListMode.Follow);
        Draw(list, rect, T0).Dispose();
        Assert.Equal(0, list.Offset);
        Assert.Equal(1, list.ItemAt(Row(rect, 4)));
        Assert.Equal(0, list.ItemAt(Row(rect, 3)));
        Assert.Null(list.ItemAt(Row(rect, 0)));
    }

    [Fact]
    public void An_item_can_have_a_mark_a_detail_and_a_line_limit()
    {
        var rect = new SKRect(10, 10, 200, 150);
        var list = new TextList();
        list.SetItems([new TextListItem(
            "a long message that wraps over several lines when the rectangle is this narrow and keeps on going",
            Detail: "2 minutes ago",
            Mark: "streamer",
            MarkColor: new SKColor(255, 0, 0),
            MaxLines: 2)]);
        using var bitmap = Draw(list, rect, T0);

        bool red = false;
        for (int y = 0; y < bitmap.Height && !red; y++)
            for (int x = 0; x < bitmap.Width; x++)
            {
                var c = bitmap.GetPixel(x, y);
                red |= c.Red > 200 && c.Green < 60 && c.Blue < 60;
            }

        Assert.True(red);
        float twoLinesAndDetail = 2 * Line + _theme.LabelSize * TextBlock.LineSpacing;
        Assert.Equal(twoLinesAndDetail, list.ContentHeight, 0.5);
    }

    [Fact]
    public void Replacing_an_item_above_the_view_keeps_the_view_where_it_is()
    {
        var rect = new SKRect(10, 10, 160, 10 + 4 * Line + 3 * Gap);
        var list = Filled(20);
        list.Offset = 8 * (Line + Gap);
        Draw(list, rect, T0).Dispose();
        int? before = list.ItemAt(Row(rect, 1));

        list.Replace(0, new TextListItem("an edited message that is now long enough to wrap over a few lines of this narrow list"));
        Draw(list, rect, T0).Dispose();

        Assert.True(list.Offset > 8 * (Line + Gap));
        Assert.Equal(before, list.ItemAt(Row(rect, 1)));
    }

    // ---- Item At A Point

    [Fact]
    public void The_item_under_a_point_is_reported_and_a_gap_is_none()
    {
        var rect = View(5);
        var list = Filled(10);
        Draw(list, rect, T0).Dispose();
        Assert.Equal(2, list.ItemAt(Row(rect, 2)));
        Assert.Null(list.ItemAt(new SKPoint(rect.Left + 20, rect.Top + Line + Gap / 2)));
        Assert.Null(list.ItemAt(new SKPoint(rect.Left - 5, rect.Top + 5)));
        Assert.Null(list.ItemAt(new SKPoint(rect.Left + 5, rect.Bottom + 5)));
    }

    [Fact]
    public void The_item_under_a_point_follows_the_offset()
    {
        var rect = View(5);
        var list = Filled(10);
        list.ScrollToItem(3);
        Draw(list, rect, T0).Dispose();
        Assert.Equal(3, list.ItemAt(Row(rect, 0)));
        Assert.Equal(5, list.ItemAt(Row(rect, 2)));
    }

    // ---- Follow Mode

    [Fact]
    public void A_new_message_scrolls_into_view_at_the_bottom()
    {
        var rect = View(5);
        var list = Filled(20, TextListMode.Follow);
        Draw(list, rect, T0).Dispose();
        Assert.Equal(list.MaxOffset, list.Offset, 0.01);
        Assert.False(list.IsMoving);
        Assert.Equal(19, list.ItemAt(Row(rect, 4)));

        list.Add(Item(20));
        Draw(list, rect, T0.AddSeconds(1)).Dispose();
        Assert.True(list.IsMoving);
        Assert.Equal(list.MaxOffset, list.Offset, 0.01);

        Draw(list, rect, T0.AddSeconds(1.5)).Dispose();
        Assert.False(list.IsMoving);
        Assert.Equal(20, list.ItemAt(Row(rect, 4)));
    }

    [Theory]
    [InlineData(MotionLevel.Reduced)]
    [InlineData(MotionLevel.Off)]
    public void A_new_message_appears_at_once_below_full_motion(MotionLevel motion)
    {
        var rect = View(5);
        var list = Filled(20, TextListMode.Follow);
        Draw(list, rect, T0, motion).Dispose();
        list.Add(Item(20));
        Draw(list, rect, T0.AddSeconds(1), motion).Dispose();
        Assert.False(list.IsMoving);
        Assert.Equal(20, list.ItemAt(Row(rect, 4)));
    }

    [Fact]
    public void A_list_scrolled_away_from_the_bottom_stays_put_and_resumes_at_the_bottom()
    {
        var rect = View(5);
        var list = Filled(20, TextListMode.Follow);
        Draw(list, rect, T0).Dispose();

        list.ScrollBy(-3 * (Line + Gap));
        float resting = list.Offset;
        list.Add(Item(20));
        Draw(list, rect, T0.AddSeconds(1)).Dispose();
        Assert.False(list.IsMoving);
        Assert.Equal(resting, list.Offset, 0.01);

        list.ScrollBy(1_000_000);
        list.Add(Item(21));
        Draw(list, rect, T0.AddSeconds(2)).Dispose();
        Assert.True(list.IsMoving);
        Assert.Equal(list.MaxOffset, list.Offset, 0.01);
    }

    [Fact]
    public void The_oldest_item_leaves_at_capacity_and_the_view_does_not_shift()
    {
        var rect = View(5);
        var list = Filled(20, TextListMode.Manual, capacity: 20);
        list.Offset = 6 * (Line + Gap);
        Draw(list, rect, T0).Dispose();
        Assert.Equal(6, list.ItemAt(Row(rect, 0)));
        Assert.Equal(7, list.ItemAt(Row(rect, 1)));

        list.Add(Item(20));
        Draw(list, rect, T0.AddSeconds(1)).Dispose();
        Assert.Equal(20, list.Count);
        Assert.Equal("message 1", list[0].Text);
        // The same message is at the same place; it is one index lower.
        Assert.Equal(5, list.ItemAt(Row(rect, 0)));
        Assert.Equal(6, list.ItemAt(Row(rect, 1)));
        Assert.Equal("message 6", list[5].Text);
    }

    [Fact]
    public void Editing_the_message_at_the_bottom_keeps_a_following_list_at_the_bottom()
    {
        var rect = new SKRect(10, 10, 160, 10 + 4 * Line + 3 * Gap);
        var list = Filled(20, TextListMode.Follow);
        Draw(list, rect, T0).Dispose();
        list.Replace(19, new TextListItem("the newest message was edited and is now long enough to wrap over several lines"));
        Draw(list, rect, T0.AddSeconds(1)).Dispose();
        Assert.Equal(list.MaxOffset, list.Offset, 0.01);
        Assert.Equal(19, list.ItemAt(new SKPoint(rect.Left + 20, rect.Bottom - 2)));
    }

    // ---- Rotating List

    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(6);

    [Fact]
    public void Headlines_rotate_on_their_interval_and_wrap_to_the_first()
    {
        var rect = View(1);
        var list = Filled(3, TextListMode.Rotate);
        list.RotateInterval = Interval;
        Draw(list, rect, T0).Dispose();
        Assert.Equal(0, list.ItemAt(Row(rect, 0)));
        Assert.Equal(T0 + Interval, list.NextChangeAt);

        var t = T0 + Interval;
        Draw(list, rect, t).Dispose();
        Assert.True(list.IsMoving);
        Draw(list, rect, t.AddSeconds(0.5)).Dispose();
        Assert.False(list.IsMoving);
        Assert.Equal(1, list.ItemAt(Row(rect, 0)));
        Assert.Equal(t + Interval, list.NextChangeAt);

        t += Interval;
        Draw(list, rect, t).Dispose();
        Draw(list, rect, t.AddSeconds(0.5)).Dispose();
        Assert.Equal(2, list.ItemAt(Row(rect, 0)));

        t += Interval;
        Draw(list, rect, t).Dispose();
        Draw(list, rect, t.AddSeconds(0.5)).Dispose();
        Assert.Equal(0, list.ItemAt(Row(rect, 0)));
    }

    [Fact]
    public void A_rotating_list_shows_only_the_items_it_was_asked_for()
    {
        var rect = View(3);
        var list = Filled(6, TextListMode.Rotate);
        list.RotateCount = 2;
        Draw(list, rect, T0).Dispose();
        Assert.Equal(0, list.ItemAt(Row(rect, 0)));
        Assert.Equal(1, list.ItemAt(Row(rect, 1)));
        Assert.Null(list.ItemAt(Row(rect, 2)));
    }

    [Fact]
    public void A_rotating_list_below_full_motion_changes_without_a_slide()
    {
        var rect = View(1);
        var list = Filled(3, TextListMode.Rotate);
        list.RotateInterval = Interval;
        Draw(list, rect, T0, MotionLevel.Off).Dispose();
        Draw(list, rect, T0 + Interval, MotionLevel.Off).Dispose();
        Assert.False(list.IsMoving);
        Assert.Equal(1, list.ItemAt(Row(rect, 0)));
    }

    [Fact]
    public void Nothing_rotates_when_everything_fits()
    {
        var rect = View(5);
        var list = Filled(3, TextListMode.Rotate);
        Draw(list, rect, T0).Dispose();
        Assert.Null(list.NextChangeAt);
        Assert.Equal(0, list.ItemAt(Row(rect, 0)));
        Assert.Equal(2, list.ItemAt(Row(rect, 2)));
        Draw(list, rect, T0 + Interval + Interval).Dispose();
        Assert.Equal(0, list.ItemAt(Row(rect, 0)));
    }
}
