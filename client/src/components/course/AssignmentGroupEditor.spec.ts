import {mount} from '@vue/test-utils'
import {describe, expect, it} from 'vitest'

import type {Schema} from '@/lib/api'
import type {GroupForm} from '@/lib/createContent'

import AssignmentGroupEditor from './AssignmentGroupEditor.vue'

const students: Schema<'CourseStudentDto'>[] = [
  {id: 's1', name: 'Alice', email: 'alice@example.com', gitHubLogin: 'alice'},
  {id: 's2', name: 'Bob', email: 'bob@example.com', gitHubLogin: 'bob'},
  {id: 's3', name: 'Cara', email: 'cara@example.com', gitHubLogin: null},
]

const groups: GroupForm[] = [
  {name: 'Grupp 1', studentIds: ['s1', 's2']},
  {name: 'Grupp 2', studentIds: ['s3']},
]

function mountEditor(modelValue: GroupForm[] = groups) {
  return mount(AssignmentGroupEditor, {
    props: {students, modelValue},
  })
}

function lastEmitted(wrapper: ReturnType<typeof mountEditor>): GroupForm[] {
  const events = wrapper.emitted('update:modelValue') ?? []
  return events.at(-1)?.[0] as GroupForm[]
}

describe('AssignmentGroupEditor', () => {
  it('slumpar alla elever till valt antal grupper', async () => {
    const wrapper = mountEditor([])
    await wrapper.find('[data-test="group-count"]').setValue('2')
    await wrapper.find('[data-test="group-randomize"]').trigger('click')

    const emitted = lastEmitted(wrapper)
    expect(emitted).toHaveLength(2)
    expect(emitted.flatMap((group) => group.studentIds).sort()).toEqual(['s1', 's2', 's3'])
    expect(emitted.every((group) => group.studentIds.length > 0)).toBe(true)
  })

  it('flyttar en elev till en annan grupp', async () => {
    const wrapper = mountEditor()
    const selects = wrapper.findAll('[data-test="student-group"]')
    await selects[0].setValue('1')

    const emitted = lastEmitted(wrapper)
    expect(emitted[0].studentIds).toEqual(['s2'])
    expect(emitted[1].studentIds).toEqual(['s3', 's1'])
  })

  it('lägger till och tar bort grupper', async () => {
    const wrapper = mountEditor()
    await wrapper.find('[data-test="group-add"]').trigger('click')
    expect(lastEmitted(wrapper)).toHaveLength(3)

    await wrapper.findAll('[data-test="group-remove"]')[2].trigger('click')
    expect(lastEmitted(wrapper)).toHaveLength(2)
  })

  it('visar vilka elever som saknar grupp', () => {
    const wrapper = mount(AssignmentGroupEditor, {
      props: {
        students,
        modelValue: [{name: 'Grupp 1', studentIds: ['s1']}],
      },
    })

    expect(wrapper.find('[data-test="group-unassigned"]').text()).toContain('2 elever saknar grupp')
  })
})
