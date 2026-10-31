/** Första bokstaven i för- och efternamn, t.ex. "Anna Lärare" -> "AL". */
export function initials(name: string): string {
  return name
    .split(' ')
    .filter((part) => part !== '')
    .slice(0, 2)
    .map((part) => part[0].toUpperCase())
    .join('') || '?'
}
