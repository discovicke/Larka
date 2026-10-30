<script setup lang="ts">
import {PhCaretRight, PhExam, PhNewspaper} from '@phosphor-icons/vue'
import {computed, ref} from 'vue'
import {RouterLink, type RouteLocationRaw} from 'vue-router'

import {Alert, AlertDescription} from '@/components/ui/alert'
import StatusBadge from '@/components/StatusBadge.vue'
import TeacherToday from '@/components/today/TeacherToday.vue'
import {Skeleton} from '@/components/ui/skeleton'
import {useCurrentUser} from '@/composables/useCurrentUser'
import {api, type Schema} from '@/lib/api'
import {formatDate} from '@/lib/content'
import {isStudentEverywhere} from '@/lib/permissions'
import {deadlineStatusKey, submissionStatusKey} from '@/lib/status'
import {gradeText} from '@/lib/studyPlan'
import {
  dateLine,
  dueThisWeekText,
  firstName,
  greeting,
  metaLine,
  progressPercent,
  relativeText,
  deadlineText,
  groupByDue,
  shortDate,
  splitByWeek,
  weekdayAndTime,
} from '@/lib/today'

type Today = Schema<'TodayDto'>
type Todo = Schema<'TodayTodoDto'>
type Feedback = Schema<'TodayFeedbackDto'>
type News = Schema<'TodayNewsDto'>
type Submission = Schema<'TodaySubmissionDto'>

type Tab = 'upcoming' | 'submitted'

const now = new Date()
const {me, loadMe} = useCurrentUser()

const today = ref<Today | null>(null)
const loading = ref(true)
const loadFailed = ref(false)
const tab = ref<Tab>('upcoming')
// Den som är lärare någonstans, i skolan eller i en kurs, ser lärarens översikt, som hämtar sin egen data.
const isTeacher = ref(false)

const name = computed(() => firstName(me.value?.name))
const mobileGreeting = computed(() => name.value
  ? `Hej ${name.value}`
  : 'Hej')
const todo = computed(() => today.value?.todo ?? [])
const groups = computed(() => {
  const {overdue, thisWeek, later} = groupByDue(todo.value, now)
  return [
    {heading: 'Försenat', items: overdue},
    {heading: 'Den här veckan', items: thisWeek},
    {heading: 'Senare', items: later},
  ].filter((group) => group.items.length > 0)
})
const dueThisWeek = computed(() => splitByWeek(todo.value, now).thisWeek.length)
const nextUp = computed(() => todo.value[0])
const moreTodo = computed(() => todo.value.slice(1))

// "Nytt" på mobilen: betyg och nyheter i en lista, de senaste först.
const recent = computed(() => {
  const feedback = (today.value?.feedback ?? []).map((item) => ({
    key: `feedback-${item.linkId}`,
    title: item.title,
    meta: `${feedbackLabel(item)} · ${timeLabel(item.gradedAt)}`,
    at: item.gradedAt,
    feedback: item,
    news: null,
  }))
  const news = (today.value?.news ?? []).map((item) => ({
    key: `news-${item.contentId}`,
    title: item.title,
    meta: [item.source, timeLabel(item.publishAt)].filter(Boolean).join(' · '),
    at: item.publishAt,
    feedback: null,
    news: item,
  }))
  return [...feedback, ...news]
    .sort((left, right) => Date.parse(right.at) - Date.parse(left.at))
    .slice(0, 5)
})

function courseLink(courseId: string | null | undefined): RouteLocationRaw | undefined {
  return courseId
    ? {name: 'course-content', params: {courseId}}
    : undefined
}

// Uppgifter och återkoppling öppnar delen själv på kurssidan.
function partLink(item: {courseId?: string | null, linkId: string}): RouteLocationRaw | undefined {
  return item.courseId
    ? {name: 'course-content', params: {courseId: item.courseId}, query: {del: item.linkId}}
    : undefined
}

function newsLink(item: News): RouteLocationRaw {
  return {name: 'inbox', query: {post: item.contentId}}
}

function timeLabel(iso: string): string {
  const date = new Date(iso)
  const sameDay = date.toDateString() === now.toDateString()
  return sameDay
    ? date.toLocaleTimeString('sv-SE', {hour: '2-digit', minute: '2-digit'})
    : formatDate(iso)
}

function feedbackLabel(item: Feedback): string {
  return item.grade
    ? 'Betyg'
    : 'Komplettera'
}

function gradeTile(item: Feedback): string {
  return item.grade
    ? gradeText(item.grade)
    : 'K'
}

function gradeTileClass(item: Feedback): string {
  if (item.grade === 'Fail') {
    return 'bg-destructive/10 text-destructive dark:bg-destructive/20'
  }
  return item.grade
    ? 'bg-success/10 text-success dark:bg-success/20'
    : 'bg-destructive/10 text-destructive dark:bg-destructive/20'
}

function statusText(item: Submission): string {
  return `Bedömd${item.grade
    ? ` · ${gradeText(item.grade)}`
    : ''}`
}

function dueStatus(item: Todo) {
  return deadlineStatusKey(item.dueAt, false, now)
}

function tabClass(value: Tab): string {
  return tab.value === value
    ? 'bg-background text-foreground font-medium shadow-sm'
    : 'text-muted-foreground'
}

async function load() {
  loading.value = true
  loadFailed.value = false
  try {
    await loadMe()
    if (!isStudentEverywhere(me.value)) {
      isTeacher.value = true
      loading.value = false
      return
    }
    const result = await api.GET('/api/me/today')
    if (result.error || !result.data) {
      loadFailed.value = true
    } else {
      today.value = result.data
    }
  } catch {
    loadFailed.value = true
  } finally {
    loading.value = false
  }
}

load()
</script>

<template>
  <TeacherToday v-if="isTeacher"/>

  <Alert v-else-if="loadFailed" variant="destructive">
    <AlertDescription>Kunde inte hämta din översikt.</AlertDescription>
  </Alert>

  <div v-else-if="loading" class="flex flex-col gap-4" data-test="today-loading">
    <Skeleton class="h-9 w-72"/>
    <Skeleton class="h-64 w-full"/>
  </div>

  <template v-else-if="today">
    <!-- Dator -->
    <div class="hidden flex-col gap-6 md:flex" data-test="today-desktop">
      <div class="flex flex-col gap-1">
        <h1 class="text-[30px] leading-[1.15]">{{ greeting(now) }}<template v-if="name">, {{ name }}</template></h1>
        <p class="text-muted-foreground">{{ dateLine(now) }} · {{ dueThisWeekText(dueThisWeek) }}</p>
      </div>

      <div class="grid grid-cols-[repeat(auto-fit,minmax(min(340px,100%),1fr))] gap-4">
        <section
          aria-labelledby="todo-heading"
          class="bg-card col-span-full overflow-hidden rounded-[11px] border shadow-[0_1px_2px_rgba(0,0,0,.04)] lg:col-span-2"
        >
          <div class="flex flex-wrap items-center gap-3 px-5 py-4">
            <div class="flex flex-col">
              <h2 id="todo-heading" class="text-base">Att göra</h2>
              <span class="text-muted-foreground text-[13px]">Från alla dina kurser</span>
            </div>
            <div role="tablist" class="bg-muted ml-auto flex rounded-[7px] p-[3px]">
              <button
                type="button"
                role="tab"
                data-test="tab-upcoming"
                :aria-selected="tab === 'upcoming'"
                class="rounded-[5px] px-2.5 py-1 text-[13px]"
                :class="tabClass('upcoming')"
                @click="tab = 'upcoming'"
              >
                Kommande
              </button>
              <button
                type="button"
                role="tab"
                data-test="tab-submitted"
                :aria-selected="tab === 'submitted'"
                class="rounded-[5px] px-2.5 py-1 text-[13px]"
                :class="tabClass('submitted')"
                @click="tab = 'submitted'"
              >
                Inlämnat
              </button>
            </div>
          </div>

          <template v-if="tab === 'upcoming'">
            <p v-if="!todo.length" class="text-muted-foreground border-t px-5 py-4 text-sm" data-test="todo-empty">
              Inget att göra just nu
            </p>
            <div v-for="group in groups" :key="group.heading" :data-test="`group-${group.heading}`">
              <h3
                class="bg-muted/40 text-muted-foreground border-y px-5 py-1.5 font-sans text-xs font-medium tracking-normal"
              >
                <StatusBadge v-if="group.heading === 'Försenat'" status="deadline.overdue" :label="group.heading"/>
                <template v-else>{{ group.heading }}</template>
              </h3>
              <component
                :is="partLink(item)
                  ? RouterLink
                  : 'div'"
                v-for="item in group.items"
                :key="item.linkId"
                :to="partLink(item)"
                data-test="todo-item"
                class="hover:bg-muted/40 flex items-center gap-3 border-b px-5 py-3.5 last:border-b-0"
              >
                <span class="bg-primary/25 text-foreground dark:bg-primary/15 dark:text-primary flex size-8 shrink-0 items-center justify-center rounded-[7px]">
                  <PhExam class="size-4" aria-hidden="true"/>
                </span>
                <span class="flex min-w-0 flex-1 flex-col">
                  <span class="truncate font-medium">{{ item.title }}</span>
                  <span class="text-muted-foreground flex flex-wrap items-center gap-x-2 text-[13px]">
                    <span v-if="item.courseTitle">{{ item.courseTitle }}</span>
                    <StatusBadge v-if="item.status === 'NeedsWork'" status="submission.needsWork"/>
                  </span>
                </span>
                <span class="flex shrink-0 flex-col items-end gap-0.5 text-[13px]">
                  <span class="font-medium" data-test="todo-due">{{ deadlineText(item.dueAt) }}</span>
                  <span class="text-muted-foreground">{{ relativeText(item.dueAt, now) }}</span>
                  <StatusBadge v-if="dueStatus(item) === 'deadline.soon'" status="deadline.soon"/>
                </span>
              </component>
            </div>
          </template>

          <template v-else>
            <p
              v-if="!today.submitted.length"
              class="text-muted-foreground border-t px-5 py-4 text-sm"
              data-test="submitted-empty"
            >
              Du har inte lämnat in något än
            </p>
            <component
              :is="partLink(item)
                ? RouterLink
                : 'div'"
              v-for="item in today.submitted"
              :key="item.linkId"
              :to="partLink(item)"
              data-test="submitted-item"
              class="hover:bg-muted/40 flex items-center gap-3 border-t px-5 py-3.5"
            >
              <PhExam class="text-muted-foreground size-4 shrink-0" aria-hidden="true"/>
              <span class="flex min-w-0 flex-1 flex-col">
                <span class="truncate font-medium">{{ item.title }}</span>
                <span class="text-muted-foreground text-[13px]">{{ item.courseTitle }}</span>
              </span>
              <span class="flex shrink-0 flex-col items-end text-[13px]">
                <StatusBadge
                  v-if="item.status !== 'Graded'"
                  :status="submissionStatusKey(item.status)"
                />
                <span v-else class="font-medium">{{ statusText(item) }}</span>
                <span v-if="item.submittedAt" class="text-muted-foreground">{{ shortDate(item.submittedAt) }}</span>
              </span>
            </component>
          </template>
        </section>

        <div class="flex flex-col gap-4">
          <section
            aria-labelledby="feedback-heading"
            class="bg-card flex flex-col gap-3 rounded-[11px] border px-5 py-4 shadow-[0_1px_2px_rgba(0,0,0,.04)]"
          >
            <h2 id="feedback-heading" class="text-base">Senaste återkoppling</h2>
            <p v-if="!today.feedback.length" class="text-muted-foreground text-sm" data-test="feedback-empty">
              Ingen återkoppling än
            </p>
            <component
              :is="partLink(item)
                ? RouterLink
                : 'div'"
              v-for="item in today.feedback"
              :key="item.linkId"
              :to="partLink(item)"
              data-test="feedback-item"
              class="flex items-center gap-3"
            >
              <span
                class="flex size-9 shrink-0 items-center justify-center rounded-[7px] font-bold"
                :class="gradeTileClass(item)"
              >
                {{ gradeTile(item) }}
              </span>
              <span class="flex min-w-0 flex-col">
                <span class="truncate font-medium">{{ item.title }}</span>
                <span class="text-muted-foreground truncate text-[13px]">
                  {{ item.feedback || feedbackLabel(item) }}
                </span>
              </span>
            </component>
          </section>

          <section
            aria-labelledby="news-heading"
            class="bg-card flex flex-col gap-3 rounded-[11px] border px-5 py-4 shadow-[0_1px_2px_rgba(0,0,0,.04)]"
          >
            <div class="flex items-center justify-between">
              <h2 id="news-heading" class="text-base">Nyheter</h2>
              <RouterLink :to="{name: 'inbox'}" class="text-muted-foreground text-[13px] hover:underline">
                Visa alla
              </RouterLink>
            </div>
            <p v-if="!today.news.length" class="text-muted-foreground text-sm" data-test="news-empty">
              Inga nyheter
            </p>
            <RouterLink
              v-for="item in today.news"
              :key="item.contentId"
              :to="newsLink(item)"
              data-test="news-item"
              class="flex flex-col"
            >
              <span class="font-medium">{{ item.title }}</span>
              <span class="text-muted-foreground text-[13px]">
                <template v-if="item.source">{{ item.source }} · </template>{{ timeLabel(item.publishAt) }}
              </span>
            </RouterLink>
          </section>
        </div>
      </div>

      <section aria-labelledby="courses-heading" class="flex flex-col gap-3">
        <h2 id="courses-heading" class="text-base">Mina kurser</h2>
        <p v-if="!today.courses.length" class="text-muted-foreground text-sm" data-test="courses-empty">
          Du läser inga kurser än
        </p>
        <div class="grid grid-cols-[repeat(auto-fill,minmax(min(300px,100%),1fr))] gap-4">
          <RouterLink
            v-for="course in today.courses"
            :key="course.courseId"
            :to="courseLink(course.courseId)!"
            data-test="course-card"
            class="bg-card hover:bg-muted/40 flex flex-col gap-3 rounded-[11px] border px-5 py-4 shadow-[0_1px_2px_rgba(0,0,0,.04)]"
          >
            <span class="text-muted-foreground font-mono text-xs">{{ course.code }}</span>
            <span class="font-heading text-lg font-semibold">{{ course.title }}</span>
            <template v-if="course.totalParts > 0">
              <span
                class="bg-muted h-1.5 overflow-hidden rounded-full"
                role="progressbar"
                :aria-valuenow="progressPercent(course.completedParts, course.totalParts)"
                aria-valuemin="0"
                aria-valuemax="100"
              >
                <span
                  class="bg-primary block h-full rounded-full"
                  :style="{width: `${progressPercent(course.completedParts, course.totalParts)}%`}"
                />
              </span>
              <span class="text-muted-foreground text-[13px]">
                {{ course.completedParts }} av {{ course.totalParts }} klara
              </span>
            </template>
            <span v-else class="text-muted-foreground text-[13px]" data-test="course-empty">Inget innehåll än</span>
          </RouterLink>
        </div>
      </section>
    </div>

    <!-- Mobil -->
    <!-- Skalet har 32px över och 64px under; mobilens sidkropp har 20px och 24px. -->
    <div class="flex flex-col gap-5 max-md:-mt-3 max-md:-mb-10 md:hidden" data-test="today-mobile">
      <div class="flex flex-col gap-0.5">
        <h1 class="text-2xl leading-[1.2]">{{ mobileGreeting }}</h1>
        <span class="text-muted-foreground text-sm">{{ dateLine(now) }}</span>
      </div>

      <component
        :is="partLink(nextUp)
          ? RouterLink
          : 'div'"
        v-if="nextUp"
        :to="partLink(nextUp)"
        data-test="next-up"
        class="flex flex-col gap-2.5 rounded-[11px] bg-sidebar p-4 text-sidebar-foreground"
      >
        <span class="text-xs font-medium tracking-[0.04em] text-sidebar-foreground/70 uppercase">Närmast</span>
        <span class="text-[17px] leading-[1.3] font-semibold">{{ nextUp.title }}</span>
        <span class="flex justify-between gap-3 text-[13px] text-sidebar-foreground/70">
          <span>{{ nextUp.courseTitle }}</span>
          <span class="font-medium text-sidebar-primary">{{ weekdayAndTime(nextUp.dueAt) }}</span>
        </span>
      </component>

      <section aria-labelledby="m-todo-heading" class="flex flex-col gap-2">
        <h2 id="m-todo-heading" class="text-[17px]">Att göra</h2>
        <p v-if="!moreTodo.length" class="text-muted-foreground text-sm" data-test="todo-empty-mobile">
          {{ nextUp
            ? 'Inget mer att göra just nu'
            : 'Inget att göra just nu' }}
        </p>
        <div v-else class="bg-card overflow-hidden rounded-[11px] border">
          <component
            :is="partLink(item)
              ? RouterLink
              : 'div'"
            v-for="item in moreTodo"
            :key="item.linkId"
            :to="partLink(item)"
            data-test="mobile-todo-item"
            class="flex min-h-[60px] items-center gap-3 border-b px-3.5 py-2 last:border-b-0"
          >
            <span class="bg-primary/25 text-foreground dark:bg-primary/15 dark:text-primary flex size-8 shrink-0 items-center justify-center rounded-[7px]">
              <PhExam class="size-4" aria-hidden="true"/>
            </span>
            <span class="flex min-w-0 flex-1 flex-col">
              <span class="truncate font-medium">{{ item.title }}</span>
              <span class="text-muted-foreground text-[13px]">{{ metaLine(item.courseTitle, item.dueAt) }}</span>
            </span>
            <PhCaretRight class="text-muted-foreground size-4 shrink-0" aria-hidden="true"/>
          </component>
        </div>
      </section>

      <section v-if="recent.length" aria-labelledby="m-recent-heading" class="flex flex-col gap-2">
        <h2 id="m-recent-heading" class="text-[17px]">Nytt</h2>
        <div class="bg-card overflow-hidden rounded-[11px] border">
          <component
            :is="RouterLink"
            v-for="entry in recent"
            :key="entry.key"
            :to="entry.news
              ? newsLink(entry.news)
              : (partLink(entry.feedback!) ?? {name: 'grades'})"
            data-test="mobile-recent-item"
            class="flex min-h-[60px] items-center gap-3 border-b px-3.5 py-2 last:border-b-0"
          >
            <span
              v-if="entry.feedback"
              class="flex size-8 shrink-0 items-center justify-center rounded-[7px] font-bold"
              :class="gradeTileClass(entry.feedback)"
            >
              {{ gradeTile(entry.feedback) }}
            </span>
            <span v-else class="bg-muted flex size-8 shrink-0 items-center justify-center rounded-[7px]">
              <PhNewspaper class="size-4" aria-hidden="true"/>
            </span>
            <span class="flex min-w-0 flex-1 flex-col">
              <span class="truncate font-medium">{{ entry.title }}</span>
              <span class="text-muted-foreground text-[13px]">{{ entry.meta }}</span>
            </span>
          </component>
        </div>
      </section>
    </div>
  </template>
</template>
