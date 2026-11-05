import {flushPromises} from '@vue/test-utils'

// Väljarna är shadcn/reka Select: en knapp (role="combobox") och en lista i en portal
// (role="listbox"). Hjälparna här styr dem som en användare gör, via tangentbordet.

function triggerOf(trigger: Element | string): HTMLElement {
  const element = typeof trigger === 'string'
    ? document.querySelector(trigger)
    : trigger
  if (!element) throw new Error(`Hittar inte väljaren ${String(trigger)}`)
  return element as HTMLElement
}

/** Öppnar väljaren och returnerar listan. */
export async function openSelect(trigger: Element | string): Promise<HTMLElement> {
  const button = triggerOf(trigger)
  button.focus()
  button.dispatchEvent(new KeyboardEvent('keydown', {key: 'Enter', bubbles: true}))
  await flushPromises()
  const listbox = document.querySelector('[role="listbox"]')
  if (!listbox) throw new Error('Väljarens lista öppnades inte')
  return listbox as HTMLElement
}

/** Texterna på alternativen i en öppnad lista, i ordning. */
export function optionTexts(root: ParentNode = document): string[] {
  return [...root.querySelectorAll('[role="option"]')].map((option) => option.textContent?.trim() ?? '')
}

/** Stänger en öppen lista utan att välja. */
export async function closeSelect() {
  const listbox = document.querySelector('[role="listbox"]')
  listbox?.dispatchEvent(new KeyboardEvent('keydown', {key: 'Escape', bubbles: true}))
  await flushPromises()
}

/** Öppnar väljaren och listar alternativen, och stänger den igen. */
export async function selectOptions(trigger: Element | string): Promise<string[]> {
  const texts = optionTexts(await openSelect(trigger))
  await closeSelect()
  return texts
}

/** Öppnar väljaren och väljer alternativet med den texten (eller som matchar mönstret). */
export async function chooseOption(trigger: Element | string, label: string | RegExp) {
  const listbox = await openSelect(trigger)
  const option = [...listbox.querySelectorAll<HTMLElement>('[role="option"]')].find((candidate) => {
    const text = candidate.textContent?.trim() ?? ''
    return typeof label === 'string'
      ? text === label
      : label.test(text)
  })
  if (!option) throw new Error(`Hittar inte alternativet ${String(label)} bland: ${optionTexts(listbox).join(', ')}`)
  option.focus()
  option.dispatchEvent(new KeyboardEvent('keydown', {key: 'Enter', bubbles: true}))
  await flushPromises()
}
