// The latest stable release of UrDeck, read from the GitHub Releases API when the site is built (never in the visitor's
// browser). The download button itself does not use the answer: it links GitHub's latest-release address, which GitHub
// resolves on click. The answer only labels it (version, date, notes, portable zip).
export const repo = 'UrDeck/urdeck';
export const installerName = 'UrDeck-win-Setup.exe';
export const portableName = 'UrDeck-win-Portable.zip';
export const installerUrl = `https://github.com/${repo}/releases/latest/download/${installerName}`;
export const releasesUrl = `https://github.com/${repo}/releases`;

/** A release as the API returns it -> what the page shows. Throws when the installer is missing: the link would 404. */
export function describeRelease(json) {
  if (!json || json.draft || json.prerelease || typeof json.tag_name !== 'string') return null;
  const assets = Array.isArray(json.assets) ? json.assets : [];
  const named = (name) => assets.find((a) => a.name === name);
  if (!named(installerName)) throw new Error(`release ${json.tag_name} has no ${installerName}`);
  return {
    version: json.tag_name.replace(/^v/, ''),
    date: typeof json.published_at === 'string' ? json.published_at.slice(0, 10) : null,
    notesUrl: json.html_url ?? `${releasesUrl}/tag/${json.tag_name}`,
    portableUrl: named(portableName)?.browser_download_url ?? null,
  };
}

/** The latest stable release, or null when none can be read (no network, rate limit, no release yet). */
export async function latestRelease(fetchImpl = fetch, token = process.env.GITHUB_TOKEN) {
  let json;
  try {
    const headers = { accept: 'application/vnd.github+json', 'user-agent': 'urdeck-site-build' };
    if (token) headers.authorization = `Bearer ${token}`;
    const response = await fetchImpl(`https://api.github.com/repos/${repo}/releases/latest`, { headers });
    if (!response.ok) {
      console.warn(`release lookup: GitHub answered ${response.status}; the page shows no version`);
      return null;
    }
    json = await response.json();
  } catch (error) {
    console.warn(`release lookup failed (${error.message}); the page shows no version`);
    return null;
  }
  return describeRelease(json);
}
