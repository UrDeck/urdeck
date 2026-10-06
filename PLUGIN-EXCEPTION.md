# UrDeck plugin exception

Additional permission under section 7 of the GNU General Public License, version 3 ("GPLv3"), granted by the
copyright holders of UrDeck for the UrDeck host, engine, official widgets and official data providers (together, the
"Program").

## Definitions

- **SDK Interface**: the public API a plugin uses to interact with the Program, meaning the public types, members and
  attributes of the `UrDeck.Sdk` and `UrDeck.Analyzer` assemblies (`Widget<TConfig>`, `IWidget`, the widget attributes,
  `WidgetConfig`, the render context, and, for data providers, `IDataProvider`, `[DataProvider]`, `IReadingSink`, the
  reading types (`Reading`, `ReadingValue`, `ReadingDescriptor`, `ReadingFormatter`) and the types they expose).
- **Plugin**: a separate module (a .NET assembly, containing widgets, data providers or both) that the Program loads at
  run time through its plugin loader and that interacts with the Program only through the SDK Interface.

## Permission

You may load a Plugin into the Program, and you may create, convey and license a Plugin under terms of your choice,
including proprietary terms. Running the Program together with a Plugin, or building a Plugin against the SDK
Interface, does not by itself make the Plugin subject to the GPLv3 or require you to license it under the GPLv3.

## Limits

- This permission does not apply to the Program itself or to any modified version of it, which remain under the
  GPLv3 and must be conveyed under it.
- It does not apply to code copied from the Program (including the official widgets and data providers) into a Plugin. Such code stays
  under the GPLv3, unless it is code that is separately licensed under the MIT License, such as the `UrDeck.Sdk` and
  `UrDeck.Analyzer` assemblies.

## Removal

If you modify the Program, you may extend this exception to your version, but you are not obliged to. If you do not
wish to, delete this exception notice from your version, as permitted by section 7 of the GPLv3.
