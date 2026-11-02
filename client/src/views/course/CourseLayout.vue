<script setup lang="ts">
import {computed} from 'vue'
import {useRoute} from 'vue-router'

import {Alert, AlertDescription} from '@/components/ui/alert'
import CourseStatusBadge from '@/components/course/CourseStatusBadge.vue'
import {Skeleton} from '@/components/ui/skeleton'
import {useCourses} from '@/composables/useCourses'
import {periodLabel} from '@/lib/coursePeriod'
import {courseStatus} from '@/lib/courseStatus'
import NotFoundView from '@/views/NotFoundView.vue'

const tabs = [
  {label: 'Innehåll', routeName: 'course-content'},
  {label: 'Nyheter', routeName: 'course-news'},
  {label: 'Om', routeName: 'course-about'},
]

const route = useRoute()
const {status, findCourse} = useCourses()
const course = computed(() => findCourse(String(route.params.courseId)))

// Period och omfattning under rubriken, som i mockupen. Delar som saknar data lämnas borta.
const showHeader = computed(() => !(route.name === 'course-content' && route.query.del))

const subtitle = computed(() => {
  if (!course.value) {
    return ''
  }
  const parts: string[] = []
  const period = periodLabel(course.value.startsOn, course.value.endsOn)
  if (period) {
    parts.push(period)
  }
  if (course.value.points !== null) {
    parts.push(`${course.value.points} p`)
  }
  return parts.join(' · ')
})
</script>

<template>
  <Skeleton v-if="status === 'loading'" class="h-24 w-full"/>
  <Alert v-else-if="status === 'failed'" variant="destructive">
    <AlertDescription>Kunde inte hämta kurserna. Ladda om sidan.</AlertDescription>
  </Alert>
  <NotFoundView v-else-if="!course"/>
  <template v-else>
    <div v-if="showHeader" class="flex flex-col gap-4 border-b">
      <div class="flex items-start justify-between gap-3">
        <div class="flex min-w-0 flex-col gap-1">
          <span class="text-muted-foreground font-mono text-xs">KURS · {{ course.code }}</span>
          <div class="flex flex-wrap items-center gap-x-3 gap-y-1">
            <h1 class="text-3xl leading-tight break-words">{{ course.title }}</h1>
            <CourseStatusBadge :status="courseStatus(course)"/>
          </div>
          <p v-if="subtitle" class="text-muted-foreground">{{ subtitle }}</p>
        </div>
      </div>

      <nav aria-label="Kursens sidor" class="flex flex-wrap gap-5">
        <RouterLink
          v-for="tab in tabs"
          :key="tab.routeName"
          :to="{name: tab.routeName, params: {courseId: course.id}}"
          class="hover:text-foreground -mb-px flex h-10 items-center border-b-2 px-0.5"
          :class="route.name === tab.routeName
            ? 'text-foreground border-primary font-medium'
            : 'text-muted-foreground border-transparent'"
          :aria-current="route.name === tab.routeName
            ? 'page'
            : undefined"
          :data-test="`tab-${tab.routeName}`"
        >
          {{ tab.label }}
        </RouterLink>
      </nav>
    </div>

    <RouterView/>
  </template>
</template>
