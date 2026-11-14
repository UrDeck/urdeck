// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Patrick Bigler

namespace UrDeck.Sdk.Data;

/// <summary>What a field of a list row is for, so that a widget that has never heard of the provider can draw the row.</summary>
public enum FieldRole
{
    /// <summary>No role: the field is for a widget that knows it by name.</summary>
    None,

    /// <summary>The row's main text. At most one field has it; without one the first text field is the title.</summary>
    Title,

    /// <summary>A second line under the title.</summary>
    Detail,

    /// <summary>A short mark before the title (a state, a count); coloured by the field's levels.</summary>
    Status,

    /// <summary>A local file path or an <c>http</c> or <c>https</c> address of a picture.</summary>
    Image,

    /// <summary>A launch target opened by a tap on the row.</summary>
    Link,
}

/// <summary>
/// What one field of a list reading's rows holds: the reading description without a path, so that the formatter formats a
/// field by the rules it has for a reading of that kind.
/// </summary>
/// <param name="Name">The field's name in a row, for example <c>game</c>.</param>
/// <param name="Kind">What the field measures; any kind but <see cref="ReadingKind.List"/>.</param>
/// <param name="Label">A short label, for example <c>Playing</c>.</param>
public sealed record ReadingField(string Name, ReadingKind Kind, string Label)
{
    /// <summary>What the field is for; a role a widget does not know is ignored.</summary>
    public FieldRole Role { get; init; }

    /// <summary>The unit text of a plain number.</summary>
    public string? Unit { get; init; }

    /// <summary>The unit to show a kind in when the widget does not choose one.</summary>
    public DisplayUnit? DisplayUnit { get; init; }

    /// <summary>The unit a <see cref="ReadingKind.Depth"/> field is reported in.</summary>
    public DepthUnit? DepthUnit { get; init; }

    /// <summary>The number of decimals to show.</summary>
    public int? Decimals { get; init; }

    /// <summary>The smallest value (canonical unit).</summary>
    public double? Min { get; init; }

    /// <summary>The largest value (canonical unit).</summary>
    public double? Max { get; init; }

    /// <summary>From this value on the field deserves attention.</summary>
    public double? Warning { get; init; }

    /// <summary>From this value on the field is a fault.</summary>
    public double? Critical { get; init; }

    /// <summary>The field as a reading description (its name as the path), which is what the formatter works from.</summary>
    public ReadingDescriptor ToDescriptor() => new(Name, Kind, Label, Label)
    {
        Unit = Unit,
        DisplayUnit = DisplayUnit,
        DepthUnit = DepthUnit,
        Decimals = Decimals,
        Min = Min,
        Max = Max,
        Warning = Warning,
        Critical = Critical,
    };
}

/// <summary>
/// One row of a <see cref="ReadingList"/>: a key and named fields. A field a row lacks is absent, which is not an error. Immutable.
/// </summary>
public sealed class ReadingRow : IEquatable<ReadingRow>
{
    private readonly Dictionary<string, ReadingValue> _fields;
    private readonly int _hash;

    /// <summary>Makes a row.</summary>
    /// <param name="key">Unique in the list and the same for the same thing across publications (a SteamID, an app id).</param>
    /// <param name="fields">The fields the row has, by name. A field holding a list makes the publication invalid.</param>
    public ReadingRow(string key, IEnumerable<KeyValuePair<string, ReadingValue>>? fields = null)
    {
        Key = key ?? "";
        _fields = new Dictionary<string, ReadingValue>(StringComparer.Ordinal);
        if (fields != null)
        {
            foreach (var (name, value) in fields)
                _fields[name] = value;
        }

        // Order independent, so that the same fields in another order are the same row.
        int hash = StringComparer.Ordinal.GetHashCode(Key);
        foreach (var (name, value) in _fields)
            hash += HashCode.Combine(StringComparer.Ordinal.GetHashCode(name), value) * 31;
        _hash = hash;
    }

    /// <summary>The row's key.</summary>
    public string Key { get; }

    /// <summary>The number of fields the row has.</summary>
    public int FieldCount => _fields.Count;

    /// <summary>The names of the fields the row has.</summary>
    public IEnumerable<string> Names => _fields.Keys;

    /// <summary>Whether a field holds a list, which is not allowed.</summary>
    public bool HasNestedList => _fields.Values.Any(v => v.Type == ReadingValueType.List);

    /// <summary>The value of a field; false when the row lacks it.</summary>
    public bool TryGet(string name, out ReadingValue value) => _fields.TryGetValue(name, out value);

    /// <summary>The value of a field; null when the row lacks it.</summary>
    public ReadingValue? Get(string name) => _fields.TryGetValue(name, out var value) ? (ReadingValue?)value : null;

    /// <summary>This row with every field passed through <paramref name="change"/>, which is how the hub cuts a long text.</summary>
    public ReadingRow Map(Func<string, ReadingValue, ReadingValue> change) =>
        new(Key, _fields.Select(f => KeyValuePair.Create(f.Key, change(f.Key, f.Value))));

    /// <inheritdoc />
    public bool Equals(ReadingRow? other)
    {
        if (other is null || _hash != other._hash || Key != other.Key || _fields.Count != other._fields.Count)
            return false;
        foreach (var (name, value) in _fields)
        {
            if (!other._fields.TryGetValue(name, out var theirs) || !value.Equals(theirs))
                return false;
        }

        return true;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as ReadingRow);

    /// <inheritdoc />
    public override int GetHashCode() => _hash;
}

/// <summary>
/// The value of a list reading: rows in order, replaced as a whole so a widget never sees part of an old list and part of a new
/// one. Two lists are equal when they have the same rows in the same order with the same keys and equal fields; the hash is
/// computed once, so comparing an unchanged list a provider publishes again is cheap and causes no repaint. Immutable.
/// </summary>
public sealed class ReadingList : IEquatable<ReadingList>
{
    /// <summary>The most rows the hub keeps.</summary>
    public const int MaxRows = 200;

    /// <summary>The most characters the hub keeps of a text field.</summary>
    public const int MaxTextLength = 500;

    /// <summary>A list with no rows.</summary>
    public static readonly ReadingList Empty = new([]);

    private readonly ReadingRow[] _rows;
    private readonly int _hash;

    /// <summary>Makes a list of the rows, in order.</summary>
    public ReadingList(IEnumerable<ReadingRow> rows)
    {
        _rows = [.. rows];
        var hash = new HashCode();
        foreach (var row in _rows)
            hash.Add(row);
        _hash = hash.ToHashCode();
    }

    /// <summary>The number of rows.</summary>
    public int Count => _rows.Length;

    /// <summary>The rows, in the provider's order.</summary>
    public IReadOnlyList<ReadingRow> Rows => _rows;

    /// <summary>The row at an index.</summary>
    public ReadingRow this[int index] => _rows[index];

    /// <inheritdoc />
    public bool Equals(ReadingList? other)
    {
        if (ReferenceEquals(this, other))
            return true;
        if (other is null || _hash != other._hash || _rows.Length != other._rows.Length)
            return false;
        for (int i = 0; i < _rows.Length; i++)
        {
            if (!_rows[i].Equals(other._rows[i]))
                return false;
        }

        return true;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as ReadingList);

    /// <inheritdoc />
    public override int GetHashCode() => _hash;
}

/// <summary>
/// The fields of a list reading by what they are for, as a generic widget reads them. The title is the field with the title
/// role, else the first text field; the others are the first field with their role. A part the description has no field for is null.
/// </summary>
/// <param name="Title">The row's main text.</param>
/// <param name="Detail">The second line.</param>
/// <param name="Status">The mark before the title.</param>
/// <param name="Image">The picture, a file path or an address.</param>
/// <param name="Link">The launch target of a tap.</param>
public sealed record ListRoles(ReadingField? Title, ReadingField? Detail, ReadingField? Status, ReadingField? Image, ReadingField? Link)
{
    /// <summary>Resolves the roles of a description.</summary>
    public static ListRoles Of(ReadingDescriptor descriptor)
    {
        var fields = descriptor.Fields;
        ReadingField? Role(FieldRole role) => fields.FirstOrDefault(f => f.Role == role);
        return new ListRoles(
            Role(FieldRole.Title) ?? fields.FirstOrDefault(f => f.Kind == ReadingKind.Text && f.Role == FieldRole.None),
            Role(FieldRole.Detail),
            Role(FieldRole.Status),
            Role(FieldRole.Image),
            Role(FieldRole.Link));
    }
}

/// <summary>
/// The fields of a list reading, in order. A list with value equality, so that two descriptions with the same fields are equal
/// (a descriptor is a record). It takes a collection expression: <c>Fields = [new ReadingField(...), ...]</c>.
/// </summary>
[System.Runtime.CompilerServices.CollectionBuilder(typeof(ReadingFields), nameof(Create))]
[System.Text.Json.Serialization.JsonConverter(typeof(ReadingFieldsJsonConverter))]
public sealed class ReadingFields : IReadOnlyList<ReadingField>, IEquatable<ReadingFields>
{
    /// <summary>No fields.</summary>
    public static readonly ReadingFields Empty = new([]);

    private readonly ReadingField[] _fields;

    /// <summary>Makes the fields, in order.</summary>
    public ReadingFields(IEnumerable<ReadingField> fields) => _fields = [.. fields];

    /// <summary>The number of fields.</summary>
    public int Count => _fields.Length;

    /// <summary>The field at an index.</summary>
    public ReadingField this[int index] => _fields[index];

    /// <summary>What a collection expression calls.</summary>
    public static ReadingFields Create(ReadOnlySpan<ReadingField> fields) => fields.IsEmpty ? Empty : new ReadingFields(fields.ToArray());

    /// <inheritdoc />
    public IEnumerator<ReadingField> GetEnumerator() => ((IEnumerable<ReadingField>)_fields).GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    /// <inheritdoc />
    public bool Equals(ReadingFields? other) => other is not null && _fields.AsSpan().SequenceEqual(other._fields);

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as ReadingFields);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var field in _fields)
            hash.Add(field);
        return hash.ToHashCode();
    }
}

/// <summary>Reads and writes <see cref="ReadingFields"/> as a JSON array, for the editor's cache of descriptions.</summary>
internal sealed class ReadingFieldsJsonConverter : System.Text.Json.Serialization.JsonConverter<ReadingFields>
{
    public override ReadingFields Read(ref System.Text.Json.Utf8JsonReader reader, Type typeToConvert, System.Text.Json.JsonSerializerOptions options) =>
        new(System.Text.Json.JsonSerializer.Deserialize<List<ReadingField>>(ref reader, options) ?? []);

    public override void Write(System.Text.Json.Utf8JsonWriter writer, ReadingFields value, System.Text.Json.JsonSerializerOptions options) =>
        System.Text.Json.JsonSerializer.Serialize(writer, value.ToArray(), options);
}
