<script setup lang="ts">
import {PhCaretDown, PhFile, PhInfo} from '@phosphor-icons/vue'
import {computed, ref, watch} from 'vue'
import {
  onBeforeRouteLeave,
  onBeforeRouteUpdate,
  useRoute,
  useRouter,
} from 'vue-router'

import PersonalDueAt from '@/components/course/PersonalDueAt.vue'
import DateTimePicker from '@/components/DateTimePicker.vue'
import StatusBadge from '@/components/StatusBadge.vue'
import MarkdownView from '@/components/editor/MarkdownView.vue'
import {Alert, AlertDescription} from '@/components/ui/alert'
import {Avatar, AvatarFallback, AvatarImage} from '@/components/ui/avatar'
import {Button} from '@/components/ui/button'
import {Collapsible, CollapsibleContent, CollapsibleTrigger} from '@/components/ui/collapsible'
import {Skeleton} from '@/components/ui/skeleton'
import {Textarea} from '@/components/ui/textarea'
import {useCourses} from '@/composables/useCourses'
import {useCrumbLabels} from '@/composables/useCrumbLabels'
import {useCurrentUser} from '@/composables/useCurrentUser'
import {useAsync} from '@/composables/useAsync'
import {useFlash} from '@/composables/useFlash'
import {api, type Schema} from '@/lib/api'
import {parseChecklist} from '@/lib/checklist'
import {formatDate} from '@/lib/content'
import {fetchContent} from '@/lib/contentApi'
import {isoOrNull} from '@/lib/form'
import {
  allowsFail,
  allowsNeedsWork,
  earliestPersonalDate,
  isAutoFail,
  lastClassDeadline,
  nextDeadline,
} from '@/lib/gradingWindow'
import {fetchGradingRows, type GradingRow, nextGradingRow} from '@/lib/gradingQueue'
import {formatSize} from '@/lib/material'
import {fetchCourseModules} from '@/lib/modulesApi'
import {initials} from '@/lib/person'
import {checkClass, checkLabel, gradeLabel, gradeName, levelsFor, nextInQueue} from '@/lib/submissions'
import {submissionStatusKey} from '@/lib/status'
import {fetchLinkSubmissions} from '@/lib/submissionsApi'

const route = useRoute()
const {setCrumbLabel} = useCrumbLabels()
const router = useRouter()
const courseId = computed(() => String(route.params.courseId))
const linkId = computed(() => String(route.params.linkId))
const submissionId = computed(() => String(route.params.submissionId))
const {can} = useCurrentUser()
const {setFlash, takeFlash} = useFlash()
const {courses, status: coursesStatus} = useCourses()

const submission = ref<Schema<'SubmissionDto'> | null>(null)
const assignment = ref<Schema<'ModuleContentItem'> | null>(null)
const feedback = ref('')
const level = ref<Schema<'GradeLevel'> | null>(null)
const formError = ref('')
const notice = ref('')
// Uppgiftstexten och kön hämtas med sidan. Uppgiften är hopfälld tills läraren öppnar den,
// och ligger kvar öppen när läraren går vidare till nästa inlämning.
const task = ref<Schema<'ContentDto'> | null>(null)
const taskOpen = ref(false)
const queue = ref<Schema<'SubmissionDto'>[]>([])
// Det läraren hade när sidan öppnades, för att se om något är ändrat och osparat.
const savedFeedback = ref('')
const savedLevel = ref<Schema<'GradeLevel'> | null>(null)
let leaving = false

// IG och Komplettera följer deadlinerna: IG först när ingen deadline återstår, Komplettera bara så länge en gör det.
// Utan uppgiften i listan finns inga deadlines att gå på, så då erbjuds allt och backend avgör.
const deadlines = computed(() => ({
  dueAt: assignment.value?.dueAt ?? null,
  resubmitDueAt: assignment.value?.resubmitDueAt ?? [],
  personalDueAt: submission.value?.personalDueAt ?? null,
}))
const failAllowed = computed(() => allowsFail(deadlines.value))
const needsWorkAllowed = computed(() => allowsNeedsWork(deadlines.value))
// Sista deadline har passerat utan inlämning eller komplettering: eleven har automatiskt IG.
const autoFail = computed(() => submission.value !== null && isAutoFail(deadlines.value, submission.value.status))
const allLevels = computed(() => levelsFor(assignment.value?.gradeScale ?? null))
const levels = computed(() => allLevels.value.filter((option) => option !== 'Fail' || failAllowed.value))
const failHidden = computed(() => allLevels.value.length > levels.value.length)
const hasGrades = computed(() => allLevels.value.length > 0)
const studentName = computed(() => submission.value?.studentName ?? '')
const repoUrl = computed(() => submission.value?.repo?.repoFullName
  ? `https://github.com/${submission.value.repo.repoFullName}`
  : '')
const shortSha = computed(() => submission.value?.repo?.lastSha?.slice(0, 7) ?? '–')
const taskChecklist = computed(() => parseChecklist(task.value?.bodyMd ?? ''))
const hasTask = computed(() => Boolean(taskChecklist.value.body) || taskChecklist.value.items.length > 0)
// Bara en inlämnad inlämning ingår i kön. Komplettera och Bedömd sparar tillbaka till listan.
const inQueue = computed(() => submission.value?.status === 'Submitted')
const next = computed(() => submission.value && inQueue.value
  ? nextInQueue(queue.value, submission.value)
  : null)
const remaining = computed(() => queue.value.filter((candidate) => candidate.status === 'Submitted').length)
// IG måste motiveras, så sparaknappen väntar på en kommentar.
const needsComment = computed(() => level.value === 'Fail' && !feedback.value.trim())
// Är kön tom går läraren vidare till nästa uppgift med obedömt arbete, annars tillbaka till Att bedöma.
const otherRow = ref<GradingRow | null>(null)
const saveLabel = computed(() => {
  if (!inQueue.value) {
    return 'Spara'
  }
  return next.value || otherRow.value
    ? 'Spara och nästa'
    : 'Spara och tillbaka'
})
const dirty = computed(() => feedback.value !== savedFeedback.value || level.value !== savedLevel.value)

function checkRunLabel(run: Schema<'GitHubCheckRunDto'>): string {
  if (run.status !== 'completed') {
    return 'Körs'
  }
  return run.conclusion === 'success'
    ? 'Godkänd'
    : 'Underkänd'
}

function checkRunClass(run: Schema<'GitHubCheckRunDto'>): string {
  return run.conclusion === 'success'
    ? 'text-success'
    : 'text-warning'
}

// Utan inlämning finns inget att betygsätta eller komplettera. Bedömning på plats är undantaget:
// där sätter läraren betyget i klassrummet utan att eleven lämnat in något.
const nothingHandedIn = computed(() => submission.value?.status === 'NotStarted')
const assessedOnSite = computed(() => task.value?.assignment?.submissionModes.includes('OnSite') ?? false)
const nothingToGrade = computed(() => nothingHandedIn.value && !assessedOnSite.value)

// Läget överst i panelen: vad som gäller för eleven just nu och vad läraren kan göra.
const upcoming = computed(() => nextDeadline(deadlines.value))
const stateLabel = computed(() => {
  const current = submission.value
  if (!current || autoFail.value) {
    return undefined
  }
  if (current.status === 'Submitted' && current.submittedAt) {
    return `Inlämnad ${formatDate(current.submittedAt)}`
  }
  if (current.status === 'NeedsWork') {
    return 'Komplettering begärd'
  }
  return current.status === 'Graded' && current.grade
    ? `Bedömd ${formatDate(current.grade.gradedAt)}`
    : undefined
})
const stateText = computed(() => {
  const current = submission.value
  if (!current) {
    return ''
  }
  const until = upcoming.value
    ? ` Eleven kan lämna in till ${formatDate(upcoming.value, true)}.`
    : ''
  if (autoFail.value) {
    return 'Sista deadline har passerat, så eleven har fått IG automatiskt. Ge eleven en egen deadline för en ny chans.'
  }
  switch (current.status) {
    case 'Submitted':
      return 'Väntar på din bedömning.'
    case 'NeedsWork':
      return `Eleven har inte kompletterat än.${until}`
    case 'Graded':
      return 'Du kan ändra bedömningen.'
    default:
      return assessedOnSite.value
        ? `Uppgiften bedöms på plats. Sätt betyget när eleven har visat sitt arbete.${until}`
        : `Eleven har inte lämnat in något att bedöma än.${until}`
  }
})

// Komplettera kräver en deadline som återstår. Finns ingen väljer läraren en ny deadline för eleven i samma steg.
const workNeedsDate = computed(() => !needsWorkAllowed.value)
const workDate = ref('')
const workMinDate = computed(() => earliestPersonalDate(lastClassDeadline(deadlines.value)))
// En begärd komplettering är redan begärd. Bedömd och inlämnad kan få en, liksom på plats utan inlämning.
const canRequestWork = computed(() => !nothingToGrade.value && submission.value?.status !== 'NeedsWork')

const hasHandedIn = computed(() => Boolean(submission.value?.bodyMd)
  || (submission.value?.files.length ?? 0) > 0
  || Boolean(submission.value?.repo))

// Elevens personliga deadline ändrades: hämta inlämningen igen så att reglerna följer den.
async function reloadSubmission() {
  const {data} = await api.GET('/api/submissions/{id}', {params: {path: {id: submissionId.value}}})
  submission.value = data ?? submission.value
}

const {run: load, loading, error: loadError} = useAsync(async () => {
  leaving = false
  notice.value = takeFlash()
  const [submissionResult, modules, queueResult] = await Promise.all([
    api.GET('/api/submissions/{id}', {params: {path: {id: submissionId.value}}}),
    fetchCourseModules(courseId.value),
    fetchLinkSubmissions(linkId.value),
  ])
  queue.value = queueResult
  const found = submissionResult.data ?? null
  submission.value = found
  const week = modules
    .find((candidate) => candidate.content.some((item) => item.linkId === linkId.value))
  assignment.value = week?.content.find((item) => item.linkId === linkId.value) ?? null
  task.value = assignment.value
    ? await fetchContent(assignment.value.contentId)
    : null
  setCrumbLabel(`assignment:${linkId.value}`, assignment.value?.title)
  setCrumbLabel(`student:${submissionId.value}`, studentName.value)
  if (found?.grade) {
    level.value = found.grade.level
    feedback.value = found.grade.feedback
  } else {
    level.value = null
    feedback.value = ''
  }
  savedFeedback.value = feedback.value
  savedLevel.value = level.value
  workDate.value = ''
}, 'Kunde inte hämta inlämningen.')

const {run: save, loading: saving, error: saveError} = useAsync(async (body: Schema<'GradeSubmissionDto'>) => {
  const current = submission.value
  if (!current) {
    return
  }
  try {
    await api.POST('/api/submissions/{id}/grade', {params: {path: {id: current.id}}, body})
    leaving = true
    setFlash('Bedömningen är sparad.')
    // Listan hämtas igen så att kön speglar det som hände medan läraren bedömde.
    const fresh = inQueue.value
      ? await fetchLinkSubmissions(linkId.value)
      : []
    const following = inQueue.value
      ? nextInQueue(fresh, current)
      : null
    if (following) {
      await router.push({name: 'submission-review', params: {courseId: courseId.value, linkId: linkId.value, submissionId: following.id}})
      return
    }

    if (!inQueue.value) {
      await router.push({name: 'assignment-submissions', params: {courseId: courseId.value, linkId: linkId.value}})
      return
    }

    // Uppgiftens kö är klar: fortsätt med nästa uppgift som har obedömt arbete, annars Att bedöma.
    const elsewhere = nextGradingRow(await fetchGradableRows(), linkId.value)
    await (elsewhere
      ? router.push({name: 'submission-review', params: {courseId: elsewhere.courseId, linkId: elsewhere.linkId, submissionId: elsewhere.submission.id}})
      : router.push({name: 'grading'}))
  } catch (exception) {
    leaving = false
    throw exception
  }
}, 'Kunde inte spara bedömningen.')

async function fetchGradableRows(): Promise<GradingRow[]> {
  try {
    return await fetchGradingRows(courses.value.filter((course) => can('grade', course.id)))
  } catch {
    return []
  }
}

// Slår upp vart Spara leder när det inte finns fler i den här uppgiftens kö.
watch([loading, coursesStatus, inQueue, next], async () => {
  otherRow.value = null
  if (loading.value || coursesStatus.value === 'loading' || !inQueue.value || next.value) {
    return
  }
  const lookedUpFor = submissionId.value
  const found = nextGradingRow(await fetchGradableRows(), linkId.value)
  if (lookedUpFor === submissionId.value) {
    otherRow.value = found
  }
})

async function publish() {
  formError.value = ''
  if (hasGrades.value && (level.value === null || !levels.value.includes(level.value))) {
    formError.value = 'Välj ett betyg.'
    return
  }
  await save({feedback: feedback.value, level: level.value, outcome: 'Graded'})
}

async function requestWork() {
  formError.value = ''
  if (!feedback.value.trim()) {
    formError.value = 'Skriv vad eleven ska komplettera.'
    return
  }
  if (workNeedsDate.value && !workDate.value) {
    formError.value = 'Välj till när eleven får komplettera.'
    return
  }
  await save({
    feedback: feedback.value,
    level: null,
    outcome: 'NeedsWork',
    personalDueAt: workNeedsDate.value
      ? isoOrNull(workDate.value)
      : null,
  })
}

// Hoppa över går till nästa utan att spara. Osparade ändringar frågar guarden om.
async function skip() {
  if (next.value) {
    await router.push({
      name: 'submission-review',
      params: {courseId: courseId.value, linkId: linkId.value, submissionId: next.value.id},
    })
  }
}

function confirmLeave(): boolean {
  if (leaving || !dirty.value) {
    return true
  }
  return window.confirm('Du har osparade ändringar i bedömningen. Lämna sidan ändå?')
}

onBeforeRouteLeave(confirmLeave)
onBeforeRouteUpdate(confirmLeave)

watch([courseId, linkId, submissionId], () => {
  load()
}, {immediate: true})
</script>

<template>
  <div class="flex min-w-0 flex-1 flex-wrap items-stretch">
    <div v-if="loading" class="flex flex-1 flex-col gap-4 p-6">
      <Skeleton class="h-9 w-64"/>
      <Skeleton class="h-40 w-full max-w-[760px]"/>
    </div>

    <Alert v-else-if="loadError" variant="destructive" class="m-4">
      <AlertDescription>{{ loadError }}</AlertDescription>
    </Alert>

    <template v-else-if="submission">
      <div class="flex-[999_1_600px] min-w-0 flex flex-col">
        <div class="flex flex-wrap items-center gap-x-4 gap-y-2 border-b px-6 py-4">
          <Avatar class="size-9">
            <AvatarImage
              v-if="submission?.studentAvatarUrl"
              :src="submission?.studentAvatarUrl"
              alt=""
              data-test="student-avatar-image"
            />
            <AvatarFallback class="text-xs font-semibold">{{ initials(studentName) }}</AvatarFallback>
          </Avatar>
          <div class="flex flex-[1_1_260px] flex-col">
            <h1 class="font-heading text-xl leading-tight font-semibold">{{ studentName }}</h1>
            <div class="text-muted-foreground text-[13px]">
              <span>{{ assignment?.title }}</span>
              <template v-if="submission.submittedAt">
                <span> · Inlämnad {{ formatDate(submission.submittedAt, true) }}</span>
              </template>
              <template v-if="submission.repo?.repoFullName">
                <span> · </span>
                <a
                  :href="repoUrl"
                  target="_blank"
                  rel="noopener"
                  class="font-mono text-xs underline underline-offset-3"
                >{{ submission.repo.repoFullName }}</a>
              </template>
            </div>
          </div>
        </div>

        <Collapsible
          v-if="hasTask"
          v-model:open="taskOpen"
          as="section"
          aria-label="Uppgiften"
          class="max-w-[760px] border-b"
          data-test="review-task"
        >
          <CollapsibleTrigger
            class="group hover:bg-muted/30 flex w-full items-center gap-2 px-6 py-3 text-left transition-colors"
            data-test="review-task-toggle"
          >
            <PhCaretDown
              class="text-muted-foreground size-4 shrink-0 transition-transform duration-200 group-data-[state=closed]:-rotate-90"
            />
            <span class="text-sm font-semibold">Uppgiften</span>
            <span class="text-muted-foreground min-w-0 flex-1 truncate text-[13px]">{{ assignment?.title }}</span>
          </CollapsibleTrigger>
          <CollapsibleContent>
            <div class="flex max-w-[68ch] flex-col gap-4 px-6 pt-1 pb-5">
              <MarkdownView v-if="taskChecklist.body" :markdown="taskChecklist.body"/>
              <section
                v-if="taskChecklist.items.length > 0"
                class="flex flex-col gap-2"
                aria-labelledby="review-checklist-h"
                data-test="review-checklist"
              >
                <h2 id="review-checklist-h" class="text-sm font-semibold">Att lämna in</h2>
                <ul class="flex list-disc flex-col gap-1.5 pl-5 text-sm">
                  <li v-for="entry in taskChecklist.items" :key="entry.index">{{ entry.text }}</li>
                </ul>
              </section>
            </div>
          </CollapsibleContent>
        </Collapsible>

        <div class="flex max-w-[760px] flex-col gap-6 p-6">
          <p v-if="!hasHandedIn" class="text-muted-foreground text-sm">
            Eleven har inte lämnat in något än.
          </p>

          <template v-else>
            <section class="flex flex-col gap-2">
              <h2 class="text-sm font-semibold">Svar</h2>
              <MarkdownView v-if="submission.bodyMd" :markdown="submission.bodyMd"/>
              <p v-else class="text-muted-foreground text-sm">Inget textsvar.</p>
            </section>

            <section v-if="submission.files.length > 0" class="flex flex-col gap-2">
              <h2 class="text-sm font-semibold">Filer</h2>
              <a
                v-for="file in submission.files"
                :key="file.id"
                :href="file.url"
                target="_blank"
                rel="noopener"
                class="flex max-w-[420px] items-center gap-2.5 rounded-[7px] border px-3 py-2.5"
              >
                <PhFile class="text-muted-foreground size-4 shrink-0"/>
                <span class="font-medium">{{ file.fileName }}</span>
                <span class="text-muted-foreground text-xs">{{ formatSize(file.sizeBytes) }}</span>
              </a>
            </section>

            <section v-if="submission.repo" class="flex flex-col gap-2" data-test="review-repo">
              <h2 class="text-sm font-semibold">Repo</h2>
              <a
                v-if="repoUrl"
                :href="repoUrl"
                target="_blank"
                rel="noopener"
                class="font-mono text-[13px] underline underline-offset-3"
              >{{ submission.repo.repoFullName }}</a>
              <p v-else class="text-muted-foreground text-sm">
                {{ submission.repo.provisioningState === 'Failed'
                  ? 'Repot kunde inte skapas.'
                  : 'Repot skapas fortfarande.' }}
              </p>
              <p v-if="submission.repo.lastCommitMessage" class="text-sm">
                {{ submission.repo.lastCommitMessage }}
              </p>
              <p class="text-muted-foreground text-xs">
                Senaste commit <span class="font-mono">{{ shortSha }}</span>
                <template v-if="submission.repo.lastCommitAt">
                  · {{ formatDate(submission.repo.lastCommitAt, true) }}
                </template>
                · {{ submission.repo.commitCount }} commits
              </p>
              <p class="text-[13px]" :class="checkClass(submission.repo.checkStatus)">
                Tester: {{ submission.repo.checksPassed }}/{{ submission.repo.checksTotal }}
                · {{ checkLabel(submission.repo.checkStatus) }}
              </p>
              <ul
                v-if="submission.repo.checkRuns.length > 0"
                class="flex flex-col gap-1 text-xs"
                data-test="review-check-runs"
              >
                <li
                  v-for="run in submission.repo.checkRuns"
                  :key="run.id"
                  class="flex items-center justify-between gap-3 rounded border px-2 py-1"
                >
                  <span class="font-mono">{{ run.name }}</span>
                  <span :class="checkRunClass(run)">{{ checkRunLabel(run) }}</span>
                </li>
              </ul>
              <p v-if="submission.repo.submissionSha" class="text-muted-foreground text-xs">
                Inlämnad commit <span class="font-mono">{{ submission.repo.submissionSha.slice(0, 7) }}</span>
              </p>
              <p v-if="submission.repo.reopenedAt" class="text-warning text-xs">
                Öppnat för komplettering {{ formatDate(submission.repo.reopenedAt, true) }}.
                Eleven kan committa igen.
              </p>
              <p v-if="submission.repo.lockError" class="text-warning text-xs">
                {{ submission.repo.lockError }}
              </p>
            </section>
          </template>
        </div>
      </div>

      <aside
        aria-label="Bedömning"
        class="flex-[1_1_340px] min-w-0 border-l bg-muted/40 max-md:border-l-0 max-md:border-t"
      >
        <!-- Följer med när läraren scrollar en lång inlämning, så betyg och knappar alltid syns. -->
        <div
          data-test="review-panel"
          class="flex flex-col gap-5 p-5 md:sticky md:top-0 md:max-h-svh md:min-h-[calc(100svh-56px)] md:overflow-y-auto"
        >
          <section class="flex flex-col items-start gap-1.5" data-test="review-state">
            <StatusBadge :status="submissionStatusKey(submission.status, autoFail)" :label="stateLabel"/>
            <p class="text-muted-foreground text-[13px]" data-test="review-state-text">{{ stateText }}</p>
          </section>

          <section v-if="!nothingToGrade" class="flex flex-col gap-2">
            <h2 class="text-sm font-semibold">Betyg</h2>
            <div
              v-if="hasGrades"
              role="radiogroup"
              class="grid gap-2"
              :class="levels.length === 3
                ? 'grid-cols-3'
                : 'grid-cols-2'"
            >
              <button
                v-for="option in levels"
                :key="option"
                type="button"
                role="radio"
                :aria-checked="level === option"
                class="h-11 rounded-[7px] border bg-background flex flex-col items-center justify-center leading-tight"
                :class="level === option
                  ? 'bg-primary text-primary-foreground border-primary'
                  : ''"
                @click="level = option"
              >
                <span class="font-bold" aria-hidden="true">{{ gradeLabel(option) }}</span>
                <span
                  class="text-[11px] text-muted-foreground"
                  :class="level === option
                    ? 'text-primary-foreground/80'
                    : ''"
                >{{ gradeName(option) }}</span>
              </button>
            </div>
            <p v-else class="text-muted-foreground text-sm">Uppgiften har inget betyg, bara återkoppling.</p>
            <p v-if="failHidden" class="text-muted-foreground text-[13px]" data-test="review-fail-later">
              IG kan sättas när sista deadline har passerat. Så länge en deadline återstår kan du begära komplettering.
            </p>
          </section>

          <!-- Utan inlämning finns inget att kommentera och inget att spara. -->
          <div v-if="!nothingToGrade" class="flex flex-col gap-2">
            <label for="feedback" class="font-heading text-sm font-semibold">Sammanfattande kommentar</label>
            <Textarea
              id="feedback"
              v-model="feedback"
              :rows="4"
              class="bg-background"
              placeholder="Skriv en kommentar till eleven…"
            />
          </div>

          <!-- Ingen deadline återstår: kompletteringen behöver en ny deadline för eleven, som sparas i samma steg. -->
          <div v-if="canRequestWork && workNeedsDate" class="flex flex-col gap-2" data-test="review-work-date">
            <label class="text-sm font-semibold">Kompletteras senast</label>
            <DateTimePicker
              v-model="workDate"
              class="bg-background"
              :min="workMinDate"
              default-time="23:59"
            />
            <p class="text-muted-foreground text-[13px]">
              Ingen deadline återstår. Välj till när eleven får komplettera, så blir det elevens egen deadline.
            </p>
          </div>

          <Alert v-if="notice" data-test="review-notice">
            <AlertDescription>{{ notice }}</AlertDescription>
          </Alert>

          <p v-if="formError" class="text-destructive text-[13px]">{{ formError }}</p>

          <Alert v-if="saveError" variant="destructive">
            <AlertDescription>{{ saveError }}</AlertDescription>
          </Alert>

          <div v-if="!nothingToGrade" class="flex flex-col gap-2">
            <p class="text-muted-foreground flex items-center gap-1.5 text-xs">
              <PhInfo class="size-3.5"/>
              Eleven ser betyget och kommentaren direkt.
            </p>
            <p v-if="inQueue" class="text-sm font-medium" data-test="review-remaining">
              {{ remaining }} kvar att bedöma
            </p>
            <p v-if="needsComment" class="text-muted-foreground text-[13px]" data-test="review-fail-hint">
              Skriv en kommentar som förklarar varför eleven får IG.
            </p>
            <Button
              class="w-full"
              :disabled="saving || needsComment"
              data-test="review-save"
              @click="publish"
            >
              {{ saveLabel }}
            </Button>
            <Button
              v-if="canRequestWork"
              variant="outline"
              class="w-full"
              :disabled="saving || (workNeedsDate && !workDate)"
              data-test="review-request-work"
              @click="requestWork"
            >
              Begär komplettering
            </Button>
            <Button
              v-if="next"
              variant="ghost"
              class="w-full"
              :disabled="saving"
              data-test="review-skip"
              @click="skip"
            >
              Hoppa över till nästa
            </Button>
          </div>

          <!-- En ny chans för en elev som inte lämnat in eller inte kompletterat. Skilt från bedömningen ovan. -->
          <div class="border-t pt-5 empty:hidden">
            <PersonalDueAt
              :link-id="linkId"
              :student-id="submission.studentId"
              :personal-due-at="submission.personalDueAt ?? null"
              :due-at="assignment?.dueAt ?? null"
              :resubmit-due-at="assignment?.resubmitDueAt ?? []"
              :status="submission.status"
              @saved="reloadSubmission"
            />
          </div>
        </div>
      </aside>
    </template>
  </div>
</template>
