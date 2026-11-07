# Deck session (refactor)

Measured 2026-11-07 on the owner's panel (HYTE Y70 Touch, 1100x3840 at 100 %), Release builds of `main` (6b657a5) and of the
`deck-session` branch, each run in a fresh copy of the build output with the owner's configuration (theme `rigline` with its
moving `horizon` shader, four pages, three widgets in the dock, Home Assistant connected). The method is `motion.md`'s: 15 s of
warm-up, then 60 one-second samples; CPU is the process's processor time as a share of one core, memory the median, GPU the
mean of the process's 3D engine utilisation (`\GPU Engine(pid_N_*engtype_3D)\Utilization Percentage`). Runs alternated
between the two builds. "Moving" is `motion: full` (the shader at 30 frames a second, a spinning border on twelve cards);
"idle" is `motion: reduced` (the shader drawn once, no border sprites).

| Case | `main` | branch |
|---|---|---|
| Idle, CPU (five runs) | 0.08, 0.08, 0.13, 0.00, 0.13 % (mean 0.08) | 0.18, 0.23, 0.13, 0.00, 0.05 % (mean 0.12) |
| Idle, private bytes | 155.2–159.3 MB | 156.5–160.3 MB |
| Moving, CPU (three runs) | 6.65, 6.90, 6.64 % (mean 6.73) | 6.46, 7.51, 6.93 % (mean 6.97) |
| Moving, GPU 3D | 1.79–1.80 % | 1.78–1.81 % |
| Moving, private bytes | 193.7–195.2 MB | 191.9–193.4 MB |

The two builds cost the same: every difference is within the noise `motion.md` records for runs of one case (0.4 % of a core,
3 MB). The idle difference of the means, 0.04 % of a core, is about one tick of the process's processor-time counter (15 ms)
over the 60 s window.

On the live deck (`urdeck.log`): the background held 29.8 to 30.0 frames a second (`Background frames stopped ...: 8045 frames
in 268.6 s`), a plugin hot reload logged `Unloaded plugin context(s) collected after 1606 ms`, and an unrelated configuration
save rebuilt the page once without restarting the background.
