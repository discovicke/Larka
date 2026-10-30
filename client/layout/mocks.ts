import type {Page} from '@playwright/test'

import type {Schema} from '../src/lib/api'
import {meAs} from '../src/test/me'

// Samma nycklar som klienten läser i sessionStorage (src/lib/auth/localAuth.ts).
const AUTH_TOKEN_KEY = 'larka.token'
const AUTH_EXPIRY_KEY = 'larka.expiresAt'

const course: Schema<'CourseDto'> = {
  id: 'c1',
  schoolId: 's1',
  code: 'DEVOPS',
  title: 'DevOps',
  description: 'Bygg, testa och släpp en tjänst.',
  points: 200,
  startsOn: '2026-08-24',
  endsOn: '2027-06-04',
}

function part(linkId: string, type: 'Theory' | 'Assignment', title: string, gradeScale: string | null) {
  return {
    linkId,
    contentId: `x-${linkId}`,
    type,
    gradeScale,
    title,
    status: 'Published',
    groupId: null,
    publishAt: '2026-10-20T08:00:00Z',
    dueAt: type === 'Assignment'
      ? '2026-10-23T21:59:00Z'
      : null,
    isPinned: false,
    blockedBy: [],
    hasStudentActivity: false,
  }
}

const modules = [
  {
    id: 'm1',
    courseId: 'c1',
    title: 'Vecka 2 – Release-pipelines och en ganska lång rubrik som får plats på en telefon',
    description: 'Vi går igenom hur man bygger, testar och släpper en tjänst.',
    position: 1,
    publishAt: null,
    content: [
      part('l1', 'Theory', 'Introduktion till continuous delivery', null),
      part('l2', 'Assignment', 'Övning: bygg en pipeline', null),
      part('l3', 'Assignment', 'Inlämning: release-pipeline med ett långt namn som ska brytas', 'PassFailDistinction'),
    ],
  },
  {id: 'm2', courseId: 'c1', title: 'Vecka 3 – Övervakning', description: '', position: 2, publishAt: null, content: []},
]

// Förra omgångens kurs, som veckor kopieras från.
const previousCourse: Schema<'CourseDto'> = {...course, id: 'c2', code: 'DEVOPS25', title: 'DevOps HT25 med ett långt namn'}
const previousModules = modules.map((module) => ({
  ...module,
  id: `${module.id}-old`,
  courseId: 'c2',
  title: `${module.title} (förra omgången)`,
}))

// Klasser i skolan som en ny kursomgång kan skapas för.
const groups: Schema<'GroupDto'>[] = [
  {id: 'g1', schoolId: 's1', type: 'Class', name: 'NET25', term: 'HT25', memberCount: 24},
  {id: 'g2', schoolId: 's1', type: 'Class', name: 'NET26 med ett långt klassnamn som får plats', term: 'HT26', memberCount: 18},
]

function studentPart(linkId: string, title: string, overrides: Record<string, unknown> = {}) {
  return {
    linkId,
    contentId: `x-${linkId}`,
    type: 'Theory',
    gradeScale: null,
    title,
    bodyMd: 'Läs igenom kapitlet.',
    publishAt: '2026-10-20T08:00:00Z',
    dueAt: null,
    isPinned: false,
    isLocked: false,
    isCompleted: false,
    canMarkDone: true,
    blockedBy: [],
    submissionStatus: null,
    gradeLevel: null,
    submissionModes: [],
    ...overrides,
  }
}

// Elevens vy av kursen: en klar del, en som kan göras nu och en som är låst, plus en framtida vecka.
const studentModules = [
  {
    id: 'm1',
    courseId: 'c1',
    title: 'Vecka 2 – Release-pipelines och en ganska lång rubrik som får plats på en telefon',
    description: 'Vi går igenom hur man bygger, testar och släpper en tjänst.',
    position: 1,
    publishAt: null,
    isAvailable: true,
    content: [
      studentPart('l1', 'Introduktion till continuous delivery', {isCompleted: true}),
      studentPart('l2', 'Övning: bygg en pipeline med ett långt namn som ska brytas'),
      studentPart('l3', 'Inlämning: release-pipeline', {
        type: 'Assignment',
        gradeScale: 'PassFailDistinction',
        dueAt: '2026-10-23T21:59:00Z',
        canMarkDone: false,
        isLocked: true,
        bodyMd: null,
        blockedBy: [{linkId: 'l2', title: 'Övning: bygg en pipeline', isSatisfied: false}],
      }),
    ],
  },
  {
    id: 'm2',
    courseId: 'c1',
    title: 'Vecka 3 – Övervakning',
    description: '',
    position: 2,
    publishAt: '2026-11-03T08:00:00Z',
    isAvailable: false,
    content: [],
  },
]

/** Sidor i appen. Alla finns bakom inloggning och hämtar sina data från mockarna nedan. */
export const routes = {
  today: '/',
  courseContent: '/courses/c1/content',
  createContent: '/courses/c1/modules/m1/new',
  notFound: '/finns-inte',
} as const

/**
 * Loggar in en låtsasanvändare (lärare eller elev i kursen c1) och svarar på /api/** med fasta data, så att
 * sidorna renderas som i verkligheten utan backend. Okända anrop får en tom lista.
 */
export async function signInAndMockApi(page: Page, role: 'Admin' | 'Teacher' | 'Student' = 'Teacher'): Promise<void> {
  await page.addInitScript(
    ([tokenKey, expiryKey, expiry]) => {
      sessionStorage.setItem(tokenKey, 'test-token')
      sessionStorage.setItem(expiryKey, expiry)
    },
    [AUTH_TOKEN_KEY, AUTH_EXPIRY_KEY, String(Date.now() + 60 * 60 * 1000)],
  )

  await page.route('**/api/**', async (route) => {
    const {pathname} = new URL(route.request().url())
    const json = (body: unknown) => route.fulfill({status: 200, contentType: 'application/json', json: body})
    if (route.request().method() !== 'GET') {
      return route.fulfill({status: 204})
    }
    switch (pathname) {
      case '/api/me':
        return json(meAs(role, ['c1', 'c2']))
      case '/api/me/notification-settings':
        return json({muted: []})
      case '/api/courses':
        return json([course, previousCourse])
      case '/api/groups':
        return json(groups)
      // Klasserna läser kurserna läraren undervisar i, så nya kursomgångar kan läggas i dem.
      case '/api/groups/g1/courses':
      case '/api/groups/g2/courses':
        return json([course, previousCourse])
      case '/api/courses/c1/modules':
        return json(modules)
      case '/api/courses/c1/student-modules':
        return json(studentModules)
      case '/api/courses/c2/modules':
        return json(previousModules)
      default:
        return json([])
    }
  })
}
