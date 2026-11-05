// Omnämningar (@) i kommentarer. I texten som sparas står en omnämning som @[Namn](id), så att servern vet vem
// som ska få notisen. I fältet där man skriver står bara @Namn, och id:na hålls vid sidan om tills texten sparas.

const tokenPattern = /@\[([^\]\r\n]{1,200})\]\(([0-9a-fA-F-]{36})\)/g

/** Namn och id för dem som nämns i fältet. */
export type MentionMap = Map<string, string>

export type MentionPart = {text: string} | {name: string, userId: string}

/** Den sparade texten uppdelad i vanlig text och omnämningar, för att visa kommentaren. */
export function mentionParts(stored: string): MentionPart[] {
  const parts: MentionPart[] = []
  let last = 0
  for (const match of stored.matchAll(tokenPattern)) {
    if (match.index > last) {
      parts.push({text: stored.slice(last, match.index)})
    }
    parts.push({name: match[1]!, userId: match[2]!})
    last = match.index + match[0].length
  }
  if (last < stored.length) {
    parts.push({text: stored.slice(last)})
  }
  return parts
}

/** Den sparade texten som den ska stå i fältet när den ändras, och vilka som nämns i den. */
export function toEditable(stored: string): {text: string, mentions: MentionMap} {
  const mentions: MentionMap = new Map()
  const text = stored.replace(tokenPattern, (_, name: string, userId: string) => {
    mentions.set(name, userId)
    return `@${name}`
  })
  return {text, mentions}
}

/**
 * Fältets text som den ska sparas. Ett @Namn som fortfarande står kvar som det valdes blir en omnämning. Har
 * någon skrivit om namnet är det bara text. Längre namn först, så att "Anna Berg" inte blir "Anna" + " Berg".
 */
export function toStored(text: string, mentions: MentionMap): string {
  const names = [...mentions.keys()].sort((left, right) => right.length - left.length)
  if (names.length === 0) {
    return text
  }

  const pattern = new RegExp(`@(${names.map(escapeRegExp).join('|')})(?![\\p{L}\\p{N}])`, 'gu')
  return text.replace(pattern, (_, name: string) => `@[${name}](${mentions.get(name)})`)
}

/**
 * Det som skrivits efter ett @ fram till markören, eller null om markören inte står i en omnämning. Ett @ räknas
 * bara i början av texten eller efter ett blanksteg, så att en e-postadress inte öppnar listan.
 */
export function mentionQueryAt(text: string, caret: number): {start: number, query: string} | null {
  const before = text.slice(0, caret)
  const match = /(^|\s)@([^\s@][^@\n]{0,40})?$/u.exec(before)
  if (!match) {
    return null
  }
  const query = match[2] ?? ''
  return {start: caret - query.length - 1, query}
}

/** Personerna vars namn matchar det som skrivits: förnamn eller efternamn börjar med det, eller hela namnet. */
export function matchPeople<T extends {name: string}>(people: T[], query: string): T[] {
  const needle = query.trim().toLocaleLowerCase('sv-SE')
  if (needle === '') {
    return people
  }
  return people.filter((person) => {
    const name = person.name.toLocaleLowerCase('sv-SE')
    return name.startsWith(needle) || name.split(/\s+/).some((word) => word.startsWith(needle))
  })
}

function escapeRegExp(text: string): string {
  return text.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')
}
