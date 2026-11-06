<script setup lang="ts">
import {PhCalendarBlank, PhUsersThree} from '@phosphor-icons/vue'
import {computed, onMounted, onUnmounted, ref, watch} from 'vue'
import {RouterLink, useRoute, useRouter} from 'vue-router'

import AttachmentList from '@/components/course/AttachmentList.vue'
import PartFacts, {type PartFact} from '@/components/course/PartFacts.vue'
import SubmissionsPanel from '@/components/course/SubmissionsPanel.vue'
import MarkdownView from '@/components/editor/MarkdownView.vue'
import StatusBadge from '@/components/StatusBadge.vue'
import {Alert, AlertDescription} from '@/components/ui/alert'
import {Button} from '@/components/ui/button'
import {Empty, EmptyDescription, EmptyHeader, EmptyMedia, EmptyTitle} from '@/components/ui/empty'
import {Skeleton} from '@/components/ui/skeleton'
import {useCrumbLabels} from '@/composables/useCrumbLabels'
import {useCurrentUser} from '@/composables/useCurrentUser'
import {useFlash} from '@/composables/useFlash'
import {isNoAccess, useNoAccess} from '@/composables/useNoAccess'
import {errorMessage, type Schema} from '@/lib/api'
import {parseChecklist} from '@/lib/checklist'
import {formatDate, partOf} from '@/lib/content'
import {fetchContent, setContentStatus} from '@/lib/contentApi'
import {fetchCourseModules} from '@/lib/modulesApi'
import {buildRows, countToGrade} from '@/lib/submissionRows'
import {gradeScaleName, gradeScaleShort, handInLabel} from '@/lib/submissions'
import {relativeText} from '@/lib/today'
import {fetchLinkSubmissions} from '@/lib/submissionsApi'

// Delens sida för den som inte är elev: läraren och admin läser delen här och går vidare till
// redigeraren eller inlämningarna. Eleven har sin egen vy av samma del på kurssidan.
const route = useRoute()
const router = useRouter()
const {setCrumbLabel} = useCrumbLabels()
const {me, can, status, loadMe} = useCurrentUser()
const {leave} = useNoAccess()

// Besked från sidan man kom från, till exempel att en bedömning är sparad.
const notice = ref(useFlash().takeFlash())

const courseId = computed(() => String(route.params.courseId))
const linkId = computed(() => String(route.params.linkId))
const canEdit = computed(() => can('editContent', courseId.value))
const canGrade = computed(() => can('grade', courseId.value))

const moduleId = ref('')
const weekTitle = ref('')
const part = ref<Schema<'ModuleContentItem'> | null>(null)
const content = ref<Schema<'ContentDto'> | null>(null)
const submissions = ref<Schema<'SubmissionDto'>[]>([])
const loading = ref(true)
const error = ref('')
const notFound = ref(false)
const publishing = ref(false)

// Inlämningar finns bara på uppgifter med inlämningssätt, och bara för dem som får läsa dem.
const hasSubmissions = computed(() => canGrade.value && part.value?.completionRule != null)
const tab = computed(() => route.query.tab === 'inlamningar' && hasSubmissions.value
  ? 'submissions'
  : 'content')

const toGrade = computed(() => countToGrade(buildRows(submissions.value)))
// En övning har ingen betygsskala. Läraren kan ge feedback men har inget att bedöma.
const isExercise = computed(() => part.value?.gradeScale === 'None')
// Samma tal som i veckans lista: nämnaren är eleverna som delen delas ut till, inte bara de som har en inlämning.
// Den som inte har börjat har ingen inlämning, så listan räcker inte som nämnare.
const handedIn = computed(() => part.value?.submittedCount
  ?? new Set(submissions.value
    .filter((submission) => submission.status !== 'NotStarted')
    .map((submission) => submission.studentId)).size)
const handedInLabel = computed(() => `${handedIn.value} av ${part.value?.studentCount ?? submissions.value.length}`)

const checklist = computed(() => parseChecklist(content.value?.bodyMd ?? ''))

// Under titeln står bara deadlinen. Inlämningssätt, betygsskala och kompletteringar står i panelen Läge.
const due = computed(() => {
  const dueAt = part.value?.dueAt
  return dueAt
    ? {date: formatDate(dueAt, true), relative: relativeText(dueAt, new Date())}
    : null
})

const facts = computed<PartFact[]>(() => {
  const item = part.value
  if (!item) {
    return []
  }
  const handIn = handInLabel(content.value?.assignment?.submissionModes ?? [])
  return [
    {key: 'handin', label: 'Lämnas in', value: handIn.charAt(0).toUpperCase() + handIn.slice(1)},
    {key: 'scale', label: 'Bedöms', value: gradeScaleShort(item.gradeScale), title: gradeScaleName(item.gradeScale)},
    {key: 'resubmit', label: item.resubmitDueAt.length === 1
      ? 'Komplettering'
      : 'Kompletteringar', value: item.resubmitDueAt.map((value) => formatDate(value, true)).join(', ')},
  ]
})

const isDraft = computed(() => part.value?.status === 'Draft')
const isScheduled = computed(() =>
  !isDraft.value && part.value !== null && new Date(part.value.publishAt) > new Date())

function tabClass(value: 'content' | 'submissions') {
  return tab.value === value
    ? 'border-primary font-medium'
    : 'text-muted-foreground border-transparent'
}

function selectTab(value: 'content' | 'submissions') {
  router.replace({query: value === 'submissions'
    ? {tab: 'inlamningar'}
    : {}})
}

// Eleven har inget att göra här: länken leder till samma del på kurssidan.
function isStudentOfCourse(): boolean {
  return !canGrade.value
    && !can('manageCourse', courseId.value)
    && (me.value?.courses ?? []).some((course) => course.courseId === courseId.value && course.role === 'Student')
}

async function load(showSkeleton = true) {
  if (!showSkeleton && loading.value) {
    return
  }

  // Rollen avgör vad sidan visar, så användaren måste vara hämtad först.
  if (status.value !== 'loaded') {
    await loadMe()
  }
  if (isStudentOfCourse()) {
    await router.replace({
      name: 'course-content',
      params: {courseId: courseId.value},
      query: {del: linkId.value},
    })
    return
  }

  loading.value = showSkeleton
  error.value = ''
  notFound.value = false
  try {
    const weeks = await fetchCourseModules(courseId.value)
    const week = weeks.find((candidate) => candidate.content.some((item) => item.linkId === linkId.value))
    const item = week?.content.find((candidate) => candidate.linkId === linkId.value)
    if (!week || !item) {
      notFound.value = true
      return
    }

    moduleId.value = week.id
    weekTitle.value = week.title
    part.value = item
    setCrumbLabel(`assignment:${linkId.value}`, item.title)
    const [loadedContent, loadedSubmissions] = await Promise.all([
      fetchContent(item.contentId),
      canGrade.value && item.completionRule != null
        ? fetchLinkSubmissions(linkId.value)
        : Promise.resolve([]),
    ])
    content.value = loadedContent
    submissions.value = loadedSubmissions
  } catch (exception) {
    // En kurs eller del som användaren inte får läsa: till Idag, som andra sidor hen inte får öppna.
    if (isNoAccess(exception)) {
      await leave()
      return
    }
    error.value = 'Kunde inte hämta delen.'
  } finally {
    loading.value = false
  }
}

async function publish() {
  if (!part.value) {
    return
  }
  publishing.value = true
  error.value = ''
  try {
    await setContentStatus(part.value.contentId, 'Published')
    await load(false)
  } catch (exception) {
    error.value = errorMessage(exception)
  } finally {
    publishing.value = false
  }
}

function reloadIfVisible() {
  if (document.visibilityState === 'visible') {
    void load(false)
  }
}

let refreshTimer: number | undefined

onMounted(() => {
  refreshTimer = window.setInterval(reloadIfVisible, 30_000)
  window.addEventListener('focus', reloadIfVisible)
})

onUnmounted(() => {
  if (refreshTimer !== undefined) {
    window.clearInterval(refreshTimer)
  }
  window.removeEventListener('focus', reloadIfVisible)
})

watch([courseId, linkId], () => {
  load()
}, {immediate: true})
</script>

<template>
  <div class="flex flex-col gap-5">
    <Alert v-if="notice" data-test="part-notice">
      <AlertDescription>{{ notice }}</AlertDescription>
    </Alert>

    <Alert v-if="error" variant="destructive">
      <AlertDescription>{{ error }}</AlertDescription>
    </Alert>

    <template v-if="loading">
      <Skeleton class="h-10 w-64"/>
      <Skeleton class="h-64 w-full rounded-[11px]"/>
    </template>

    <Empty v-else-if="notFound && !error" class="border border-dashed">
      <EmptyHeader>
        <EmptyMedia variant="icon">
          <PhUsersThree/>
        </EmptyMedia>
        <EmptyTitle>Delen finns inte</EmptyTitle>
        <EmptyDescription>Den kan ha tagits bort från kursen.</EmptyDescription>
      </EmptyHeader>
    </Empty>

    <template v-else-if="part">
      <div class="flex flex-col gap-4" :class="{'border-b pb-4': !hasSubmissions}">
        <div class="flex flex-col gap-2">
          <p class="text-muted-foreground flex flex-wrap items-center gap-x-1.5 gap-y-1 text-sm" data-test="part-eyebrow">
            <component :is="partOf(part).icon" class="size-4"/>
            <span>{{ partOf(part).label }}</span>
            <span aria-hidden="true">·</span>
            <span>{{ weekTitle }}</span>
          </p>
          <div class="flex flex-wrap items-start justify-between gap-3">
            <h1 class="font-heading min-w-0 text-[30px] leading-tight font-semibold">{{ part.title }}</h1>
            <Button
              v-if="canEdit"
              variant="outline"
              as-child
              data-test="edit-part"
            >
              <RouterLink
                :to="{
                  name: 'edit-content',
                  params: {courseId, moduleId, linkId},
                }"
              >
                Redigera
              </RouterLink>
            </Button>
          </div>
          <p v-if="due" class="flex flex-wrap items-center gap-x-2 gap-y-1 text-[15px]" data-test="fact-due">
            <PhCalendarBlank class="text-muted-foreground size-4"/>
            <span>Deadline <strong class="font-semibold">{{ due.date }}</strong></span>
            <span class="text-muted-foreground">{{ due.relative }}</span>
          </p>
        </div>

        <div
          v-if="hasSubmissions"
          role="tablist"
          aria-label="Delens flikar"
          class="flex gap-5 border-b"
        >
          <button
            type="button"
            role="tab"
            :aria-selected="tab === 'content'"
            class="-mb-px h-10 border-b-2 text-[15px]"
            :class="tabClass('content')"
            data-test="tab-content"
            @click="selectTab('content')"
          >
            Innehåll
          </button>
          <button
            type="button"
            role="tab"
            :aria-selected="tab === 'submissions'"
            class="-mb-px h-10 border-b-2 text-[15px]"
            :class="tabClass('submissions')"
            data-test="tab-submissions"
            @click="selectTab('submissions')"
          >
            Inlämningar {{ handedInLabel }}
          </button>
        </div>
      </div>

      <SubmissionsPanel
        v-if="tab === 'submissions'"
        :course-id="courseId"
        :link-id="linkId"
        :submissions="submissions"
        :on-site="(content?.assignment?.submissionModes ?? []).includes('OnSite')"
        :student-count="part?.studentCount ?? undefined"
        :exercise="isExercise"
        @reload="load(false)"
      />

      <div v-else class="grid gap-6 lg:grid-cols-[minmax(0,1fr)_300px]">
        <div class="flex min-w-0 flex-col gap-6">
          <section class="flex max-w-[68ch] flex-col gap-3" data-test="part-content">
            <h2 class="text-lg font-semibold">
              {{ part.type === 'Assignment'
                ? 'Uppgiften'
                : 'Innehåll' }}
            </h2>
            <MarkdownView v-if="checklist.body" :markdown="checklist.body"/>
            <p v-else-if="checklist.items.length === 0" class="text-muted-foreground text-sm">
              Inget innehåll än.
            </p>
          </section>

          <section
            v-if="checklist.items.length > 0"
            class="flex max-w-[68ch] flex-col gap-3"
            aria-labelledby="part-checklist-h"
            data-test="part-checklist"
          >
            <h2 id="part-checklist-h" class="text-lg font-semibold">Att lämna in</h2>
            <ul class="flex list-disc flex-col gap-1.5 pl-5">
              <li v-for="entry in checklist.items" :key="entry.index">{{ entry.text }}</li>
            </ul>
          </section>

          <AttachmentList :content-id="part.contentId" :can-manage="false" class="max-w-[68ch]"/>
        </div>

        <aside class="max-lg:order-first">
          <section class="bg-card flex flex-col gap-3 rounded-xl border p-4" aria-labelledby="part-state-h" data-test="part-state">
            <h2 id="part-state-h" class="text-sm font-semibold">Läge</h2>
            <dl class="flex flex-col gap-2 text-sm">
              <div class="flex items-center justify-between gap-2">
                <dt class="text-muted-foreground">Publicering</dt>
                <dd>
                  <StatusBadge v-if="isDraft" status="publishing.draft"/>
                  <StatusBadge
                    v-else-if="isScheduled"
                    status="publishing.scheduled"
                    :label="`Schemalagd ${formatDate(part.publishAt)}`"
                  />
                  <template v-else>Publicerad {{ formatDate(part.publishAt) }}</template>
                </dd>
              </div>
              <template v-if="hasSubmissions">
                <div class="flex items-center justify-between gap-2">
                  <dt class="text-muted-foreground">Inlämnade</dt>
                  <dd>{{ handedInLabel }}</dd>
                </div>
                <div v-if="!isExercise" class="flex items-center justify-between gap-2">
                  <dt class="text-muted-foreground">Att bedöma</dt>
                  <dd>{{ toGrade }}</dd>
                </div>
              </template>
            </dl>

            <PartFacts :facts="facts" class="border-t pt-3"/>

            <p v-if="isDraft" class="text-muted-foreground text-sm">Eleverna ser inte den här delen än.</p>
            <Button
              v-if="isDraft && canEdit"
              :disabled="publishing"
              data-test="publish-part"
              @click="publish"
            >
              Publicera
            </Button>
            <Button
              v-if="hasSubmissions && toGrade > 0"
              data-test="grade-part"
              @click="selectTab('submissions')"
            >
              Bedöm {{ toGrade }} {{ toGrade === 1
                ? 'inlämning'
                : 'inlämningar' }}
            </Button>
          </section>
        </aside>
      </div>
    </template>
  </div>
</template>
