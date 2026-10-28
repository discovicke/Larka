// Rena hjälpare för kalendern. Alla datum är lokala och veckan börjar på måndag.

const months = [
  'januari', 'februari', 'mars', 'april', 'maj', 'juni',
  'juli', 'augusti', 'september', 'oktober', 'november', 'december',
]

export const weekdayNames = ['Mån', 'Tis', 'Ons', 'Tor', 'Fre', 'Lör', 'Sön']

export function addDays(date: Date, days: number): Date {
  return new Date(date.getFullYear(), date.getMonth(), date.getDate() + days)
}

/** Måndagen i veckan som datumet ligger i, kl. 00:00 lokal tid. */
export function startOfWeek(date: Date): Date {
  const sinceMonday = (date.getDay() + 6) % 7
  return addDays(date, -sinceMonday)
}

/** Svenskt (ISO) veckonummer: vecka 1 är veckan med årets första torsdag. */
export function weekNumber(date: Date): number {
  const monday = startOfWeek(date)
  const thursday = addDays(monday, 3)
  const firstDay = new Date(thursday.getFullYear(), 0, 1)
  const dayOfYear = Math.round((thursday.getTime() - firstDay.getTime()) / 86_400_000)
  return Math.floor(dayOfYear / 7) + 1
}

export function weekDays(start: Date): Date[] {
  return Array.from({length: 7}, (_, index) => addDays(start, index))
}

/** Månadsrutnätet: 6 veckor om 7 dagar, med måndag först. */
export function monthGrid(date: Date): Date[] {
  const first = new Date(date.getFullYear(), date.getMonth(), 1)
  const start = startOfWeek(first)
  return Array.from({length: 42}, (_, index) => addDays(start, index))
}

/** Datumet som nyckel i lokal tid: 2026-10-19. */
export function dateKey(date: Date): string {
  const month = String(date.getMonth() + 1).padStart(2, '0')
  const day = String(date.getDate()).padStart(2, '0')
  return `${date.getFullYear()}-${month}-${day}`
}

export function isSameDay(left: Date, right: Date): boolean {
  return dateKey(left) === dateKey(right)
}

/** Delar in raderna per lokalt datum, i tidsordning. */
export function groupByDate<Item extends {at: string}>(items: Item[]): Map<string, Item[]> {
  const groups = new Map<string, Item[]>()
  const sorted = [...items].sort((left, right) => new Date(left.at).getTime() - new Date(right.at).getTime())
  for (const item of sorted) {
    const key = dateKey(new Date(item.at))
    groups.set(key, [...(groups.get(key) ?? []), item])
  }
  return groups
}

/** "19–25 oktober 2026", "26 oktober – 1 november 2026" eller över årsskiftet. */
export function weekHeading(start: Date): string {
  const end = addDays(start, 6)
  const endText = `${end.getDate()} ${months[end.getMonth()]} ${end.getFullYear()}`
  if (start.getFullYear() !== end.getFullYear()) {
    return `${start.getDate()} ${months[start.getMonth()]} ${start.getFullYear()} – ${endText}`
  }
  if (start.getMonth() !== end.getMonth()) {
    return `${start.getDate()} ${months[start.getMonth()]} – ${endText}`
  }
  return `${start.getDate()}–${endText}`
}

/** "oktober 2026" */
export function monthHeading(date: Date): string {
  return `${months[date.getMonth()]} ${date.getFullYear()}`
}

/** "vecka 43" */
export function weekLabel(date: Date): string {
  return `vecka ${weekNumber(date)}`
}

/** Klockslaget, "23:59". */
export function timeText(iso: string): string {
  return new Date(iso).toLocaleTimeString('sv-SE', {hour: '2-digit', minute: '2-digit'})
}
