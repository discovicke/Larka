const monthNames = ['jan', 'feb', 'mar', 'apr', 'maj', 'jun', 'jul', 'aug', 'sep', 'okt', 'nov', 'dec']

function shortDate(value: string): string {
  const date = new Date(`${value}T00:00:00`)
  return `${date.getDate()} ${monthNames[date.getMonth()]}`
}

/** Kursens datum, t.ex. "12 okt – 13 nov 2026" eller "Från 16 nov 2026". Tomt utan datum. */
export function periodLabel(startsOn: string | null, endsOn: string | null): string {
  if (startsOn && endsOn) {
    const sameYear = startsOn.slice(0, 4) === endsOn.slice(0, 4)
    return sameYear
      ? `${shortDate(startsOn)} – ${shortDate(endsOn)} ${endsOn.slice(0, 4)}`
      : `${shortDate(startsOn)} ${startsOn.slice(0, 4)} – ${shortDate(endsOn)} ${endsOn.slice(0, 4)}`
  }
  if (startsOn) {
    return `Från ${shortDate(startsOn)} ${startsOn.slice(0, 4)}`
  }
  if (endsOn) {
    return `Till ${shortDate(endsOn)} ${endsOn.slice(0, 4)}`
  }
  return ''
}
