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

/** Brödtexten delad i stycken på tomma rader, utan tomma stycken. */
export function paragraphs(bodyMd: string): string[] {
  return bodyMd
    .split(/\n\s*\n/)
    .map((paragraph) => paragraph.trim())
    .filter((paragraph) => paragraph !== '')
}
