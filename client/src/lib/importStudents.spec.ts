import {describe, expect, it} from 'vitest'

import {parseStudentLines, previewLines} from '@/lib/importStudents'

describe('parseStudentLines', () => {
  it('läser namn och e-post åtskilda av komma, tabb eller semikolon', () => {
    const lines = parseStudentLines('Siv Student, siv@skola.se\nNils Ny\tnils@skola.se\nOlle Ovan;olle@skola.se')

    expect(lines.map((line) => [line.name, line.email, line.unreadable])).toEqual([
      ['Siv Student', 'siv@skola.se', false],
      ['Nils Ny', 'nils@skola.se', false],
      ['Olle Ovan', 'olle@skola.se', false],
    ])
  })

  it('läser e-post först, Namn <e-post> och citerade namn', () => {
    const lines = parseStudentLines('siv@skola.se, Siv Student\nNils Ny <NILS@skola.se>\n"Olle Ovan", olle@skola.se')

    expect(lines.map((line) => [line.name, line.email])).toEqual([
      ['Siv Student', 'siv@skola.se'],
      ['Nils Ny', 'nils@skola.se'],
      ['Olle Ovan', 'olle@skola.se'],
    ])
  })

  it('hoppar över tomma rader men behåller radnumret', () => {
    const lines = parseStudentLines('Siv, siv@skola.se\n\n  \nNils, nils@skola.se\r\n')

    expect(lines.map((line) => line.number)).toEqual([1, 4])
  })

  it('markerar rader utan namn eller giltig e-post som oläsbara', () => {
    const lines = parseStudentLines('bara ett namn\nsiv@skola.se\nSiv, siv@skola\nNamn, E-post')

    expect(lines.map((line) => line.unreadable)).toEqual([true, true, true, true])
  })
})

describe('previewLines', () => {
  const users = [
    {id: 'u1', email: 'siv@skola.se', role: 'Student' as const},
    {id: 'u2', email: 'lars@skola.se', role: 'Teacher' as const},
    {id: 'u3', email: 'nils@skola.se', role: 'Student' as const},
  ]

  it('sorterar rader i befintliga, nya och överhoppade', () => {
    const lines = parseStudentLines([
      'Nils, NILS@skola.se',
      'Olle, olle@skola.se',
      'Siv, siv@skola.se',
      'Lars, lars@skola.se',
      'Olle igen, olle@skola.se',
      'trasig',
    ].join('\n'))

    const preview = previewLines(lines, users, ['u1'])

    expect(preview.map((line) => [line.email, line.kind])).toEqual([
      ['nils@skola.se', 'existing'],
      ['olle@skola.se', 'new'],
      ['siv@skola.se', 'skipped'],
      ['lars@skola.se', 'skipped'],
      ['olle@skola.se', 'skipped'],
    ])
    expect(preview[2].note).toBe('Är redan med i klassen.')
  })
})
