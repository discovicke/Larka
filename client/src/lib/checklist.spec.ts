import {describe, expect, it} from 'vitest'

import {parseChecklist} from './checklist'

describe('parseChecklist', () => {
  it('plockar ut punkterna i ordning och lämnar resten av texten', () => {
    const {items, body} = parseChecklist('Gör uppgiften.\n\n- [ ] Länk till körning\n- [x] Skärmbild\n\nLycka till!')

    expect(items).toEqual([
      {index: 0, text: 'Länk till körning'},
      {index: 1, text: 'Skärmbild'},
    ])
    expect(body).toBe('Gör uppgiften.\n\nLycka till!')
  })

  it('godtar andra punkttecken och CRLF', () => {
    const {items} = parseChecklist('* [ ] A\r\n+ [X] B')

    expect(items.map((item) => item.text)).toEqual(['A', 'B'])
  })

  it('ger inga punkter för text utan checklista och rör inte texten', () => {
    const markdown = '- vanlig punkt\n- [ ]\n[ ] inte en lista'
    const {items, body} = parseChecklist(markdown)

    expect(items).toEqual([])
    expect(body).toBe(markdown)
  })

  it('räknar inte rader i kodblock', () => {
    const {items, body} = parseChecklist('```md\n- [ ] exempel\n```\n- [ ] riktig')

    expect(items).toEqual([{index: 0, text: 'riktig'}])
    expect(body).toContain('- [ ] exempel')
  })

  it('hanterar tom text', () => {
    expect(parseChecklist('')).toEqual({items: [], body: ''})
  })
})
