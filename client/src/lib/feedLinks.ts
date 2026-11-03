import type {Schema} from '@/lib/api'

// Delade steg för listor som byggs av flödenas länkar (Nyheter och Material).
export type FeedLink = Pick<Schema<'ContentFeedItem'>, 'linkId' | 'contentId' | 'courseId' | 'groupId'>

/**
 * Länkarna grupperade per innehåll, i den ordning innehållet först syns. En grupps flöde tar med kursernas
 * länkar, så samma länk kan komma både från kursen och från gruppen. Den räknas bara en gång.
 */
export function linksByContent<T extends FeedLink>(items: T[]): T[][] {
  const byContent = new Map<string, T[]>()
  const seen = new Set<string>()
  for (const item of items) {
    if (seen.has(item.linkId)) {
      continue
    }
    seen.add(item.linkId)
    const links = byContent.get(item.contentId)
    if (links) {
      links.push(item)
    } else {
      byContent.set(item.contentId, [item])
    }
  }
  return [...byContent.values()]
}

/** Var innehållet är publicerat: kursnamnen först, sedan gruppnamnen, utan dubbletter. */
export function linkTargets(links: FeedLink[], names: Map<string, string>): string[] {
  const found: string[] = []
  for (const key of ['courseId', 'groupId'] as const) {
    for (const link of links) {
      const id = link[key]
      const name = id
        ? names.get(id)
        : undefined
      if (name && !found.includes(name)) {
        found.push(name)
      }
    }
  }
  return found
}
