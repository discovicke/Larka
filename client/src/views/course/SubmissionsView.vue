<script setup lang="ts">
import {PhCheck, PhMagnifyingGlass, PhUsersThree} from '@phosphor-icons/vue'
import {computed, ref, watch} from 'vue'
import {
  RouterLink,
  useRoute,
  useRouter,
} from 'vue-router'

import MarkdownView from '@/components/editor/MarkdownView.vue'
import {Alert, AlertDescription} from '@/components/ui/alert'
import {Badge} from '@/components/ui/badge'
import {Button} from '@/components/ui/button'
import {Empty, EmptyDescription, EmptyHeader, EmptyMedia, EmptyTitle} from '@/components/ui/empty'
import {Input} from '@/components/ui/input'
import {Skeleton} from '@/components/ui/skeleton'
import {api, type Schema} from '@/lib/api'
import {formatDate, partOf} from '@/lib/content'
import {checkClass, checkLabel, gradeLabel, statusLabel} from '@/lib/submissions'

type Filter = 'all' | 'due' | 'needsWork' | 'notStarted'

interface Stat {
  label: string
  value: number
  suffix: string
  class: string
  labelClass?: string
  valueClass?: string
}

const route = useRoute()
const router = useRouter()
const courseId = computed(() => String(route.params.courseId))
const linkId = computed(() => String(route.params.linkId))

const weekTitle = ref('')
const assignment = ref<Schema<'ModuleContentItem'> | null>(null)
const content = ref<Schema<'ContentDto'> | null>(null)
const submissions = ref<Schema<'SubmissionDto'>[]>([])
const users = ref<Schema<'UserDto'>[]>([])
const loading = ref(true)
const error = ref('')
const notFound = ref(false)
const filter = ref<Filter>('all')
const search = ref('')

const tab = computed(() => route.query.tab === 'instructions'
  ? 'instructions'
  : 'students')

const names = computed(() => new Map(users.value.map((user) => [user.id, user.name])))

function nameOf(submission: Schema<'SubmissionDto'>): string {
  return names.value.get(submission.studentId) ?? submission.studentId
}

function countOf(status: Schema<'SubmissionStatus'>): number {
  return submissions.value.filter((submission) => submission.status === status).length
}

const stats = computed<Stat[]>(() => [
  {
    label: 'Inlämnade',
    value: countOf('Submitted') + countOf('Graded') + countOf('NeedsWork'),
    suffix: `av ${submissions.value.length}`,
    class: '',
  },
  {
    label: 'Att bedöma',
    value: countOf('Submitted'),
    suffix: '',
    class: '',
  },
  {
    label: 'Bedömda',
    value: countOf('Graded'),
    suffix: '',
    class: '',
  },
  {
    label: 'Komplettering',
    value: countOf('NeedsWork'),
    suffix: '',
    class: 'border-amber-200 bg-amber-50 dark:border-amber-900 dark:bg-amber-950/40',
    labelClass: 'text-amber-800 dark:text-amber-300',
    valueClass: 'text-amber-900 dark:text-amber-200',
  },
])

const filters = computed<{value: Filter, label: string}[]>(() => [
  {value: 'all', label: `Alla ${submissions.value.length}`},
  {value: 'due', label: `Att bedöma ${countOf('Submitted')}`},
  {value: 'needsWork', label: `Komplettering ${countOf('NeedsWork')}`},
  {value: 'notStarted', label: `Ej inlämnad ${countOf('NotStarted')}`},
])

const statusOrder: Record<Schema<'SubmissionStatus'>, number> = {
  Submitted: 0,
  NeedsWork: 1,
  Graded: 2,
  NotStarted: 3,
}

const sorted = computed(() => [...submissions.value]
  .sort((left, right) => statusOrder[left.status] - statusOrder[right.status]
    || nameOf(left).localeCompare(nameOf(right), 'sv')))

const visible = computed(() => {
  const text = search.value.trim().toLowerCase()
  return sorted.value.filter((submission) => {
    if (filter.value === 'due' && submission.status !== 'Submitted') {
      return false
    }
    if (filter.value === 'needsWork' && submission.status !== 'NeedsWork') {
      return false
    }
    if (filter.value === 'notStarted' && submission.status !== 'NotStarted') {
      return false
    }
    return !text
      || nameOf(submission).toLowerCase().includes(text)
  })
})

const hasRepos = computed(() => submissions.value.some((submission) => submission.repo !== null))

const published = computed(() => assignment.value?.status === 'Published')

const summary = computed(() => {
  const item = assignment.value
  if (!item) {
    return ''
  }
  const parts: string[] = [weekTitle.value, scaleLabel(item.gradeScale)]
  if (item.dueAt) {
    parts.push(`Slutar ${formatDate(item.dueAt, true)}`)
  }
  return parts.filter((part) => part !== '').join(' · ')
})

function scaleLabel(scale: Schema<'GradeScale'> | null): string {
  if (scale === 'PassFail') {
    return 'IG–G'
  }
  if (scale === 'PassFailDistinction') {
    return 'IG–VG'
  }
  if (scale === 'None') {
    return 'Endast återkoppling'
  }
  return ''
}

function tabClass(value: 'students' | 'instructions') {
  return tab.value === value
    ? 'border-primary font-medium'
    : 'text-muted-foreground border-transparent'
}

function selectTab(value: 'students' | 'instructions') {
  router.replace({query: value === 'instructions'
    ? {tab: 'instructions'}
    : {}})
}

function filterClass(value: Filter) {
  return filter.value === value
    ? 'bg-background shadow-sm font-medium'
    : 'text-muted-foreground'
}

function statusClass(submission: Schema<'SubmissionDto'>): string {
  return submission.status === 'Submitted'
    ? 'border-emerald-200 bg-emerald-50 text-emerald-700 dark:border-emerald-900 dark:bg-emerald-950/40 dark:text-emerald-300'
    : 'border-amber-200 bg-amber-50 text-amber-800 dark:border-amber-900 dark:bg-amber-950/40 dark:text-amber-300'
}

function statusText(submission: Schema<'SubmissionDto'>): string {
  return submission.status === 'Submitted'
    ? 'Inlämnad'
    : statusLabel(submission)
}

function gradeTileClass(level: Schema<'GradeLevel'> | null): string {
  if (level === null) {
    return 'bg-muted text-muted-foreground'
  }
  return level === 'Fail'
    ? 'bg-red-50 text-red-700 dark:bg-red-950/40 dark:text-red-300'
    : 'bg-emerald-50 text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-300'
}

function reviewRoute(submission: Schema<'SubmissionDto'>) {
  return {
    name: 'submission-review',
    params: {courseId: courseId.value, linkId: linkId.value, submissionId: submission.id},
  }
}

function rowBorder(index: number) {
  return index < visible.value.length - 1
    ? 'border-b'
    : ''
}

async function load() {
  loading.value = true
  error.value = ''
  notFound.value = false
  try {
    const [moduleResult, submissionResult, userResult] = await Promise.all([
      api.GET('/api/courses/{id}/modules', {params: {path: {id: courseId.value}}}),
      api.GET('/api/links/{id}/submissions', {params: {path: {id: linkId.value}}}),
      api.GET('/api/users'),
    ])
    users.value = userResult.data ?? []
    submissions.value = submissionResult.data ?? []
    const week = (moduleResult.data ?? [])
      .find((candidate) => candidate.content.some((item) => item.linkId === linkId.value))
    const item = week?.content.find((candidate) => candidate.linkId === linkId.value)
    if (!week || !item) {
      notFound.value = true
      return
    }
    weekTitle.value = week.title
    assignment.value = item
    const contentResult = await api.GET('/api/content/{id}', {params: {path: {id: item.contentId}}})
    content.value = contentResult.data ?? null
  } catch {
    error.value = 'Kunde inte hämta inlämningarna.'
  } finally {
    loading.value = false
  }
}

watch([courseId, linkId], () => {
  load()
}, {immediate: true})
</script>

<template>
  <div class="flex flex-col gap-5">
    <Alert v-if="error" variant="destructive">
      <AlertDescription>{{ error }}</AlertDescription>
    </Alert>

    <template v-if="loading">
      <Skeleton class="h-10 w-64"/>
      <Skeleton class="h-24 w-full rounded-[11px]"/>
      <Skeleton class="h-64 w-full rounded-[11px]"/>
    </template>

    <Empty v-else-if="notFound && !error" class="border border-dashed">
      <EmptyHeader>
        <EmptyMedia variant="icon">
          <PhUsersThree/>
        </EmptyMedia>
        <EmptyTitle>Uppgiften finns inte</EmptyTitle>
        <EmptyDescription>Den kan ha tagits bort från kursen.</EmptyDescription>
      </EmptyHeader>
    </Empty>

    <template v-else-if="!error && assignment">
      <div class="flex flex-col gap-4 border-b">
        <div class="flex flex-wrap items-end justify-between gap-4">
          <div class="flex flex-col gap-2">
            <div class="flex flex-wrap items-center gap-2">
              <Badge variant="outline">{{ partOf(assignment).label }}</Badge>
              <Badge
                variant="outline"
                :class="published
                  ? 'border-emerald-200 bg-emerald-50 text-emerald-700 dark:border-emerald-900 dark:bg-emerald-950/40 dark:text-emerald-300'
                  : ''"
              >
                {{ published
                  ? 'Publicerad'
                  : 'Utkast' }}
              </Badge>
            </div>
            <h1 class="font-heading text-[30px] leading-tight font-semibold">{{ assignment.title }}</h1>
            <p class="text-muted-foreground text-[13px]">{{ summary }}</p>
          </div>
        </div>

        <div role="tablist" aria-label="Uppgiftens flikar" class="flex gap-5">
          <button
            type="button"
            role="tab"
            :aria-selected="tab === 'students'"
            class="h-10 border-b-2 text-sm"
            :class="tabClass('students')"
            @click="selectTab('students')"
          >
            Elever
          </button>
          <button
            type="button"
            role="tab"
            :aria-selected="tab === 'instructions'"
            class="h-10 border-b-2 text-sm"
            :class="tabClass('instructions')"
            @click="selectTab('instructions')"
          >
            Instruktioner
          </button>
        </div>
      </div>

      <div class="grid grid-cols-[repeat(auto-fit,minmax(min(200px,100%),1fr))] gap-3">
        <div
          v-for="stat in stats"
          :key="stat.label"
          class="rounded-[11px] border px-4 py-3.5 flex flex-col gap-0.5"
          :class="stat.class"
          data-test="stat-card"
        >
          <span class="text-[13px] text-muted-foreground" :class="stat.labelClass">{{ stat.label }}</span>
          <span class="font-heading text-2xl font-semibold" :class="stat.valueClass">
            {{ stat.value }}
            <span v-if="stat.suffix" class="text-sm font-normal text-muted-foreground">{{ stat.suffix }}</span>
          </span>
        </div>
      </div>

      <div v-if="tab === 'instructions'" class="max-w-[760px]">
        <MarkdownView v-if="content" :markdown="content.bodyMd"/>
      </div>

      <Empty v-else-if="submissions.length === 0" class="border border-dashed">
        <EmptyHeader>
          <EmptyMedia variant="icon">
            <PhUsersThree/>
          </EmptyMedia>
          <EmptyTitle>Inga elever än</EmptyTitle>
          <EmptyDescription>Uppgiften har inte delats ut till några elever.</EmptyDescription>
        </EmptyHeader>
      </Empty>

      <section v-else aria-label="Elever" class="overflow-hidden rounded-[11px] border">
        <div class="flex flex-wrap items-center gap-2.5 border-b px-4 py-3">
          <div class="relative flex-[0_1_240px]">
            <label for="submission-search" class="sr-only">Sök elev</label>
            <PhMagnifyingGlass class="text-muted-foreground pointer-events-none absolute top-1/2 left-2.5 size-4 -translate-y-1/2"/>
            <Input
              id="submission-search"
              v-model="search"
              class="pl-8"
              placeholder="Sök elev…"
            />
          </div>

          <div role="tablist" class="bg-muted flex rounded-[7px] p-[3px]">
            <button
              v-for="choice in filters"
              :key="choice.value"
              type="button"
              role="tab"
              :aria-selected="filter === choice.value"
              class="rounded-[5px] px-2.5 py-1 text-[13px]"
              :class="filterClass(choice.value)"
              @click="filter = choice.value"
            >
              {{ choice.label }}
            </button>
          </div>
        </div>

        <div class="overflow-x-auto">
          <table class="w-full min-w-[760px] text-[13px]">
            <thead>
              <tr class="bg-muted/50 text-muted-foreground text-left">
                <th scope="col" class="border-b px-4 py-2.5 font-medium">Elev</th>
                <th scope="col" class="border-b px-3 py-2.5 font-medium">Inlämnad</th>
                <th scope="col" class="border-b px-3 py-2.5 font-medium">Filer</th>
                <th v-if="hasRepos" scope="col" class="border-b px-3 py-2.5 font-medium">Tester</th>
                <th scope="col" class="border-b px-3 py-2.5 font-medium">Status</th>
                <th scope="col" class="border-b px-4 py-2.5 text-right font-medium">Betyg</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="(submission, index) in visible" :key="submission.id" data-test="submission-row">
                <td class="px-4 py-3" :class="rowBorder(index)">
                  <div class="font-medium">{{ nameOf(submission) }}</div>
                  <div v-if="submission.repo" class="font-mono text-xs text-muted-foreground">
                    {{ submission.repo.repoFullName }}
                  </div>
                </td>
                <td class="px-3 py-3" :class="rowBorder(index)">
                  {{ submission.submittedAt
                    ? formatDate(submission.submittedAt, true)
                    : '–' }}
                </td>
                <td class="px-3 py-3" :class="rowBorder(index)">
                  {{ submission.files.length > 0
                    ? submission.files.length
                    : '–' }}
                </td>
                <td v-if="hasRepos" class="px-3 py-3" :class="rowBorder(index)">
                  <span v-if="submission.repo" :class="checkClass(submission.repo.checkStatus)">
                    {{ checkLabel(submission.repo.checkStatus) }}
                  </span>
                  <span v-else class="text-muted-foreground">–</span>
                </td>
                <td class="px-3 py-3" :class="rowBorder(index)">
                  <Badge
                    v-if="submission.status === 'Submitted' || submission.status === 'NeedsWork'"
                    variant="outline"
                    :class="statusClass(submission)"
                  >
                    {{ statusText(submission) }}
                  </Badge>
                  <span v-else class="text-muted-foreground">{{ statusLabel(submission) }}</span>
                </td>
                <td class="px-4 py-3 text-right" :class="rowBorder(index)">
                  <div class="flex justify-end">
                    <Button
                      v-if="submission.status === 'Submitted' || submission.status === 'NeedsWork'"
                      as-child
                      class="h-7 px-2.5 text-[13px]"
                    >
                      <RouterLink :to="reviewRoute(submission)">Bedöm</RouterLink>
                    </Button>
                    <RouterLink
                      v-else-if="submission.status === 'Graded'"
                      :to="reviewRoute(submission)"
                      class="size-7 rounded-[7px] font-bold flex items-center justify-center"
                      :class="gradeTileClass(submission.grade?.level ?? null)"
                    >
                      <PhCheck v-if="!submission.grade?.level" class="size-3.5"/>
                      <template v-else>{{ gradeLabel(submission.grade.level) }}</template>
                    </RouterLink>
                    <span v-else class="text-muted-foreground">–</span>
                  </div>
                </td>
              </tr>
              <tr v-if="visible.length === 0">
                <td
                  :colspan="hasRepos
                    ? 6
                    : 5"
                  class="text-muted-foreground px-4 py-6 text-center"
                >
                  Inga elever matchar sökningen.
                </td>
              </tr>
            </tbody>
          </table>
        </div>

        <div class="text-muted-foreground border-t px-4 py-2.5 text-[13px]">
          Visar {{ visible.length }} av {{ submissions.length }} elever
        </div>
      </section>
    </template>
  </div>
</template>
