<script setup lang="ts">
import {PhExam} from '@phosphor-icons/vue'
import {computed, ref} from 'vue'
import {RouterLink, type RouteLocationRaw} from 'vue-router'

import StatusBadge from '@/components/StatusBadge.vue'
import {Alert, AlertDescription} from '@/components/ui/alert'
import {Skeleton} from '@/components/ui/skeleton'
import {useCurrentUser} from '@/composables/useCurrentUser'
import {useGradingCount} from '@/composables/useGradingCount'
import {api, type Schema} from '@/lib/api'
import {deadlineStatusKey} from '@/lib/status'
import {
  dateLine,
  deadlineText,
  firstName,
  greeting,
  progressPercent,
  shortDate,
  weekdayAndTime,
} from '@/lib/today'

type Teaching = Schema<'TeachingDto'>
type ToGrade = Schema<'TeachingToGradeDto'>
type Deadline = Schema<'TeachingDeadlineDto'>
type Scheduled = Schema<'TeachingScheduledDto'>
type Course = Schema<'TeachingCourseDto'>

const now = new Date()
const {me} = useCurrentUser()
const {setGradingCount} = useGradingCount()

const teaching = ref<Teaching | null>(null)
const loading = ref(true)
const loadFailed = ref(false)

const name = computed(() => firstName(me.value?.name))
const mobileGreeting = computed(() => name.value
  ? `Hej ${name.value}`
  : 'Hej')
const toGradeLine = computed(() => (teaching.value?.toGradeTotal ?? 0) === 0
  ? 'Inget att bedöma'
  : `${teaching.value!.toGradeTotal} att bedöma`)

async function load() {
  loading.value = true
  loadFailed.value = false
  try {
    const result = await api.GET('/api/me/teaching')
    if (result.error || !result.data) {
      loadFailed.value = true
    } else {
      teaching.value = result.data
      setGradingCount(result.data.toGradeTotal)
    }
  } catch {
    loadFailed.value = true
  } finally {
    loading.value = false
  }
}

// "inlämnad för 2 timmar sedan", "inlämnad igår". Äldre inlämningar visar datum.
function submittedText(iso: string): string {
  const minutes = Math.round((now.getTime() - new Date(iso).getTime()) / 60_000)
  if (minutes < 1) {
    return 'inlämnad nyss'
  }
  if (minutes < 60) {
    return `inlämnad för ${minutes} min sedan`
  }
  const hours = Math.round(minutes / 60)
  if (hours < 24) {
    return `inlämnad för ${hours} ${hours === 1
      ? 'timme'
      : 'timmar'} sedan`
  }
  const days = Math.round(hours / 24)
  if (days === 1) {
    return 'inlämnad igår'
  }
  return days < 7
    ? `inlämnad för ${days} dagar sedan`
    : `inlämnad ${shortDate(iso)}`
}

function deadlineProgress(deadline: Deadline): number {
  return progressPercent(deadline.submitted, deadline.students)
}

function scheduledKind(item: Scheduled): string {
  return item.kind === 'Week'
    ? 'Vecka'
    : 'Del'
}

function studentCount(course: Course): string {
  return course.students === 1
    ? '1 elev'
    : `${course.students} elever`
}

function submissionLink(item: ToGrade): RouteLocationRaw {
  return {
    name: 'submission-review',
    params: {
      courseId: item.courseId,
      linkId: item.linkId,
      submissionId: item.submissionId,
    },
  }
}

function assignmentLink(item: Deadline): RouteLocationRaw {
  return {name: 'assignment-submissions', params: {courseId: item.courseId, linkId: item.linkId}}
}

function courseLink(courseId: string): RouteLocationRaw {
  return {name: 'course-content', params: {courseId}}
}

load()
</script>

<template>
  <Alert v-if="loadFailed" variant="destructive">
    <AlertDescription>Kunde inte hämta din översikt.</AlertDescription>
  </Alert>

  <div v-else-if="loading" class="flex flex-col gap-4" data-test="teacher-loading">
    <Skeleton class="h-9 w-72"/>
    <Skeleton class="h-64 w-full"/>
  </div>

  <div
    v-else-if="teaching"
    class="flex flex-col gap-5 max-md:-mt-3 max-md:-mb-10 md:gap-6"
    data-test="teacher-dashboard"
  >
    <div class="flex flex-col gap-0.5 md:gap-1">
      <h1 class="hidden text-[30px] leading-[1.15] md:block">
        {{ greeting(now) }}<template v-if="name">, {{ name }}</template>
      </h1>
      <h1 class="text-2xl leading-[1.2] md:hidden">{{ mobileGreeting }}</h1>
      <p class="text-muted-foreground text-sm md:text-base">{{ dateLine(now) }} · {{ toGradeLine }}</p>
    </div>

    <div class="grid grid-cols-1 gap-4 md:grid-cols-[repeat(auto-fit,minmax(min(340px,100%),1fr))]">
      <section
        aria-labelledby="t-to-grade-heading"
        class="bg-card overflow-hidden rounded-[11px] border shadow-[0_1px_2px_rgba(0,0,0,.04)] md:col-span-full lg:col-span-2"
      >
        <div class="flex flex-wrap items-center gap-3 px-5 py-4">
          <div class="flex flex-col">
            <h2 id="t-to-grade-heading" class="text-base">Att bedöma</h2>
            <span class="text-muted-foreground text-[13px]">Inlämningar som väntar på dig</span>
          </div>
          <RouterLink :to="{name: 'grading'}" class="text-muted-foreground ml-auto text-[13px] hover:underline">
            Visa alla
          </RouterLink>
        </div>
        <p
          v-if="!teaching.toGrade.length"
          class="text-muted-foreground border-t px-5 py-4 text-sm"
          data-test="to-grade-empty"
        >
          Inget att bedöma just nu.
        </p>
        <RouterLink
          v-for="item in teaching.toGrade"
          :key="item.submissionId"
          :to="submissionLink(item)"
          data-test="to-grade-item"
          class="hover:bg-muted/40 flex items-center gap-3 border-t px-5 py-3.5"
        >
          <span class="bg-primary/25 text-foreground dark:bg-primary/15 dark:text-primary flex size-8 shrink-0 items-center justify-center rounded-[7px]">
            <PhExam class="size-4" aria-hidden="true"/>
          </span>
          <span class="flex min-w-0 flex-1 flex-col">
            <span class="truncate font-medium">{{ item.studentName }}</span>
            <span class="text-muted-foreground truncate text-[13px]">
              {{ item.title }} · {{ item.courseTitle }}
            </span>
          </span>
          <span class="text-muted-foreground shrink-0 text-[13px]">{{ submittedText(item.submittedAt) }}</span>
        </RouterLink>
      </section>

      <div class="flex flex-col gap-4">
        <section
          aria-labelledby="t-deadlines-heading"
          class="bg-card flex flex-col gap-3 rounded-[11px] border px-5 py-4 shadow-[0_1px_2px_rgba(0,0,0,.04)]"
        >
          <div class="flex flex-col">
            <h2 id="t-deadlines-heading" class="text-base">Kommande deadlines</h2>
            <span class="text-muted-foreground text-[13px]">Närmaste två veckorna</span>
          </div>
          <p v-if="!teaching.deadlines.length" class="text-muted-foreground text-sm" data-test="deadline-empty">
            Inga deadlines de närmaste två veckorna.
          </p>
          <RouterLink
            v-for="item in teaching.deadlines"
            :key="item.linkId"
            :to="assignmentLink(item)"
            data-test="deadline-item"
            class="flex flex-col gap-1.5"
          >
            <span class="flex flex-wrap items-baseline justify-between gap-x-3 gap-y-1">
              <span class="min-w-32 flex-1 truncate font-medium">{{ item.title }}</span>
              <span class="text-muted-foreground flex shrink-0 items-center gap-2 text-[13px]">
                <StatusBadge v-if="deadlineStatusKey(item.dueAt, false, now)" :status="deadlineStatusKey(item.dueAt, false, now)!"/>
                {{ deadlineText(item.dueAt) }}
              </span>
            </span>
            <span class="text-muted-foreground text-[13px]">{{ item.courseTitle }}</span>
            <span
              class="bg-muted h-1.5 overflow-hidden rounded-full"
              role="progressbar"
              :aria-valuenow="deadlineProgress(item)"
              aria-valuemin="0"
              aria-valuemax="100"
            >
              <span
                class="bg-primary block h-full rounded-full"
                :style="{width: `${deadlineProgress(item)}%`}"
              />
            </span>
            <span class="text-muted-foreground text-[13px]">
              {{ item.submitted }} av {{ item.students }} inlämnade
            </span>
          </RouterLink>
        </section>

        <section
          aria-labelledby="t-scheduled-heading"
          class="bg-card flex flex-col gap-3 rounded-[11px] border px-5 py-4 shadow-[0_1px_2px_rgba(0,0,0,.04)]"
        >
          <div class="flex flex-col">
            <h2 id="t-scheduled-heading" class="text-base">Schemalagt</h2>
            <span class="text-muted-foreground text-[13px]">Närmaste sju dagarna</span>
          </div>
          <p v-if="!teaching.scheduled.length" class="text-muted-foreground text-sm" data-test="scheduled-empty">
            Inget schemalagt de närmaste sju dagarna.
          </p>
          <RouterLink
            v-for="item in teaching.scheduled"
            :key="`${item.kind}-${item.linkId ?? item.moduleId}`"
            :to="courseLink(item.courseId)"
            data-test="scheduled-item"
            class="flex flex-col"
          >
            <span class="font-medium">{{ item.title }}</span>
            <span class="text-muted-foreground text-[13px]">
              {{ item.courseTitle }} · {{ scheduledKind(item) }} · {{ weekdayAndTime(item.publishAt) }}
            </span>
          </RouterLink>
        </section>
      </div>
    </div>

    <section aria-labelledby="t-courses-heading" class="flex flex-col gap-3">
      <h2 id="t-courses-heading" class="text-base">Mina kurser</h2>
      <p v-if="!teaching.courses.length" class="text-muted-foreground text-sm" data-test="teacher-courses-empty">
        Du undervisar inte i någon kurs än.
      </p>
      <div class="grid grid-cols-1 gap-4 md:grid-cols-[repeat(auto-fill,minmax(min(300px,100%),1fr))]">
        <RouterLink
          v-for="course in teaching.courses"
          :key="course.courseId"
          :to="courseLink(course.courseId)"
          data-test="teacher-course-card"
          class="bg-card hover:bg-muted/40 flex flex-col gap-3 rounded-[11px] border px-5 py-4 shadow-[0_1px_2px_rgba(0,0,0,.04)]"
        >
          <span class="text-muted-foreground font-mono text-xs">{{ course.code }}</span>
          <span class="font-heading text-lg font-semibold">{{ course.title }}</span>
          <span class="flex flex-wrap items-center gap-2 text-[13px]">
            <span class="text-muted-foreground">{{ studentCount(course) }}</span>
            <span
              v-if="course.toGrade > 0"
              class="rounded-full bg-warning/10 px-2 py-0.5 text-xs font-medium text-warning dark:bg-warning/20"
            >
              {{ course.toGrade }} att bedöma
            </span>
          </span>
        </RouterLink>
      </div>
    </section>
  </div>
</template>
