import {describe, expect, it} from 'vitest'

import {matchPeople, mentionParts, mentionQueryAt, toEditable, toStored} from '@/lib/mentions'

const anna = '00000000-0000-0000-0000-00000000000a'
const annaBerg = '00000000-0000-0000-0000-00000000000b'

describe('mentions', () => {
  it('delar upp sparad text i text och omnämningar', () => {
    expect(mentionParts(`Hej @[Anna](${anna}), kul!`)).toEqual([
      {text: 'Hej '},
      {name: 'Anna', userId: anna},
      {text: ', kul!'},
    ])
    expect(mentionParts('Bara text')).toEqual([{text: 'Bara text'}])
  })

  it('gör om sparad text till fältets text och tillbaka', () => {
    const stored = `@[Anna Berg](${annaBerg}) och @[Anna](${anna})`
    const editable = toEditable(stored)

    expect(editable.text).toBe('@Anna Berg och @Anna')
    expect(toStored(editable.text, editable.mentions)).toBe(stored)
  })

  it('ett namn som skrivits om eller fortsätter med bokstäver blir inte en omnämning', () => {
    const mentions = new Map([['Anna', anna]])

    expect(toStored('@Annika och @Ann', mentions)).toBe('@Annika och @Ann')
    expect(toStored('@Anna!', mentions)).toBe(`@[Anna](${anna})!`)
  })

  it('hittar det som skrivits efter @ fram till markören', () => {
    expect(mentionQueryAt('Hej @an', 7)).toEqual({start: 4, query: 'an'})
    expect(mentionQueryAt('@', 1)).toEqual({start: 0, query: ''})
    expect(mentionQueryAt('Hej @Anna B', 11)).toEqual({start: 4, query: 'Anna B'})
    expect(mentionQueryAt('bo@skolan.se', 12)).toBeNull()
    expect(mentionQueryAt('Hej @an\nnästa', 13)).toBeNull()
  })

  it('matchar förnamn, efternamn och början av hela namnet', () => {
    const people = [{name: 'Anna Berg'}, {name: 'Bo Ek'}, {name: 'Åsa Öberg'}]

    expect(matchPeople(people, '')).toHaveLength(3)
    expect(matchPeople(people, 'berg')).toEqual([{name: 'Anna Berg'}])
    expect(matchPeople(people, 'öb')).toEqual([{name: 'Åsa Öberg'}])
    expect(matchPeople(people, 'anna b')).toEqual([{name: 'Anna Berg'}])
    expect(matchPeople(people, 'erg')).toEqual([])
  })
})
