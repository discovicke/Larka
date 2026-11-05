import {enableAutoUnmount, flushPromises, mount} from '@vue/test-utils'
import {afterEach, describe, expect, it} from 'vitest'

import DatePicker from '@/components/DatePicker.vue'
import {clickDay, openPicker} from '@/test/datePicker'
import {chooseOption} from '@/test/select'

enableAutoUnmount(afterEach)

function render(props: Record<string, unknown> = {}) {
  return mount(DatePicker, {props: {id: 'd', ...props}, attachTo: document.body})
}

describe('DatePicker', () => {
  it('visar datumet på svenska och platshållare när det är tomt', async () => {
    const empty = render()
    expect(empty.find('#d').text()).toBe('Välj datum')
    const filled = render({modelValue: '2027-10-11'})
    expect(filled.find('#d').text()).toBe('11 okt 2027')
  })

  it('skickar yyyy-mm-dd när man väljer en dag', async () => {
    const wrapper = render({modelValue: '2027-10-11'})
    await openPicker(wrapper.find('#d').element)
    await clickDay('2027-10-20')
    expect(wrapper.emitted('update:modelValue')).toEqual([['2027-10-20']])
  })

  it('bläddrar till en annan månad', async () => {
    const wrapper = render({modelValue: '2027-10-11'})
    await openPicker(wrapper.find('#d').element)
    await clickDay('2027-12-03')
    expect(wrapper.emitted('update:modelValue')).toEqual([['2027-12-03']])
  })

  it('hoppar direkt till en annan månad och ett annat år med listorna i rubriken', async () => {
    const wrapper = render({modelValue: '2027-10-11'})
    await openPicker(wrapper.find('#d').element)
    await chooseOption('[data-test="calendar-year"]', '2030')
    await chooseOption('[data-test="calendar-month"]', 'mars')

    const day = document.querySelector('[data-slot="calendar-cell-trigger"][data-value="2030-03-14"]:not([data-outside-view])') as HTMLElement
    expect(day).not.toBeNull()
    day.click()
    await flushPromises()
    expect(wrapper.emitted('update:modelValue')).toEqual([['2030-03-14']])
  })

  it('börjar veckan på måndag', async () => {
    const wrapper = render({modelValue: '2027-10-11'})
    await openPicker(wrapper.find('#d').element)
    const heads = [...document.querySelectorAll('[data-slot="calendar-head-cell"], th')].map((cell) => cell.textContent?.trim().toLowerCase())
    expect(heads.slice(0, 2)).toEqual(['m', 't'])
  })

  it('kan rensas när fältet är valfritt', async () => {
    const wrapper = render({modelValue: '2027-10-11', clearable: true})
    await openPicker(wrapper.find('#d').element)
    const clear = [...document.querySelectorAll('[data-slot="popover-content"] button')].find((btn) => btn.textContent?.trim() === 'Rensa') as HTMLElement
    clear.click()
    await flushPromises()
    expect(wrapper.emitted('update:modelValue')).toEqual([['']])
  })

  it('stänger av dagar utanför min och max', async () => {
    const wrapper = render({modelValue: '2027-10-11', min: '2027-10-05', max: '2027-10-25'})
    await openPicker(wrapper.find('#d').element)
    const cell = (day: string) => document.querySelector(`[data-slot="calendar-cell-trigger"][data-value="${day}"]:not([data-outside-view])`)
    expect(cell('2027-10-04')?.hasAttribute('data-disabled')).toBe(true)
    expect(cell('2027-10-26')?.hasAttribute('data-disabled')).toBe(true)
    expect(cell('2027-10-15')?.hasAttribute('data-disabled')).toBe(false)
  })

  it('är avstängd när disabled', () => {
    expect(render({disabled: true}).find('#d').attributes('disabled')).toBeDefined()
  })
})
