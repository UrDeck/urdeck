// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Patrick Bigler

namespace UrDeck.Sdk.Data;

/// <summary>
/// Declares a class as a data provider: it owns the readings under <see cref="Id"/> and the engine samples it. The
/// class must implement <see cref="IDataProvider"/> and have a public parameterless constructor.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class DataProviderAttribute : Attribute
{
    public DataProviderAttribute(string id, string name)
    {
        Id = id;
        Name = name;
    }

    /// <summary>Lowercase letters, digits, <c>.</c> and <c>-</c>; the part of a reading id before the colon.</summary>
    public string Id { get; }

    public string Name { get; }

    /// <summary>The sampling interval when the user sets none, in milliseconds.</summary>
    public int DefaultIntervalMs { get; set; } = 1000;

    /// <summary>The fastest sampling the provider allows; a faster setting is raised to it.</summary>
    public int MinIntervalMs { get; set; } = 250;
}
