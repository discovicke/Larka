import {
  createRouter,
  createWebHistory,
  type RouteLocationNormalizedLoaded,
  type RouteLocationRaw,
  type RouteRecordRaw,
  type RouterHistory,
} from 'vue-router'

import {useCourses} from '@/composables/useCourses'
import AppLayout from '@/layouts/AppLayout.vue'
import NotFoundView from '@/views/NotFoundView.vue'
import PlaceholderView from '@/views/PlaceholderView.vue'
import CourseContentView from '@/views/course/CourseContentView.vue'
import CourseLayout from '@/views/course/CourseLayout.vue'
import CourseNewsView from '@/views/course/CourseNewsView.vue'

export interface Breadcrumb {
  label: string
  to?: RouteLocationRaw
}

declare module 'vue-router' {
  interface RouteMeta {
    // Sidans namn, används i fliken i webbläsaren och som brödsmula om breadcrumbs saknas.
    title?: string
    breadcrumbs?: (route: RouteLocationNormalizedLoaded) => Breadcrumb[]
  }
}

function courseBreadcrumbs(route: RouteLocationNormalizedLoaded, ...rest: Breadcrumb[]): Breadcrumb[] {
  const courseId = String(route.params.courseId)
  const course: Breadcrumb = {
    label: useCourses().findCourse(courseId)?.title ?? 'Kurs',
    to: {name: 'course-content', params: {courseId}},
  }
  return [{label: 'Kurser'}, course, ...rest]
}

function courseTab(path: string, name: string, title: string): RouteRecordRaw {
  return {
    path,
    name,
    component: PlaceholderView,
    props: {nested: true},
    meta: {title, breadcrumbs: (route) => courseBreadcrumbs(route, {label: title})},
  }
}

export const routes: RouteRecordRaw[] = [
  {
    path: '/',
    name: 'app',
    component: AppLayout,
    children: [
      {path: '', name: 'today', component: PlaceholderView, meta: {title: 'Idag'}},
      {path: 'calendar', name: 'calendar', component: PlaceholderView, meta: {title: 'Kalender'}},
      {path: 'inbox', name: 'inbox', component: PlaceholderView, meta: {title: 'Inkorg'}},
      {path: 'grading', name: 'grading', component: PlaceholderView, meta: {title: 'Att bedöma'}},
      {
        path: 'courses/:courseId',
        component: CourseLayout,
        children: [
          {path: '', redirect: (to) => ({name: 'course-content', params: to.params})},
          {
            path: 'content',
            name: 'course-content',
            component: CourseContentView,
            // Innehåll är kursens startsida, så brödsmulan slutar på kursen.
            meta: {title: 'Innehåll', breadcrumbs: (route) => courseBreadcrumbs(route)},
          },
          {
            path: 'news',
            name: 'course-news',
            component: CourseNewsView,
            meta: {title: 'Nyheter', breadcrumbs: (route) => courseBreadcrumbs(route, {label: 'Nyheter'})},
          },
          courseTab('channels', 'course-channels', 'Kanaler'),
          courseTab('staff', 'course-staff', 'Personal'),
          courseTab('about', 'course-about', 'Om'),
        ],
      },
      {
        path: 'courses/:courseId/modules/:moduleId/new',
        name: 'create-content',
        // Laddas först när sidan öppnas, så att editorn inte hamnar i resten av appen.
        component: () => import('@/views/course/CreateContentView.vue'),
        meta: {
          title: 'Ny del',
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
          breadcrumbs: (route) => courseBreadcrumbs(
            route,
            {label: 'Innehåll', to: {name: 'course-content', params: {courseId: route.params.courseId}}},
            {label: 'Redigera del'},
          ),
        },
      },
      {path: ':pathMatch(.*)*', name: 'not-found', component: NotFoundView, meta: {title: 'Sidan finns inte'}},
    ],
  },
  // Verktyg för att skapa testdata. Länkas inte i navigationen och ligger utanför layouten.
  {path: '/dev', name: 'dev', component: () => import('@/views/DevView.vue'), meta: {title: 'Testdata'}},
]

export function createAppRouter(history: RouterHistory = createWebHistory()) {
  const router = createRouter({history, routes})
  router.afterEach((to) => {
    document.title = to.meta.title
      ? `${to.meta.title} · RebornLMS`
      : 'RebornLMS'
  })
  return router
}

export const router = createAppRouter()
