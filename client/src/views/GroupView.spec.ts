import {flushPromises} from '@vue/test-utils'
import {describe, expect, it, vi} from 'vitest'

import {mockApi, requests, sent} from '@/test/mockApi'
import {meAs} from '@/test/me'
import {renderApp} from '@/test/renderApp'

const classGroup = {id: 'g1', schoolId: 's1', type: 'Class', name: 'TE22A', term: 'HT25'}
const committee = {id: 'g2', schoolId: 's1', type: 'Committee', name: 'Elevrådet', term: ''}

const devops = {id: 'c1', schoolId: 's1', code: 'DEVOPS', title: 'DevOps'}

const java = {id: 'c2', schoolId: 's1', code: 'JAVA', title: 'Java'}

const users = [
  {id: 'u1', schoolId: 's1', name: 'Siv Student', email: 'siv@example.com', role: 'Student'},
  {id: 'u5', schoolId: 's1', name: 'Nils Ny', email: 'nils@example.com', role: 'Student'},
]

const members = [
  {userId: 'u1', name: 'Siv Student', role: 'Student'},
  {userId: 'u2', name: 'Lars Lärare', role: 'Teacher'},
  {userId: 'u3', name: 'Malin Mentor', role: 'Mentor'},
  {userId: 'u4', name: 'Stina Student', role: 'Student'},
]

interface Backend {
  group?: typeof classGroup
  courses?: unknown[]
  members?: unknown[]
  membersStatus?: number
  role?: 'Teacher' | 'Admin'
  // Status för skrivande anrop, som POST, PUT och DELETE.
  writeStatus?: number
}

function backend(options: Backend = {}) {
  const group = options.group ?? classGroup
  mockApi((method, path) => {
    if (path === '/api/me') {
      return {status: 200, body: meAs(options.role ?? 'Teacher', ['c1'])}
    }
    if (path === '/api/users') {
      return {status: 200, body: users}
    }
    if (path === '/api/courses') {
      return {status: 200, body: [devops, java]}
    }
    if (method !== 'GET' && options.writeStatus) {
      return {status: options.writeStatus, body: 'fel'}
    }
    if (method === 'POST' && path === `/api/groups/${group.id}/members`) {
      return {status: 201, body: {}}
    }
    if (method === 'PUT' || method === 'DELETE' || path.includes('/courses/')) {
      return {status: method === 'DELETE'
        ? 204
        : 200, body: method === 'DELETE'
        ? undefined
        : {}}
    }
    if (path === `/api/groups/${group.id}`) {
      return {status: 200, body: group}
    }
    if (path === `/api/groups/${group.id}/courses`) {
      return {status: 200, body: options.courses ?? [devops]}
    }
    if (path === `/api/groups/${group.id}/members`) {
      return options.membersStatus
        ? {status: options.membersStatus, body: 'fel'}
        : {status: 200, body: options.members ?? members}
    }
    return {status: 404}
  })
}

describe('GroupView', () => {
  it('visar namn, typ och starttermin i rubriken', async () => {
    backend()

    const {wrapper} = await renderApp('/groups/g1')

    expect(wrapper.find('h1').text()).toBe('TE22A')
    expect(wrapper.find('[data-test="group-subtitle"]').text()).toBe('Klass · startade HT25')
  })

  it('grupperar medlemmarna efter roll med lärare först', async () => {
    backend()

    const {wrapper} = await renderApp('/groups/g1')

    const sections = wrapper.findAll('[data-test="member-role"]')
    expect(sections.map((section) => section.find('h3').text())).toEqual(['Lärare', 'Mentor', 'Elev'])
    expect(sections[0].findAll('[data-test="member-name"]').map((name) => name.text())).toEqual(['Lars Lärare'])
    expect(sections[2].findAll('[data-test="member-row"]')).toHaveLength(2)
  })

  it('döljer roller utan medlemmar', async () => {
    backend({members: [members[0]]})

    const {wrapper} = await renderApp('/groups/g1')

    const sections = wrapper.findAll('[data-test="member-role"]')
    expect(sections.map((section) => section.find('h3').text())).toEqual(['Elev'])
  })

  it('en kurs länkar till kursens sida', async () => {
    backend()

    const {wrapper} = await renderApp('/groups/g1')

    const link = wrapper.find('[data-test="course-row"] a[href="/courses/c1/content"]')
    expect(link.text()).toBe('DevOps')
  })

  it('ordnar kurserna som en tidslinje med datum och läge', async () => {
    vi.useFakeTimers({toFake: ['Date']})
    vi.setSystemTime(new Date(2026, 9, 29))
    try {
      backend({courses: [
        {...java, startsOn: '2026-12-01', endsOn: '2027-01-15'},
        {id: 'c3', schoolId: 's1', code: 'CS', title: 'C#', startsOn: '2025-08-25', endsOn: '2025-10-10'},
        devops,
        {id: 'c4', schoolId: 's1', code: 'DB', title: 'Databaser', startsOn: '2025-10-13', endsOn: '2025-12-19'},
        {...devops, id: 'c5', code: 'DEV2', title: 'DevOps 2', startsOn: '2026-10-12', endsOn: '2026-11-13'},
      ]})

      const {wrapper} = await renderApp('/groups/g1')

      const parts = wrapper.findAll('[data-test="course-group"]')
      expect(parts.map((part) => part.find('h3').text())).toEqual(['Pågår', 'Kommande', 'Avslutade'])
      expect(parts[0].findAll('[data-test="course-row"] a').map((link) => link.text())).toEqual(['DevOps', 'DevOps 2'])
      expect(parts[2].findAll('[data-test="course-row"] a').map((link) => link.text())).toEqual(['Databaser', 'C#'])
      const running = parts[0].findAll('[data-test="course-row"]')[1]
      expect(running.find('[data-test="course-dates"]').text()).toBe('12 okt – 13 nov 2026')
      expect(running.find('[data-test="course-status"]').text()).toBe('Pågår')
      expect(parts[1].find('[data-test="course-status"]').text()).toBe('Kommande')
      expect(parts[2].find('[data-test="course-dates"]').text()).toBe('13 okt – 19 dec 2025')
      expect(parts[2].find('[data-test="course-status"]').text()).toBe('Avslutad')
      // Utan datum finns varken datum eller läge, men kursen syns under Pågår.
      expect(parts[0].findAll('[data-test="course-row"]')[0].find('[data-test="course-status"]').exists()).toBe(false)
    } finally {
      vi.useRealTimers()
    }
  })

  it('en kommitté använder samma kurslayout', async () => {
    backend({group: committee, courses: [devops]})

    const {wrapper} = await renderApp('/groups/g2')

    expect(wrapper.find('[data-test="course-group"] h3').text()).toBe('Pågår')
  })

  it('döljer kurserna för en grupp utan kurser', async () => {
    backend({group: committee, courses: []})

    const {wrapper} = await renderApp('/groups/g2')

    expect(wrapper.find('h1').text()).toBe('Elevrådet')
    expect(wrapper.find('[data-test="group-subtitle"]').text()).toBe('Kommitté')
    expect(wrapper.find('#courses-heading').exists()).toBe(false)
  })

  it('visar att gruppen saknar medlemmar', async () => {
    backend({members: []})

    const {wrapper} = await renderApp('/groups/g1')

    expect(wrapper.text()).toContain('Gruppen har inga medlemmar ännu.')
  })

  it('visar sidan som saknas när gruppen inte finns', async () => {
    backend()

    const {wrapper} = await renderApp('/groups/okand')

    expect(wrapper.find('[data-test="not-found"]').exists()).toBe(true)
  })

  it('visar ett fel när medlemmarna inte kan hämtas', async () => {
    backend({membersStatus: 500})

    const {wrapper} = await renderApp('/groups/g1')

    expect(wrapper.find('[role="alert"]').text()).toContain('Kunde inte hämta gruppen.')
  })

  it('Alla grupper är aktiv i sidomenyn', async () => {
    backend()

    const {wrapper} = await renderApp('/groups/g1')

    expect(wrapper.find('[data-slot="sidebar"] a[href="/groups"]').attributes('data-active')).toBe('true')
  })

  describe('som admin', () => {
    const modal = () => document.querySelector('[role="dialog"]') as HTMLElement

    async function click(element: Element | null) {
      (element as HTMLElement).click()
      await flushPromises()
    }

    function roleOf(wrapper: Awaited<ReturnType<typeof renderApp>>['wrapper'], name: string) {
      return wrapper.findAll('[data-test="member-role"]')
        .find((section) => section.text().includes(name))
        ?.find('h3').text()
    }

    it('visar inga adminverktyg för en lärare', async () => {
      backend()

      const {wrapper} = await renderApp('/groups/g1')

      expect(wrapper.find('[data-test="add-member"]').exists()).toBe(false)
      expect(wrapper.find('[data-test="remove-member"]').exists()).toBe(false)
      expect(wrapper.find('[data-test="member-role-select"]').exists()).toBe(false)
      expect(wrapper.find('[data-test="link-course-button"]').exists()).toBe(false)
      expect(wrapper.find('[data-test="unlink-course"]').exists()).toBe(false)
    })

    it('visar verktygen även för en grupp utan medlemmar och kurser', async () => {
      backend({role: 'Admin', members: [], courses: []})

      const {wrapper} = await renderApp('/groups/g1')

      expect(wrapper.find('[data-test="add-member"]').text()).toBe('Lägg till medlem')
      expect(wrapper.text()).toContain('Gruppen har inga kurser ännu.')
      expect(wrapper.find('[data-test="link-course-button"]').text()).toBe('Koppla kurs')
    })

    it('lägger till en medlem som hamnar under sin roll utan att ladda om', async () => {
      backend({role: 'Admin'})
      const {wrapper} = await renderApp('/groups/g1')

      await click(wrapper.find('[data-test="add-member"]').element)
      const options = [...modal().querySelectorAll('[data-test="member-user"] option')].map((option) => option.textContent?.trim())
      // Siv är redan medlem.
      expect(options.some((text) => text?.includes('Siv Student'))).toBe(false)
      expect(options.some((text) => text?.includes('Nils Ny'))).toBe(true)

      const user = modal().querySelector('[data-test="member-user"]') as HTMLSelectElement
      user.value = 'u5'
      user.dispatchEvent(new Event('change'))
      const role = modal().querySelector('[data-test="member-role-select"]') as HTMLSelectElement
      role.value = 'Mentor'
      role.dispatchEvent(new Event('change'))
      await flushPromises()
      await click(modal().querySelector('[data-test="confirm-add-member"]'))

      expect(sent('POST', '/api/groups/g1/members')).toEqual({userId: 'u5', role: 'Mentor'})
      expect(roleOf(wrapper, 'Nils Ny')).toBe('Mentor')
      expect(modal()).toBeNull()
    })

    it('filtrerar användarna på namn och e-post', async () => {
      backend({role: 'Admin'})
      const {wrapper} = await renderApp('/groups/g1')

      await click(wrapper.find('[data-test="add-member"]').element)
      const search = modal().querySelector('[data-test="member-search"]') as HTMLInputElement
      search.value = 'zzz'
      search.dispatchEvent(new Event('input'))
      await flushPromises()

      expect(modal().querySelectorAll('[data-test="member-user"] option')).toHaveLength(1)
      expect(modal().textContent).toContain('Inga användare hittades')
    })

    it('byter roll och flyttar medlemmen till den nya rollgruppen', async () => {
      backend({role: 'Admin'})
      const {wrapper} = await renderApp('/groups/g1')

      const select = wrapper.find('select[aria-label="Roll för Siv Student"]')
      await select.setValue('Teacher')
      await flushPromises()

      expect(sent('PUT', '/api/groups/g1/members/u1')).toEqual({role: 'Teacher'})
      expect(roleOf(wrapper, 'Siv Student')).toBe('Lärare')
    })

    it('behåller rollen och visar felet när rollbytet misslyckas', async () => {
      backend({role: 'Admin', writeStatus: 500})
      const {wrapper} = await renderApp('/groups/g1')

      await wrapper.find('select[aria-label="Roll för Siv Student"]').setValue('Teacher')
      await flushPromises()

      expect(roleOf(wrapper, 'Siv Student')).toBe('Elev')
      expect(wrapper.find('[role="alert"]').text()).toContain('Kunde inte ändra rollen för Siv Student.')
      expect((wrapper.find('select[aria-label="Roll för Siv Student"]').element as HTMLSelectElement).value).toBe('Student')
    })

    it('tar bort en medlem efter bekräftelse', async () => {
      backend({role: 'Admin'})
      const {wrapper} = await renderApp('/groups/g1')

      await wrapper.findAll('[data-test="remove-member"]')[2].trigger('click')
      await flushPromises()
      expect(modal().textContent).toContain('Ta bort Siv Student från TE22A?')
      expect(requests.some((request) => request.method === 'DELETE')).toBe(false)
      await click(modal().querySelector('[data-test="confirm-remove-member"]'))

      expect(requests.some((request) => request.method === 'DELETE' && request.path === '/api/groups/g1/members/u1')).toBe(true)
      expect(wrapper.text()).not.toContain('Siv Student')
    })

    it('behåller medlemmen och visar felet när borttagningen misslyckas', async () => {
      backend({role: 'Admin', writeStatus: 500})
      const {wrapper} = await renderApp('/groups/g1')

      await wrapper.findAll('[data-test="remove-member"]')[2].trigger('click')
      await flushPromises()
      await click(modal().querySelector('[data-test="confirm-remove-member"]'))

      expect(modal().querySelector('[role="alert"]')?.textContent).toContain('fel')
      expect(wrapper.text()).toContain('Siv Student')
    })

    it('kopplar en kurs som inte redan är kopplad', async () => {
      backend({role: 'Admin'})
      const {wrapper} = await renderApp('/groups/g1')

      await click(wrapper.find('[data-test="link-course-button"]').element)
      const select = modal().querySelector('[data-test="link-course"]') as HTMLSelectElement
      // DevOps är redan kopplad.
      expect([...select.options].map((option) => option.value)).toEqual(['', 'c2'])
      select.value = 'c2'
      select.dispatchEvent(new Event('change'))
      await flushPromises()
      await click(modal().querySelector('[data-test="confirm-link-course"]'))

      expect(requests.some((request) => request.method === 'POST' && request.path === '/api/groups/g1/courses/c2')).toBe(true)
      expect(wrapper.findAll('[data-test="course-row"]').map((row) => row.find('a').text())).toEqual(['DevOps', 'Java'])
    })

    it('visar att kursen redan är kopplad vid 409', async () => {
      backend({role: 'Admin', writeStatus: 409})
      const {wrapper} = await renderApp('/groups/g1')

      await click(wrapper.find('[data-test="link-course-button"]').element)
      const select = modal().querySelector('[data-test="link-course"]') as HTMLSelectElement
      select.value = 'c2'
      select.dispatchEvent(new Event('change'))
      await flushPromises()
      await click(modal().querySelector('[data-test="confirm-link-course"]'))

      expect(modal().textContent).toContain('Kursen är redan kopplad till gruppen.')
      expect(wrapper.findAll('[data-test="course-row"]')).toHaveLength(1)
    })

    it('kopplar bort en kurs efter bekräftelse', async () => {
      backend({role: 'Admin'})
      const {wrapper} = await renderApp('/groups/g1')

      await wrapper.find('[data-test="unlink-course"]').trigger('click')
      await flushPromises()
      expect(modal().textContent).toContain('Koppla bort DevOps från TE22A?')
      await click(modal().querySelector('[data-test="confirm-unlink-course"]'))

      expect(requests.some((request) => request.method === 'DELETE' && request.path === '/api/groups/g1/courses/c1')).toBe(true)
      expect(wrapper.findAll('[data-test="course-row"]')).toHaveLength(0)
      expect(wrapper.text()).toContain('Gruppen har inga kurser ännu.')
    })
  })
})
