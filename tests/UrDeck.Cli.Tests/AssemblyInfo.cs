// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using Xunit.Sdk;
using Xunit.v3;

// The tests change the working directory (the tool prints paths relative to it).
[assembly: Parallelization(Mode = ParallelMode.None)]
