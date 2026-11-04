// Samma gränser som backend (TextLimits.cs). Klienten säger till innan texten skickas, så att ingen skriver
// klart en lång text och först då får ett fel.

export const textLimits = {
  title: 200,
  contentBody: 100_000,
  comment: 5_000,
  submissionText: 50_000,
  feedback: 20_000,
  courseDescription: 20_000,
} as const

/** Räknaren syns först när texten närmar sig gränsen, så att den inte stör vanligt skrivande. */
const showFrom = 0.8

const number = new Intl.NumberFormat('sv-SE')

export function isTooLong(text: string | null | undefined, max: number): boolean {
  return (text?.length ?? 0) > max
}

/**
 * Texten under fältet: tom långt under gränsen, "4 100 av 5 000 tecken" nära den och ett fel över den.
 * Backend räknar tecken som JavaScript (UTF-16), så samma text ger samma svar på båda ställena.
 */
export function textLimitNote(text: string | null | undefined, max: number): {message: string, tooLong: boolean} {
  const length = text?.length ?? 0
  if (length > max) {
    return {
      message: `Texten är för lång: ${number.format(length)} tecken, högst ${number.format(max)}.`,
      tooLong: true,
    }
  }
  if (length >= max * showFrom) {
    return {message: `${number.format(length)} av ${number.format(max)} tecken`, tooLong: false}
  }
  return {message: '', tooLong: false}
}
