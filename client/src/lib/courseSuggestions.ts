const dayMs = 24 * 60 * 60 * 1000

/** Terminen ett datum (yyyy-mm-dd) ligger i: HT26 för hösten 2026, VT27 för våren 2027. */
export function termCode(date: string): string {
  const year = date.slice(2, 4)
  const month = Number(date.slice(5, 7))
  return month >= 7
    ? `HT${year}`
    : `VT${year}`
}

// En termin eller ett löpnummer som ett tidigare förslag har lagt till: _HT26, _VT27 eller _2.
const addedSuffix = /_(?:[HV]T\d{2}|\d+)$/i

/**
 * Föreslår en kurskod som följer källkursens mönster: .NET27 och .NET25_DevOps ger .NET27_DevOps. Finns koden redan
 * i skolan, till exempel när kursen går igen i samma klass, läggs terminen från startdatumet till (TE24A_Webb1_HT26),
 * och finns även den, eller saknas startdatum, ett löpnummer (TE24A_Webb1_2).
 */
export function suggestCourseCode(
  groupName: string,
  sourceCode: string,
  takenCodes: readonly string[] = [],
  startsOn = '',
): string {
  const prefix = groupName.trim().replace(/\s+/g, '_')
  // Tillägget tas bara bort när koden ändå har klass och kursdel kvar, så att DEVOPS_2 inte blir DEVOPS.
  const stripped = sourceCode.replace(addedSuffix, '')
  const sourceBase = stripped.includes('_')
    ? stripped
    : sourceCode
  const suffix = sourceBase.includes('_')
    ? sourceBase.slice(sourceBase.lastIndexOf('_') + 1)
    : sourceBase
  const base = `${prefix}_${suffix}`
  const taken = new Set(takenCodes.map((code) => code.toLocaleLowerCase('sv')))
  const isFree = (code: string) => !taken.has(code.toLocaleLowerCase('sv'))
  if (isFree(base)) {
    return base
  }
  const withTerm = startsOn
    ? `${base}_${termCode(startsOn)}`
    : base
  let code = withTerm
  for (let number = 2; !isFree(code); number += 1) {
    code = `${withTerm}_${number}`
  }
  return code
}

const smallWords = new Set(['i', 'med', 'och', 'för', 'av', 'på', 'till', 'en', 'ett'])

/** Bokstäver och siffror utan accenter: Nätbygge blir Natbygge, C# blir C och .NET blir NET. */
function plain(word: string): string {
  return word.normalize('NFD').replace(/[^A-Za-z0-9]/g, '')
}

function capitalize(text: string): string {
  return text.charAt(0).toUpperCase() + text.slice(1)
}

/**
 * Föreslår en kort förkortning av kursens namn i Edugrades stil. Små ord som "med" och "och" räknas inte.
 * Ett ord blir ordet självt om det har högst sex tecken (DevOps), annars de fyra första (Databaser blir Data).
 * Två ord blir två bokstäver från varje (Designmönster & arkitektur blir DeAr). Tre ord eller fler blir
 * begynnelsebokstäverna, högst fyra (Objektorienterad Programmering med C# & .NET blir OPCN).
 */
export function abbreviateCourseTitle(title: string): string {
  const words = title
    .split(/[\s,&+/]+/)
    .filter((word) => !smallWords.has(word.toLocaleLowerCase('sv')))
    .map(plain)
    .filter((word) => word !== '')
  if (words.length === 0) {
    return ''
  }
  if (words.length === 1) {
    const word = words[0]!
    return word.length <= 6
      ? word
      : capitalize(word.slice(0, 4))
  }
  if (words.length === 2) {
    return words.map((word) => capitalize(word.slice(0, 2).toLowerCase())).join('')
  }
  return words.slice(0, 4).map((word) => word.charAt(0).toUpperCase()).join('')
}

/**
 * Föreslår en kurskod för en ny tom kurs: klassens kod, understreck och förkortningen av namnet (.NET30 och
 * Databaser ger .NET30_Data). Finns koden redan i skolan läggs en siffra till (.NET30_Data2).
 */
export function suggestNewCourseCode(groupName: string, title: string, takenCodes: readonly string[]): string {
  const abbreviation = abbreviateCourseTitle(title)
  if (!abbreviation) {
    return ''
  }
  const base = `${groupName.trim().replace(/\s+/g, '_')}_${abbreviation}`
  const taken = new Set(takenCodes.map((code) => code.toLocaleLowerCase('sv')))
  let code = base
  for (let number = 2; taken.has(code.toLocaleLowerCase('sv')); number += 1) {
    code = `${base}${number}`
  }
  return code
}

/** Föreslår en egen titel för kopian så att två kurser inte heter lika: DevOps för NET27 ger "DevOps – NET27". */
export function suggestCourseTitle(groupName: string, sourceTitle: string): string {
  const group = groupName.trim()
  return !group || sourceTitle.toLocaleLowerCase('sv').includes(group.toLocaleLowerCase('sv'))
    ? sourceTitle
    : `${sourceTitle} – ${group}`
}

/** Antal dagar mellan två datum på formen yyyy-mm-dd, eller null om något saknas. */
export function courseLengthDays(startsOn: string | null | undefined, endsOn: string | null | undefined): number | null {
  if (!startsOn || !endsOn) {
    return null
  }
  return Math.round((Date.parse(endsOn) - Date.parse(startsOn)) / dayMs)
}

/** Datumet ett antal dagar efter date, på formen yyyy-mm-dd. */
export function addDays(date: string, days: number): string {
  return new Date(Date.parse(date) + days * dayMs).toISOString().slice(0, 10)
}
