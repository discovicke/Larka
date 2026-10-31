import {describe, expect, it} from 'vitest'

import {initials} from './person'

describe('initials', () => {
  it('tar första bokstaven i för- och efternamn', () => {
    expect(initials('Anna Lärare')).toBe('AL')
  })

  it('klarar ett namn och extra mellanslag', () => {
    expect(initials('Anna')).toBe('A')
    expect(initials(' Anna  Lärare ')).toBe('AL')
  })

  it('visar frågetecken när namnet saknas', () => {
    expect(initials('')).toBe('?')
  })
})
