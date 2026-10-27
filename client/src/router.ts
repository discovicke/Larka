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
import GradingView from '@/views/GradingView.vue'
import GroupsView from '@/views/GroupsView.vue'
import InboxView from '@/views/InboxView.vue'
import MaterialView from '@/views/MaterialView.vue'
import NotFoundView from '@/views/NotFoundView.vue'
import PlaceholderView from '@/views/PlaceholderView.vue'
import SettingsView from '@/views/SettingsView.vue'
import CourseContentView from '@/views/course/CourseContentView.vue'
import CourseLayout from '@/views/course/CourseLayout.vue'
import CourseNewsView from '@/views/course/CourseNewsView.vue'
import SubmissionsView from '@/views/course/SubmissionsView.vue'

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
      {path: 'inbox', name: 'inbox', component: InboxView, meta: {title: 'Inkorg', fullBleed: true}},
      {path: 'material', name: 'material', component: MaterialView, meta: {title: 'Material'}},
      {path: 'grading', name: 'grading', component: GradingView, meta: {title: 'Att bedöma'}},
      {path: 'groups', name: 'groups', component: GroupsView, meta: {title: 'Alla grupper'}},
      {path: 'settings', name: 'settings', component: SettingsView, meta: {title: 'Inställningar'}},
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
      {
        path: 'courses/:courseId/assignments/:linkId',
        name: 'assignment-submissions',
        component: SubmissionsView,
        meta: {
          title: 'Inlämningar',
          breadcrumbs: (route) => courseBreadcrumbs(
            route,
            {label: 'Innehåll', to: {name: 'course-content', params: {courseId: route.params.courseId}}},
            {label: 'Inlämningar'},
          ),
        },
      },
      {
        path: 'courses/:courseId/assignments/:linkId/submissions/:submissionId',
        name: 'submission-review',
        // Laddas först när sidan öppnas, som redigeringssidan.
        component: () => import('@/views/course/ReviewView.vue'),
        meta: {
          title: 'Bedöm',
          fullBleed: true,
          breadcrumbs: (route) => courseBreadcrumbs(
            route,
            {label: 'Innehåll', to: {name: 'course-content', params: {courseId: route.params.courseId}}},
            {
              label: 'Inlämningar',
              to: {
                name: 'assignment-submissions',
                params: {courseId: route.params.courseId, linkId: route.params.linkId},
              },
            },
            {label: 'Bedöm'},
          ),
        },
      },
      {path: ':pathMatch(.*)*', name: 'not-found', component: NotFoundView, meta: {title: 'Sidan finns inte'}},
    ],
  },
  // Verktyg för att skapa testdata. Länkas inte i navigationen och ligger utanför layouten.
  {path: '/dev', name: 'dev', component: () => import('@/views/DevView.vue'), meta: {title: 'Testdata'}},
  {
    path: '/dev/student',
    name: 'dev-student',
    component: () => import('@/views/student/StudentCourseContentView.vue'),
    meta: {title: 'Elevvy'},
  },
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
