import { defineCollection } from 'astro:content';
import { glob } from 'astro/loaders';
import { z } from 'astro/zod';
import { sections } from './lib/sections';

// User documentation: Markdown and MDX in src/content/docs, one file per page. `section` and `order` place a page in the
// navigation. The meaning of a widget's settings is its declared help text, read from the product's catalog by SettingsTable.
const docs = defineCollection({
  loader: glob({ pattern: '**/*.{md,mdx}', base: './src/content/docs' }),
  schema: z.object({
    title: z.string(),
    description: z.string(),
    section: z.enum(sections),
    order: z.number(),
  }),
});

// The theme cookbook: one folder per recipe in site/cookbook/<recipe>/ holding theme.json, recipe.json and a README.md whose
// text is the explanation. The page is made from the folder (src/lib/cookbook.ts), the entry's id is the folder's name.
const cookbook = defineCollection({
  loader: glob({ pattern: '*/README.md', base: './cookbook', generateId: ({ entry }) => entry.split('/')[0] }),
  schema: z.object({}),
});

export const collections = { docs, cookbook };
