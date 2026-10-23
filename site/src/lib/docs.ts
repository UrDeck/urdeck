// The documentation's navigation: the pages of the `docs` collection and the cookbook's recipes, by section.
import { getCollection } from 'astro:content';
import { sections } from './sections';
import { getRecipes } from './cookbook';

export type NavItem = { section: (typeof sections)[number]; title: string; href: string; order: number };

export const hrefOf = (id: string) => (id === 'index' ? '/docs/' : `/docs/${id}/`);

export async function navigation(): Promise<NavItem[]> {
  const pages = (await getCollection('docs')).map((p) => ({ section: p.data.section, title: p.data.title, href: hrefOf(p.id), order: p.data.order }));
  // A recipe follows the cookbook's own page.
  const cookbook = pages.find((p) => p.href === '/docs/cookbook/')?.order ?? 0;
  const recipes = (await getRecipes()).map((r) => ({ section: 'Cookbook' as const, title: r.title, href: `/docs/cookbook/${r.id}/`, order: cookbook + 1 + r.order / 1000 }));
  return [...pages, ...recipes].sort((a, b) => a.order - b.order);
}
