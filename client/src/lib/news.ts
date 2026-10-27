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

/** Första bokstaven i för- och efternamn, t.ex. "Anna Lärare" -> "AL". */
export function initials(name: string): string {
  return name
    .split(' ')
    .filter((part) => part !== '')
    .slice(0, 2)
    .map((part) => part[0].toUpperCase())
    .join('') || '?'
}

/** Det som behövs från ett flödesobjekt för att slå ihop nyheter. */
export interface NewsLink {
  linkId: string
  contentId: string
  courseId: string | null
  groupId: string | null
  publishAt: string
  isPinned: boolean
}

export interface NewsEntry<T extends NewsLink> {
  post: T
  links: T[]
  targets: string[]
}

function targetNames<T extends NewsLink>(links: T[], key: 'courseId' | 'groupId', names: Map<string, string>): string[] {
  const found: string[] = []
  for (const link of links) {
    const id = link[key]
    const name = id
      ? names.get(id)
      : undefined
    if (name && !found.includes(name)) {
      found.push(name)
    }
  }
  return found
}

/**
 * En post per innehåll, även om den är publicerad i flera kurser och grupper.
 * `names` översätter kurs- och grupp-id till namn. Kursnamnen kommer först bland målen.
 */
export function mergeNews<T extends NewsLink>(items: T[], names: Map<string, string>): NewsEntry<T>[] {
  const byContent = new Map<string, T[]>()
  // En grupps flöde tar med kursernas länkar, så samma länk kan komma från både kursen och gruppen.
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

  const entries = [...byContent.values()].map((contentLinks): NewsEntry<T> => {
    const links = sortNews(contentLinks)
    return {
      // Fäst om någon av länkarna är fäst, så att listan kan sortera på posten.
      post: {...links[0], isPinned: contentLinks.some((link) => link.isPinned)},
      links,
      targets: [
        ...targetNames(contentLinks, 'courseId', names),
        ...targetNames(contentLinks, 'groupId', names),
      ],
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
