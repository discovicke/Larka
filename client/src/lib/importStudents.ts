import type {Schema} from '@/lib/api'

export type ImportOutcome = Schema<'ImportStudentOutcome'>

/** En rad i den inklistrade listan. Raden går att läsa när både namn och e-post finns. */
export interface PastedLine {
  /** Radens nummer i texten, från 1. */
  number: number
  raw: string
  name: string
  email: string
  /** Sant när raden saknar en giltig e-postadress eller ett namn. */
  unreadable: boolean
}

const emailPattern = /^[^\s@]+@[^\s@]+\.[^\s@]+$/

/**
 * Läser en inklistrad lista med en person per rad: namn och e-post åtskilda av tabb, komma eller semikolon, i
 * valfri ordning, eller "Namn <e-post>". Tomma rader hoppas över.
 */
export function parseStudentLines(text: string): PastedLine[] {
  const lines: PastedLine[] = []
  text.split(/\r?\n/).forEach((raw, index) => {
    if (raw.trim() === '') {
      return
    }
    const {name, email} = splitLine(raw)
    lines.push({
      number: index + 1,
      raw: raw.trim(),
      name,
      email,
      unreadable: name === '' || !emailPattern.test(email),
    })
  })
  return lines
}

function splitLine(raw: string): {name: string, email: string} {
  const angled = /^(.*?)<\s*([^<>]+?)\s*>\s*$/.exec(raw)
  if (angled) {
    return {name: cleanName(angled[1]), email: angled[2].trim().toLowerCase()}
  }

  const separator = raw.includes('\t')
    ? '\t'
    : raw.includes(';')
      ? ';'
      : ','
  const fields = raw.split(separator).map((field) => field.trim()).filter((field) => field !== '')
  const email = fields.find((field) => field.includes('@')) ?? ''
  const name = fields.filter((field) => field !== email).join(' ')
  return {name: cleanName(name), email: email.toLowerCase()}
}

function cleanName(name: string): string {
  return name.trim().replace(/^"(.*)"$/, '$1').trim()
}

/** Vad som händer med en läsbar rad när listan läggs till, innan något har skickats. */
export type PreviewKind = 'existing' | 'new' | 'skipped'

export interface PreviewLine extends PastedLine {
  kind: PreviewKind
  /** Varför raden hoppas över. */
  note: string
}

/**
 * Sorterar de läsbara raderna mot skolans användare: befintliga elever, nya och sådana som hoppas över
 * (redan med i klassen, lärare eller admin, eller samma adress två gånger).
 */
export function previewLines(
  lines: PastedLine[],
  users: Pick<Schema<'UserDto'>, 'id' | 'email' | 'role'>[],
  memberIds: string[],
): PreviewLine[] {
  const byEmail = new Map(users.map((user) => [user.email.toLowerCase(), user]))
  const seen = new Set<string>()
  return lines
    .filter((line) => !line.unreadable)
    .map((line): PreviewLine => {
      if (seen.has(line.email)) {
        return {...line, kind: 'skipped', note: 'Adressen står redan en gång i listan.'}
      }
      seen.add(line.email)
      const user = byEmail.get(line.email)
      if (!user) {
        return {...line, kind: 'new', note: ''}
      }
      if (user.role !== 'Student') {
        return {...line, kind: 'skipped', note: 'Är lärare eller admin. Lägg till hen som lärare i stället.'}
      }
      return memberIds.includes(user.id)
        ? {...line, kind: 'skipped', note: 'Är redan med i klassen.'}
        : {...line, kind: 'existing', note: ''}
    })
}

export const importOutcomeLabels: Record<ImportOutcome, string> = {
  Created: 'Ny elev skapad och tillagd',
  Added: 'Tillagd',
  AlreadyMember: 'Var redan med i klassen',
  NotStudent: 'Är lärare eller admin, inte tillagd',
  Duplicate: 'Adressen stod redan en gång i listan',
  Invalid: 'Gick inte att läsa, eller adressen hör till en annan skola',
}

/** Rader som lades till i klassen. */
export function wasAdded(outcome: ImportOutcome): boolean {
  return outcome === 'Created' || outcome === 'Added'
}
