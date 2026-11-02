import {flushPromises} from '@vue/test-utils'
import {describe, expect, it, vi} from 'vitest'

import DatePicker from '@/components/DatePicker.vue'
import {mockApi, requests, sent} from '@/test/mockApi'
import {meAs, mentorAs} from '@/test/me'
import {renderApp} from '@/test/renderApp'

const classGroup = {id: 'g1', schoolId: 's1', type: 'Class', name: 'TE22A', term: 'HT25'}
const committee = {id: 'g2', schoolId: 's1', type: 'Committee', name: 'Elevrådet', term: ''}

const devops = {id: 'c1', schoolId: 's1', code: 'DEVOPS', title: 'DevOps'}

const java = {id: 'c2', schoolId: 's1', code: 'JAVA', title: 'Java'}

const users = [
  {id: 'u1', schoolId: 's1', name: 'Siv Student', email: 'siv@example.com', role: 'Student'},
  {id: 'u2', schoolId: 's1', name: 'Lars Lärare', email: 'lars@example.com', role: 'Teacher'},
  {id: 'u6', schoolId: 's1', name: 'Tina Tränare', email: 'tina@example.com', role: 'Teacher'},
  {id: 'u5', schoolId: 's1', name: 'Nils Ny', email: 'nils@example.com', role: 'Student'},
  // Den inloggade admin (meAs) och en annan admin. Admin kan välja sig själv som lärare, aldrig en annan admin.
  {id: 'u-me', schoolId: 's1', name: 'Mia Admin', email: 'anna@example.com', role: 'Admin'},
  {id: 'u7', schoolId: 's1', name: 'Ada Admin', email: 'ada@example.com', role: 'Admin'},
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
  // Den inloggade är mentor i klassen (och står i medlemslistan som det).
  mentor?: boolean
  // Lärarna per kurs-id. Utan post har kursen inga lärare.
  teachers?: Record<string, {userId: string, name: string, email: string}[]>
  // Namnen på de andra klasserna som läser kursen, per kurs-id.
  shared?: Record<string, string[]>
  importResult?: unknown
  // Status för skrivande anrop, som POST, PUT och DELETE.
  writeStatus?: number
}

function backend(options: Backend = {}) {
  const group = options.group ?? classGroup
  // Kurser som kopplats under testet. Lärarsvaret följer med, som på riktigt.
  const linked: unknown[] = []
  mockApi((method, path, body) => {
    if (path === '/api/me') {
      return {status: 200, body: options.mentor
        ? mentorAs(['c1'])
        : meAs(options.role ?? 'Teacher', ['c1'])}
    }
    if (path === '/api/users') {
      return {status: 200, body: users}
    }
    if (method === 'POST' && path === '/api/courses') {
      return {status: 201, body: {...java, id: 'c9', code: (body as {code: string}).code, title: (body as {title: string}).title}}
    }
    if (path === '/api/courses') {
      return {status: 200, body: [devops, java]}
    }
    const teachersPath = /^\/api\/courses\/([^/]+)\/teachers/.exec(path)
    if (method === 'GET' && path === `/api/groups/${group.id}/course-teachers`) {
      return {status: 200, body: [...(options.courses ?? [devops]), ...linked].map((course) => ({
        courseId: (course as {id: string}).id,
        teachers: options.teachers?.[(course as {id: string}).id] ?? [],
        sharedWith: options.shared?.[(course as {id: string}).id] ?? [],
      }))}
    }
    const linkPath = /^\/api\/groups\/[^/]+\/courses\/([^/]+)$/.exec(path)
    if (method === 'POST' && linkPath) {
      linked.push({id: linkPath[1]})
    }
    if (method !== 'GET' && options.writeStatus) {
      return {status: options.writeStatus, body: 'fel'}
    }
    if (teachersPath && method === 'POST') {
      const user = users.find((found) => found.id === (body as {userId: string}).userId)!
      return {status: 201, body: {userId: user.id, name: user.name, email: user.email}}
    }
    if (method === 'POST' && path === `/api/groups/${group.id}/members/import`) {
      return {status: 200, body: options.importResult}
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
    if (path === `/api/groups/${group.id}/students/u1/overview`) {
      return {status: 200, body: {
        studentId: 'u1',
        name: 'Siv Student',
        groupId: group.id,
        courses: [{
          courseId: 'c1',
          title: 'DevOps',
          code: 'DEVOPS',
          assignmentCount: 6,
          needsWorkCount: 1,
          lateCount: 2,
          submittedCount: 1,
          gradedCount: 2,
          failCount: 0,
          passCount: 1,
          distinctionCount: 1,
        }],
      }}
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
  it('låter mentorn öppna en elevs läge i klassens kurser', async () => {
    backend({mentor: true, members: [...members, {userId: 'u-me', name: 'Malin Ekström', role: 'Mentor'}]})

    const {wrapper} = await renderApp('/groups/g1')
    const rows = wrapper.findAll('[data-test="member-row"]')
    const studentRow = rows.find((row) => row.text().includes('Siv Student'))!
    expect(rows.filter((row) => row.find('[data-test="member-overview"]').exists())).toHaveLength(2)
    expect(rows.find((row) => row.text().includes('Lars Lärare'))!.find('[data-test="member-overview"]').exists())
      .toBe(false)

    await studentRow.find('[data-test="member-overview"]').trigger('click')
    await flushPromises()

    const dialog = document.querySelector('[role="dialog"]')!
    expect(dialog.textContent).toContain('Siv Student')
    expect(dialog.textContent).toContain('DevOps')
    expect(dialog.querySelector('[data-test="overview-needs-work"]')!.textContent).toContain('Komplettera 1')
    expect(dialog.querySelector('[data-test="overview-late"]')!.textContent).toContain('Försenat 2')
    expect(dialog.querySelector('[data-test="overview-submitted"]')!.textContent).toContain('Inlämnat, ej bedömt 1')
    expect(dialog.querySelector('[data-test="overview-graded"]')!.textContent).toContain('Bedömda 2 av 6 (G 1 · VG 1)')
    expect(requests.some((request) => request.path === '/api/groups/g1/students/u1/overview')).toBe(true)
  })

  it('visar elevernas läge för admin men inte för en lärare i klassen', async () => {
    backend({role: 'Admin'})
    const admin = await renderApp('/groups/g1')
    expect(admin.wrapper.findAll('[data-test="member-overview"]')).toHaveLength(2)

    backend({role: 'Teacher'})
    const teacher = await renderApp('/groups/g1')
    expect(teacher.wrapper.find('[data-test="member-overview"]').exists()).toBe(false)
  })

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
      expect(parts.map((part) => part.find('h3, [data-test="finished-courses-toggle"]').text()))
        .toEqual(['Pågår', 'Kommande', 'Avslutade (2)'])
      // Avslutade är hopfällda tills man öppnar dem.
      expect(parts[2].findAll('[data-test="course-row"]')).toHaveLength(0)
      await parts[2].find('[data-test="finished-courses-toggle"]').trigger('click')
      await flushPromises()
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

    expect(wrapper.text()).toContain('Klassen har inga medlemmar ännu.')
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
      expect(wrapper.find('[data-test="member-menu"]').exists()).toBe(false)
      expect(wrapper.find('select').exists()).toBe(false)
      expect(wrapper.find('[data-test="link-course-button"]').exists()).toBe(false)
      expect(wrapper.find('[data-test="unlink-course"]').exists()).toBe(false)
    })

    it('visar verktygen även för en grupp utan medlemmar och kurser', async () => {
      backend({role: 'Admin', members: [], courses: []})

      const {wrapper} = await renderApp('/groups/g1')

      expect(wrapper.find('[data-test="add-member"]').text()).toBe('Lägg till medlem')
      expect(wrapper.text()).toContain('Klassen har inga kurser ännu.')
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

      // Klassen har redan en mentor, så Mentor är inget val här, och bara lärare kan vara lärare i gruppen.
      const role = modal().querySelector('[data-test="member-role-select"]') as HTMLSelectElement
      expect([...role.options].map((option) => option.value)).toEqual(['Teacher', 'Student'])
      expect(modal().querySelector('[data-test="has-mentor"]')).not.toBeNull()
      role.value = 'Teacher'
      role.dispatchEvent(new Event('change'))
      await flushPromises()
      const teacherOptions = [...modal().querySelectorAll('[data-test="member-user"] option')]
        .map((option) => option.textContent?.trim())
      expect(teacherOptions.some((text) => text?.includes('Nils Ny'))).toBe(false)
      const user = modal().querySelector('[data-test="member-user"]') as HTMLSelectElement
      user.value = 'u6'
      user.dispatchEvent(new Event('change'))
      await flushPromises()
      await click(modal().querySelector('[data-test="confirm-add-member"]'))

      expect(sent('POST', '/api/groups/g1/members')).toEqual({userId: 'u6', role: 'Teacher'})
      expect(roleOf(wrapper, 'Tina Tränare')).toBe('Lärare')
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

    async function openMemberMenu(wrapper: Awaited<ReturnType<typeof renderApp>>['wrapper'], name: string) {
      await wrapper.find(`[aria-label="Fler val för ${name}"]`).trigger('keydown', {key: 'Enter'})
      await flushPromises()
    }

    async function chooseAction(label: string) {
      const item = [...document.querySelectorAll('[role="menuitem"]')]
        .find((candidate) => candidate.textContent?.trim() === label) as HTMLElement
      item.click()
      await flushPromises()
    }

    function chooseRole(value: string) {
      const select = modal().querySelector('[data-test="change-role-select"]') as HTMLSelectElement
      select.value = value
      select.dispatchEvent(new Event('change'))
      return flushPromises()
    }

    it('visar bara namnen och en meny per medlem', async () => {
      backend({role: 'Admin'})
      const {wrapper} = await renderApp('/groups/g1')

      expect(wrapper.findAll('[data-test="member-row"]')).toHaveLength(4)
      expect(wrapper.findAll('[data-test="member-menu"]')).toHaveLength(4)
      expect(wrapper.find('select').exists()).toBe(false)
      expect(wrapper.text()).not.toContain('Ta bort')
      await openMemberMenu(wrapper, 'Siv Student')
      expect([...document.querySelectorAll('[role="menuitem"]')].map((item) => item.textContent?.trim()))
        .toEqual(['Ändra roll', 'Ta bort'])
    })

    it('byter roll i en dialog och laddar om listan', async () => {
      backend({role: 'Admin'})
      const {wrapper} = await renderApp('/groups/g1')

      await openMemberMenu(wrapper, 'Siv Student')
      await chooseAction('Ändra roll')
      expect(modal().textContent).toContain('Ändra roll för Siv Student')
      expect(modal().querySelector('[data-test="confirm-change-role"]')?.hasAttribute('disabled')).toBe(true)
      await chooseRole('Teacher')
      await click(modal().querySelector('[data-test="confirm-change-role"]'))

      expect(sent('PUT', '/api/groups/g1/members/u1')).toEqual({role: 'Teacher'})
      expect(requests.some((request) => request.method === 'DELETE')).toBe(false)
      expect(modal()).toBeNull()
    })

    it('byter mentor när en medlem får rollen Mentor', async () => {
      backend({role: 'Admin'})
      const {wrapper} = await renderApp('/groups/g1')

      await openMemberMenu(wrapper, 'Lars Lärare')
      await chooseAction('Ändra roll')
      await chooseRole('Mentor')
      expect(modal().querySelector('[data-test="mentor-leaves"]')?.textContent).toContain('Malin Mentor lämnar klassen')
      await click(modal().querySelector('[data-test="confirm-change-role"]'))

      expect(sent('PUT', '/api/groups/g1/members/u2')).toEqual({role: 'Mentor'})
      // API:et låter den förra mentorn lämna klassen, så klienten tar inte bort någon själv.
      expect(requests.some((request) => request.method === 'DELETE')).toBe(false)
    })

    it('behåller rollen och visar felet när rollbytet misslyckas', async () => {
      backend({role: 'Admin', writeStatus: 500})
      const {wrapper} = await renderApp('/groups/g1')

      await openMemberMenu(wrapper, 'Siv Student')
      await chooseAction('Ändra roll')
      await chooseRole('Teacher')
      await click(modal().querySelector('[data-test="confirm-change-role"]'))

      expect(modal().querySelector('[role="alert"]')?.textContent).toContain('Kunde inte ändra rollen för Siv Student.')
      expect(roleOf(wrapper, 'Siv Student')).toBe('Elev')
    })

    it('tar bort en medlem efter bekräftelse', async () => {
      backend({role: 'Admin'})
      const {wrapper} = await renderApp('/groups/g1')

      await openMemberMenu(wrapper, 'Siv Student')
      await chooseAction('Ta bort')
      expect(modal().textContent).toContain('Ta bort Siv Student från TE22A?')
      expect(requests.some((request) => request.method === 'DELETE')).toBe(false)
      await click(modal().querySelector('[data-test="confirm-remove-member"]'))

      expect(requests.some((request) => request.method === 'DELETE' && request.path === '/api/groups/g1/members/u1')).toBe(true)
      expect(wrapper.find('[data-test="toast"]').text()).toContain('Siv Student är borttagen ur TE22A.')
      expect(wrapper.text().replace(wrapper.find('[data-test="toaster"]').text(), '')).not.toContain('Siv Student')
    })

    it('behåller medlemmen och visar felet när borttagningen misslyckas', async () => {
      backend({role: 'Admin', writeStatus: 500})
      const {wrapper} = await renderApp('/groups/g1')

      await openMemberMenu(wrapper, 'Siv Student')
      await chooseAction('Ta bort')
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
      expect(wrapper.find('[data-test="toast"]').text()).toContain('Java är kopplad till TE22A.')
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
      expect(wrapper.text()).toContain('Klassen har inga kurser ännu.')
    })

    function pick(element: Element | null, value: string) {
      const select = element as HTMLSelectElement
      select.value = value
      select.dispatchEvent(new Event('change'))
      return flushPromises()
    }

    function type(selector: string, value: string) {
      const input = modal().querySelector(selector) as HTMLInputElement | HTMLTextAreaElement
      input.value = value
      input.dispatchEvent(new Event('input'))
      return flushPromises()
    }

    it('har Ny kurs som primär knapp och Koppla kurs som kontur', async () => {
      backend({role: 'Admin'})

      const {wrapper} = await renderApp('/groups/g1')

      const create = wrapper.find('[data-test="new-course-button"]')
      expect(create.text()).toBe('Ny kurs')
      expect(create.attributes('data-variant')).toBeUndefined()
      expect(wrapper.find('[data-test="link-course-button"]').attributes('data-variant')).toBe('outline')
    })

    it('visar kursens lärare, eller Ingen lärare som varning', async () => {
      backend({
        role: 'Admin',
        courses: [devops, java],
        teachers: {c1: [{userId: 'u2', name: 'Lars Lärare', email: 'lars@example.com'}]},
      })

      const {wrapper} = await renderApp('/groups/g1')

      const rows = wrapper.findAll('[data-test="course-row"]')
      expect(rows[0].find('[data-test="course-teacher-names"]').text()).toBe('Lärare: Lars Lärare')
      expect(rows[0].find('[data-status="course.noTeacher"]').exists()).toBe(false)
      const flag = rows[1].find('[data-status="course.noTeacher"]')
      expect(flag.text()).toBe('Ingen lärare')
      expect(flag.attributes('data-tone')).toBe('warning')
    })

    it('visar inga lärare på kursraden för en lärare', async () => {
      backend()

      const {wrapper} = await renderApp('/groups/g1')

      expect(wrapper.find('[data-test="course-teachers"]').exists()).toBe(false)
      expect(requests.some((request) => request.path.endsWith('/teachers'))).toBe(false)
    })

    it('lägger till och tar bort en lärare på kursraden', async () => {
      backend({role: 'Admin', teachers: {c1: [{userId: 'u2', name: 'Lars Lärare', email: 'lars@example.com'}]}})
      const {wrapper} = await renderApp('/groups/g1')

      await wrapper.find('[data-test="edit-teachers"]').trigger('click')
      await flushPromises()
      const options = [...modal().querySelectorAll('[data-test="add-teacher-select"] option')]
      // Lars undervisar redan, eleverna är inga lärare och den andra admin kan inte väljas. Admin själv kan det.
      expect(options.map((option) => (option as HTMLOptionElement).value)).toEqual(['', 'u-me', 'u6'])
      await pick(modal().querySelector('[data-test="add-teacher-select"]'), 'u6')
      await click(modal().querySelector('[data-test="add-teacher"]'))

      expect(sent('POST', '/api/courses/c1/teachers')).toEqual({userId: 'u6'})
      expect(wrapper.find('[data-test="course-teacher-names"]').text()).toBe('Lärare: Lars Lärare, Tina Tränare')

      await click(modal().querySelectorAll('[data-test="remove-teacher"]')[0])
      // Den sista läraren tas inte bort förrän admin bekräftat.
      await click(modal().querySelectorAll('[data-test="remove-teacher"]')[0])
      expect(requests.filter((request) => request.method === 'DELETE')).toHaveLength(1)
      await click(document.querySelector('[data-test="confirm-remove-last"]'))

      expect(requests.filter((request) => request.method === 'DELETE').map((request) => request.path))
        .toEqual(['/api/courses/c1/teachers/u2', '/api/courses/c1/teachers/u6'])
      expect(wrapper.find('[data-status="course.noTeacher"]').exists()).toBe(true)
    })

    it('behåller den sista läraren om admin avbryter', async () => {
      backend({role: 'Admin', teachers: {c1: [{userId: 'u2', name: 'Lars Lärare', email: 'lars@example.com'}]}})
      const {wrapper} = await renderApp('/groups/g1')

      await wrapper.find('[data-test="edit-teachers"]').trigger('click')
      await flushPromises()
      await click(modal().querySelector('[data-test="remove-teacher"]'))

      expect(document.querySelector('[data-test="confirm-last-teacher"]')?.textContent).toContain('enda lärare')
      const cancel = [...document.querySelectorAll('[data-test="confirm-last-teacher"] button')]
        .find((button) => button.textContent?.trim() === 'Avbryt')
      await click(cancel ?? null)

      expect(requests.some((request) => request.method === 'DELETE')).toBe(false)
      expect(wrapper.find('[data-test="course-teacher-names"]').text()).toBe('Lärare: Lars Lärare')
    })

    it('ersätter en lärare i ett steg: lägger till den nya och tar bort den gamla', async () => {
      backend({role: 'Admin', teachers: {c1: [{userId: 'u2', name: 'Lars Lärare', email: 'lars@example.com'}]}})
      const {wrapper} = await renderApp('/groups/g1')

      await wrapper.find('[data-test="edit-teachers"]').trigger('click')
      await flushPromises()
      await click(modal().querySelector('[data-test="replace-teacher"]'))
      expect(modal().textContent).toContain('Ersätt Lars Lärare med')
      await pick(modal().querySelector('[data-test="add-teacher-select"]'), 'u6')
      await click(modal().querySelector('[data-test="add-teacher"]'))

      expect(requests.filter((request) => request.method !== 'GET').map((request) => `${request.method} ${request.path}`))
        .toEqual(['POST /api/courses/c1/teachers', 'DELETE /api/courses/c1/teachers/u2'])
      expect(wrapper.find('[data-test="course-teacher-names"]').text()).toBe('Lärare: Tina Tränare')
      // Ersätt är ett byte och kräver ingen bekräftelse för att kursen står utan lärare.
      expect(document.querySelector('[data-test="confirm-last-teacher"]')).toBeNull()
    })

    it('säger att kursen delas med en annan klass', async () => {
      backend({
        role: 'Admin',
        teachers: {c1: [{userId: 'u2', name: 'Lars Lärare', email: 'lars@example.com'}]},
        shared: {c1: ['.NET24']},
      })
      const {wrapper} = await renderApp('/groups/g1')

      await wrapper.find('[data-test="edit-teachers"]').trigger('click')
      await flushPromises()

      expect(modal().querySelector('[data-test="shared-with"]')?.textContent).toContain('Delas med .NET24')
    })

    it('hämtar lärarna för alla kurser i ett anrop', async () => {
      backend({role: 'Admin', courses: [devops, java]})

      await renderApp('/groups/g1')

      expect(requests.filter((request) => request.path.endsWith('/teachers'))).toHaveLength(0)
      expect(requests.filter((request) => request.path === '/api/groups/g1/course-teachers')).toHaveLength(1)
    })

    it('visar lärarna på en nykopplad kurs utan att sidan laddas om', async () => {
      backend({
        role: 'Admin',
        teachers: {c2: [{userId: 'u6', name: 'Tina Tränare', email: 'tina@example.com'}]},
      })
      const {wrapper} = await renderApp('/groups/g1')

      await click(wrapper.find('[data-test="link-course-button"]').element)
      await pick(modal().querySelector('[data-test="link-course"]'), 'c2')
      await click(modal().querySelector('[data-test="confirm-link-course"]'))

      const rows = wrapper.findAll('[data-test="course-row"]')
      expect(rows[1].find('[data-test="course-teacher-names"]').text()).toBe('Lärare: Tina Tränare')
    })

    it('utser en mentor som redan är med i klassen, och den förra lämnar via API:et', async () => {
      backend({role: 'Admin'})
      const {wrapper} = await renderApp('/groups/g1')

      expect(wrapper.find('[data-test="set-mentor"]').text()).toBe('Byt mentor')
      await wrapper.find('[data-test="set-mentor"]').trigger('click')
      await flushPromises()
      await pick(modal().querySelector('[data-test="mentor-user"]'), 'u2')
      expect(modal().querySelector('[data-test="mentor-leaves"]')?.textContent).toContain('Malin Mentor lämnar klassen')
      await click(modal().querySelector('[data-test="confirm-mentor"]'))

      expect(sent('PUT', '/api/groups/g1/members/u2')).toEqual({role: 'Mentor'})
      expect(requests.some((request) => request.method === 'DELETE')).toBe(false)
      expect(modal()).toBeNull()
    })

    it('utser en mentor som inte är med i klassen när klassen saknar mentor', async () => {
      backend({role: 'Admin', members: [members[0]]})
      const {wrapper} = await renderApp('/groups/g1')

      expect(wrapper.find('[data-test="no-mentor"]').text()).toBe('Klassen har ingen mentor ännu.')
      expect(wrapper.find('[data-test="set-mentor"]').text()).toBe('Utse mentor')
      await wrapper.find('[data-test="set-mentor"]').trigger('click')
      await flushPromises()
      await pick(modal().querySelector('[data-test="mentor-user"]'), 'u6')
      await click(modal().querySelector('[data-test="confirm-mentor"]'))

      expect(sent('POST', '/api/groups/g1/members')).toEqual({userId: 'u6', role: 'Mentor'})
      expect(requests.some((request) => request.method === 'DELETE')).toBe(false)
    })

    it('visar ingen mentorsrad för en kommitté', async () => {
      backend({role: 'Admin', group: committee, members: [], courses: []})

      const {wrapper} = await renderApp('/groups/g2')

      expect(wrapper.find('[data-test="set-mentor"]').exists()).toBe(false)
    })

    it('förhandsvisar en inklistrad lista och lägger till eleverna', async () => {
      backend({
        role: 'Admin',
        importResult: {
          lines: [
            {name: 'Nils Ny', email: 'nils@example.com', outcome: 'Added'},
            {name: 'Olle Ovan', email: 'olle@example.com', outcome: 'Created'},
          ],
          added: [
            {userId: 'u5', name: 'Nils Ny', role: 'Student'},
            {userId: 'u7', name: 'Olle Ovan', role: 'Student'},
          ],
        },
      })
      const {wrapper} = await renderApp('/groups/g1')

      await click(wrapper.find('[data-test="add-member"]').element)
      await click(modal().querySelector('[data-test="show-paste"]'))
      await type('[data-test="import-text"]', [
        'Nils Ny, nils@example.com',
        'Olle Ovan\tolle@example.com',
        'Siv Student, siv@example.com',
        'Lars Lärare, lars@example.com',
        'bara ett namn',
      ].join('\n'))

      const titles = [...modal().querySelectorAll('[data-test="import-preview"] h3')].map((heading) => heading.textContent)
      expect(titles).toEqual([
        'Finns redan, läggs till i klassen (1)',
        'Nya, skapas utan lösenord (1)',
        'Hoppas över (2)',
        'Går inte att läsa (1)',
      ])
      expect(requests.some((request) => request.method === 'POST')).toBe(false)
      const confirm = modal().querySelector('[data-test="confirm-import"]') as HTMLButtonElement
      expect(confirm.textContent?.trim()).toBe('Lägg till 2 elever')
      await click(confirm)

      expect(sent('POST', '/api/groups/g1/members/import')).toEqual({
        lines: [
          {name: 'Nils Ny', email: 'nils@example.com'},
          {name: 'Olle Ovan', email: 'olle@example.com'},
          {name: 'Siv Student', email: 'siv@example.com'},
          {name: 'Lars Lärare', email: 'lars@example.com'},
          {name: 'bara ett namn', email: ''},
        ],
      })
      expect(modal().querySelector('[data-test="import-summary"]')?.textContent).toContain('2 av 2 rader')
      expect(modal().querySelectorAll('[data-test="import-report-row"]')).toHaveLength(2)
      expect(roleOf(wrapper, 'Olle Ovan')).toBe('Elev')
    })

    async function addFirstTeacher() {
      await click(modal().querySelector('[data-test="new-course-add-teacher"]'))
      await click(modal().querySelector('[data-test="new-course-teacher-option"]'))
    }

    it('visar vad som återstår för en ny klass', async () => {
      backend({role: 'Admin', courses: [], members: []})
      const {wrapper} = await renderApp('/groups/g1')

      const steps = wrapper.findAll('[data-test="group-setup-step"]').map((step) => step.text())
      expect(steps).toEqual(['Lägg till kurser', 'Lägg in elever', 'Utse mentor'])
    })

    it('fyller i formuläret från en tidigare kurs och skapar allt i ett anrop', async () => {
      backend({role: 'Admin', courses: []})
      const {wrapper} = await renderApp('/groups/g1')

      await click(wrapper.find('[data-test="new-course-button"]').element)
      await click(modal().querySelector('[data-test="new-course-source"]'))

      const input = (name: string) => modal().querySelector(`[data-test="new-course-${name}"]`) as HTMLInputElement
      expect(input('title').value).not.toBe('')
      expect(input('code').value).toContain('TE22A_')
      const pickers = wrapper.findAllComponents(DatePicker)
      pickers[0].vm.$emit('update:modelValue', '2026-11-02')
      pickers[1].vm.$emit('update:modelValue', '2026-12-18')
      await flushPromises()
      await addFirstTeacher()
      await click(modal().querySelector('[data-test="save-new-course"]'))

      const paths = requests.filter((request) => request.method === 'POST').map((request) => request.path)
      expect(paths).toHaveLength(1)
      expect(paths[0]).toMatch(/\/api\/courses\/[^/]+\/runs$/)
      expect(wrapper.find('[data-test="group-notice"]').text()).toContain('skapades')
    })

    async function fillNewCourse(wrapper: Awaited<ReturnType<typeof renderApp>>['wrapper'], title: string, code: string) {
      await click(wrapper.find('[data-test="new-course-button"]').element)
      await click(modal().querySelector('[data-test="new-course-empty"]'))
      await type('[data-test="new-course-title"]', title)
      await type('[data-test="new-course-code"]', code)
      const pickers = wrapper.findAllComponents(DatePicker)
      pickers[0].vm.$emit('update:modelValue', '2026-11-02')
      pickers[1].vm.$emit('update:modelValue', '2026-12-18')
      await flushPromises()
    }

    it('skapar kursen, kopplar den till klassen och lägger till lärarna i ordning', async () => {
      backend({role: 'Admin', courses: []})
      const {wrapper} = await renderApp('/groups/g1')

      await fillNewCourse(wrapper, 'Testning', 'TEST25')
      await addFirstTeacher()
      await addFirstTeacher()
      await click(modal().querySelector('[data-test="save-new-course"]'))

      expect(requests.filter((request) => request.method === 'POST').map((request) => request.path)).toEqual([
        '/api/courses',
        '/api/groups/g1/courses/c9',
        '/api/courses/c9/teachers',
        '/api/courses/c9/teachers',
      ])
      expect(sent('POST', '/api/courses')).toEqual({
        schoolId: 's1', title: 'Testning', code: 'TEST25', points: null, startsOn: '2026-11-02', endsOn: '2026-12-18',
      })
      expect(modal()).toBeNull()
    })

    it('kräver namn, kod, datum och lärare innan något skickas', async () => {
      backend({role: 'Admin'})
      const {wrapper} = await renderApp('/groups/g1')

      await click(wrapper.find('[data-test="new-course-button"]').element)
      await click(modal().querySelector('[data-test="new-course-empty"]'))
      await click(modal().querySelector('[data-test="save-new-course"]'))

      for (const name of ['title', 'code', 'dates', 'teacher']) {
        expect(modal().querySelector(`[data-test="new-course-${name}-error"]`)).not.toBeNull()
      }
      expect(requests.some((request) => request.method === 'POST')).toBe(false)
    })

    it('säger vilket steg som misslyckades och fortsätter vid nytt försök utan att skapa kursen igen', async () => {
      backend({role: 'Admin', courses: [], writeStatus: undefined})
      const {wrapper} = await renderApp('/groups/g1')
      let fail = true
      const original = globalThis.fetch
      vi.stubGlobal('fetch', async (input: Request | string, init?: RequestInit) => {
        const request = input instanceof Request
          ? input
          : new Request(new URL(input, window.location.origin), init)
        if (fail && request.method === 'POST' && request.url.endsWith('/api/courses/c9/teachers')) {
          fail = false
          return new Response(JSON.stringify('Servern svarade inte'), {
            status: 500, headers: {'Content-Type': 'application/json'},
          })
        }
        return original(input, init)
      })

      await fillNewCourse(wrapper, 'Java', 'JAVA25')
      await addFirstTeacher()
      await click(modal().querySelector('[data-test="save-new-course"]'))

      const message = modal().querySelector('[data-test="new-course-error"]')?.textContent ?? ''
      expect(message).toContain('Kursen Java skapades och kopplades till TE22A, men Lars Lärare kunde inte läggas till som lärare.')

      await click(modal().querySelector('[data-test="save-new-course"]'))

      expect(requests.filter((request) => request.method === 'POST' && request.path === '/api/courses')).toHaveLength(1)
      expect(requests.filter((request) => request.path === '/api/groups/g1/courses/c9')).toHaveLength(1)
      expect(requests.filter((request) => request.method === 'POST' && request.path === '/api/courses/c9/teachers')).toHaveLength(1)
      expect(modal()).toBeNull()
    })
  })
})
