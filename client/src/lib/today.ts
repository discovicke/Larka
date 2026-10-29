import {addDays, startOfWeek, weekNumber} from '@/lib/calendar'
import {formatDate} from '@/lib/content'

// Rena hjälpare för Idag-sidan. Alla tider är lokala.

const hour = 3_600_000
const urgentWithin = 48 * hour

/** "God morgon", "God eftermiddag" eller "God kväll" efter klockslaget. */
export function greeting(now: Date): string {
  const hours = now.getHours()
  if (hours >= 5 && hours < 12) {
    return 'God morgon'
  }
  return hours >= 12 && hours < 18
    ? 'God eftermiddag'
    : 'God kväll'
}

/** "Onsdag 21 oktober · vecka 43" */
export function dateLine(now: Date): string {
  const text = now.toLocaleDateString('sv-SE', {weekday: 'long', day: 'numeric', month: 'long'})
  return `${text.charAt(0).toUpperCase()}${text.slice(1)} · vecka ${weekNumber(now)}`
}

/** Förnamnet i ett fullständigt namn, eller tomt när namnet saknas. */
export function firstName(name: string | undefined): string {
  return name?.trim().split(/\s+/)[0] ?? ''
}

/** "3 saker förfaller den här veckan" */
export function dueThisWeekText(count: number): string {
  if (count === 0) {
    return 'Inget förfaller den här veckan'
  }
  return `${count} ${count === 1
    ? 'sak förfaller'
    : 'saker förfaller'} den här veckan`
}

/** Delar uppgifterna i den här veckan (måndag–söndag) och senare. Ordningen behålls. */
export function splitByWeek<Item extends {dueAt: string}>(items: Item[], now: Date) {
  const weekEnd = addDays(startOfWeek(now), 7).getTime()
  return {
    thisWeek: items.filter((item) => new Date(item.dueAt).getTime() < weekEnd),
    later: items.filter((item) => new Date(item.dueAt).getTime() >= weekEnd),
  }
}

/** Deadlinen har passerat. */
export function isOverdue(iso: string, now: Date): boolean {
  return new Date(iso).getTime() < now.getTime()
}

/** Deadlinen ligger framför oss men inom två dygn. */
export function isDueSoon(iso: string, now: Date): boolean {
  const diff = new Date(iso).getTime() - now.getTime()
  return diff >= 0 && diff <= urgentWithin
}

/** Klassen för en deadline: röd när den har passerat, gul inom två dygn, annars `fallback`. */
export function dueTextClass(iso: string, now: Date, fallback = ''): string {
  if (isOverdue(iso, now)) {
    return 'text-destructive'
  }
  return isDueSoon(iso, now)
    ? 'text-warning'
    : fallback
}

/** Delar uppgifterna i försenade, den här veckan och senare. Ordningen behålls. */
export function groupByDue<Item extends {dueAt: string}>(items: Item[], now: Date) {
  const overdue = items.filter((item) => isOverdue(item.dueAt, now))
  return {
    overdue,
    ...splitByWeek(items.filter((item) => !isOverdue(item.dueAt, now)), now),
  }
}

/** "om 3 timmar", "imorgon", "om 2 dagar", "om 3 veckor". */
export function relativeText(iso: string, now: Date): string {
  const due = new Date(iso)
  const diff = due.getTime() - now.getTime()
  if (diff < 0) {
    return 'passerad'
  }
  if (diff < hour) {
    return `om ${Math.max(1, Math.round(diff / 60_000))} min`
  }
  const dayDiff = Math.round(
    (new Date(due.getFullYear(), due.getMonth(), due.getDate()).getTime()
      - new Date(now.getFullYear(), now.getMonth(), now.getDate()).getTime()) / (24 * hour),
  )
  if (dayDiff === 0) {
    const hours = Math.round(diff / hour)
    return `om ${hours} ${hours === 1
      ? 'timme'
      : 'timmar'}`
  }
  if (dayDiff === 1) {
    return 'imorgon'
  }
  if (dayDiff < 14) {
    return `om ${dayDiff} dagar`
  }
  return `om ${Math.floor(dayDiff / 7)} veckor`
}

/** "lör 31 okt 23:59" */
export function weekdayAndTime(iso: string): string {
  return formatDate(iso, true)
}

/** "Fre 23 okt 23:59", med versal på veckodagen. */
export function deadlineText(iso: string): string {
  const text = formatDate(iso, true)
  return `${text.charAt(0).toUpperCase()}${text.slice(1)}`
}

/** "fre 23 okt" */
export function shortDate(iso: string): string {
  return formatDate(iso)
}

/** Sekundärraden: kursens namn och datumet med tid, t.ex. "DevOps · sön 25 okt 23:59". */
export function metaLine(courseTitle: string | null | undefined, iso: string | null | undefined): string {
  return [courseTitle, iso
    ? formatDate(iso, true)
    : null].filter(Boolean).join(' · ')
}

/** Andelen klara delar, 0–100. */
export function progressPercent(completed: number, total: number): number {
  return total === 0
    ? 0
    : Math.round((completed / total) * 100)
}
