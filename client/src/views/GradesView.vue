<script setup lang="ts">
import {PhCaretRight, PhGraduationCap} from '@phosphor-icons/vue'
import {computed, ref} from 'vue'

import {Alert, AlertDescription} from '@/components/ui/alert'
import CourseStatusBadge from '@/components/course/CourseStatusBadge.vue'
import {Badge} from '@/components/ui/badge'
import {Card, CardContent, CardDescription, CardHeader, CardTitle} from '@/components/ui/card'
import {Empty, EmptyDescription, EmptyHeader, EmptyMedia, EmptyTitle} from '@/components/ui/empty'
import {Skeleton} from '@/components/ui/skeleton'
import {api} from '@/lib/api'
import {type CourseStatus, courseStatus} from '@/lib/courseStatus'
import {gradeBadgeVariant} from '@/lib/submissions'
import {
  assignmentStatus,
  coursePeriod,
  gradeText,
  shortDate,
  type StudyPlan,
  type StudyPlanAssignment,
  type StudyPlanCourse,
} from '@/lib/studyPlan'

const plan = ref<StudyPlan | null>(null)
const loading = ref(true)
const error = ref('')
const openFeedback = ref<string | null>(null)

const courses = computed(() => plan.value?.courses ?? [])
const current = computed(() => courses.value.find((course) => course.status === 'Ongoing'))
const next = computed(() => courses.value.find((course) => course.status === 'Upcoming'))
const assignmentCourses = computed(() => courses.value.filter((course) =>
  course.status === 'Ongoing' && course.assignments.length > 0))

const progress = computed(() => {
  const total = plan.value?.totalPoints ?? 0
  const completed = plan.value?.completedPoints ?? 0
  return total > 0
    ? Math.min(100, Math.round((completed / total) * 100))
    : 0
})

function titleWithPoints(course: StudyPlanCourse): string {
  return course.points === null
    ? course.title
    : `${course.title} · ${course.points} p`
}

// Datumen styr märket, som på kurssidan. Saknas de gäller studieplanens egen status.
function badgeStatus(course: StudyPlanCourse): CourseStatus {
  return courseStatus(course) ?? {Completed: 'finished', Ongoing: 'running', Upcoming: 'upcoming'}[course.status] as CourseStatus
}

function assignmentStatusClass(assignment: StudyPlanAssignment): string {
  if (assignment.status === 'Graded' && assignment.grade) {
    return assignment.grade === 'Fail'
      ? 'text-destructive font-semibold'
      : 'text-success font-semibold'
  }
  if (assignment.status === 'NeedsWork') {
    return 'text-warning'
  }
  return 'text-muted-foreground'
}

function isFeedbackOpen(assignment: StudyPlanAssignment): boolean {
  return openFeedback.value === assignment.linkId
}

function toggleFeedback(assignment: StudyPlanAssignment) {
  if (!assignment.feedback) {
    return
  }
  openFeedback.value = isFeedbackOpen(assignment)
    ? null
    : assignment.linkId
}

async function load() {
  loading.value = true
  error.value = ''
  try {
    const {data} = await api.GET('/api/me/study-plan')
    plan.value = data ?? null
  } catch {
    error.value = 'Kunde inte hämta studieplanen.'
    plan.value = null
  } finally {
    loading.value = false
  }
}

load()
</script>

<template>
  <div class="flex flex-col gap-5">
    <header class="flex flex-col gap-1">
      <h1 class="font-heading text-[30px] leading-tight font-semibold">Studieplan och betyg</h1>
      <p class="text-muted-foreground">Dina kurser, poängen du klarat av och betygen på kursdelarna.</p>
    </header>

    <Alert v-if="error" variant="destructive">
      <AlertDescription>{{ error }}</AlertDescription>
    </Alert>

    <div v-else-if="loading" class="flex flex-col gap-2">
      <Skeleton v-for="index in 4" :key="index" class="h-12 w-full"/>
    </div>

    <Empty v-else-if="courses.length === 0" class="border border-dashed">
      <EmptyHeader>
        <EmptyMedia variant="icon">
          <PhGraduationCap/>
        </EmptyMedia>
        <EmptyTitle>Ingen studieplan än</EmptyTitle>
        <EmptyDescription>Kurserna du läser som elev visas här med poäng och betyg.</EmptyDescription>
      </EmptyHeader>
    </Empty>

    <template v-else>
      <div class="grid grid-cols-[repeat(auto-fit,minmax(min(220px,100%),1fr))] gap-3">
        <Card size="sm" class="gap-2 py-4">
          <CardHeader class="gap-1 px-4">
            <CardDescription class="text-[13px]">Avklarade poäng</CardDescription>
            <CardTitle class="text-2xl font-semibold">
              {{ plan?.completedPoints ?? 0 }} p
              <span class="text-muted-foreground text-sm font-normal">av {{ plan?.totalPoints ?? 0 }} p</span>
            </CardTitle>
          </CardHeader>
          <CardContent class="px-4">
            <div class="bg-muted h-1.5 overflow-hidden rounded-full">
              <div class="bg-primary h-full rounded-full" :style="{width: `${progress}%`}"/>
            </div>
          </CardContent>
        </Card>

        <Card size="sm" class="gap-1 py-4">
          <CardHeader class="gap-1 px-4">
            <CardDescription class="text-[13px]">Pågår nu</CardDescription>
            <CardTitle class="text-xl font-semibold">
              {{ current
                ? titleWithPoints(current)
                : '–' }}
            </CardTitle>
            <CardDescription v-if="current?.endsOn" class="text-[13px]">
              Slutar {{ shortDate(current.endsOn) }}
            </CardDescription>
          </CardHeader>
        </Card>

        <Card size="sm" class="gap-1 py-4">
          <CardHeader class="gap-1 px-4">
            <CardDescription class="text-[13px]">Härnäst</CardDescription>
            <CardTitle class="text-xl font-semibold">
              {{ next
                ? titleWithPoints(next)
                : '–' }}
            </CardTitle>
            <CardDescription v-if="next?.startsOn" class="text-[13px]">
              Startar {{ shortDate(next.startsOn) }}
            </CardDescription>
          </CardHeader>
        </Card>
      </div>

      <section aria-label="Kurser" class="bg-card overflow-hidden rounded-[11px] border">
        <ul class="divide-muted divide-y sm:hidden" data-test="study-plan-course-list">
          <li
            v-for="course in courses"
            :key="course.courseId"
            class="flex flex-col gap-1.5 px-4 py-3"
            data-test="study-plan-course-card"
          >
            <RouterLink
              :to="{name: 'course-content', params: {courseId: course.courseId}}"
              class="font-medium hover:underline"
            >
              {{ course.title }}
            </RouterLink>
            <p class="text-muted-foreground text-[13px]">
              {{ coursePeriod(course) }} · {{ course.points === null
                ? '–'
                : `${course.points} p` }}
            </p>
            <div class="flex items-center justify-between gap-2">
              <CourseStatusBadge :status="badgeStatus(course)"/>
              <Badge v-if="course.grade" :variant="gradeBadgeVariant(course.grade)">
                {{ gradeText(course.grade) }}
              </Badge>
              <span v-else class="text-muted-foreground text-sm">{{ gradeText(course.grade) }}</span>
            </div>
          </li>
        </ul>
        <div class="hidden sm:block">
          <table class="w-full border-collapse text-sm">
            <thead>
              <tr class="bg-muted/50 text-muted-foreground text-left text-[13px]">
                <th scope="col" class="border-b px-4 py-2.5 font-medium">Kurs</th>
                <th scope="col" class="border-b px-3 py-2.5 font-medium">Period</th>
                <th scope="col" class="border-b px-3 py-2.5 text-right font-medium">Poäng</th>
                <th scope="col" class="border-b px-3 py-2.5 font-medium">Status</th>
                <th scope="col" class="border-b px-4 py-2.5 text-right font-medium">Betyg</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="course in courses" :key="course.courseId" data-test="study-plan-course">
                <td class="border-b border-muted px-4 py-2.5">
                  <RouterLink
                    :to="{name: 'course-content', params: {courseId: course.courseId}}"
                    class="hover:underline"
                  >
                    {{ course.title }}
                  </RouterLink>
                </td>
                <td class="text-muted-foreground border-b border-muted px-3 py-2.5 whitespace-nowrap">
                  {{ coursePeriod(course) }}
                </td>
                <td class="border-b border-muted px-3 py-2.5 text-right">{{ course.points ?? '–' }}</td>
                <td class="border-b border-muted px-3 py-2.5">
                  <CourseStatusBadge :status="badgeStatus(course)"/>
                </td>
                <td class="border-b border-muted px-4 py-2.5 text-right">
                  <Badge v-if="course.grade" :variant="gradeBadgeVariant(course.grade)">
                    {{ gradeText(course.grade) }}
                  </Badge>
                  <span v-else class="text-muted-foreground">{{ gradeText(course.grade) }}</span>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </section>

      <section
        v-if="assignmentCourses.length > 0"
        aria-label="Betyg på kursdelar"
        class="flex flex-col gap-3"
      >
        <h2 class="font-heading text-lg font-semibold">Betyg på kursdelar</h2>
        <div
          v-for="course in assignmentCourses"
          :key="course.courseId"
          class="bg-card overflow-hidden rounded-[11px] border"
        >
          <div class="bg-muted/50 text-muted-foreground border-b px-3 py-2 text-[13px] font-medium">
            {{ course.title }} · {{ course.submittedCount }} av {{ course.assignmentCount }} inlämnade
          </div>
          <div class="bg-card text-[13px]">
            <div
              v-for="assignment in course.assignments"
              :key="assignment.linkId"
              class="border-b border-muted last:border-b-0"
              data-test="study-plan-assignment"
            >
              <div class="flex items-center">
                <button
                  type="button"
                  class="flex min-w-0 flex-1 flex-col items-start gap-0.5 px-3 py-2 text-left sm:flex-row sm:items-center sm:justify-between sm:gap-2 disabled:cursor-default disabled:hover:bg-transparent"
                  :class="assignment.feedback
                    ? 'hover:bg-muted/50'
                    : ''"
                  :disabled="!assignment.feedback"
                  :aria-expanded="isFeedbackOpen(assignment)
                    ? 'true'
                    : 'false'"
                  @click="toggleFeedback(assignment)"
                >
                  <span class="flex min-w-0 items-center gap-1.5">
                    <PhCaretRight
                      v-if="assignment.feedback"
                      class="text-muted-foreground size-3 transition-transform"
                      :class="{'rotate-90': isFeedbackOpen(assignment)}"
                    />
                    {{ assignment.title }}
                  </span>
                  <span class="sm:shrink-0" :class="assignmentStatusClass(assignment)">
                    {{ assignmentStatus(assignment) }}
                  </span>
                </button>
                <RouterLink
                  :to="{name: 'course-content', params: {courseId: course.courseId}, query: {del: assignment.linkId}}"
                  class="text-muted-foreground shrink-0 px-3 py-2 hover:underline"
                  data-test="study-plan-open"
                >
                  Öppna
                </RouterLink>
              </div>
              <p
                v-if="isFeedbackOpen(assignment) && assignment.feedback"
                class="text-muted-foreground px-3 pb-2.5"
                data-test="study-plan-feedback"
              >
                {{ assignment.feedback }}
              </p>
            </div>
          </div>
        </div>
      </section>
    </template>
  </div>
</template>
