import {flushPromises, mount} from '@vue/test-utils'
import {describe, expect, it} from 'vitest'

import type {Schema} from '@/lib/api'
import {mockApi} from '@/test/mockApi'

import RelatedMaterial from './RelatedMaterial.vue'

const link: Schema<'RelatedLinkDto'> = {
  linkId: 'l1',
  contentId: 'x1',
  title: 'Teori om pipelines',
  type: 'Theory',
  moduleId: 'm1',
  moduleTitle: 'Vecka 1',
  courseId: 'c1',
}

function backend() {
  mockApi((_method, path) => path === '/api/content/x1'
    ? {
        status: 200,
        body: {
          id: 'x1',
          authorId: 'u1',
          type: 'Theory',
          title: 'Teori om pipelines',
          bodyMd: '# Continuous delivery',
          status: 'Published',
          createdAt: '2026-10-01T08:00:00Z',
          assignment: null,
        },
      }
    : {status: 404})
}

describe('RelatedMaterial', () => {
  it('visar inget när uppgiften saknar referenser', () => {
    const wrapper = mount(RelatedMaterial, {props: {links: []}})
    expect(wrapper.find('[data-test="related-material"]').exists()).toBe(false)
  })

  it('öppnar teoridelen i en sheet utan att lämna uppgiften', async () => {
    backend()
    const wrapper = mount(RelatedMaterial, {
      props: {links: [link]},
      attachTo: document.body,
    })

    expect(wrapper.find('[data-test="related-material"]').text()).toContain('Teori om pipelines')
    await wrapper.find('[data-test="related-open"]').trigger('click')
    await flushPromises()
    await flushPromises()

    const sheet = document.querySelector('[data-test="related-sheet"]')
    expect(sheet?.textContent).toContain('Continuous delivery')
  })
})
