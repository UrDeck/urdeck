import { defineCollection } from 'astro:content';
import { glob } from 'astro/loaders';
import { z } from 'astro/zod';

// User documentation: Markdown in src/content/docs, one file per page. `order` sorts the navigation.
const docs = defineCollection({
  loader: glob({ pattern: '**/*.md', base: './src/content/docs' }),
  schema: z.object({
    title: z.string(),
    description: z.string(),
    section: z.enum(['Start', 'Widgets', 'Look']),
    order: z.number(),
  }),
});

export const collections = { docs };
