// The reference tables of the documentation, read from the product: the published schemas (docs/schemas/**) and the catalog
// (src/generated/catalog.json). Used by the table components while the site builds and by scripts/check-output.mjs after it,
// so what the components render and what the check expects come from the same code.
//
// A table fails the build when a key, a setting or a finding has no description. Each component writes what it rendered to
// .astro/urdeck-reference.jsonl (reset when a build starts); the check reads it to find what no page shows.
import { appendFileSync, existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';

/** A site build and its checks run with site/ as the working directory. */
export const siteRoot = () => process.cwd();
export const schemaFile = (name, root = siteRoot()) => join(root, '..', 'docs', 'schemas', name);
export const renderedFile = (root = siteRoot()) => join(root, '.astro', 'urdeck-reference.jsonl');

export const readSchema = (name, root = siteRoot()) => JSON.parse(readFileSync(schemaFile(name, root), 'utf8'));

// ---- Schema rows ---------------------------------------------------------------------------------------------------------

const refName = (node) => (typeof node.$ref === 'string' ? node.$ref.split('/').pop() : null);

/** The node a `$ref` points to (the keys written beside the `$ref` are kept, they are the property's own). */
function resolve(schema, node) {
  const name = refName(node);
  if (!name) return node;
  const target = schema.$defs?.[name];
  if (!target) throw new Error(`the schema refers to $defs/${name}, which it does not define`);
  const { $ref, ...own } = node;
  return { ...target, ...own };
}

const code = (value) => `\`${value}\``;
const number = (n) => String(n);

function range(node) {
  const { minimum, maximum } = node;
  if (minimum !== undefined && maximum !== undefined) return `${number(minimum)} to ${number(maximum)}`;
  if (minimum !== undefined) return `at least ${number(minimum)}`;
  if (maximum !== undefined) return `at most ${number(maximum)}`;
  return '';
}

function length(node) {
  const { minItems, maxItems } = node;
  if (minItems !== undefined && minItems === maxItems) return `exactly ${minItems}`;
  if (minItems !== undefined && maxItems !== undefined) return `${minItems} to ${maxItems}`;
  if (minItems !== undefined) return `at least ${minItems}`;
  if (maxItems !== undefined) return `at most ${maxItems}`;
  return '';
}

/** What a value may be, in words: `round`, `chamfer` / number, 0 to 0.5 / list of 2 to 6 colours. Backticks mark code. */
export function valueOf(schema, node) {
  const ref = refName(node);
  const n = resolve(schema, node);
  if (ref === 'color') return 'colour';
  if (n.const !== undefined) return code(JSON.stringify(n.const));
  if (n.enum) return n.enum.map((v) => code(v)).join(', ');
  if (n.oneOf) return n.oneOf.map((alt) => (alt.const !== undefined ? code(JSON.stringify(alt.const)) : `a ${refName(alt) ?? resolve(schema, alt).type ?? 'value'}`)).join(' or ');
  switch (n.type) {
    case 'integer': return ['whole number', range(n)].filter(Boolean).join(', ');
    case 'number': return ['number', range(n)].filter(Boolean).join(', ');
    case 'boolean': return 'true or false';
    case 'string': return 'text';
    case 'object': return n.additionalProperties && typeof n.additionalProperties === 'object' ? 'group per name' : 'group';
    case 'array': {
      const items = n.items ? resolve(schema, n.items) : {};
      const what = refName(n.items ?? {}) === 'color' ? 'colours'
        : items.enum ? `names of ${items.enum.map((v) => code(v)).join(', ')}`
          : items.type === 'number' ? 'numbers' : items.type === 'object' ? 'groups' : 'text';
      return ['list of', length(n), what].filter(Boolean).join(' ');
    }
    default: return '';
  }
}

export const defaultOf = (schema, node) => {
  const n = resolve(schema, node);
  return n.default === undefined ? '' : code(JSON.stringify(n.default));
};

/** The nodes below a property: its `properties`, a map's value keys (under `<name>`) or an array's item keys (under `[]`). */
function children(schema, node, path) {
  const n = resolve(schema, node);
  const out = [];
  const keys = (holder, prefix) => {
    for (const [key, value] of Object.entries(resolve(schema, holder).properties ?? {})) out.push([`${prefix}.${key}`, value]);
  };
  keys(n, path);
  if (n.additionalProperties && typeof n.additionalProperties === 'object') keys(n.additionalProperties, `${path}.<name>`);
  if (n.items && typeof n.items === 'object') keys(n.items, `${path}[]`);
  return out;
}

/** Every key of the schema as [dotted path, node], groups included, in document order. */
function entries(schema) {
  const out = [];
  const visit = (list) => {
    for (const [path, node] of list) {
      out.push([path, node]);
      visit(children(schema, node, path));
    }
  };
  visit(Object.entries(schema.properties ?? {}));
  return out;
}

export const allPaths = (schema) => entries(schema).map(([path]) => path);

/**
 * The rows of a table: the key at `path` and everything below it, or the whole schema when `path` is empty. Throws when a
 * key has no description of its own (a `$ref` does not lend one).
 */
export function rowsOf(schemaName, path, root = siteRoot()) {
  const schema = readSchema(schemaName, root);
  const all = entries(schema);
  const start = path ? all.filter(([full]) => full === path) : Object.entries(schema.properties ?? {});
  if (start.length === 0) throw new Error(`${schemaName}: there is no key "${path}" in the schema`);
  const rows = [];
  const visit = (list, depth) => {
    for (const [full, node] of list) {
      const description = typeof node.description === 'string' ? node.description.trim() : '';
      if (description === '') throw new Error(`${schemaName}: the key "${full}" has no description (docs/schemas/${schemaName})`);
      const kids = children(schema, node, full);
      rows.push({ path: full, depth, group: kids.length > 0, value: valueOf(schema, node), default: defaultOf(schema, node), description });
      visit(kids, depth + 1);
    }
  };
  visit(start, 0);
  return rows;
}

// ---- The catalog ---------------------------------------------------------------------------------------------------------

export const readCatalog = (root = siteRoot()) => JSON.parse(readFileSync(join(root, 'src', 'generated', 'catalog.json'), 'utf8'));

/** The type column of a widget setting, from the catalog's type names. Backticks mark code. */
export function settingValue(setting) {
  switch (setting.type) {
    case 'onOff': return 'true or false';
    case 'number': return 'number';
    case 'wholeNumber': return 'whole number';
    case 'text': return 'text';
    case 'list': return 'list';
    case 'values': return (setting.values ?? []).map((v) => code(v)).join(', ');
    default: return setting.type;
  }
}

export const settingDefault = (setting) => (setting.default === null || setting.default === undefined || (Array.isArray(setting.default) && setting.default.length === 0) ? '' : code(JSON.stringify(setting.default)));

/** `code` spans of a description as <code>, everything else escaped: the one inline syntax the tables use. */
export function inline(text) {
  const escape = (s) => s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
  return escape(text).replace(/`([^`]+)`/g, '<code>$1</code>');
}

// ---- What the pages rendered ---------------------------------------------------------------------------------------------

/** Appends one record of what a component showed: { kind: 'schema'|'settings'|'findings'|'builtin', ... }. */
export function record(entry, root = siteRoot()) {
  const file = renderedFile(root);
  mkdirSync(dirname(file), { recursive: true });
  appendFileSync(file, JSON.stringify(entry) + '\n');
}

/** Forget the previous build's records; called when a build starts. */
export function resetRecords(root = siteRoot()) {
  const file = renderedFile(root);
  mkdirSync(dirname(file), { recursive: true });
  writeFileSync(file, '');
}

export function readRecords(root = siteRoot()) {
  const file = renderedFile(root);
  if (!existsSync(file)) return null;
  return readFileSync(file, 'utf8').split('\n').filter(Boolean).map((line) => JSON.parse(line));
}
