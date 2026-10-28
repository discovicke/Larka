import {flushPromises} from '@vue/test-utils'

/** Alla dagsknappar i den öppna kalendern (portal, därför via document). */
function dayCell(value: string): HTMLElement | null {
  return document.querySelector(`[data-slot="calendar-cell-trigger"][data-value="${value}"]:not([data-outside-view])`)
}

function navButton(slot: 'prev' | 'next'): HTMLElement {
  return document.querySelector(`[data-slot="calendar-${slot}-button"]`) as HTMLElement
}

/** Öppnar väljaren (knappen) och returnerar kalenderns popover. */
export async function openPicker(trigger: Element | string) {
  const button = (typeof trigger === 'string'
    ? document.querySelector(trigger)
    : trigger) as HTMLElement
  button.click()
  await flushPromises()
  return document.querySelector('[data-slot="popover-content"]') as HTMLElement
}

/** Bläddrar i kalendern och klickar på dagen, som en användare. Värde: 'yyyy-mm-dd'. */
export async function clickDay(value: string) {
  for (let i = 0; i < 400 && !dayCell(value); i++) {
    const shown = document.querySelector('[data-slot="calendar-cell-trigger"]:not([data-outside-view])')?.getAttribute('data-value')
    navButton(shown && shown > value
      ? 'prev'
      : 'next').click()
    await flushPromises()
  }
  const cell = dayCell(value)
  if (!cell) throw new Error(`Hittar inte dagen ${value} i kalendern`)
  cell.click()
  await flushPromises()
}

/** Öppnar väljaren och väljer ett datum. */
export async function pickDate(trigger: Element | string, value: string) {
  await openPicker(trigger)
  await clickDay(value)
}

/** Öppnar väljaren, väljer dag och skriver tiden ('hh:mm') i tidsfältet. */
export async function pickDateTime(trigger: Element | string, value: string) {
  const [date, time] = value.split('T')
  await openPicker(trigger)
  await clickDay(date!)
  const input = document.querySelector('[data-slot="popover-content"] input[type="time"]') as HTMLInputElement
  input.value = time!
  input.dispatchEvent(new Event('input'))
  await flushPromises()
  await closePicker()
}

/** Stänger den öppna väljaren med Escape, som en användare. */
export async function closePicker() {
  const content = document.querySelector('[data-slot="popover-content"]')
  content?.dispatchEvent(new KeyboardEvent('keydown', {key: 'Escape', bubbles: true}))
  await flushPromises()
  await new Promise((resolve) => setTimeout(resolve, 0))
  await flushPromises()
}
