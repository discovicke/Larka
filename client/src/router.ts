import {
  createRouter,
  createWebHistory,
  type RouteLocationNormalizedLoaded,
  type RouteLocationRaw,
  type RouteRecordRaw,
  type RouterHistory,
} from 'vue-router'

import {useCourses} from '@/composables/useCourses'
import {useCrumbLabels} from '@/composables/useCrumbLabels'
import {useCurrentUser} from '@/composables/useCurrentUser'
import AppLayout from '@/layouts/AppLayout.vue'
import {auth} from '@/lib/auth'
import type {Action} from '@/lib/permissions'
import CalendarView from '@/views/CalendarView.vue'
import GradesView from '@/views/GradesView.vue'
import GradingView from '@/views/GradingView.vue'
import GroupView from '@/views/GroupView.vue'
import GroupsView from '@/views/GroupsView.vue'
import InboxView from '@/views/InboxView.vue'
import LoginView from '@/views/LoginView.vue'
import MaterialView from '@/views/MaterialView.vue'
import NotFoundView from '@/views/NotFoundView.vue'
import SettingsView from '@/views/SettingsView.vue'
import TodayView from '@/views/TodayView.vue'
import CourseAboutView from '@/views/course/CourseAboutView.vue'
import CourseContentPage from '@/views/course/CourseContentPage.vue'
import CourseLayout from '@/views/course/CourseLayout.vue'
import CourseNewsView from '@/views/course/CourseNewsView.vue'
import PartView from '@/views/course/PartView.vue'

export interface Breadcrumb {
  label: string
  to?: RouteLocationRaw
}

declare module 'vue-router' {
  interface RouteMeta {
    // Sidans namn, används i fliken i webbläsaren och som brödsmula om breadcrumbs saknas.
    title?: string
    breadcrumbs?: (route: RouteLocationNormalizedLoaded) => Breadcrumb[]
    // Sidan fyller ytan under headern, utan padding och maxbredd.
    fullBleed?: boolean
    // Behörigheten som krävs för sidan, t.ex. 'grade'. Kontrolleras i beforeEach med can().
    requires?: Action
    // Sidan kräver ingen inloggning, t.ex. själva inloggningssidan.
    public?: boolean
  }
}

function courseBreadcrumbs(route: RouteLocationNormalizedLoaded, ...rest: Breadcrumb[]): Breadcrumb[] {
  const courseId = String(route.params.courseId)
  const course: Breadcrumb = {
    label: useCourses().findCourse(courseId)?.title ?? 'Kurs',
    to: {name: 'course-content', params: {courseId}},
  }
  return [{label: 'Kurser', to: {name: 'groups', query: {filter: 'courses'}}}, course, ...rest]
}

// Uppgiften och inlämningarna ligger under kursen: uppgiften öppnar delens sida.
function assignmentBreadcrumbs(route: RouteLocationNormalizedLoaded, ...rest: Breadcrumb[]): Breadcrumb[] {
  const {courseId, linkId} = route.params
  return courseBreadcrumbs(
    route,
    {
      label: useCrumbLabels().crumbLabel(`assignment:${linkId}`, 'Uppgift'),
      to: {name: 'part', params: {courseId, linkId}},
    },
    ...rest,
  )
}

export const routes: RouteRecordRaw[] = [
  {
    path: '/',
    name: 'app',
    component: AppLayout,
    children: [
      {path: '', name: 'today', component: TodayView, meta: {title: 'Idag'}},
      {path: 'calendar', name: 'calendar', component: CalendarView, meta: {title: 'Kalender'}},
      {path: 'inbox', name: 'inbox', component: InboxView, meta: {title: 'Inkorg', fullBleed: true}},
      {path: 'material', name: 'material', component: MaterialView, meta: {title: 'Material'}},
      {path: 'grades', name: 'grades', component: GradesView, meta: {title: 'Studieplan och betyg'}},
      {path: 'grading', name: 'grading', component: GradingView, meta: {title: 'Att bedöma', requires: 'grade'}},
      {path: 'groups', name: 'groups', component: GroupsView, meta: {title: 'Alla grupper'}},
      {
        path: 'groups/:groupId',
        name: 'group',
        component: GroupView,
        meta: {
          title: 'Grupp',
          breadcrumbs: (route) => [
            {label: 'Alla grupper', to: {name: 'groups'}},
            {label: useCrumbLabels().crumbLabel(`group:${route.params.groupId}`, 'Grupp')},
          ],
        },
      },
      {path: 'settings', name: 'settings', component: SettingsView, meta: {title: 'Inställningar'}},
      // Program och klasser finns inte längre: admin hanterar klasser och grupper på Alla grupper.
      {path: 'settings/groups', redirect: {name: 'groups'}},
      {
        path: 'courses/:courseId',
        component: CourseLayout,
        children: [
          {path: '', redirect: (to) => ({name: 'course-content', params: to.params})},
          {
            path: 'content',
            name: 'course-content',
            component: CourseContentPage,
            // Innehåll är kursens startsida, så brödsmulan slutar på kursen.
            meta: {title: 'Innehåll', breadcrumbs: (route) => courseBreadcrumbs(route)},
          },
          {
            path: 'news',
            name: 'course-news',
            component: CourseNewsView,
            meta: {title: 'Nyheter', breadcrumbs: (route) => courseBreadcrumbs(route, {label: 'Nyheter'})},
          },
          {
            path: 'staff',
            name: 'course-staff',
            // Personal är en del av Om. Gamla länkar landar på personalen.
            redirect: (to) => ({name: 'course-about', params: to.params, hash: '#personal'}),
          },
          {
            path: 'about',
            name: 'course-about',
            component: CourseAboutView,
            meta: {title: 'Om', breadcrumbs: (route) => courseBreadcrumbs(route, {label: 'Om'})},
          },
        ],
      },
      {
        path: 'courses/:courseId/modules/:moduleId/new',
        name: 'create-content',
        // Laddas först när sidan öppnas, så att editorn inte hamnar i resten av appen.
        component: () => import('@/views/course/CreateContentView.vue'),
        meta: {
          title: 'Ny del',
          requires: 'editContent',
          breadcrumbs: (route) => courseBreadcrumbs(
            route,
            {label: 'Innehåll', to: {name: 'course-content', params: {courseId: route.params.courseId}}},
            {label: 'Ny del'},
          ),
        },
      },
      {
        path: 'courses/:courseId/modules/:moduleId/content/:linkId/edit',
        name: 'edit-content',
        component: () => import('@/views/course/CreateContentView.vue'),
        meta: {
          title: 'Redigera del',
          requires: 'editContent',
          breadcrumbs: (route) => courseBreadcrumbs(
            route,
            {label: 'Innehåll', to: {name: 'course-content', params: {courseId: route.params.courseId}}},
            {label: 'Redigera del'},
          ),
        },
      },
      {
        path: 'courses/:courseId/parts/:linkId',
        name: 'part',
        component: PartView,
        meta: {
          title: 'Del',
          breadcrumbs: (route) => courseBreadcrumbs(route, {
            label: useCrumbLabels().crumbLabel(`assignment:${route.params.linkId}`, 'Del'),
          }),
        },
      },
      // Inlämningarna är en flik på delens sida.
      {
        path: 'courses/:courseId/assignments/:linkId',
        name: 'assignment-submissions',
        redirect: (to) => ({name: 'part', params: to.params, query: {tab: 'inlamningar'}}),
      },
      {
        path: 'courses/:courseId/assignments/:linkId/submissions/:submissionId',
        name: 'submission-review',
        // Laddas först när sidan öppnas, som redigeringssidan.
        component: () => import('@/views/course/ReviewView.vue'),
        meta: {
          title: 'Bedöm',
          fullBleed: true,
          requires: 'readSubmissions',
          breadcrumbs: (route) => assignmentBreadcrumbs(
            route,
            {
              label: 'Inlämningar',
              to: {
                name: 'assignment-submissions',
                params: {courseId: route.params.courseId, linkId: route.params.linkId},
              },
            },
            {label: useCrumbLabels().crumbLabel(`student:${route.params.submissionId}`, 'Inlämning')},
          ),
        },
      },
      {path: ':pathMatch(.*)*', name: 'not-found', component: NotFoundView, meta: {title: 'Sidan finns inte'}},
    ],
  },
  // Inloggningen ligger utanför layouten: sidomenyn och headern kräver en inloggad användare.
  {path: '/login', name: 'login', component: LoginView, meta: {title: 'Logga in', public: true}},
]

// Bara inloggade får se sidorna. Den som inte är inloggad skickas till inloggningen, med sidan i redirect.
function authGuard(to: RouteLocationNormalizedLoaded): RouteLocationRaw | true {
  if (to.meta.public) {
    return to.name === 'login' && auth.isSignedIn()
      ? {name: 'today'}
      : true
  }
  // Sidan man ville till följer med, så att den öppnas igen efter inloggningen.
  return auth.isSignedIn()
    ? true
    : {name: 'login', query: {redirect: to.fullPath}}
}

export function createAppRouter(history: RouterHistory = createWebHistory()) {
  const router = createRouter({history, routes})
  router.beforeEach(authGuard)
  // Sidor som kräver en behörighet kontrolleras först när användaren är hämtad.
  // Svarar /api/me inte släpps sidan igenom: API:et svarar ändå 403 för den som inte får, och
  // ett tillfälligt fel ska inte låsa ute alla.
  router.beforeEach(async (to) => {
    const {requires} = to.meta
    if (!requires) {
      return true
    }

    const {status, loadMe, can} = useCurrentUser()
    if (status.value !== 'loaded') {
      await loadMe()
    }
    if (status.value === 'failed') {
      return true
    }

    const courseId = typeof to.params.courseId === 'string'
      ? to.params.courseId
      : undefined
    return can(requires, courseId)
      ? true
      : {name: 'today'}
  })
  router.afterEach((to) => {
    document.title = to.meta.title
      ? `${to.meta.title} · Lärka`
      : 'Lärka'
  })
  return router
}

export const router = createAppRouter()
