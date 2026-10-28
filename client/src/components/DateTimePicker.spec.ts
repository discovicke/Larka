import {enableAutoUnmount, flushPromises, mount} from '@vue/test-utils'
import {afterEach, describe, expect, it} from 'vitest'

import DateTimePicker from '@/components/DateTimePicker.vue'
import {clickDay, openPicker} from '@/test/datePicker'

enableAutoUnmount(afterEach)

function render(props: Record<string, unknown> = {}) {
  return mount(DateTimePicker, {props: {id: 'dt', ...props}, attachTo: document.body})
}

function timeInput() {
  return document.querySelector('[data-slot="popover-content"] input[type="time"]') as HTMLInputElement
}

describe('DateTimePicker', () => {
  it('visar datum och tid på svenska', () => {
    const wrapper = render({modelValue: '2027-10-11T09:00'})
    expect(wrapper.find('#dt').text()).toBe('11 okt 2027 09:00')
  })

  it('visar platshållare när det är tomt', () => {
    expect(render().find('#dt').text()).toBe('Välj datum och tid')
  })

  it('en vald dag behåller klockslaget', async () => {
    const wrapper = render({modelValue: '2027-10-11T14:30'})
    await openPicker(wrapper.find('#dt').element)
    await clickDay('2027-10-13')
    expect(wrapper.emitted('update:modelValue')).toEqual([['2027-10-13T14:30']])
  })

  it('en ny dag utan tid får standardtid', async () => {
    const wrapper = render()
    await openPicker(wrapper.find('#dt').element)
    await clickDay(`${new Date().getFullYear()}-${String(new Date().getMonth() + 1).padStart(2, '0')}-15`)
    expect(String(wrapper.emitted('update:modelValue')![0]![0])).toMatch(/-15T09:00$/)
  })

  it('ändrad tid behåller datumet', async () => {
    const wrapper = render({modelValue: '2027-10-11T09:00'})
    await openPicker(wrapper.find('#dt').element)
    timeInput().value = '17:45'
    timeInput().dispatchEvent(new Event('input'))
    await flushPromises()
    expect(wrapper.emitted('update:modelValue')).toEqual([['2027-10-11T17:45']])
  })

  it('rensar till tom sträng', async () => {
    const wrapper = render({modelValue: '2027-10-11T09:00', clearable: true})
    await openPicker(wrapper.find('#dt').element)
    const clear = [...document.querySelectorAll('[data-slot="popover-content"] button')].find((btn) => btn.textContent?.trim() === 'Rensa') as HTMLElement
    clear.click()
    await flushPromises()
    expect(wrapper.emitted('update:modelValue')).toEqual([['']])
  })

  it('börjar veckan på måndag', async () => {
    const wrapper = render({modelValue: '2027-10-11T09:00'})
    await openPicker(wrapper.find('#dt').element)
    const heads = [...document.querySelectorAll('th')].map((cell) => cell.textContent?.trim().toLowerCase())
    expect(heads.slice(0, 2)).toEqual(['m', 't'])
  })
})
