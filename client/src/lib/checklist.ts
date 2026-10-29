// Uppgiftstexten kan innehålla en checklista för eleven: rader som `- [ ] Länk till en lyckad körning`.
// Raderna plockas ut ur texten och visas som egna bockrutor. Eleven bockar av, och servern sparar
// bockarna per punktens plats (0, 1, 2 ...) bland checklisteraderna.

export interface ChecklistItem {
  index: number
  text: string
}

const taskLine = /^ {0,3}[-*+]\s+\[[ xX]\]\s+(\S.*)$/
const fence = /^ {0,3}(```|~~~)/

/**
 * Delar texten i checklistepunkter och den övriga texten. Rader i kodblock räknas inte som punkter.
 * Punkternas ordning är stabil så länge läraren inte lägger till eller flyttar rader.
 */
export function parseChecklist(markdown: string): {items: ChecklistItem[], body: string} {
  const items: ChecklistItem[] = []
  const rest: string[] = []
  let inFence = false
  for (const line of markdown.split(/\r?\n/)) {
    if (fence.test(line)) {
      inFence = !inFence
    }
    const match = inFence
      ? null
      : taskLine.exec(line)
    if (match) {
      items.push({index: items.length, text: match[1].trim()})
    } else {
      rest.push(line)
    }
  }
  const body = rest.join('\n').replace(/\n{3,}/g, '\n\n').trim()
  return {items, body}
}
