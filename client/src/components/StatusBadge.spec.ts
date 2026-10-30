import {mount} from '@vue/test-utils'
import {describe, expect, it} from 'vitest'

import StatusBadge from '@/components/StatusBadge.vue'

describe('StatusBadge', () => {
  it('visar lägets text och ton', () => {
    const badge = mount(StatusBadge, {props: {status: 'deadline.overdue'}})

    expect(badge.text()).toBe('Försenad')
    expect(badge.attributes('data-status')).toBe('deadline.overdue')
    expect(badge.attributes('data-tone')).toBe('destructive')
    expect(badge.attributes('data-variant')).toBe('destructive')
  })

  it('neutrala lägen är en kontur', () => {
    const badge = mount(StatusBadge, {props: {status: 'course.running'}})

    expect(badge.attributes('data-variant')).toBe('outline')
  })

  it('Klar är grön och har en ikon', () => {
    const badge = mount(StatusBadge, {props: {status: 'progress.done'}})

    expect(badge.attributes('data-variant')).toBe('success')
    expect(badge.find('svg').exists()).toBe(true)
  })

  it('label ersätter texten', () => {
    const badge = mount(StatusBadge, {props: {status: 'publishing.scheduled', label: 'Schemalagd 3 nov'}})

    expect(badge.text()).toBe('Schemalagd 3 nov')
    expect(badge.attributes('data-variant')).toBe('info')
  })

  it('icon=false döljer ikonen', () => {
    const badge = mount(StatusBadge, {props: {status: 'progress.done', icon: false}})

    expect(badge.find('svg').exists()).toBe(false)
  })

  it('ett läge utan ikon visar bara texten', () => {
    const badge = mount(StatusBadge, {props: {status: 'publishing.draft'}})

    expect(badge.find('svg').exists()).toBe(false)
    expect(badge.text()).toBe('Utkast')
  })
})
