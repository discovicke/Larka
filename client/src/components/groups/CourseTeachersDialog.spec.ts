import {flushPromises, mount, type VueWrapper} from '@vue/test-utils'
import {afterEach, describe, expect, it} from 'vitest'

import CourseTeachersDialog from '@/components/groups/CourseTeachersDialog.vue'
import {mockApi} from '@/test/mockApi'
import {selectOptions} from '@/test/select'

const users = [
  {id: 'u1', schoolId: 's1', name: 'Anna Admin', email: 'anna@example.com', role: 'Admin'},
  {id: 'u2', schoolId: 's1', name: 'Lars Lärare', email: 'lars@example.com', role: 'Teacher'},
  {id: 'u3', schoolId: 's1', name: 'Siv Student', email: 'siv@example.com', role: 'Student'},
  {id: 'u4', schoolId: 's1', name: 'Karin Lärare', email: 'karin@example.com', role: 'Teacher'},
]

const course = {
  id: 'c1', schoolId: 's1', title: 'Matte', code: 'MA1', description: '', points: 100, startsOn: null, endsOn: null,
}

// Dialogen portas till body, så den måste tas bort mellan testerna.
const mounted: VueWrapper[] = []

afterEach(() => {
  mounted.splice(0).forEach((wrapper) => wrapper.unmount())
})

describe('CourseTeachersDialog', () => {
  it('listar bara lärare som inte redan undervisar, aldrig admin eller elever', async () => {
    mockApi((method, path) => method === 'GET' && path === '/api/users'
      ? {status: 200, body: users}
      : {status: 404, body: 'fel'})
    const wrapper = mount(CourseTeachersDialog, {
      props: {
        open: false,
        course,
        teachers: [{userId: 'u4', name: 'Karin Lärare', email: 'karin@example.com'}],
      },
      attachTo: document.body,
    })
    mounted.push(wrapper)
    await wrapper.setProps({open: true})
    await flushPromises()

    expect(await selectOptions('[data-test="add-teacher-select"]')).toHaveLength(1)
  })
})
