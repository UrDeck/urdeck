// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using Xunit;

namespace UrDeck.Engine.Tests;

public class ScratchFailingTest
{
    [Fact]
    public void Fails() => Assert.Fail("deliberate failure for ci-path-filters task 3.5");
}
