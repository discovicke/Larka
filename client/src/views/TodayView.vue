<script setup lang="ts">
import {PhCaretRight, PhExam, PhNewspaper} from '@phosphor-icons/vue'
import {computed, ref} from 'vue'
import {RouterLink, type RouteLocationRaw} from 'vue-router'

import {Alert, AlertDescription} from '@/components/ui/alert'
import StatusBadge from '@/components/StatusBadge.vue'
import GitHubOnboardingBanner from '@/components/GitHubOnboardingBanner.vue'
import AdminToday from '@/components/today/AdminToday.vue'
import TeacherToday from '@/components/today/TeacherToday.vue'
import {Empty, EmptyDescription, EmptyHeader, EmptyTitle} from '@/components/ui/empty'
import {Skeleton} from '@/components/ui/skeleton'
import {useAsync} from '@/composables/useAsync'
import {useCurrentUser} from '@/composables/useCurrentUser'
import {api, type Schema, unwrap} from '@/lib/api'
import {formatDate} from '@/lib/content'
import {splitFinished} from '@/lib/courseStatus'
import {isStudentEverywhere, teachesAnywhere} from '@/lib/permissions'
import {deadlineStatusKey, submissionStatusKey} from '@/lib/status'
import {gradeText} from '@/lib/studyPlan'
import {gradeLabel, gradeName} from '@/lib/submissions'
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
  isOverdue,
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
// Avslutade kurser ligger kvar i Studieplan och i sidomenyn, men inte bland dagens kurser.
const currentCourses = computed(() => splitFinished(today.value?.courses ?? []).current)
const tab = ref<Tab>('upcoming')
// Idag följer relationen: admin ser skolans översikt, den som undervisar i en kurs ser lärarens, och alla andra,
// eleven, ser elevens. Båda hämtar sin egen data. Admin undervisar aldrig, så ingen ser båda.
const isAdmin = ref(false)
const isTeacher = ref(false)
// Den som har loggat in första gången utan att hamna i en klass har inget att se än.
const noClass = computed(() => !!me.value && !isAdmin.value && !isTeacher.value
  && me.value.groups.length === 0 && me.value.courses.length === 0)

const name = computed(() => firstName(me.value?.name))
const mobileGreeting = computed(() => name.value
  ? `Hej ${name.value}`
  : 'Hej')
// Delar som läraren vill att eleven kompletterar kommer först, före allt annat att göra.
const todo = computed(() => {
  const items = today.value?.todo ?? []
  return [
    ...items.filter((item) => item.status === 'NeedsWork'),
    ...items.filter((item) => item.status !== 'NeedsWork'),
  ]
})
const groups = computed(() => {
  const needsWork = todo.value.filter((item) => item.status === 'NeedsWork')
  const {overdue, thisWeek, later} = groupByDue(todo.value.filter((item) => item.status !== 'NeedsWork'), now)
  return [
    {heading: 'Att komplettera', items: needsWork},
    {heading: 'Försenat', items: overdue},
    {heading: 'Den här veckan', items: thisWeek},
    {heading: 'Senare', items: later},
  ].filter((group) => group.items.length > 0)
})
// Bara det som förfaller från nu räknas: en komplettering med passerad deadline står i Att göra men förfaller inte.
const dueThisWeek = computed(() => splitByWeek(
  (today.value?.todo ?? []).filter((item) => !isOverdue(item.dueAt, now)),
  now,
).thisWeek.length)
const nextUp = computed(() => todo.value[0])
// Lärarens kommentar till det som ska kompletteras, så att Närmast på mobilen säger vad som behövs.
const nextUpFeedback = computed(() => nextUp.value?.status === 'NeedsWork'
  ? today.value?.feedback?.find((item) => item.linkId === nextUp.value?.linkId)?.feedback ?? ''
  : '')
const moreTodo = computed(() => todo.value.slice(1))

// "Nytt" på mobilen: betyg och nyheter i en lista, de senaste först.
const recent = computed(() => {
  const feedback = (today.value?.feedback ?? []).map((item) => ({
    key: `feedback-${item.linkId}`,
    title: item.title,
    meta: `${feedbackLabel(item)}, ${timeLabel(item.gradedAt)}`,
    at: item.gradedAt,
    feedback: item,
    news: null,
  }))
  const news = (today.value?.news ?? []).map((item) => ({
    key: `news-${item.contentId}`,
    title: item.title,
    meta: [item.source, timeLabel(item.publishAt)].filter(Boolean).join(', '),
    at: item.publishAt,
    feedback: null,
    news: item,
  }))
  // Återkoppling på det som ska kompletteras står först, så att den syns även när nyheterna är nyare.
  const needsWorkIds = new Set(todo.value.filter((item) => item.status === 'NeedsWork').map((item) => item.linkId))
  const first = (entry: {feedback: {linkId: string} | null}) => entry.feedback && needsWorkIds.has(entry.feedback.linkId)
    ? 0
    : 1
  return [...feedback, ...news]
    .sort((left, right) => first(left) - first(right) || Date.parse(right.at) - Date.parse(left.at))
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

// Återkoppling utan betyg är antingen en komplettering eller feedback på en övning, som inte har någon betygsskala.
function feedbackLabel(item: Feedback): string {
  if (item.grade) {
    return 'Betyg'
  }
  return item.needsWork
    ? 'Komplettera'
    : 'Återkoppling'
}

// Brickan är en bokstav i en rund ruta, och får hela namnet som etikett.
function gradeTile(item: Feedback): string {
  if (item.grade) {
    return gradeLabel(item.grade)
  }
  return item.needsWork
    ? 'K'
    : 'Å'
}

function gradeTileName(item: Feedback): string {
  return item.grade
    ? gradeName(item.grade)
    : feedbackLabel(item)
}

function gradeTileClass(item: Feedback): string {
  if (item.grade === 'Fail' || (!item.grade && item.needsWork)) {
    return 'bg-grade-fail/10 text-grade-fail dark:bg-grade-fail/20'
  }
  if (item.grade === 'Distinction') {
    return 'bg-primary/25 text-foreground dark:bg-primary/15 dark:text-primary'
  }
  return item.grade
    ? 'bg-grade-pass/10 text-grade-pass dark:bg-grade-pass/20'
    : 'bg-muted text-muted-foreground'
}

function statusText(item: Submission): string {
  return item.grade
    ? gradeText(item.grade)
    : 'Bedömd'
}

function dueStatus(item: Todo) {
  return deadlineStatusKey(item.dueAt, false, now)
}

function tabClass(value: Tab): string {
  return tab.value === value
    ? 'bg-background text-foreground font-medium shadow-sm'
    : 'text-muted-foreground'
}

const {run: load, loading, error} = useAsync(async () => {
  await loadMe()
  isAdmin.value = me.value?.role === 'Admin'
  isTeacher.value = teachesAnywhere(me.value)
  if (!isStudentEverywhere(me.value)) {
    return
  }
  today.value = unwrap(await api.GET('/api/me/today'))
})
const loadFailed = computed(() => error.value !== '')

load()
</script>

<template>
  <GitHubOnboardingBanner/>

  <div v-if="isAdmin" class="flex flex-col gap-6" data-test="admin-today">
    <AdminToday/>
    <TeacherToday v-if="isTeacher" embedded/>
  </div>

  <TeacherToday v-else-if="isTeacher"/>

  <Alert v-else-if="loadFailed" variant="destructive">
    <AlertDescription>Kunde inte hämta din översikt.</AlertDescription>
  </Alert>

  <div v-else-if="loading" class="flex flex-col gap-4" data-test="today-loading">
    <Skeleton class="h-9 w-72"/>
    <Skeleton class="h-64 w-full"/>
  </div>

  <Empty v-else-if="noClass" class="border border-dashed" data-test="today-no-class">
    <EmptyHeader>
      <EmptyTitle>Du är inte med i någon klass än</EmptyTitle>
      <EmptyDescription>
        Skolans admin lägger in dig i din klass. Sedan ser du dina kurser, uppgifter och nyheter här.
      </EmptyDescription>
    </EmptyHeader>
  </Empty>

  <template v-else-if="today">
    <!-- Dator -->
    <div class="hidden flex-col gap-6 md:flex" data-test="today-desktop">
      <div class="flex flex-col gap-1">
        <h1 class="text-[30px] leading-[1.15]">{{ greeting(now) }}<template v-if="name">, {{ name }}</template></h1>
        <p class="text-muted-foreground flex flex-wrap items-center gap-2">
          <span>{{ dateLine(now) }}</span>
          <span class="bg-border h-3 w-px" aria-hidden="true"/>
          <span>{{ dueThisWeekText(dueThisWeek) }}</span>
        </p>
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
                role="img"
                :aria-label="gradeTileName(item)"
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
                <template v-if="item.source">{{ item.source }}, </template>{{ timeLabel(item.publishAt) }}
              </span>
            </RouterLink>
          </section>
        </div>
      </div>

      <section aria-labelledby="courses-heading" class="flex flex-col gap-3">
        <h2 id="courses-heading" class="text-base">Mina kurser</h2>
        <p v-if="!currentCourses.length" class="text-muted-foreground text-sm" data-test="courses-empty">
          Du läser inga kurser än
        </p>
        <div class="grid grid-cols-[repeat(auto-fill,minmax(min(300px,100%),1fr))] gap-4">
          <RouterLink
            v-for="course in currentCourses"
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
        <span class="text-xs font-medium tracking-[0.04em] text-sidebar-foreground/70 uppercase" data-test="next-up-label">
          {{ nextUp.status === 'NeedsWork'
            ? 'Att komplettera'
            : 'Närmast' }}
        </span>
        <span class="text-[17px] leading-[1.3] font-semibold">{{ nextUp.title }}</span>
        <span v-if="nextUpFeedback" class="line-clamp-2 text-sm text-sidebar-foreground/85" data-test="next-up-feedback">
          {{ nextUpFeedback }}
        </span>
        <span class="flex justify-between gap-3 text-[13px] text-sidebar-foreground/70">
          <span>{{ nextUp.courseTitle }}</span>
          <span class="font-medium text-sidebar-primary">
            {{ isOverdue(nextUp.dueAt, now)
              ? `Deadline passerad ${weekdayAndTime(nextUp.dueAt)}`
              : weekdayAndTime(nextUp.dueAt) }}
          </span>
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
              <span class="text-muted-foreground flex flex-wrap items-center gap-x-2 text-[13px]">
                <span>{{ metaLine(item.courseTitle, item.dueAt) }}</span>
                <StatusBadge v-if="item.status === 'NeedsWork'" status="submission.needsWork"/>
              </span>
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
              role="img"
              :aria-label="gradeTileName(entry.feedback)"
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
