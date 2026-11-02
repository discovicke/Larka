import {type APIRequestContext, expect, type Page} from '@playwright/test'

import type {StoredToken} from './auth'

// Hjälpmedel för journeys. Varje test bygger sin egen värld med unika namn via API:et (en klass, en lärare, elever
// och en kurs), så att testen inte beror på demodatan eller på varandra. Själva flödet som testet handlar om går
// alltid genom gränssnittet.

export const demoSchoolId = '00000000-0000-0000-0000-00000000000a'
export const password = 'hemlig-e2e-1'

// Ett kort unikt tecken-block för namn och e-post.
export function uid(): string {
  return `${Date.now().toString(36)}${Math.random().toString(36).slice(2, 6)}`
}

export async function json<T>(response: {ok(): boolean, status(): number, text(): Promise<string>, url(): string}): Promise<T> {
  if (!response.ok()) {
    throw new Error(`API-anrop misslyckades: ${response.url()} → ${response.status()} ${await response.text()}`)
  }
  const text = await response.text()
  return (text
    ? JSON.parse(text)
    : undefined) as T
}

export interface Person {
  id: string
  name: string
  email: string
}

export interface World {
  mentor?: Person
  tag: string
  group: {id: string, name: string}
  course: {id: string, title: string}
  teacher: Person
  students: Person[]
}

export async function createUser(
  admin: APIRequestContext,
  name: string,
  role: 'Teacher' | 'Student' | 'Admin',
  withPassword = true,
): Promise<Person> {
  const email = `${name.toLowerCase().replaceAll(/[^a-z0-9]+/g, '.')}@e2e.test`
  const user = await json<{id: string}>(await admin.post('/api/users', {
    data: {schoolId: demoSchoolId, name, email, role, password: withPassword
      ? password
      : null},
  }))
  return {id: user.id, name, email}
}

// Loggar in med API:et och ger tokenen i samma form som global-setup sparar den.
export async function login(request: APIRequestContext, username: string, secret = password): Promise<StoredToken> {
  return await json<StoredToken>(await request.post('/api/auth/login', {data: {username, password: secret}}))
}

export interface WorldOptions {
  students?: number
  // En mentor för klassen. Mentor är en roll i klassen, så kontot är ett lärarkonto.
  mentor?: boolean
  // Så många dagar från idag börjar kursen. Standard: två veckor sedan.
  courseStartsInDays?: number
}

// En klass med elever, en lärare och en kurs som är kopplad till klassen och undervisas av läraren.
export async function createWorld(admin: APIRequestContext, options: WorldOptions = {}): Promise<World> {
  const tag = uid()
  const group = await json<{id: string, name: string}>(await admin.post('/api/groups', {
    data: {schoolId: demoSchoolId, type: 'Class', name: `E2E ${tag}`, term: 'HT26'},
  }))
  const teacher = await createUser(admin, `Lärare ${tag}`, 'Teacher')
  const students: Person[] = []
  for (let index = 1; index <= (options.students ?? 1); index++) {
    const student = await createUser(admin, `Elev${index} ${tag}`, 'Student')
    await json(await admin.post(`/api/groups/${group.id}/members`, {data: {userId: student.id, role: 'Student'}}))
    students.push(student)
  }
  const course = await json<{id: string, title: string}>(await admin.post('/api/courses', {
    data: {
      schoolId: demoSchoolId,
      title: `Kurs ${tag}`,
      code: `E2E-${tag}`.toUpperCase(),
      points: 25,
      startsOn: isoDay(options.courseStartsInDays ?? -14),
      endsOn: isoDay(60),
    },
  }))
  await json(await admin.post(`/api/groups/${group.id}/courses/${course.id}`))
  await json(await admin.post(`/api/courses/${course.id}/teachers`, {data: {userId: teacher.id}}))
  let mentor: Person | undefined
  if (options.mentor) {
    mentor = await createUser(admin, `Mentor ${tag}`, 'Teacher')
    await json(await admin.post(`/api/groups/${group.id}/members`, {data: {userId: mentor.id, role: 'Mentor'}}))
  }
  return {tag, group, course, teacher, students, mentor}
}

// Datum som ÅÅÅÅ-MM-DD, så många dagar från idag.
export function isoDay(offsetDays: number): string {
  const date = new Date(Date.now() + offsetDays * 86_400_000)
  return date.toISOString().slice(0, 10)
}

export function isoAt(offsetDays: number): string {
  return new Date(Date.now() + offsetDays * 86_400_000).toISOString()
}

export interface Part {
  contentId: string
  linkId: string
  moduleId: string
  title: string
}

export async function createModule(teacher: APIRequestContext, courseId: string, title: string, publishAt?: string) {
  return await json<{id: string, title: string}>(await teacher.post(`/api/courses/${courseId}/modules`, {
    data: {title, publishAt: publishAt ?? isoAt(-7)},
  }))
}

interface PartOptions {
  type?: 'Theory' | 'Assignment' | 'News'
  body?: string
  dueInDays?: number
  published?: boolean
  gradeScale?: 'None' | 'PassFail' | 'PassFailDistinction'
}

// En del i en vecka, som läraren hade skapat den. Uppgifter lämnas in som text.
export async function createPart(
  teacher: APIRequestContext,
  courseId: string,
  moduleId: string,
  title: string,
  options: PartOptions = {},
): Promise<Part> {
  const type = options.type ?? 'Theory'
  const created = await json<{content: {id: string}, link: {id: string}}>(await teacher.post('/api/content/with-link', {
    data: {
      content: {
        type,
        title,
        bodyMd: options.body ?? `Text för ${title}`,
        status: options.published === false
          ? 'Draft'
          : 'Published',
        ...(type === 'Assignment'
          ? {gradeScale: options.gradeScale ?? 'PassFailDistinction', submissionModes: ['Text']}
          : {}),
      },
      link: {
        courseId,
        moduleId,
        publishAt: isoAt(-1),
        ...(options.dueInDays === undefined
          ? {}
          : {dueAt: isoAt(options.dueInDays)}),
      },
    },
  }))
  return {contentId: created.content.id, linkId: created.link.id, moduleId, title}
}

// Eleven lämnar in en text via API:et. För data som ett test behöver men inte handlar om.
export async function submitAs(student: APIRequestContext, linkId: string, text: string) {
  return await json<{id: string}>(await student.post(`/api/links/${linkId}/submissions`, {multipart: {bodyMd: text}}))
}

export async function grade(
  teacher: APIRequestContext,
  submissionId: string,
  feedback: string,
  level: 'Pass' | 'Distinction' | 'Fail' | null,
  needsWork = false,
) {
  return await json(await teacher.post(`/api/submissions/${submissionId}/grade`, {data: {feedback, level, needsWork}}))
}

// Skriver i redigeraren (TipTap) som ett id eller en etikett pekar ut.
export async function typeInEditor(page: Page, text: string, scope = page.locator('.tiptap, [contenteditable="true"]').first()) {
  await scope.click()
  await page.keyboard.type(text)
  await expect(scope).toContainText(text)
}

// Väljer en dag (1-28) i månaden som kalendern visar, i en datumväljare vars knapp har ett id.
export async function pickDay(page: Page, buttonId: string, day: number) {
  await page.locator(`#${buttonId}`).click()
  const cell = page.locator('[data-reka-calendar-cell-trigger]:not([data-outside-view])').getByText(String(day), {exact: true})
  await cell.click()
  await page.keyboard.press('Escape')
}

// En kurs utan klass och med en lärare, för flöden där admin kopplar den själv.
export async function createLooseCourse(admin: APIRequestContext, tag: string, teacher: Person) {
  const course = await json<{id: string, title: string}>(await admin.post('/api/courses', {
    data: {schoolId: demoSchoolId, title: `Lös kurs ${tag}`, code: `LOS-${tag}`.toUpperCase(), points: 10, startsOn: isoDay(-5), endsOn: isoDay(30)},
  }))
  await json(await admin.post(`/api/courses/${course.id}/teachers`, {data: {userId: teacher.id}}))
  return course
}
