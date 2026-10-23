// "Configuration Pages" of the website spec: every built-in theme, background and widget, every widget setting, every finding
// code and every key of the published schemas must be in the documentation. The table components record what they rendered
// (src/lib/reference.mjs); this compares it with what the product ships. Pure, so the test can feed it a fixture catalog.

/**
 * @param catalog   the product's catalog (themes, backgrounds, widgets with settings, findings)
 * @param schemas   [{ name: 'v1/theme.json', paths: [...every key as a dotted path] }]
 * @param records   what the components rendered, or null when there is no record at all
 * @param troubleshooting  the text of the troubleshooting page
 * @returns a list of sentences, empty when nothing is missing
 */
export function completenessProblems({ catalog, schemas, records, troubleshooting }) {
  if (records === null) return ['no record of what the reference components rendered (.astro/urdeck-reference.jsonl is missing)'];
  const problems = [];
  const named = (kind, field) => new Set(records.filter((r) => r.kind === kind).flatMap((r) => (r[field] ? [].concat(r[field]) : [])));

  for (const schema of schemas) {
    const shown = new Set(records.filter((r) => r.kind === 'schema' && r.schema === schema.name).flatMap((r) => r.paths));
    const missing = schema.paths.filter((p) => !shown.has(p));
    if (missing.length > 0) problems.push(`${schema.name}: no documentation page shows ${missing.length} key(s): ${missing.slice(0, 12).join(', ')}${missing.length > 12 ? ', ...' : ''}`);
  }

  const builtin = (what) => new Set(records.filter((r) => r.kind === 'builtin' && r.what === what).flatMap((r) => r.names));
  const lists = [
    ['theme', 'themes', catalog.themes.map((t) => t.name)],
    ['background', 'backgrounds', catalog.backgrounds.map((b) => b.name)],
    ['widget', 'widgets', catalog.widgets.map((w) => w.typeId)],
  ];
  for (const [label, what, names] of lists) {
    const shown = builtin(what);
    for (const name of names) if (!shown.has(name)) problems.push(`the built-in ${label} "${name}" is on no documentation page (a BuiltinList kind="${what}" would list it)`);
  }

  const settings = new Map(records.filter((r) => r.kind === 'settings').map((r) => [r.widget, new Set(r.names)]));
  for (const widget of catalog.widgets) {
    const shown = settings.get(widget.typeId);
    if (!shown) {
      problems.push(`the widget ${widget.typeId} has no settings table (add a SettingsTable to its page)`);
      continue;
    }
    for (const s of widget.settings) if (!shown.has(s.name)) problems.push(`the setting "${s.name}" of ${widget.typeId} is not in its settings table`);
  }

  const codes = named('findings', 'codes');
  for (const f of catalog.findings) {
    if (!codes.has(f.code)) problems.push(`the finding ${f.code} is in no findings table`);
    else if (!troubleshooting.includes(f.code)) problems.push(`the finding ${f.code} does not appear on the troubleshooting page`);
  }
  return problems;
}
