import {describe, expect, it} from 'vitest'

import {isTooLong, textLimitNote} from './textLimits'

// Intl skriver tusentalen med ett hårt mellanslag.
const spaced = (text: string) => text.replace(/\s/g, ' ')

describe('textLimitNote', () => {
  it('säger ingenting långt under gränsen', () => {
    expect(textLimitNote('Hej', 5000)).toEqual({message: '', tooLong: false})
    expect(textLimitNote(null, 5000)).toEqual({message: '', tooLong: false})
  })

  it('visar räknaren nära gränsen', () => {
    const note = textLimitNote('a'.repeat(4100), 5000)
    expect(spaced(note.message)).toBe('4 100 av 5 000 tecken')
    expect(note.tooLong).toBe(false)
  })

  it('räknar exakt gränsen som tillåten', () => {
    expect(textLimitNote('a'.repeat(5000), 5000).tooLong).toBe(false)
    expect(isTooLong('a'.repeat(5000), 5000)).toBe(false)
  })

  it('säger till när texten är för lång', () => {
    const note = textLimitNote('a'.repeat(5001), 5000)
    expect(spaced(note.message)).toBe('Texten är för lång: 5 001 tecken, högst 5 000.')
    expect(note.tooLong).toBe(true)
    expect(isTooLong('a'.repeat(5001), 5000)).toBe(true)
  })
})
