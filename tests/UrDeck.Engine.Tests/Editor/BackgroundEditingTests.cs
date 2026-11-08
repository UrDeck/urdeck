// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Text.Json.Nodes;
using UrDeck.Engine.Config;
using UrDeck.Engine.Editor;
using UrDeck.Engine.Tests.Support;
using Xunit;

namespace UrDeck.Engine.Tests.Editor;

public class BackgroundEditingTests
{
    private readonly FakeTime _time = new();

    private (ConfigDocument Document, BackgroundEditing Editing) Open(string json)
    {
        var document = new ConfigDocument((JsonObject)JsonNode.Parse(json)!, readOnly: false, time: _time);
        return (document, new BackgroundEditing(document));
    }

    private static string Background(ConfigDocument document) => document.Root["background"]?.ToJsonString() ?? "<absent>";

    [Fact]
    public void The_theme_is_set_and_choosing_the_same_one_changes_nothing()
    {
        var (document, editing) = Open("""{ "theme": "default-dark", "pages": [] }""");

        Assert.False(editing.SetTheme("default-dark"));
        Assert.True(editing.SetTheme("carbon"));
        Assert.Equal("carbon", document.Root["theme"]!.GetValue<string>());
    }

    [Fact]
    public void The_themes_choice_removes_the_background_and_none_writes_a_type()
    {
        var (document, editing) = Open("""{ "background": { "type": "shader", "name": "nebula", "dim": 0.4 } }""");

        Assert.True(editing.UseThemesBackground());
        Assert.Equal("<absent>", Background(document));
        Assert.False(editing.UseThemesBackground());

        Assert.True(editing.UseNoBackground());
        Assert.Equal("""{"type":"none"}""", Background(document));
    }

    [Fact]
    public void Another_shader_keeps_still_and_the_dimming_but_not_the_parameters()
    {
        var (document, editing) = Open("""{ "background": { "type": "shader", "name": "nebula", "speed": 0, "dim": 0.4, "density": 3 } }""");

        Assert.True(editing.UseShader("plasma"));

        Assert.Equal("""{"type":"shader","name":"plasma","speed":0,"dim":0.4}""", Background(document));
    }

    [Fact]
    public void A_shader_after_a_moving_one_or_the_themes_choice_moves_and_a_picture_keeps_the_dimming()
    {
        var (document, editing) = Open("""{ "background": { "type": "shader", "name": "nebula", "dim": 0.2 } }""");
        editing.UseShader("plasma");
        Assert.Equal("""{"type":"shader","name":"plasma","dim":0.2}""", Background(document));

        editing.UsePicture(@"C:\pics\wall.jpg");
        Assert.Equal("image", document.Root["background"]!["type"]!.GetValue<string>());
        Assert.Equal(@"C:\pics\wall.jpg", document.Root["background"]!["path"]!.GetValue<string>());
        Assert.Equal(0.2, document.Root["background"]!["dim"]!.GetValue<double>());

        var (themed, themedEditing) = Open("""{ "theme": "carbon" }""");
        themedEditing.UseShader("horizon");
        Assert.Equal("""{"type":"shader","name":"horizon"}""", Background(themed));
        Assert.False(themedEditing.UseShader("horizon"));
    }

    [Fact]
    public void Still_writes_speed_zero_and_moving_removes_it_for_a_shader_only()
    {
        var (document, editing) = Open("""{ "background": { "type": "shader", "name": "nebula", "density": 3 } }""");

        Assert.True(editing.SetStill(true));
        Assert.Equal(0, document.Root["background"]!["speed"]!.GetValue<int>());
        Assert.True(editing.SetStill(false));
        Assert.Null(document.Root["background"]!["speed"]);
        Assert.Equal(3, document.Root["background"]!["density"]!.GetValue<int>());

        var (picture, pictureEditing) = Open("""{ "background": { "type": "image", "path": "a.png" } }""");
        Assert.False(pictureEditing.SetStill(true));
        Assert.Equal("""{"type":"image","path":"a.png"}""", Background(picture));
    }

    [Fact]
    public void Dim_is_written_clamped_and_removed_with_null_and_needs_a_shader_or_a_picture()
    {
        var (document, editing) = Open("""{ "background": { "type": "shader", "name": "nebula" } }""");

        Assert.True(editing.SetDim(0.456));
        Assert.Equal(0.46, document.Root["background"]!["dim"]!.GetValue<double>());
        Assert.True(editing.SetDim(2));
        Assert.Equal(0.7, document.Root["background"]!["dim"]!.GetValue<double>());
        Assert.True(editing.SetDim(null));
        Assert.Null(document.Root["background"]!["dim"]);

        var (none, noneEditing) = Open("""{ "background": { "type": "none" } }""");
        Assert.False(noneEditing.SetDim(0.3));
        var (themed, themedEditing) = Open("""{ }""");
        Assert.False(themedEditing.SetDim(0.3));
        Assert.Equal("<absent>", Background(themed));
        Assert.Equal("""{"type":"none"}""", Background(none));
    }

    [Fact]
    public void Reset_removes_only_the_parameters_in_one_undoable_step()
    {
        var (document, editing) = Open("""{ "background": { "type": "shader", "name": "nebula", "speed": 0, "dim": 0.3, "density": 3, "glow": 0.5 } }""");

        Assert.True(editing.ResetParameters());
        Assert.Equal("""{"type":"shader","name":"nebula","speed":0,"dim":0.3}""", Background(document));
        Assert.False(editing.ResetParameters());

        document.History.Undo();
        Assert.Equal(3, document.Root["background"]!["density"]!.GetValue<int>());
        Assert.Equal(0.5, document.Root["background"]!["glow"]!.GetValue<double>());
    }

    [Fact]
    public void The_motion_level_is_one_of_three_names_or_follows_windows()
    {
        var (document, editing) = Open("""{ }""");

        Assert.True(editing.SetMotionLevel("off"));
        Assert.Equal("off", document.Root["motion"]!.GetValue<string>());
        Assert.False(editing.SetMotionLevel("sometimes"));
        Assert.True(editing.SetMotionLevel(null));
        Assert.Null(document.Root["motion"]);
    }

    [Fact]
    public void A_choice_after_a_still_or_a_dim_made_in_this_session_keeps_them()
    {
        // The numbers an edit wrote are .NET values, not the elements of a parsed file.
        var (document, editing) = Open("""{ "background": { "type": "shader", "name": "nebula" } }""");

        Assert.True(editing.SetStill(true));
        Assert.True(editing.SetDim(0.4));
        Assert.True(editing.UseShader("plasma"));
        Assert.Equal(0, document.Root["background"]!["speed"]!.GetValue<int>());
        Assert.Equal(0.4, document.Root["background"]!["dim"]!.GetValue<double>());

        Assert.True(editing.UsePicture(@"C:\p.png"));
        Assert.Equal(0.4, document.Root["background"]!["dim"]!.GetValue<double>());
        Assert.Null(document.Root["background"]!["speed"]);
    }

    [Fact]
    public void Numbers_are_read_whatever_made_the_node()
    {
        Assert.Equal(0, BackgroundEditing.NumberOf(JsonValue.Create(0)));
        Assert.Equal(2.5, BackgroundEditing.NumberOf(JsonValue.Create(2.5)));
        Assert.Equal(3, BackgroundEditing.NumberOf(JsonNode.Parse("3")));
        Assert.Null(BackgroundEditing.NumberOf(JsonValue.Create("3")));
        Assert.Null(BackgroundEditing.NumberOf(null));
    }
}
