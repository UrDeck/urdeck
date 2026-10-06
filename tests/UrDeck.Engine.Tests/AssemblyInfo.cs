// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using Xunit;

// The hub tests drive a fake clock and wait on real threads, one test burns CPU on purpose, and several tests redirect
// process-wide state (the log, the shadow plugin folder). Run them one at a time so a slow or busy machine cannot
// starve one test's threads while another measures. The whole suite still takes only a few seconds.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
