import {CalendarDate, parseDate} from '@internationalized/date'

const dateFormat = new Intl.DateTimeFormat('sv-SE', {day: 'numeric', month: 'short', year: 'numeric'})

/** 'yyyy-mm-dd' -> CalendarDate, undefined om tomt eller ogiltigt. */
export function toCalendarDate(value: string | undefined | null): CalendarDate | undefined {
  if (!value) return undefined
  try {
    return parseDate(value.slice(0, 10))
  } catch {
    return undefined
  }
}

/** CalendarDate -> 'yyyy-mm-dd'. */
export function fromCalendarDate(date: {year: number, month: number, day: number}): string {
  const pad = (num: number, len = 2) => String(num).padStart(len, '0')
  return `${pad(date.year, 4)}-${pad(date.month)}-${pad(date.day)}`
}

/** Svensk visning, t.ex. '11 okt 2027'. */
export function formatCalendarDate(value: string | undefined | null): string {
  const date = toCalendarDate(value)
  if (!date) return ''
  return dateFormat.format(new Date(date.year, date.month - 1, date.day)).replace(/\./g, '')
}

/** Delar 'yyyy-mm-ddThh:mm' i datum och tid. */
export function splitDateTime(value: string | undefined | null): {date: string, time: string} {
  if (!value) return {date: '', time: ''}
  const [date = '', time = ''] = value.split('T')
  return {date, time: time.slice(0, 5)}
}

/** Svensk visning, t.ex. '11 okt 2027 09:00'. */
export function formatDateTime(value: string | undefined | null): string {
  const {date, time} = splitDateTime(value)
  const text = formatCalendarDate(date)
  return text && time
    ? `${text} ${time}`
    : text
}
