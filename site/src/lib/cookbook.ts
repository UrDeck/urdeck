// The cookbook recipes: site/cookbook/<recipe>/recipe.json (title, summary, order, what to show, background), theme.json (shown
// as it is in the repository, imported raw) and README.md (the explanation, the `cookbook` collection). The picture is made from
// the folder by the media pipeline and found in the media manifest as `cookbook:<recipe>`.
import { getCollection } from 'astro:content';
import { cookbookMedia } from './media';

export type Recipe = {
  id: string;
  title: string;
  summary: string;
  order: number;
  show: 'page' | 'dock' | { widget: string; size: string };
  background: string | null;
  theme: string;
  image: ReturnType<typeof cookbookMedia>;
};

const recipes = import.meta.glob<{ default: Omit<Recipe, 'id' | 'theme' | 'image'> }>('/cookbook/*/recipe.json', { eager: true });
const themes = import.meta.glob<string>('/cookbook/*/theme.json', { eager: true, query: '?raw', import: 'default' });

const folder = (file: string) => file.split('/')[2];

export async function getRecipes(): Promise<Recipe[]> {
  const entries = await getCollection('cookbook');
  return Object.entries(recipes)
    .map(([file, module]) => {
      const id = folder(file);
      const theme = themes[`/cookbook/${id}/theme.json`];
      if (theme === undefined) throw new Error(`cookbook/${id}/ has a recipe.json and no theme.json`);
      if (!entries.some((e) => e.id === id)) throw new Error(`cookbook/${id}/ has no README.md, the explanation the page shows`);
      return { ...module.default, id, theme, image: cookbookMedia(id) };
    })
    .sort((a, b) => a.order - b.order || a.id.localeCompare(b.id));
}
