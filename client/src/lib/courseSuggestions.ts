const dayMs = 24 * 60 * 60 * 1000

/** Föreslår en kurskod som följer källkursens mönster: NET27 och .NET25_DEVOPS ger NET27_DEVOPS. */
export function suggestCourseCode(groupName: string, sourceCode: string): string {
  const prefix = groupName.trim().replace(/\s+/g, '_')
  const suffix = sourceCode.includes('_')
    ? sourceCode.slice(sourceCode.lastIndexOf('_') + 1)
    : sourceCode
  return `${prefix}_${suffix}`
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
