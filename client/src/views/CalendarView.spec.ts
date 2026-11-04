import {flushPromises} from '@vue/test-utils'
import {afterEach, beforeEach, describe, expect, it, vi} from 'vitest'

import type {Schema} from '@/lib/api'
import {mockApi, respond} from '@/test/mockApi'
import {meAs} from '@/test/me'
import {renderApp} from '@/test/renderApp'

const devops: Schema<'CourseDto'> = {id: 'c1', schoolId: 's1', code: 'DEVOPS', title: 'DevOps', description: '', points: null, startsOn: null, endsOn: null}
const databases: Schema<'CourseDto'> = {id: 'c2', schoolId: 's1', code: 'DB', title: 'Databaser', description: '', points: null, startsOn: null, endsOn: null}

// Lokala tider, så att testet inte beror på tidszonen.
function local(month: number, day: number, hour = 23, minute = 59) {
  return new Date(2026, month - 1, day, hour, minute).toISOString()
}

function item(linkId: string, title: string, at: string, extra: Partial<Schema<'CalendarItemDto'>> = {}): Schema<'CalendarItemDto'> {
  return {
    linkId,
    contentId: `x-${linkId}`,
    title,
    contentType: 'Assignment',
    courseId: 'c1',
    courseTitle: 'DevOps',
    groupId: null,
    kind: 'Deadline',
    at,
    ...extra,
  }
}

const thisWeek = [
  item('l1', 'Release-pipeline', local(10, 23)),
  item('l2', 'Normalisering', local(10, 21, 12, 0), {courseId: 'c2', courseTitle: 'Databaser'}),
  item('l3', 'Teori vecka 44', local(10, 22, 7, 0), {contentType: 'Theory', kind: 'Publish'}),
]

let calendarUrls: URL[] = []

function backend(options: {calendar?: Schema<'CalendarItemDto'>[], fail?: boolean, courses?: Schema<'CourseDto'>[], role?: 'Student' | 'Teacher'} = {}) {
  mockApi((_method, path) => {
    if (path === '/api/me') {
      return respond<'/api/me', 'get'>(meAs(options.role ?? 'Student', ['c1', 'c2']))
    }
    if (path === '/api/courses') {
      return respond<'/api/courses', 'get'>(options.courses ?? [devops, databases])
    }
    if (path === '/api/calendar') {
      return options.fail
        ? {status: 500, body: 'Server error'}
        : respond<'/api/calendar', 'get'>(options.calendar ?? [])
    }
    return {status: 404}
  })
  // mockApi sparar bara sökvägen, så frågeparametrarna fångas här.
  const inner = globalThis.fetch
  calendarUrls = []
  vi.stubGlobal('fetch', (input: Request | string, init?: RequestInit) => {
    const url = new URL(input instanceof Request
      ? input.url
      : input, window.location.origin)
    if (url.pathname === '/api/calendar') {
      calendarUrls.push(url)
    }
    return inner(input, init)
  })
}

function chipTexts(wrapper: Awaited<ReturnType<typeof renderApp>>['wrapper'], key: string) {
  return wrapper.findAll(`[data-test="day-${key}"] [data-test="calendar-chip"]`).map((chip) => chip.text())
}

beforeEach(() => {
  vi.useFakeTimers({toFake: ['Date']})
  vi.setSystemTime(new Date(2026, 9, 21, 12, 0))
})

afterEach(() => {
  vi.useRealTimers()
})

describe('CalendarView', () => {
  it('visar raderna i rätt dag med rubrik och veckonummer', async () => {
    backend({calendar: thisWeek})

    const {wrapper} = await renderApp('/calendar')

    expect(wrapper.find('h1').text()).toContain('19–25 oktober 2026')
    expect(wrapper.find('h1').text()).toContain('vecka 43')
    expect(wrapper.findAll('[data-test="week-grid"] > div')).toHaveLength(7)

    const friday = chipTexts(wrapper, '2026-10-23')
    expect(friday).toHaveLength(1)
    expect(friday[0]).toContain('23:59')
    expect(friday[0]).toContain('Release-pipeline')
    expect(friday[0]).toContain('Deadline')
    expect(chipTexts(wrapper, '2026-10-22')[0]).toContain('Publiceras')
    expect(chipTexts(wrapper, '2026-10-21')[0]).toContain('Normalisering')
    expect(chipTexts(wrapper, '2026-10-19')).toHaveLength(0)
  })

  it('visar kompletteringsdeadlines och elevens egen deadline som deadlines', async () => {
    backend({calendar: [
      item('l1', 'Release-pipeline', local(10, 22, 12, 0), {kind: 'ResubmitDeadline'}),
      item('l1', 'Release-pipeline', local(10, 23), {kind: 'PersonalDeadline'}),
    ]})

    const {wrapper} = await renderApp('/calendar')

    expect(chipTexts(wrapper, '2026-10-22')[0]).toContain('Kompletteringsdeadline')
    expect(chipTexts(wrapper, '2026-10-23')[0]).toContain('Din deadline')
  })

  it('skiljer deadlinetyperna åt med ikon och etikett också i månadsvyn', async () => {
    backend({calendar: [
      item('l1', 'Release-pipeline', local(10, 21), {kind: 'Deadline'}),
      item('l1', 'Release-pipeline', local(10, 22, 12, 0), {kind: 'ResubmitDeadline'}),
      item('l1', 'Release-pipeline', local(10, 23), {kind: 'PersonalDeadline'}),
    ]})
    const {wrapper} = await renderApp('/calendar')
    await wrapper.findAll('[role="tab"]')[1].trigger('click')
    await flushPromises()

    const chip = (key: string) => wrapper.find(`[data-test="day-${key}"] [data-test="calendar-chip"]`)
    expect(chip('2026-10-21').find('[data-test="chip-icon-Deadline"]').exists()).toBe(true)
    expect(chip('2026-10-22').find('[data-test="chip-icon-ResubmitDeadline"]').exists()).toBe(true)
    expect(chip('2026-10-23').find('[data-test="chip-icon-PersonalDeadline"]').exists()).toBe(true)
    expect(chip('2026-10-23').attributes('title')).toBe('Din deadline')
    expect(chip('2026-10-23').find('[data-test="chip-label"]').text()).toContain('Din deadline')
  })

  it('hämtar veckans intervall och nästa vecka när man bläddrar', async () => {
    backend({calendar: thisWeek})
    const {wrapper} = await renderApp('/calendar')

    expect(calendarUrls).toHaveLength(1)
    expect(calendarUrls[0].searchParams.get('from')).toBe(new Date(2026, 9, 19).toISOString())
    expect(calendarUrls[0].searchParams.get('to')).toBe(new Date(2026, 9, 26).toISOString())

    await wrapper.find('button[aria-label="Nästa vecka"]').trigger('click')
    await flushPromises()

    expect(calendarUrls).toHaveLength(2)
    expect(calendarUrls[1].searchParams.get('from')).toBe(new Date(2026, 9, 26).toISOString())
    expect(calendarUrls[1].searchParams.get('to')).toBe(new Date(2026, 10, 2).toISOString())
    expect(wrapper.find('h1').text()).toContain('26 oktober – 1 november 2026')
  })

  it('går tillbaka till idag', async () => {
    backend({calendar: thisWeek})
    const {wrapper} = await renderApp('/calendar')

    await wrapper.find('button[aria-label="Föregående vecka"]').trigger('click')
    await flushPromises()
    expect(wrapper.find('h1').text()).toContain('12–18 oktober 2026')

    const today = wrapper.findAll('button').find((button) => button.text() === 'Idag')
    await today?.trigger('click')
    await flushPromises()
    expect(wrapper.find('h1').text()).toContain('19–25 oktober 2026')
  })

  it('byter till månad och hämtar hela rutnätet', async () => {
    backend({calendar: thisWeek})
    const {wrapper} = await renderApp('/calendar')

    const tabs = wrapper.findAll('[role="tab"]')
    expect(tabs.map((tab) => tab.text())).toEqual(['Vecka', 'Månad'])
    await tabs[1].trigger('click')
    await flushPromises()

    expect(wrapper.find('h1').text()).toContain('oktober 2026')
    expect(wrapper.findAll('[data-test="month-grid"] > div')).toHaveLength(42)
    const last = calendarUrls[calendarUrls.length - 1]
    expect(last.searchParams.get('from')).toBe(new Date(2026, 8, 28).toISOString())
    expect(last.searchParams.get('to')).toBe(new Date(2026, 10, 9).toISOString())
    expect(chipTexts(wrapper, '2026-10-23')[0]).toContain('Release-pipeline')
  })

  it('visar högst tre rader per dag i månaden och sedan +N till', async () => {
    const many = ['a', 'b', 'c', 'd', 'e'].map((name, index) =>
      item(`m${index}`, `Uppgift ${name}`, local(10, 23, 8 + index, 0)))
    backend({calendar: many})
    const {wrapper} = await renderApp('/calendar')

    await wrapper.findAll('[role="tab"]')[1].trigger('click')
    await flushPromises()

    expect(chipTexts(wrapper, '2026-10-23')).toHaveLength(3)
    expect(wrapper.find('[data-test="day-2026-10-23"] [data-test="calendar-more"]').text()).toBe('+2 till')
  })

  it('lägger avslutade kurser sist i en egen grupp i kursväljaren', async () => {
    backend({calendar: thisWeek, courses: [
      {...devops, startsOn: '2020-01-01', endsOn: '2020-06-01'},
      databases,
    ]})
    const {wrapper} = await renderApp('/calendar')

    const select = wrapper.find('#calendar-course')
    expect(select.findAll('option').map((option) => option.text()))
      .toEqual(['Alla kurser', 'Databaser', 'DevOps'])
    expect(select.find('optgroup').attributes('label')).toBe('Avslutade')
    expect(select.findAll('optgroup option').map((option) => option.text())).toEqual(['DevOps'])
  })

  it('filtrerar bort andra kurser med kursväljaren', async () => {
    backend({calendar: thisWeek})
    const {wrapper} = await renderApp('/calendar')

    const select = wrapper.find('#calendar-course')
    expect(select.findAll('option').map((option) => option.text()))
      .toEqual(['Alla kurser', 'DevOps', 'Databaser'])
    await select.setValue('c1')
    await flushPromises()

    expect(chipTexts(wrapper, '2026-10-21')).toHaveLength(0)
    expect(chipTexts(wrapper, '2026-10-23')).toHaveLength(1)
  })

  it('länkar uppgifter till inlämningarna för lärare och annars till kursen', async () => {
    backend({calendar: thisWeek, role: 'Teacher'})
    const teacherPage = await renderApp('/calendar')
    expect(teacherPage.wrapper.find('[data-test="day-2026-10-23"] a').attributes('href'))
      .toBe('/courses/c1/assignments/l1')
    expect(teacherPage.wrapper.find('[data-test="day-2026-10-22"] a').attributes('href'))
      .toMatch(/^\/courses\/c1\/content/)

    backend({calendar: thisWeek, role: 'Student'})
    const studentPage = await renderApp('/calendar')
    expect(studentPage.wrapper.find('[data-test="day-2026-10-23"] a').attributes('href'))
      .toMatch(/^\/courses\/c1\/content\?del=[\w-]+$/)
  })

  it('visar tomt-läge när inget finns', async () => {
    backend({calendar: []})

    const {wrapper} = await renderApp('/calendar')

    expect(wrapper.text()).toContain('Inget i kalendern den här veckan')

    await wrapper.findAll('[role="tab"]')[1].trigger('click')
    await flushPromises()
    expect(wrapper.text()).toContain('Inget i kalendern den här månaden')
  })

  it('visar fel när hämtningen misslyckas', async () => {
    backend({fail: true})

    const {wrapper} = await renderApp('/calendar')

    expect(wrapper.text()).toContain('Kunde inte hämta kalendern.')
    expect(wrapper.find('[data-test="week-grid"]').exists()).toBe(false)
  })
})
