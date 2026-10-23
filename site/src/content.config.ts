import { defineCollection } from 'astro:content';
import { glob } from 'astro/loaders';
import { z } from 'astro/zod';
import { sections } from './lib/sections';

// User documentation: Markdown and MDX in src/content/docs, one file per page. `section` and `order` place a page in the
// navigation. A widget's page also holds `settings`, the meaning of each of its settings (SettingsTable adds the names,
// types and defaults from the product's catalog).
const docs = defineCollection({
  loader: glob({ pattern: '**/*.{md,mdx}', base: './src/content/docs' }),
  schema: z.object({
    title: z.string(),
    description: z.string(),
    section: z.enum(sections),
    order: z.number(),
    settings: z.record(z.string(), z.string()).optional(),
  }),
});

// The theme cookbook: one folder per recipe in site/cookbook/<recipe>/ holding theme.json, recipe.json and a README.md whose
// text is the explanation. The page is made from the folder (src/lib/cookbook.ts), the entry's id is the folder's name.
const cookbook = defineCollection({
  loader: glob({ pattern: '*/README.md', base: './cookbook', generateId: ({ entry }) => entry.split('/')[0] }),
  schema: z.object({}),
});

export const collections = { docs, cookbook };
