<script setup lang="ts">
import {
  PhArrowClockwise,
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

import {Alert, AlertDescription} from '@/components/ui/alert'
import {Badge} from '@/components/ui/badge'
import {Button} from '@/components/ui/button'
import {Collapsible, CollapsibleContent, CollapsibleTrigger} from '@/components/ui/collapsible'
import {NativeSelect, NativeSelectOption} from '@/components/ui/native-select'
import {Skeleton} from '@/components/ui/skeleton'
import {useCourses} from '@/composables/useCourses'
import {useDevOptions} from '@/composables/useDevOptions'
import {api, errorMessage, type Schema} from '@/lib/api'
import {formatDate, partOf} from '@/lib/content'

// Elevvyn finns på /dev/student tills inloggning (#80) finns. Grupp och kurs väljs därför här,
// och alla anrop går mot den valda gruppen så att svaret blir samma som en elev i gruppen får.

const {options, loading: loadingOptions, refresh} = useDevOptions()
const {courses: courseDtos, loadCourses} = useCourses()

const groupId = ref('')
const courseId = ref('')
const modules = ref<Schema<'StudentModuleWithContentDto'>[]>([])
const loading = ref(false)
const error = ref('')
const savingLinkId = ref('')
const selectedLinkId = ref('')
const filter = ref<'all' | 'todo' | 'submissions'>('all')

const groups = computed(() => options.groups)
const courses = computed(() => options.courses)
const hasSelection = computed(() => groupId.value !== '' && courseId.value !== '')
const selectedCourse = computed(() => courseDtos.value.find((course) => course.id === courseId.value))
const selectedGroup = computed(() => groups.value.find((group) => group.value === groupId.value))

const allItems = computed(() => modules.value.flatMap((module) => module.content))
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

const selectedModule = computed(() => modules.value.find((module) =>
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

onMounted(async () => {
  await Promise.all([refresh('groups'), refresh('courses'), loadCourses()])
  groupId.value ||= groups.value[0]?.value ?? ''
  courseId.value ||= courses.value[0]?.value ?? ''
})

watch([groupId, courseId], async () => {
  selectedLinkId.value = ''
  if (!hasSelection.value) {
    modules.value = []
    return
  }
  await load()
})

async function load() {
  loading.value = true
  error.value = ''
  try {
    const {data} = await api.GET('/api/groups/{groupId}/courses/{courseId}/modules', {
      params: {path: {groupId: groupId.value, courseId: courseId.value}},
    })
    modules.value = data ?? []
    if (selectedLinkId.value && !allItems.value.some((item) => item.linkId === selectedLinkId.value)) {
      selectedLinkId.value = ''
    }
  } catch (exception) {
    error.value = errorMessage(exception)
    modules.value = []
    selectedLinkId.value = ''
  } finally {
    loading.value = false
  }
}

async function setCompleted(linkId: string, completed: boolean) {
  savingLinkId.value = linkId
  error.value = ''
  try {
    const {data} = await api.PUT('/api/groups/{groupId}/courses/{courseId}/links/{linkId}/completion', {
      params: {path: {groupId: groupId.value, courseId: courseId.value, linkId}},
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

function openPart(item: Schema<'StudentContentItem'>) {
  selectedLinkId.value = item.linkId
}

function closePart() {
  selectedLinkId.value = ''
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

function countLabel(count: number): string {
  return count === 1
    ? `${count} del`
    : `${count} delar`
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
    return 'text-emerald-600'
  }
  if (item.isLocked) {
    return 'text-stone-400'
  }
  if (item.submissionStatus === 'Submitted') {
    return 'text-blue-600'
  }
  if (item.submissionStatus === 'NeedsWork' || item.gradeLevel === 'Fail') {
    return 'text-rose-600'
  }
  return 'text-stone-400'
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
  <main class="mx-auto flex max-w-[1120px] flex-col gap-6 p-6">
    <header class="bg-muted/30 flex flex-col gap-3 rounded-xl border border-dashed p-4">
      <div class="flex flex-wrap items-start justify-between gap-2">
        <div>
          <p class="text-muted-foreground text-[11px] font-medium tracking-wide uppercase">Testläge</p>
          <p class="text-muted-foreground text-sm">
            Elevvyn är tillfällig tills inloggning finns (#80). Välj grupp och kurs för att se
            veckorna som en elev i gruppen ser dem.
          </p>
        </div>
        <RouterLink to="/dev" class="text-sm underline">Till testdata</RouterLink>
      </div>
      <div class="flex flex-wrap items-end gap-2">
        <label class="flex flex-col gap-1 text-sm">
          Grupp
          <NativeSelect v-model="groupId" data-test="student-group" :disabled="loadingOptions.groups">
            <NativeSelectOption value="">Välj grupp</NativeSelectOption>
            <NativeSelectOption v-for="group in groups" :key="group.value" :value="group.value">
              {{ group.label }}
            </NativeSelectOption>
          </NativeSelect>
        </label>
        <label class="flex flex-col gap-1 text-sm">
          Kurs
          <NativeSelect v-model="courseId" data-test="student-course" :disabled="loadingOptions.courses">
            <NativeSelectOption value="">Välj kurs</NativeSelectOption>
            <NativeSelectOption v-for="course in courses" :key="course.value" :value="course.value">
              {{ course.label }}
            </NativeSelectOption>
          </NativeSelect>
        </label>
        <Button
          type="button"
          variant="outline"
          :disabled="!hasSelection || loading"
          @click="load"
        >
          <PhArrowClockwise/>
          Uppdatera
        </Button>
      </div>
    </header>

    <Alert v-if="error" variant="destructive">
      <AlertDescription>{{ error }}</AlertDescription>
    </Alert>

    <template v-if="loading">
      <Skeleton v-for="index in 2" :key="index" class="h-32 w-full rounded-xl"/>
    </template>

    <p v-else-if="!hasSelection" class="text-muted-foreground text-sm">
      Välj en grupp och en kurs för att se innehållet.
    </p>

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
          <span>/</span>
          <span>{{ selectedCourse?.title ?? 'Kurs' }}</span>
          <span>/</span>
          <span>{{ selectedModule.title }}</span>
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
              class="border-emerald-200 bg-emerald-50 text-emerald-700"
            >
              <PhCheckCircle/>
              Klar
            </Badge>
          </div>
          <h1 class="text-3xl leading-tight font-semibold">{{ selectedItem.title }}</h1>
          <p class="text-muted-foreground flex flex-wrap items-center gap-x-2 gap-y-1 text-sm">
            <span>{{ selectedModule.title }}</span>
            <template v-if="selectedItem.dueAt">
              <span>·</span>
              <span class="font-medium text-rose-600">{{ dueLabel(selectedItem) }}</span>
            </template>
            <template v-if="selectedItem.type === 'News'">
              <span>·</span>
              <span>Nyhet</span>
            </template>
          </p>
        </header>

        <div class="grid gap-6 lg:grid-cols-[minmax(0,1fr)_320px]">
          <div class="flex flex-col gap-6">
            <section v-if="selectedItem.isLocked" class="flex flex-col items-start gap-3 rounded-xl border border-dashed p-6">
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
                  class="text-sm leading-relaxed whitespace-pre-wrap text-stone-700"
                >
                  {{ selectedItem.bodyMd }}
                </p>
                <p v-else class="text-muted-foreground text-sm">Inget innehåll än.</p>
              </section>
            </template>

            <section
              v-if="selectedItem.type !== 'News' && !selectedItem.isLocked"
              class="overflow-hidden rounded-xl border"
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
                    <PhLockOpen class="size-4 text-emerald-600"/>
                    <span class="font-medium">{{ item.title }}</span>
                    <span class="text-muted-foreground text-xs">{{ kindOf(item) }}</span>
                  </li>
                </ul>
              </div>
            </section>
          </div>

          <aside class="flex flex-col gap-4">
            <section class="flex flex-col gap-3 rounded-xl border p-4" aria-labelledby="status-h">
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

            <section v-if="selectedItem.dueAt" class="flex flex-col gap-2 rounded-xl border p-4">
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
      <header class="flex flex-wrap items-start justify-between gap-4 border-b pb-4">
        <div class="flex flex-col gap-1">
          <span class="text-muted-foreground font-mono text-xs">KURS · {{ selectedCourse?.code }}</span>
          <h1 class="text-3xl leading-tight font-semibold">{{ selectedCourse?.title }}</h1>
          <p class="text-muted-foreground text-sm">
            {{ selectedGroup?.label }} · {{ completable.length }} delar
          </p>
        </div>
        <div class="flex items-center gap-3">
          <div class="flex flex-col items-end text-sm">
            <span class="text-muted-foreground text-xs">Din progress</span>
            <span class="font-medium">{{ completedCount }} av {{ completable.length }} klara</span>
          </div>
          <div class="grid size-12 place-items-center rounded-full border-2 border-dashed text-sm font-semibold">
            {{ progressPercent }}%
          </div>
        </div>
      </header>

      <div class="flex flex-wrap items-center justify-between gap-3">
        <div role="tablist" class="bg-muted flex w-fit gap-1 rounded-lg p-1">
          <button
            v-for="option in filters"
            :key="option.value"
            type="button"
            role="tab"
            :aria-selected="filter === option.value"
            class="rounded-md px-2.5 py-1 text-[13px]"
            :class="filter === option.value
              ? 'bg-background font-medium shadow-sm'
              : 'text-muted-foreground'"
            :data-test="`student-filter-${option.value}`"
            @click="filter = option.value"
          >
            {{ filterLabel(option.value) }}
          </button>
        </div>
        <div class="text-muted-foreground flex flex-wrap items-center gap-3 text-xs">
          <span class="flex items-center gap-1"><PhEye class="size-3.5"/>Teori</span>
          <span class="flex items-center gap-1"><PhPaperPlaneTilt class="size-3.5"/>Inlämning</span>
          <span class="flex items-center gap-1"><PhLock class="size-3.5"/>Låst</span>
        </div>
      </div>

      <div class="grid gap-6 lg:grid-cols-[minmax(0,1fr)_300px]">
        <div class="flex flex-col gap-4">
          <Collapsible
            v-for="module in modules"
            :key="module.id"
            :default-open="true"
            as="section"
            :aria-label="module.title"
            class="overflow-hidden rounded-xl border"
          >
            <CollapsibleTrigger
              class="group bg-muted/50 hover:bg-muted/70 flex w-full items-center gap-3 px-4 py-3 text-left transition-colors"
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
                class="border-emerald-200 bg-emerald-50 text-emerald-700"
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
                    {{ countLabel(section.items.length) }}
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
                    <component
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
                          class="truncate text-sm"
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
                            ? 'border-emerald-200 bg-emerald-50 text-emerald-700'
                            : ''"
                        >
                          {{ submissionLabel(item) }}
                        </Badge>
                        <span
                          v-if="item.dueAt && !item.isLocked"
                          class="text-[12px] whitespace-nowrap"
                          :class="item.isCompleted
                            ? 'text-muted-foreground'
                            : 'font-medium text-rose-600'"
                        >
                          {{ dueLabel(item) }}
                        </span>
                      </span>
                      <PhCaretRight class="text-muted-foreground size-4 shrink-0"/>
                    </button>

                    <Button
                      v-if="item.canMarkDone && !item.isLocked"
                      variant="ghost"
                      size="icon-sm"
                      :aria-label="item.isCompleted
                        ? 'Ångra klarmarkering'
                        : 'Klarmarkera'"
                      :data-test="`${item.isCompleted
                        ? 'undo'
                        : 'complete'}-${item.linkId}`"
                      :disabled="savingLinkId === item.linkId"
                      @click.stop="setCompleted(item.linkId, !item.isCompleted)"
                    >
                      <PhCheckCircle v-if="item.isCompleted" class="text-emerald-600"/>
                      <PhCircle v-else/>
                    </Button>
                  </div>
                </CollapsibleContent>
              </Collapsible>
            </CollapsibleContent>
          </Collapsible>
        </div>

        <aside class="flex flex-col gap-4">
          <section class="flex flex-col gap-3 rounded-xl border p-4">
            <h2 class="text-sm font-semibold">Din progress</h2>
            <div class="flex items-baseline gap-1.5">
              <span class="text-2xl font-semibold">{{ completedCount }}</span>
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

          <section class="flex flex-col gap-2 rounded-xl border p-4">
            <h2 class="flex items-center gap-2 text-sm font-semibold">
              <PhLockOpen class="text-muted-foreground size-4"/>
              Så funkar låsningar
            </h2>
            <p class="text-muted-foreground text-[13px]">
              Låsta delar visas med ett hänglås. De öppnas när delarna de kräver är klara, och då
              flyttas de till Nästa steg.
            </p>
          </section>
        </aside>
      </div>
    </template>
  </main>
</template>
