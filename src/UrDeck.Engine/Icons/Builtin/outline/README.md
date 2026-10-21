# outline

The built-in icon set: [Lucide](https://lucide.dev) icons at the release named by `upstreamVersion` in `manifest.json`, copied
once from the `lucide-static` package (the `icons/` folder, files unchanged). The licence text is in `LICENSE-lucide.txt` (ISC,
and MIT for the icons derived from Feather).

To update: `npm pack lucide-static@<version>`, copy the files the manifest names over these, set `upstreamVersion`, and run
`urdeck-cli theme validate` on a theme that names `outline` (the set must stay free of findings). The product has no runtime
dependency on Lucide, and draws no credit for it.
