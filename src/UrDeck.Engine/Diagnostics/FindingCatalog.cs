// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

namespace UrDeck.Engine.Diagnostics;

/// <summary>A finding code with its severity as the documentation gives it (<c>error, stops</c> for a shader error that stops it loading) and what it means.</summary>
public sealed record FindingCode(string Code, string Severity, string Summary);

/// <summary>
/// Every finding code of the theme checks (<see cref="Themes.ThemeCodes"/>) and the background lint
/// (<see cref="Backgrounds.ShaderCodes"/>), with a one-line summary, for the documentation and <c>urdeck-cli catalog export</c>.
/// A test keeps it in step with the code tables; the full text is in <c>docs/themes.md</c> and <c>docs/backgrounds.md</c>.
/// </summary>
public static class FindingCatalog
{
    public static IReadOnlyList<FindingCode> All { get; } =
    [
        new("THEME001", "error", "`theme.json` is missing, is not valid JSON or holds no object: add it, or fix the syntax at the position given. Nothing else is checked"),
        new("THEME002", "warning", "a key the deck does not read (a typo, or a key of a later format): fix the name, the message suggests the nearest key"),
        new("THEME003", "warning, error", "a value of the wrong JSON type (also logged). An error for a value of format 1 (`card.gap`, `colors`...), where the deck skips the whole theme"),
        new("THEME004", "warning", "a number outside its range, which the message gives (also logged): the deck uses the default"),
        new("THEME005", "warning", "a value that is not one of the names allowed, which the message lists (also logged; the case does not matter)"),
        new("THEME006", "warning", "a colour that is not `#RRGGBB` or `#AARRGGBB` (also logged)"),
        new("THEME007", "warning", "a list outside its length limits or with an invalid element (also logged): the deck drops the list whole"),
        new("THEME008", "warning", "a group that needs a member it does not have, such as a gradient without `colors` (also logged): the group is ignored"),
        new("THEME010", "warning", "`format` is newer than the deck reads (also logged): the deck ignores what it does not know"),
        new("THEME020", "error", "a font file is missing from the theme, is not a `.ttf`, `.otf` or `.ttc`, or its path leaves the theme's folder (also logged when it leaves the folder or cannot be loaded): add the file, fix the name, keep it inside the folder"),
        new("THEME021", "warning", "a font file is over 3 MB (subset it) or has no licence file beside it (`*-OFL.txt`, `LICENSE*` or `*-LICENSE*`): add the licence so the theme can be shared"),
        new("THEME022", "warning", "a font named by family, which is used only where it is installed: put a `.ttf` or `.otf` in the folder and name the file"),
        new("THEME023", "warning", "a weight outside a variable font's range, or one a static font does not have, for a role (also logged): choose a weight the file has"),
        new("THEME040", "warning", "`background.default.name` is not a background that exists: use a built-in one, or point `--background-dir` at yours"),
        new("THEME041", "warning", "`meta.pairsWith.backgrounds` names a background that does not exist: fix the name or remove it"),
        new("THEME042", "warning", "the default background is not in the theme's own `pairsWith` list: add it"),
        new("THEME043", "warning", "the still background picture is missing, leaves the folder or is over 8 MB"),
        new("THEME044", "warning", "`icons.set` names an icon set that exists neither in the theme's `icons/` folder nor among the built-in sets (also logged): fix the name"),
        new("THEME050", "warning", "a border style, gradient or animation with `card.borderWidth` 0 (also logged): give the border a width"),
        new("THEME051", "warning", "`card.borderAnimate` `spin` without a conic `card.borderGradient` (also logged): the deck switches the animation off"),
        new("THEME052", "warning", "a pattern whose marks are closer than three pixels at the panel width (also logged): raise `spacing`"),
        new("THEME053", "warning", "outside corner marks reaching past `card.gap`, thickness included (also logged): move them in or widen the gap"),
        new("THEME054", "warning", "a shadow or glow whose reach is more than the gap over a translucent fill (logged as information, the deck limits it): reduce the blur or offset"),
        new("THEME055", "warning", "a line (border, highlight, accent strip, corner mark) that resolves under one pixel, which the deck draws 1 px wide (also logged)"),
        new("THEME056", "info", "`card.compact.shape` `circle` on a card that is not square, which is a capsule (also logged)"),
        new("THEME057", "warning", "`card.borderAnimate` on a `dashed` `card.borderStyle` (also logged): the border is drawn still, because the moving ring is stretched round the card and a dash pattern fitted to each side does not survive that"),
        new("THEME060", "warning", "the text colour on the card fill, or on its gradient's darkest or lightest stop, is under 4.5:1: change the colours; a translucent fill is composited over `colors.background`, and the message says so because the page behind may be a moving background"),
        new("THEME061", "warning", "the text colour on a card tint is under 4.5:1 (a tinted card keeps `colors.text`): change the tint at the position the message names"),
        new("THEME062", "info", "the muted text colour on the card fill is under 3:1"),
        new("THEME070", "error", "an icon set's `manifest.json` is missing, is not valid JSON, has a `version` other than 1 or has no `keys` (also logged): add or fix it"),
        new("THEME071", "error", "a manifest entry names a file that is missing, does not end in `.svg` or leaves the set's folder (also logged)"),
        new("THEME072", "error", "an icon file breaks the file rules: over 4 KB, no `viewBox` of `0 0 24 24`, an element or attribute that is not allowed, a `transform`, not valid XML (also logged)"),
        new("THEME073", "warning", "a manifest key is not one of the product's icon keys, so no widget asks for it"),
        new("THEME074", "warning", "the manifest's `license` or `source` is empty, or the set's folder has no licence file"),
        new("THEME075", "warning", "an `.svg` file in the set's folder that no manifest entry names (the tool alone reports it)"),
        new("THEME076", "info", "the set has no icon for some of the product's keys; the message names them and says `outline` answers them"),
        new("THEME080", "warning", "`meta` lacks a `name`, a `description` or a `mode`: add them so an editor can list the theme"),
        new("THEME090", "info", "more than one continuous animation is on (also logged): each costs CPU while it runs"),
        new("SHADER001", "error, stops", "`manifest.json` is missing: add the file (see the minimal manifest)"),
        new("SHADER002", "error, stops", "not valid JSON, or the top level is not an object: fix the syntax at the position given"),
        new("SHADER003", "error, stops", "`manifest` is missing, not an integer, or not a version this product reads: use `\"manifest\": 1`"),
        new("SHADER004", "warning", "an unknown key in the manifest, a parameter or a sample: fix the spelling or remove it"),
        new("SHADER005", "warning", "a value has the wrong JSON type (a number where text is expected): fix the value"),
        new("SHADER010", "error, stops", "the entry file does not exist: create it, or fix `entry`"),
        new("SHADER011", "error, stops", "`entry` is absolute or leaves the folder: name a file inside it"),
        new("SHADER012", "error", "the folder's name is not a valid background name (letters, digits, `-`, `_`): rename the folder. The deck could never select it, so only `urdeck-cli` reports it"),
        new("SHADER020", "error, stops", "the shader does not compile: the compiler's message follows, with its line number"),
        new("SHADER021", "error, stops", "the shader declares a `uniform shader`: the host has nothing to give it; remove it"),
        new("SHADER022", "warning", "a uniform has a type that cannot be set (`int`, a matrix): it stays 0; use `float`"),
        new("SHADER023", "warning", "a `// default:` cannot be read: give numbers matching the type, or a `#RRGGBB` colour"),
        new("SHADER030", "warning", "the manifest annotates a parameter the shader does not declare: fix the name"),
        new("SHADER031", "warning", "the shader declares a parameter the manifest does not annotate: add it under `parameters`"),
        new("SHADER032", "error", "the manifest annotates a name that belongs to the product (`speed`, `iTime`, ...): remove it"),
        new("SHADER033", "warning", "`kind` `color` on a parameter without three or four components: use `float3` or `float4`"),
        new("SHADER034", "warning", "an unknown `kind`: use `number` or `color`"),
        new("SHADER035", "warning", "`min` is above `max`, or the shader's default is outside the range: fix the range or the default"),
        new("SHADER040", "warning", "no `title`"),
        new("SHADER041", "warning", "no `description`"),
        new("SHADER042", "warning", "a parameter that is not `hidden` has no `description`"),
        new("SHADER050", "warning", "no `samples`: render a picture and list it"),
        new("SHADER051", "error", "a sample file does not exist: create it or fix `file`"),
        new("SHADER052", "error", "a sample `file` is absolute or leaves the folder: name a file inside it"),
        new("SHADER053", "warning", "an unknown sample `type`: use `image` (or `video`)"),
        new("SHADER060", "info", "the shader says `layout(color)` and the manifest says `kind` `number`: the manifest wins"),
        new("SHADER070", "warning", "only `urdeck-cli shader render` reports it: the picture is still the theme's background colour everywhere, so the shader drew nothing at that time. Check `--time` and the alpha of the returned colour; a shader can be legitimately blank at one instant"),
    ];
}
