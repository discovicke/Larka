const dayMs = 24 * 60 * 60 * 1000
const months = ['jan', 'feb', 'mar', 'apr', 'maj', 'jun', 'jul', 'aug', 'sep', 'okt', 'nov', 'dec']

export interface TimelineProgress {
  percent: number
  daysLeft: number | null
  nearEnd: boolean
  hasDates: boolean
}

function parseDateOnly(value: string): Date | null {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(value)) {
    return null
  }
  const [year, month, day] = value.split('-').map(Number)
  return new Date(year, month - 1, day)
}

function atMidnight(value: Date): Date {
  return new Date(value.getFullYear(), value.getMonth(), value.getDate())
}

/** Tidslinjen är tidsbaserad: hur stor del av kursperioden som passerat. */
export function timelineProgress(
  startsOn: string | null,
  endsOn: string | null,
  today = new Date(),
): TimelineProgress {
  const start = startsOn
    ? parseDateOnly(startsOn)
    : null
  const end = endsOn
    ? parseDateOnly(endsOn)
    : null
  if (!start || !end) {
    return {percent: 0, daysLeft: null, nearEnd: false, hasDates: false}
  }

  const now = atMidnight(today)
  const total = end.getTime() - start.getTime()
  if (total <= 0) {
    return {percent: 100, daysLeft: 0, nearEnd: false, hasDates: true}
  }

  const elapsed = Math.min(Math.max(now.getTime() - start.getTime(), 0), total)
  const daysLeft = Math.max(0, Math.ceil((end.getTime() - now.getTime()) / dayMs))
  return {
    percent: Math.round((elapsed / total) * 100),
    daysLeft,
    nearEnd: daysLeft > 0 && daysLeft <= 21,
    hasDates: true,
  }
}

/** Datum med år i tidslinjen, t.ex. "12 jan 2026". */
export function timelineDate(value: string | null): string {
  if (!value) {
    return 'Datum saknas'
  }
  const date = parseDateOnly(value)
  if (!date) {
    return value
  }
  return `${date.getDate()} ${months[date.getMonth()]} ${date.getFullYear()}`
}
