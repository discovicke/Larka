<script setup lang="ts">
import {PhDotsThree, PhPlus} from '@phosphor-icons/vue'
import {computed, ref, watch} from 'vue'
import {useRoute} from 'vue-router'

import {Alert, AlertDescription} from '@/components/ui/alert'
import CourseStatusBadge from '@/components/course/CourseStatusBadge.vue'
import NewCourseRunDialog from '@/components/course/NewCourseRunDialog.vue'
import {Button} from '@/components/ui/button'
import {DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger} from '@/components/ui/dropdown-menu'
import {Skeleton} from '@/components/ui/skeleton'
import {useCourses} from '@/composables/useCourses'
import {useCurrentUser} from '@/composables/useCurrentUser'
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
const {can} = useCurrentUser()
// Admin får skapa en ny omgång av vilken kurs som helst, läraren bara av en kurs hen undervisar i.
const canCreateRun = computed(() => course.value !== undefined
  && (can('manageCourse') || can('editContent', course.value.id)))
const runOpen = ref(false)
// Layouten återanvänds när man byter kurs, så dialogen får inte följa med.
watch(() => route.params.courseId, () => {
  runOpen.value = false
})

// Period och omfattning under rubriken, som i mockupen. Delar som saknar data lämnas borta.
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
    <div class="flex flex-col gap-4 border-b">
      <div class="flex items-start justify-between gap-3">
        <div class="flex min-w-0 flex-col gap-1">
          <span class="text-muted-foreground font-mono text-xs">KURS · {{ course.code }}</span>
          <div class="flex flex-wrap items-center gap-x-3 gap-y-1">
            <h1 class="text-3xl leading-tight break-words">{{ course.title }}</h1>
            <CourseStatusBadge :status="courseStatus(course)"/>
          </div>
          <p v-if="subtitle" class="text-muted-foreground">{{ subtitle }}</p>
        </div>
        <DropdownMenu v-if="canCreateRun">
          <DropdownMenuTrigger as-child>
            <Button
              variant="outline"
              size="icon"
              class="shrink-0"
              aria-label="Kursens åtgärder"
              data-test="course-menu"
            >
              <PhDotsThree weight="bold"/>
            </Button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end" class="min-w-52">
            <DropdownMenuItem class="min-h-10" data-test="new-run" @select="runOpen = true">
              <PhPlus/>
              Ny kursomgång…
            </DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>
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

    <NewCourseRunDialog v-if="canCreateRun" v-model:open="runOpen" :course="course"/>
  </template>
</template>
