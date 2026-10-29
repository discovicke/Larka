<script setup lang="ts">
import {
  PhArrowLeft,
  PhCalendarBlank,
  PhCaretDown,
  PhCaretRight,
  PhCheckCircle,
  PhCircle,
  PhEye,
  PhLock,
  PhLockOpen,
  PhPaperPlaneTilt,
} from '@phosphor-icons/vue'
import type {Component} from 'vue'
import {computed, onMounted, ref, watch} from 'vue'
import {useRoute, useRouter} from 'vue-router'

import GitHubAssignmentBlock from '@/components/GitHubAssignmentBlock.vue'
import SubmissionPanel from '@/components/student/SubmissionPanel.vue'
import {Alert, AlertDescription} from '@/components/ui/alert'
import {Badge} from '@/components/ui/badge'
import {Button} from '@/components/ui/button'
import {Collapsible, CollapsibleContent, CollapsibleTrigger} from '@/components/ui/collapsible'
import {Item, ItemMedia} from '@/components/ui/item'
import {Skeleton} from '@/components/ui/skeleton'
import {useCurrentUser} from '@/composables/useCurrentUser'
import {api, errorMessage, type Schema} from '@/lib/api'
import {countLabel, formatDate, partOf} from '@/lib/content'
import {gradeBadgeVariant, gradeLabel as gradeShort} from '@/lib/submissions'

// Elevens vy av en kurs: veckor, låsta delar och klarmarkering. Den visas i stället för redigeraren
// på kursens innehållssida för den som inte kan redigera. Servern vet vilka grupper eleven går i,
// så klienten skickar bara kursen.

const props = defineProps<{courseId: string}>()

const route = useRoute()
const router = useRouter()
const {me, loadMe} = useCurrentUser()
const modules = ref<Schema<'StudentModuleWithContentDto'>[]>([])
const loading = ref(true)
const error = ref('')
const savingLinkId = ref('')
const selectedLinkId = ref('')
const filter = ref<'all' | 'todo' | 'submissions'>('all')

// Framtida veckor är låsta skal. Deras eventuella innehåll får inte räknas i progress eller
// dyka upp i detaljvyn, även om API:t skulle skicka med något.
const availableModules = computed(() => modules.value.filter((module) => module.isAvailable))
const allItems = computed(() => availableModules.value.flatMap((module) => module.content))
const visibleModules = computed(() => filter.value === 'all'
  ? modules.value
  : availableModules.value)
const completable = computed(() => allItems.value.filter((item) => item.type !== 'News'))
const completedCount = computed(() => completable.value.filter((item) => item.isCompleted).length)
const todoCount = computed(() => completable.value.length - completedCount.value)
const availableCount = computed(() => allItems.value.filter(isAvailable).length)
const submissionCount = computed(() => allItems.value.filter((item) => kindOf(item) === 'Inlämning').length)
const gradedCount = computed(() => allItems.value.filter((item) => item.submissionStatus === 'Graded').length)
const lockedCount = computed(() => allItems.value.filter((item) => item.isLocked && !item.isCompleted).length)
const progressPercent = computed(() => completable.value.length === 0
  ? 0
  : Math.round((completedCount.value / completable.value.length) * 100))

const selectedModule = computed(() => availableModules.value.find((module) =>
  module.content.some((item) => item.linkId === selectedLinkId.value)))
const selectedItem = computed(() => selectedModule.value?.content
  .find((item) => item.linkId === selectedLinkId.value) ?? null)
const unlocked = computed(() => selectedModule.value && selectedItem.value
  ? selectedModule.value.content.filter((item) =>
      item.blockedBy.some((blocker) => blocker.linkId === selectedItem.value!.linkId))
  : [])

const filters: {value: 'all' | 'todo' | 'submissions', label: string}[] = [
  {value: 'all', label: 'Allt'},
  {value: 'todo', label: 'Att göra nu'},
  {value: 'submissions', label: 'Inlämningar'},
]

watch(() => props.courseId, async () => {
  selectedLinkId.value = ''
  await load()
}, {immediate: true})

onMounted(loadMe)

// Efter en inlämning hämtas veckorna om utan skelett, så att vyn och statusen uppdateras på plats.
async function reload() {
  try {
    const {data} = await api.GET('/api/courses/{courseId}/student-modules', {
      params: {path: {courseId: props.courseId}},
    })
    modules.value = data ?? []
  } catch (exception) {
    error.value = errorMessage(exception)
  }
}

async function load() {
  loading.value = true
  error.value = ''
  try {
    const {data} = await api.GET('/api/courses/{courseId}/student-modules', {
      params: {path: {courseId: props.courseId}},
    })
    modules.value = data ?? []
  } catch (exception) {
    error.value = errorMessage(exception)
    modules.value = []
    selectedLinkId.value = ''
  } finally {
    loading.value = false
  }
  syncFromQuery()
}

async function setCompleted(linkId: string, completed: boolean) {
  savingLinkId.value = linkId
  error.value = ''
  try {
    const {data} = await api.PUT('/api/courses/{courseId}/links/{linkId}/completion', {
      params: {path: {courseId: props.courseId, linkId}},
      body: {completed},
    })
    if (data) {
      modules.value = modules.value.map((module) => module.id === data.id
        ? data
        : module)
    }
  } catch (exception) {
    error.value = errorMessage(exception)
    await load()
  } finally {
    savingLinkId.value = ''
  }
}

// Den öppna delen bor i adressen (?del=<linkId>), så att notiser och länkar kan peka på den och
// bakåtknappen fungerar. Okända och låsta delar, eller delar i en vecka som inte öppnat, ignoreras
// och eleven hamnar i översikten.
const wantedLinkId = computed(() => typeof route.query.del === 'string'
  ? route.query.del
  : '')

watch(wantedLinkId, syncFromQuery)

// Lärarens kommentar och betyg hör till inlämningen, så de hämtas när en bedömd uppgift öppnas.
const feedback = ref<Schema<'GradeDto'> | null>(null)

watch(() => {
  const item = selectedItem.value
  return item?.type === 'Assignment' && (item.submissionStatus === 'Graded' || item.submissionStatus === 'NeedsWork')
    ? item.linkId
    : ''
}, async (linkId) => {
  feedback.value = null
  if (!linkId) {
    return
  }
  try {
    const {data} = await api.GET('/api/links/{id}/submissions', {params: {path: {id: linkId}}})
    if (selectedItem.value?.linkId === linkId) {
      feedback.value = data?.[0]?.grade ?? null
    }
  } catch {
    // Kommentaren är ett tillägg; statusen i sidopanelen visas ändå.
  }
}, {immediate: true})

function syncFromQuery() {
  if (loading.value) {
    return
  }
  const wanted = wantedLinkId.value
  const item = allItems.value.find((candidate) => candidate.linkId === wanted)
  if (item && !item.isLocked) {
    selectedLinkId.value = wanted
    return
  }
  selectedLinkId.value = ''
  if (wanted && modules.value.length > 0) {
    setQuery('')
  }
}

function setQuery(linkId: string) {
  const query = {...route.query}
  delete query.del
  router.replace({query: linkId
    ? {...query, del: linkId}
    : query})
}

function openPart(item: Schema<'StudentContentItem'>) {
  selectedLinkId.value = item.linkId
  // En låst del visar bara förklaringen och går inte att länka till.
  setQuery(item.isLocked
    ? ''
    : item.linkId)
}

function repoUrlFor(item: Schema<'StudentContentItem'>): string | null {
  return item.repo?.repoFullName
    ? `https://github.com/${item.repo.repoFullName}`
    : null
}

function closePart() {
  selectedLinkId.value = ''
  setQuery('')
}

interface PartSection {
  key: 'next' | 'unlocking' | 'later' | 'news' | 'completed'
  title: string
  items: Schema<'StudentContentItem'>[]
  defaultOpen: boolean
  empty: string
}

function isAvailable(item: Schema<'StudentContentItem'>): boolean {
  return item.type !== 'News' && !item.isLocked && !item.isCompleted
}

function sectionsFor(module: Schema<'StudentModuleWithContentDto'>): PartSection[] {
  const available = module.content.filter(isAvailable)
  const availableIds = new Set(available.map((item) => item.linkId))
  const locked = module.content.filter((item) =>
    item.type !== 'News' && item.isLocked && !item.isCompleted)

  // Bara låsta delar som öppnas när delarna i "Nästa steg" är klara. Då visas ett lager fram,
  // inte hela beroendekedjan.
  const unlocking = locked.filter((item) => {
    const missing = missingBlockers(item)
    return missing.length > 0 && missing.every((blocker) => availableIds.has(blocker.linkId))
  })
  const unlockingIds = new Set(unlocking.map((item) => item.linkId))
  const later = locked.filter((item) => !unlockingIds.has(item.linkId))

  const sections: PartSection[] = [
    {
      key: 'next',
      title: 'Nästa steg',
      items: available,
      defaultOpen: true,
      empty: filter.value === 'submissions'
        ? 'Inga inlämningar att göra just nu.'
        : module.content.length === 0
          ? 'Inga publicerade delar i veckan än.'
          : 'Inga delar att göra just nu.',
    },
    {
      key: 'unlocking',
      title: 'Låses upp härnäst',
      items: unlocking,
      defaultOpen: true,
      empty: '',
    },
    {
      key: 'later',
      title: 'Låst senare',
      items: later,
      defaultOpen: false,
      empty: '',
    },
    {
      key: 'news',
      title: 'Nyheter',
      items: module.content.filter((item) => item.type === 'News'),
      defaultOpen: true,
      empty: '',
    },
    {
      key: 'completed',
      title: 'Klart',
      items: module.content.filter((item) => item.type !== 'News' && item.isCompleted),
      defaultOpen: false,
      empty: '',
    },
  ]

  if (filter.value === 'todo') {
    return sections.slice(0, 1)
  }

  if (filter.value === 'submissions') {
    const withSubmissions = sections.map((section) => ({
      ...section,
      items: section.items.filter((item) => kindOf(item) === 'Inlämning'),
    }))
    return [
      withSubmissions[0],
      ...withSubmissions.slice(1).filter((section) => section.items.length > 0),
    ]
  }

  return sections.filter((section) => section.key === 'next' || section.items.length > 0)
}

function filterLabel(value: 'all' | 'todo' | 'submissions'): string {
  if (value === 'todo') {
    return `Att göra nu (${availableCount.value})`
  }
  if (value === 'submissions') {
    return `Inlämningar (${submissionCount.value})`
  }
  return 'Allt'
}

function moduleProgress(module: Schema<'StudentModuleWithContentDto'>): {done: number, total: number} {
  const parts = module.content.filter((item) => item.type !== 'News')
  return {
    done: parts.filter((item) => item.isCompleted).length,
    total: parts.length,
  }
}

function kindOf(item: Schema<'StudentContentItem'>): string {
  return partOf(item).label
}

function detailsOf(item: Schema<'StudentContentItem'>): string {
  const scale = gradeScaleLabel(item.gradeScale)
  return scale
    ? `${kindOf(item)} · ${scale}`
    : kindOf(item)
}

function gradeScaleLabel(scale: Schema<'GradeScale'> | null): string | null {
  if (scale === 'PassFail') {
    return 'IG–G'
  }
  if (scale === 'PassFailDistinction') {
    return 'IG–VG'
  }
  return null
}

function rowIcon(item: Schema<'StudentContentItem'>): Component {
  if (item.isCompleted) {
    return PhCheckCircle
  }
  if (item.isLocked) {
    return PhLock
  }
  if (item.submissionStatus === 'Submitted') {
    return PhPaperPlaneTilt
  }
  return PhCircle
}

function rowIconClass(item: Schema<'StudentContentItem'>): string {
  if (item.isCompleted) {
    return 'text-success'
  }
  if (item.isLocked) {
    return 'text-muted-foreground'
  }
  if (item.submissionStatus === 'Submitted') {
    return 'text-info'
  }
  if (item.submissionStatus === 'NeedsWork' || item.gradeLevel === 'Fail') {
    return 'text-destructive'
  }
  return 'text-muted-foreground'
}

function missingBlockers(item: Schema<'StudentContentItem'>): Schema<'StudentBlockerDto'>[] {
  return item.blockedBy.filter((blocker) => !blocker.isSatisfied)
}

function submissionLabel(item: Schema<'StudentContentItem'>): string {
  if (item.submissionStatus === 'Submitted') {
    return 'Inlämnad'
  }
  if (item.submissionStatus === 'NeedsWork') {
    return 'Behöver kompletteras'
  }
  if (item.submissionStatus === 'Graded') {
    return gradeLabel(item.gradeLevel)
  }
  return 'Inte inlämnad'
}

function gradeLabel(level: Schema<'GradeLevel'> | null): string {
  if (level === 'Pass') {
    return 'Godkänd (G)'
  }
  if (level === 'Distinction') {
    return 'Väl godkänd (VG)'
  }
  if (level === 'Fail') {
    return 'Icke godkänd (IG)'
  }
  return 'Betygsatt'
}

function statusHeading(item: Schema<'StudentContentItem'>): string {
  if (item.isCompleted) {
    return 'Klar'
  }
  if (item.isLocked) {
    return 'Låst'
  }
  if (item.type === 'Assignment') {
    return submissionLabel(item)
  }
  return 'Inte påbörjad'
}

function dueLabel(item: Schema<'StudentContentItem'>): string {
  return item.dueAt
    ? `Slutar ${formatDate(item.dueAt, true)}`
    : ''
}
</script>

<template>
  <div class="flex flex-col gap-6" data-test="student-view">
    <Alert v-if="error" variant="destructive">
      <AlertDescription>{{ error }}</AlertDescription>
    </Alert>

    <template v-if="loading">
      <Skeleton v-for="index in 2" :key="index" class="h-32 w-full rounded-xl"/>
    </template>

    <p v-else-if="modules.length === 0 && !error" class="text-muted-foreground text-sm">
      Kursen har inga veckor än.
    </p>

    <template v-else-if="selectedItem && selectedModule">
      <div class="flex flex-col gap-6" data-test="student-detail">
        <div class="text-muted-foreground flex flex-wrap items-center gap-1 text-sm">
          <Button
            variant="ghost"
            size="sm"
            data-test="student-back"
            @click="closePart"
          >
            <PhArrowLeft/>
            Tillbaka
          </Button>
        </div>

        <header class="flex flex-col gap-3 border-b pb-4">
          <div class="flex flex-wrap items-center gap-2">
            <Badge variant="outline">
              <component :is="partOf(selectedItem).icon"/>
              {{ kindOf(selectedItem) }}
            </Badge>
            <Badge v-if="gradeScaleLabel(selectedItem.gradeScale)" variant="outline">
              Betygsskala {{ gradeScaleLabel(selectedItem.gradeScale) }}
            </Badge>
            <Badge
              v-if="selectedItem.isLocked"
              variant="outline"
              class="text-muted-foreground"
            >
              <PhLock/>
              Låst
            </Badge>
            <Badge
              v-else-if="selectedItem.isCompleted"
              variant="outline"
              class="border-success/20 bg-success/10 text-success dark:border-success/30 dark:bg-success/20"
            >
              <PhCheckCircle/>
              Klar
            </Badge>
          </div>
          <h2 class="text-3xl leading-tight font-semibold">{{ selectedItem.title }}</h2>
          <p class="text-muted-foreground flex flex-wrap items-center gap-x-2 gap-y-1 text-sm">
            <span>{{ selectedModule.title }}</span>
            <template v-if="selectedItem.dueAt">
              <span>·</span>
              <span class="font-medium text-destructive">{{ dueLabel(selectedItem) }}</span>
            </template>
            <template v-if="selectedItem.type === 'News'">
              <span>·</span>
              <span>Nyhet</span>
            </template>
          </p>
        </header>

        <div class="grid gap-6 lg:grid-cols-[minmax(0,1fr)_320px]">
          <div class="flex flex-col gap-6">
            <section v-if="selectedItem.isLocked" class="bg-card flex flex-col items-start gap-3 rounded-xl border border-dashed p-6">
              <span class="bg-muted grid size-10 place-items-center rounded-full">
                <PhLock class="text-muted-foreground size-5"/>
              </span>
              <div>
                <h2 class="text-lg font-semibold">Den här delen är låst</h2>
                <p class="text-muted-foreground text-sm">
                  Öppnas när du har klarat de delar den väntar på. Börja med delarna i Nästa steg.
                </p>
              </div>
            </section>

            <template v-else>
              <section class="flex max-w-[68ch] flex-col gap-3">
                <h2 class="text-lg font-semibold">
                  {{ selectedItem.type === 'Assignment'
                    ? 'Uppgiften'
                    : 'Innehåll' }}
                </h2>
                <p
                  v-if="selectedItem.bodyMd"
                  class="text-foreground/90 text-sm leading-relaxed whitespace-pre-wrap"
                >
                  {{ selectedItem.bodyMd }}
                </p>
                <p v-else class="text-muted-foreground text-sm">Inget innehåll än.</p>
              </section>
            </template>

            <GitHubAssignmentBlock
              v-if="selectedItem.hasRepoMode && !selectedItem.isLocked"
              :git-hub-login="me?.gitHubLogin"
              :repo-url="repoUrlFor(selectedItem)"
              :latest-commit="selectedItem.repo?.lastSha"
              :check-status="selectedItem.repo?.checkStatus"
              :repo-provisioning-state="selectedItem.repo?.provisioningState"
              :repo-provisioning-error="selectedItem.repo?.provisioningError"
              :redirect-to="route.fullPath"
            />

            <section
              v-if="feedback"
              class="bg-card flex flex-col gap-2 rounded-xl border p-4"
              aria-labelledby="feedback-h"
              data-test="student-feedback"
            >
              <div class="flex flex-wrap items-center justify-between gap-2">
                <h2 id="feedback-h" class="text-sm font-semibold">
                  {{ selectedItem.submissionStatus === 'NeedsWork'
                    ? 'Läraren vill att du kompletterar'
                    : 'Lärarens kommentar' }}
                </h2>
                <Badge
                  v-if="selectedItem.submissionStatus === 'NeedsWork'"
                  variant="outline"
                  class="border-warning/20 bg-warning/10 text-warning dark:border-warning/30 dark:bg-warning/20"
                >
                  Behöver kompletteras
                </Badge>
                <Badge v-else-if="feedback.level" :variant="gradeBadgeVariant(feedback.level)" data-test="grade-stamp">
                  {{ gradeShort(feedback.level) }}
                </Badge>
              </div>
              <p v-if="feedback.feedback" class="text-sm leading-relaxed whitespace-pre-wrap">
                {{ feedback.feedback }}
              </p>
              <p class="text-muted-foreground text-xs">Bedömd {{ formatDate(feedback.gradedAt, true) }}</p>
            </section>

            <SubmissionPanel
              v-if="selectedItem.type === 'Assignment' && !selectedItem.isLocked"
              :item="selectedItem"
              @submitted="reload"
            />

            <section
              v-if="selectedItem.type !== 'News' && !selectedItem.isLocked"
              class="bg-card overflow-hidden rounded-xl border"
              aria-labelledby="unlocks-h"
            >
              <div class="bg-muted/40 border-b px-4 py-2.5">
                <h2 id="unlocks-h" class="text-sm font-semibold">Låser upp</h2>
              </div>
              <div class="flex flex-col gap-2 px-4 py-3 text-sm">
                <p v-if="unlocked.length > 0">När du är klar med den här delen öppnas:</p>
                <p v-else class="text-muted-foreground">
                  Den här delen låser inte upp någon annan del.
                </p>
                <ul v-if="unlocked.length > 0" class="flex flex-col gap-2">
                  <li
                    v-for="item in unlocked"
                    :key="item.linkId"
                    class="flex items-center gap-2"
                  >
                    <PhLockOpen class="size-4 text-success"/>
                    <span class="font-medium">{{ item.title }}</span>
                    <span class="text-muted-foreground text-xs">{{ kindOf(item) }}</span>
                  </li>
                </ul>
              </div>
            </section>
          </div>

          <aside class="flex flex-col gap-4">
            <section class="bg-card flex flex-col gap-3 rounded-xl border p-4" aria-labelledby="status-h">
              <h2 id="status-h" class="text-sm font-semibold">Din status</h2>
              <div class="flex items-center gap-2">
                <component :is="rowIcon(selectedItem)" class="size-5" :class="rowIconClass(selectedItem)"/>
                <span class="font-medium">{{ statusHeading(selectedItem) }}</span>
              </div>

              <template v-if="selectedItem.canMarkDone && !selectedItem.isLocked">
                <Button
                  v-if="!selectedItem.isCompleted"
                  :disabled="savingLinkId === selectedItem.linkId"
                  :data-test="`complete-${selectedItem.linkId}`"
                  @click="setCompleted(selectedItem.linkId, true)"
                >
                  <PhCircle/>
                  Klarmarkera
                </Button>
                <Button
                  v-else
                  variant="outline"
                  :disabled="savingLinkId === selectedItem.linkId"
                  :data-test="`undo-${selectedItem.linkId}`"
                  @click="setCompleted(selectedItem.linkId, false)"
                >
                  <PhCheckCircle/>
                  Klar – ångra
                </Button>
              </template>
              <template v-else-if="selectedItem.type === 'Assignment'">
                <p class="text-muted-foreground text-sm">
                  Statusen uppdateras när du lämnar in uppgiften eller får betyg.
                </p>
                <dl class="flex flex-col gap-1.5 text-sm">
                  <div class="flex items-center justify-between gap-2">
                    <dt class="text-muted-foreground">Inlämning</dt>
                    <dd>{{ submissionLabel(selectedItem) }}</dd>
                  </div>
                  <div class="flex items-center justify-between gap-2">
                    <dt class="text-muted-foreground">Betyg</dt>
                    <dd>
                      {{ selectedItem.gradeLevel
                        ? gradeLabel(selectedItem.gradeLevel)
                        : 'Inte bedömd än' }}
                    </dd>
                  </div>
                </dl>
              </template>
            </section>

            <section v-if="selectedItem.dueAt" class="bg-card flex flex-col gap-2 rounded-xl border p-4">
              <h2 class="flex items-center gap-2 text-sm font-semibold">
                <PhCalendarBlank class="text-muted-foreground size-4"/>
                Deadline
              </h2>
              <p class="text-sm">{{ formatDate(selectedItem.dueAt, true) }}</p>
            </section>
          </aside>
        </div>
      </div>
    </template>

    <template v-else>
      <div class="flex flex-wrap items-center justify-between gap-3">
        <div role="tablist" class="bg-muted flex w-fit rounded-[7px] p-[3px]">
          <Button
            v-for="option in filters"
            :key="option.value"
            type="button"
            role="tab"
            size="sm"
            variant="ghost"
            :aria-selected="filter === option.value"
            :class="filter === option.value
              ? 'bg-background h-auto rounded-[5px] px-2.5 py-1 text-[13px] font-medium shadow-sm'
              : 'text-muted-foreground h-auto rounded-[5px] px-2.5 py-1 text-[13px]'"
            :data-test="`student-filter-${option.value}`"
            @click="filter = option.value"
          >
            {{ filterLabel(option.value) }}
          </Button>
        </div>
        <div class="text-muted-foreground flex flex-wrap items-center gap-3 text-[13px]">
          <span class="flex items-center gap-1"><PhEye class="size-3.5"/>Teori</span>
          <span class="flex items-center gap-1"><PhPaperPlaneTilt class="size-3.5"/>Inlämning</span>
          <span class="flex items-center gap-1"><PhLock class="size-3.5"/>Låst</span>
        </div>
      </div>

      <div class="flex flex-wrap items-start gap-6">
        <div class="flex min-w-0 flex-[999_1_560px] flex-col gap-4">
          <template v-for="module in visibleModules" :key="module.id">
            <Collapsible
              v-if="module.isAvailable"
              :default-open="true"
              as="section"
              :aria-label="module.title"
              class="bg-card text-card-foreground overflow-hidden rounded-[11px] border"
            >
              <CollapsibleTrigger
                class="group bg-muted/50 hover:bg-muted/70 flex w-full items-center gap-3 px-4 py-[14px] text-left transition-colors"
                data-test="student-module"
              >
                <PhCaretDown
                  class="text-muted-foreground size-4 shrink-0 transition-transform duration-200 group-data-[state=closed]:-rotate-90"
                />
                <span class="flex min-w-0 flex-1 flex-col gap-0.5">
                  <span class="truncate font-sans text-[15px] font-semibold">{{ module.title }}</span>
                  <span class="text-muted-foreground text-[13px]">
                    {{ moduleProgress(module).done }} av {{ moduleProgress(module).total }} klara
                    <template v-if="module.description"> · {{ module.description }}</template>
                  </span>
                </span>
                <Badge
                  v-if="moduleProgress(module).total > 0
                    && moduleProgress(module).done === moduleProgress(module).total"
                  variant="outline"
                  class="border-success/20 bg-success/10 text-success dark:border-success/30 dark:bg-success/20"
                >
                  <PhCheckCircle/>
                  Klar
                </Badge>
              </CollapsibleTrigger>
              <CollapsibleContent>
                <Collapsible
                  v-for="section in sectionsFor(module)"
                  :key="section.key"
                  :default-open="section.defaultOpen"
                  as="section"
                  class="border-t first:border-t-0"
                  :aria-label="`${section.title} i ${module.title}`"
                >
                  <CollapsibleTrigger
                    class="group hover:bg-muted/30 flex w-full items-center gap-2 py-2 pr-4 pl-9 text-left transition-colors"
                    :data-test="`student-section-${section.key}`"
                  >
                    <PhCaretDown
                      class="text-muted-foreground size-3.5 shrink-0 transition-transform duration-200 group-data-[state=closed]:-rotate-90"
                    />
                    <span class="text-muted-foreground min-w-0 flex-1 truncate text-[11px] font-semibold tracking-[0.08em] uppercase">
                      {{ section.title }}
                    </span>
                    <span class="text-muted-foreground ml-auto text-[11px]">
                      {{ countLabel(section.items.length, 'del', 'delar') }}
                    </span>
                  </CollapsibleTrigger>
                  <CollapsibleContent>
                    <p
                      v-if="section.items.length === 0"
                      class="text-muted-foreground border-t py-3 pr-4 pl-12 text-sm"
                    >
                      {{ section.empty }}
                    </p>

                    <div
                      v-for="item in section.items"
                      :key="item.linkId"
                      class="hover:bg-muted/30 flex items-center gap-2 border-t py-2.5 pr-4 pl-12"
                      :data-test="`student-part-${item.linkId}`"
                    >
                      <Button
                        v-if="item.canMarkDone && !item.isLocked"
                        variant="ghost"
                        size="icon-sm"
                        class="-ml-1.5 shrink-0"
                        :aria-label="item.isCompleted
                          ? 'Ångra klarmarkering'
                          : 'Klarmarkera'"
                        :data-test="`${item.isCompleted
                          ? 'undo'
                          : 'complete'}-${item.linkId}`"
                        :disabled="savingLinkId === item.linkId"
                        @click.stop="setCompleted(item.linkId, !item.isCompleted)"
                      >
                        <PhCheckCircle v-if="item.isCompleted" class="size-[18px] text-success"/>
                        <PhCircle v-else class="size-[18px]"/>
                      </Button>
                      <component
                        v-else
                        :is="rowIcon(item)"
                        class="size-[18px] shrink-0"
                        :class="rowIconClass(item)"
                        :aria-label="statusHeading(item)"
                        role="img"
                      />
                      <button
                        type="button"
                        class="flex min-w-0 flex-1 items-center gap-3 text-left"
                        :data-test="`student-open-${item.linkId}`"
                        @click="openPart(item)"
                      >
                        <component :is="partOf(item).icon" class="text-muted-foreground size-4 shrink-0"/>
                        <span class="flex min-w-0 flex-1 flex-col gap-0.5">
                          <span
                            class="text-sm break-words"
                            :class="item.isLocked
                              ? 'text-muted-foreground'
                              : 'font-medium'"
                          >
                            {{ item.title }}
                          </span>
                          <span class="text-muted-foreground text-[12px]">{{ detailsOf(item) }}</span>
                        </span>
                        <span class="hidden items-center gap-2 sm:flex">
                          <Badge
                            v-if="item.type === 'Assignment' && !item.isLocked"
                            variant="outline"
                            class="h-[22px] rounded-[5px]"
                            :class="item.isCompleted
                              ? 'border-success/20 bg-success/10 text-success dark:border-success/30 dark:bg-success/20'
                              : ''"
                          >
                            {{ submissionLabel(item) }}
                          </Badge>
                          <span
                            v-if="item.dueAt && !item.isLocked"
                            class="text-[12px] whitespace-nowrap"
                            :class="item.isCompleted
                              ? 'text-muted-foreground'
                              : 'font-medium text-destructive'"
                          >
                            {{ dueLabel(item) }}
                          </span>
                        </span>
                        <PhCaretRight class="text-muted-foreground size-4 shrink-0"/>
                      </button>
                    </div>
                  </CollapsibleContent>
                </Collapsible>
              </CollapsibleContent>
            </Collapsible>

            <Item
              v-else
              as="section"
              :aria-label="module.title"
              variant="outline"
              size="sm"
              class="bg-card rounded-[11px] px-4 py-[14px]"
              data-test="student-module-locked"
            >
              <ItemMedia variant="icon" class="text-muted-foreground">
                <PhLock/>
              </ItemMedia>
              <span class="flex min-w-0 flex-1 flex-col gap-0.5">
                <span class="truncate font-sans text-[15px] font-semibold">{{ module.title }}</span>
                <span class="text-muted-foreground text-[13px]">Veckan är låst</span>
              </span>
              <Badge variant="outline" class="gap-1.5">
                <PhCalendarBlank/>
                Öppnar {{ module.publishAt
                  ? formatDate(module.publishAt, true)
                  : 'senare' }}
              </Badge>
            </Item>
          </template>
        </div>

        <aside class="flex min-w-0 flex-[1_1_280px] flex-col gap-4">
          <section class="bg-card flex flex-col gap-2.5 rounded-[11px] border p-4">
            <h2 class="text-sm font-semibold">Din progress</h2>
            <div class="flex items-baseline gap-1.5">
              <span class="text-[28px] font-semibold tracking-tight">{{ completedCount }}</span>
              <span class="text-muted-foreground text-sm">av {{ completable.length }} delar klara</span>
            </div>
            <div class="bg-muted h-1.5 overflow-hidden rounded-full">
              <div
                class="bg-primary h-full rounded-full transition-all"
                :style="{width: `${progressPercent}%`}"
              />
            </div>
            <dl class="flex flex-col gap-1.5 pt-1 text-[13px]">
              <div class="flex items-center justify-between gap-2">
                <dt class="text-muted-foreground">Tillgängligt nu</dt>
                <dd>{{ availableCount }}</dd>
              </div>
              <div class="flex items-center justify-between gap-2">
                <dt class="text-muted-foreground">Betygsatta delar</dt>
                <dd>{{ gradedCount }}</dd>
              </div>
              <div class="flex items-center justify-between gap-2">
                <dt class="text-muted-foreground">Låsta delar</dt>
                <dd>{{ lockedCount }}</dd>
              </div>
              <div class="flex items-center justify-between gap-2">
                <dt class="text-muted-foreground">Kvar totalt</dt>
                <dd>{{ todoCount }}</dd>
              </div>
            </dl>
          </section>
        </aside>
      </div>
    </template>
  </div>
</template>
