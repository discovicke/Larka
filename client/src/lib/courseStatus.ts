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
