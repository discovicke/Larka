import type {Schema} from '@/lib/api'
import {linksByContent, linkTargets} from '@/lib/feedLinks'

/** Fästa nyheter först, sedan nyast publicerad först. Returnerar en ny lista. */
export function sortNews<T extends {isPinned: boolean, publishAt: string}>(posts: T[]): T[] {
  return [...posts].sort((left, right) => {
    if (left.isPinned !== right.isPinned) {
      return left.isPinned
        ? -1
        : 1
    }
    return Date.parse(right.publishAt) - Date.parse(left.publishAt)
  })
}

/** Det som behövs från ett flödesobjekt för att slå ihop nyheter. */
export type NewsLink = Pick<Schema<'ContentFeedItem'>, 'linkId' | 'contentId' | 'courseId' | 'groupId' | 'publishAt' | 'isPinned'>

export interface NewsEntry<T extends NewsLink> {
  post: T
  links: T[]
  targets: string[]
}

/**
 * En post per innehåll, även om den är publicerad i flera kurser och grupper.
 * `names` översätter kurs- och grupp-id till namn. Kursnamnen kommer först bland målen.
 */
export function mergeNews<T extends NewsLink>(items: T[], names: Map<string, string>): NewsEntry<T>[] {
  const entries = linksByContent(items).map((contentLinks): NewsEntry<T> => {
    const links = sortNews(contentLinks)
    return {
      // Fäst om någon av länkarna är fäst, så att listan kan sortera på posten.
      post: {...links[0], isPinned: contentLinks.some((link) => link.isPinned)},
      links,
      targets: linkTargets(contentLinks, names),
    }
  })

  return sortNews(entries.map((entry) => ({entry, isPinned: entry.post.isPinned, publishAt: entry.post.publishAt})))
    .map((item) => item.entry)
}

/** Dagens rubrik i inkorgen, räknat i lokala kalenderdagar. */
export function dayGroup(iso: string, now: Date): 'Idag' | 'Igår' | 'Tidigare' {
  const startOfDay = (date: Date) => new Date(date.getFullYear(), date.getMonth(), date.getDate()).getTime()
  // Avrundat dygnsavstånd klarar dygnen kring sommartid, som är 23 och 25 timmar.
  const days = Math.round((startOfDay(now) - startOfDay(new Date(iso))) / 86_400_000)
  if (days === 0) {
    return 'Idag'
  }
  if (days === 1) {
    return 'Igår'
  }
  return 'Tidigare'
}
