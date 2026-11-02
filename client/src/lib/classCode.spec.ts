import {describe, expect, it} from 'vitest'

import {classCodeFromName, normalizeClassCode} from './classCode'

describe('normalizeClassCode', () => {
  it.each([
    ['.NET25', 'net25'],
    ['NET 25', 'net25'],
    ['TE22-A', 'te22a'],
  ])('ger %s koden %s', (name, expected) => {
    expect(normalizeClassCode(name)).toBe(expected)
  })
})

describe('classCodeFromName', () => {
  it.each([
    ['.NET25', 'net25'],
    ['NET 25', 'net25'],
    ['TE22A', 'te22a'],
    ['  FIB25 ', 'fib25'],
  ])('ger %s koden %s', (name, expected) => {
    expect(classCodeFromName(name)).toBe(expected)
  })

  it.each(['NET 25 DevOps', 'Elevråd', '2025', '', '...'])('ger %j ingen kod', (name) => {
    expect(classCodeFromName(name)).toBeNull()
  })
})
