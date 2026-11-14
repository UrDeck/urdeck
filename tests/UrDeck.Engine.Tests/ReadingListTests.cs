// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using SkiaSharp;
using UrDeck.Engine.Themes;
using UrDeck.Sdk;
using UrDeck.Sdk.Components;
using UrDeck.Sdk.Data;
using Xunit;

namespace UrDeck.Engine.Tests;

[Collection("TextLayoutCache")]
public sealed class ReadingListTests : IDisposable
{
    private readonly Theme _theme = ThemeResolver.Resolve(new ThemeStore("").Load(ThemeStore.DefaultName), 275);

    public void Dispose() => _theme.Dispose();

    private sealed class Source : IReadingSource
    {
        public Reading Read(string id) => Reading.Unavailable("test");

        public ReadingDescriptor? Describe(string id) => null;

        public bool RegionUsesFahrenheit => false;
    }

    private static readonly ReadingDescriptor Friends = new("friends/playing", ReadingKind.List, "Friends playing", "Friends playing")
    {
        Unit = "playing",
        EmptyText = "Nobody is playing",
        Fields =
        [
            new ReadingField("name", ReadingKind.Text, "Name") { Role = FieldRole.Title },
            new ReadingField("game", ReadingKind.Text, "Game") { Role = FieldRole.Detail },
            new ReadingField("hours", ReadingKind.Number, "Hours") { Unit = "h", Decimals = 1 },
            new ReadingField("avatar", ReadingKind.Text, "Avatar") { Role = FieldRole.Image },
        ],
    };

    private static ReadingRow Row(string key, string name, string? game = null, double? hours = null)
    {
        var fields = new List<KeyValuePair<string, ReadingValue>> { new("name", name) };
        if (game != null)
            fields.Add(new("game", game));
        if (hours != null)
            fields.Add(new("hours", hours.Value));
        return new ReadingRow(key, fields);
    }

    private static ReadingList ThreeFriends() => new([Row("1", "Alex", "Deep Rock Galactic"), Row("2", "Sam"), Row("3", "Kim", "Factorio")]);

    [Fact]
    public void TwoListsWithTheSameRows_AreEqual_AndHashTheSame()
    {
        var a = ThreeFriends();
        var b = ThreeFriends();

        Assert.True(a.Equals(b));
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.Equal(ReadingValue.FromList(a), ReadingValue.FromList(b));
    }

    [Fact]
    public void ListsDifferingInOrderKeyOrFieldValue_AreNotEqual()
    {
        var a = ThreeFriends();

        Assert.NotEqual(a, new ReadingList([a[2], a[1], a[0]]));
        Assert.NotEqual(a, new ReadingList([Row("9", "Alex", "Deep Rock Galactic"), a[1], a[2]]));
        Assert.NotEqual(a, new ReadingList([Row("1", "Alex", "Other game"), a[1], a[2]]));
        Assert.NotEqual(ReadingValue.FromList(a), ReadingValue.FromList(ReadingList.Empty));
    }

    [Fact]
    public void RowFieldOrder_DoesNotMatter()
    {
        var a = new ReadingRow("1", [new("name", "Alex"), new("game", "X")]);
        var b = new ReadingRow("1", [new("game", "X"), new("name", "Alex")]);

        Assert.Equal(a, b);
    }

    [Fact]
    public void ReadingValue_StaysTheSameSize_WithAListShape()
    {
        // The list shares the text slot: type, double, reference, int offset.
        Assert.True(System.Runtime.CompilerServices.Unsafe.SizeOf<ReadingValue>() <= 32);
        Assert.Equal(ReadingValueType.List, ReadingValue.FromList(ThreeFriends()).Type);
        Assert.Equal("", ReadingValue.FromList(ThreeFriends()).Text);
        Assert.Equal(0, ReadingValue.FromText("x").List.Count);
    }

    [Fact]
    public void AListRead_AsOneValue_IsItsCount_WithTheListsUnit()
    {
        var text = ReadingFormatter.Format(Reading.Ok(ReadingValue.FromList(ThreeFriends())), Friends, null, new Source());

        Assert.Equal("3", text.Value);
        Assert.Equal("playing", text.Unit);
        Assert.True(text.IsCurrent);
    }

    [Fact]
    public void AnEmptyList_ReadsAsZero_NotADash()
    {
        var text = ReadingFormatter.Format(Reading.Ok(ReadingValue.FromList(ReadingList.Empty)), Friends, null, new Source());

        Assert.Equal("0", text.Value);
    }

    [Fact]
    public void AListWithoutADescriptor_StillReadsAsItsCount()
    {
        var text = ReadingFormatter.Format(Reading.Ok(ReadingValue.FromList(ThreeFriends())), null, null, new Source());

        Assert.Equal("3", text.Value);
    }

    [Fact]
    public void AField_IsFormattedLikeAReadingOfItsKind()
    {
        var hours = Friends.Fields[2];

        var text = ReadingFormatter.FormatField(Row("1", "Alex", hours: 12.25), hours, null, new Source());

        Assert.Equal("12.3", text.Value);
        Assert.Equal("h", text.Unit);
    }

    [Fact]
    public void AnAbsentField_IsADash()
    {
        var text = ReadingFormatter.FormatField(Row("2", "Sam"), Friends.Fields[1], null, new Source());

        Assert.Equal(ReadingFormatter.Dash, text.Value);
        Assert.False(Row("2", "Sam").TryGet("game", out _));
    }

    [Fact]
    public void Roles_FollowTheDescription_AndTheTitleFallsBackToTheFirstTextField()
    {
        var roles = ListRoles.Of(Friends);
        Assert.Equal("name", roles.Title!.Name);
        Assert.Equal("game", roles.Detail!.Name);
        Assert.Equal("avatar", roles.Image!.Name);
        Assert.Null(roles.Link);

        var bare = ListRoles.Of(new ReadingDescriptor("x", ReadingKind.List, "x", "x")
        {
            Fields = [new ReadingField("n", ReadingKind.Number, "N"), new ReadingField("t", ReadingKind.Text, "T")],
        });
        Assert.Equal("t", bare.Title!.Name);
    }

    [Fact]
    public void TextListFromAList_MapsRolesToItemParts_KeyedByRow()
    {
        var list = new TextList();

        list.SetItems(ThreeFriends(), Friends, new Source());

        Assert.Equal(3, list.Count);
        Assert.Equal("Alex", list[0].Text);
        Assert.Equal("Deep Rock Galactic", list[0].Detail);
        Assert.Null(list[1].Detail);
        Assert.Equal(["1", "2", "3"], new[] { list[0].Key, list[1].Key, list[2].Key });
    }

    [Fact]
    public void TextListFromAList_TakesAtMostMaxRows()
    {
        var list = new TextList();

        list.SetItems(ThreeFriends(), Friends, new Source(), maxRows: 2);

        Assert.Equal(2, list.Count);
    }

    [Fact]
    public void Paging_MovesByWholeRows_AndBackToTheStart()
    {
        var rows = Enumerable.Range(0, 30).Select(i => Row(i.ToString(System.Globalization.CultureInfo.InvariantCulture), "Friend " + i, "Game " + i)).ToList();
        var rect = new SKRect(0, 0, 300, 120);
        var time = DateTime.Now;
        using var bitmap = new SKBitmap(300, 120);
        using var canvas = new SKCanvas(bitmap);
        var list = new TextList { WholeRows = true };
        list.SetItems(new ReadingList(rows), Friends, new Source());
        list.Draw(canvas, _theme, rect, time);

        list.Page(1);
        list.Draw(canvas, _theme, rect, time);
        float first = list.Offset;
        list.Page(1);
        list.Draw(canvas, _theme, rect, time);

        Assert.True(first > 0);
        Assert.True(list.Offset > first);
        // Every page starts on a row: the offset is the top of some item.
        Assert.Contains(Enumerable.Range(0, list.Count).Select(i => TopOfItem(list, i, rect, canvas, time)), top => Math.Abs(top - first) < 0.5f);

        list.Page(-1);
        list.Draw(canvas, _theme, rect, time);
        Assert.Equal(first, list.Offset, 1);
        list.Page(-1);
        list.Draw(canvas, _theme, rect, time);
        Assert.Equal(0, list.Offset, 1);
    }

    /// <summary>The offset at which the item is at the top, found by scrolling to it.</summary>
    private float TopOfItem(TextList list, int index, SKRect rect, SKCanvas canvas, DateTime time)
    {
        var probe = new TextList { WholeRows = true };
        for (int i = 0; i < list.Count; i++)
            probe.Add(list[i]);
        probe.Draw(canvas, _theme, rect, time);
        probe.ScrollToItem(index);
        probe.Draw(canvas, _theme, rect, time);
        return probe.Offset;
    }

    [Fact]
    public void ScrollPosition_StaysOnTheFirstVisibleRowThatSurvives()
    {
        var rows = Enumerable.Range(0, 30).Select(i => Row(i.ToString(System.Globalization.CultureInfo.InvariantCulture), "Friend " + i, "Game " + i)).ToList();
        var rect = new SKRect(0, 0, 300, 120);
        var time = DateTime.Now;
        using var bitmap = new SKBitmap(300, 120);
        using var canvas = new SKCanvas(bitmap);
        var list = new TextList();
        list.SetItems(new ReadingList(rows), Friends, new Source());
        list.Draw(canvas, _theme, rect, time);
        list.ScrollToItem(2);
        list.Draw(canvas, _theme, rect, time);
        float before = list.Offset;

        // The first two rows go; row "2" was at the top and is now the first.
        list.SetItems(new ReadingList(rows.Skip(2)), Friends, new Source());
        list.Draw(canvas, _theme, rect, time);

        Assert.Equal("Friend 2", list[0].Text);
        Assert.Equal(0, list.Offset, 1);
        Assert.True(before > 0);
    }
}
