import {mount} from '@vue/test-utils'
import {describe, expect, it} from 'vitest'

import ContentFormFields from '@/components/course/ContentFormFields.vue'
import {type ContentForm, emptyForm} from '@/lib/createContent'

function show(changes: Partial<ContentForm>) {
  const form = {...emptyForm(), title: 'Titel', partType: 'Submission' as const, ...changes}
  return mount(ContentFormFields, {
    props: {form, isEditing: false, errors: {}, submissionModes: []},
    global: {stubs: {MarkdownEditor: true}},
  })
}

describe('ContentFormFields, kompletteringsdeadlines', () => {
  it('knappen är avstängd tills sista dagen är satt', () => {
    const wrapper = show({})

    expect(wrapper.find('[data-test="resubmit-add"]').attributes('disabled')).toBeDefined()
  })

  it('lägger till och tar bort rader', async () => {
    const wrapper = show({dueAt: '2026-11-01T10:00'})
    const add = wrapper.find('[data-test="resubmit-add"]')
    expect(add.attributes('disabled')).toBeUndefined()

    await add.trigger('click')
    await add.trigger('click')
    expect(wrapper.findAll('[data-test="resubmit-remove"]')).toHaveLength(2)

    await wrapper.find('[data-test="resubmit-remove"]').trigger('click')
    expect(wrapper.findAll('[data-test="resubmit-remove"]')).toHaveLength(1)
  })

  it('visas inte för teori', () => {
    const wrapper = show({partType: 'Theory'})

    expect(wrapper.find('[data-test="resubmit-field"]').exists()).toBe(false)
  })
})
