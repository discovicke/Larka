import {flushPromises, mount, type VueWrapper} from '@vue/test-utils'
import {afterEach, describe, expect, it} from 'vitest'

import UnlinkCourseDialog from '@/components/groups/UnlinkCourseDialog.vue'
import {mockApi, requests} from '@/test/mockApi'

const course = {id: 'c1', schoolId: 's1', code: 'DEVOPS', title: 'DevOps', description: '', points: null, startsOn: null, endsOn: null}

// Dialogen portas till body, så den måste tas bort mellan testerna.
const mounted: VueWrapper[] = []

afterEach(() => {
  mounted.splice(0).forEach((wrapper) => wrapper.unmount())
})

async function openDialog(status: number) {
  mockApi(() => ({status, body: status === 204
    ? undefined
    : 'fel'}))
  const wrapper = mount(UnlinkCourseDialog, {
    props: {open: true, groupId: 'g1', groupName: 'TE22A', course},
    attachTo: document.body,
  })
  await flushPromises()
  mounted.push(wrapper)
  return wrapper
}

const confirm = () => document.querySelector('[data-test="confirm-unlink-course"]') as HTMLElement

describe('UnlinkCourseDialog', () => {
  it('frågar och kopplar bort kursen vid bekräftelse', async () => {
    const wrapper = await openDialog(204)
    expect(document.querySelector('[role="dialog"]')?.textContent).toContain('Koppla bort DevOps från TE22A?')

    confirm().click()
    await flushPromises()

    expect(requests.at(-1)).toMatchObject({method: 'DELETE', path: '/api/groups/g1/courses/c1'})
    expect(wrapper.emitted('unlinked')?.[0]).toEqual(['c1'])
  })

  it('visar felet när det misslyckas', async () => {
    const wrapper = await openDialog(500)

    confirm().click()
    await flushPromises()

    expect(document.querySelector('[role="alert"]')?.textContent).toContain('fel')
    expect(wrapper.emitted('unlinked')).toBeUndefined()
  })
})
