import {flushPromises, mount} from '@vue/test-utils'
import {describe, expect, it} from 'vitest'

import DateTimePicker from '@/components/DateTimePicker.vue'
import {mockApi, requests, sent} from '@/test/mockApi'

import PersonalDueAt from './PersonalDueAt.vue'

const path = '/api/links/l1/students/u1/personal-due-at'

function render(props: Partial<InstanceType<typeof PersonalDueAt>['$props']> = {}) {
  mockApi((method, requestPath) => requestPath === path && method === 'PUT'
    ? {status: 200, body: {}}
    : {status: 404})
  return mount(PersonalDueAt, {
    props: {
      linkId: 'l1',
      studentId: 'u1',
      personalDueAt: null,
      dueAt: '2026-11-05T12:00:00Z',
      resubmitDueAt: [],
      status: 'NotStarted',
      ...props,
    },
  })
}

describe('PersonalDueAt', () => {
  it('skickar vald deadline som ISO och meddelar att den sparats', async () => {
    const wrapper = render()

    wrapper.findComponent(DateTimePicker).vm.$emit('update:modelValue', '2030-01-15T23:59')
    await flushPromises()
    await wrapper.find('[data-test="personal-due-at-save"]').trigger('click')
    await flushPromises()

    expect(sent('PUT', path)).toEqual({dueAt: new Date('2030-01-15T23:59').toISOString()})
    expect(wrapper.emitted('saved')).toHaveLength(1)
  })

  it('kan inte spara utan valt datum och har ingen Ta bort när ingen deadline finns', () => {
    const wrapper = render()

    expect(wrapper.find('[data-test="personal-due-at-save"]').attributes('disabled')).toBeDefined()
    expect(wrapper.find('[data-test="personal-due-at-remove"]').exists()).toBe(false)
  })

  it('visar nuvarande deadline och tar bort den med null', async () => {
    const wrapper = render({personalDueAt: '2030-01-15T22:59:00Z'})

    expect(wrapper.find('[data-test="personal-due-at-current"]').text()).toContain('Eleven har deadline')
    await wrapper.find('[data-test="personal-due-at-remove"]').trigger('click')
    await flushPromises()

    expect(sent('PUT', path)).toEqual({dueAt: null})
    expect(wrapper.emitted('saved')).toHaveLength(1)
  })

  it('döljs utan uppgiftsdeadline, för bedömda inlämningar och för inlämningar som väntar på bedömning', () => {
    expect(render({dueAt: null}).find('[data-test="personal-due-at"]').exists()).toBe(false)
    expect(render({status: 'Graded'}).find('[data-test="personal-due-at"]').exists()).toBe(false)
    expect(render({status: 'Submitted'}).find('[data-test="personal-due-at"]').exists()).toBe(false)
    expect(render({status: 'NeedsWork'}).find('[data-test="personal-due-at"]').exists()).toBe(true)
    expect(requests.filter((request) => request.method === 'PUT')).toHaveLength(0)
  })

  it('anger klassens sista deadline som tidigaste dag', () => {
    const wrapper = render({dueAt: '2099-01-01T12:00:00Z', resubmitDueAt: ['2099-02-10T12:00:00Z']})

    expect(wrapper.findComponent(DateTimePicker).props('min')).toBe('2099-02-10')
    expect(wrapper.text()).toContain('efter uppgiftens sista deadline')
  })
})
