/** Kursens läge utifrån datumen: pågår, kommande eller avslutad. Utan datum finns inget läge. */
export type CourseStatus = 'running' | 'upcoming' | 'finished'

export const courseStatusLabels: Record<CourseStatus, string> = {
  running: 'Pågår',
  upcoming: 'Kommande',
  finished: 'Avslutad',
}

/** Datumen är hela dagar (yyyy-mm-dd). En kurs pågår till och med slutdagen. */
export function courseStatus(
  course: {startsOn?: string | null, endsOn?: string | null},
  today: Date = new Date(),
): CourseStatus | null {
  const day = localDay(today)
  if (course.endsOn && course.endsOn < day) {
    return 'finished'
  }
  if (course.startsOn && course.startsOn > day) {
    return 'upcoming'
  }
  if (course.startsOn || course.endsOn) {
    return 'running'
  }
  return null
}

function localDay(date: Date): string {
  const month = String(date.getMonth() + 1).padStart(2, '0')
  const day = String(date.getDate()).padStart(2, '0')
  return `${date.getFullYear()}-${month}-${day}`
}

type Dated = {startsOn?: string | null, endsOn?: string | null}

/** Avslutad är en kurs vars slutdatum har passerat. Kurser utan datum räknas som pågående. */
export function isFinished(course: Dated, today: Date = new Date()): boolean {
  return courseStatus(course, today) === 'finished'
}

/** Delar upp i pågående (och kommande, och utan datum) och avslutade. Ordningen inom varje del behålls. */
export function splitFinished<T extends Dated>(
  courses: readonly T[],
  today: Date = new Date(),
): {current: T[], finished: T[]} {
  const current: T[] = []
  const finished: T[] = []
  for (const course of courses) {
    (isFinished(course, today)
      ? finished
      : current).push(course)
  }
  return {current, finished}
}

/**
 * En klass är avslutad när den har kurser och alla är avslutade. Servern räknar ut den sista slutdagen
 * (lastCourseEndsOn) och lämnar den tom för en klass utan kurser eller med en kurs utan slutdatum.
 */
export function isGroupFinished(group: {lastCourseEndsOn?: string | null}, today: Date = new Date()): boolean {
  return isFinished({endsOn: group.lastCourseEndsOn}, today)
}
