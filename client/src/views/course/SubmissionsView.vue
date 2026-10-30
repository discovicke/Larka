<script setup lang="ts">
import {
  PhArrowsClockwise,
  PhCheck,
  PhCopy,
  PhLock,
  PhMagnifyingGlass,
  PhUsersThree,
} from '@phosphor-icons/vue'
import {computed, ref, watch} from 'vue'
import {
  RouterLink,
  useRoute,
  useRouter,
} from 'vue-router'

import MarkdownView from '@/components/editor/MarkdownView.vue'
import StatusBadge from '@/components/StatusBadge.vue'
import {Alert, AlertDescription} from '@/components/ui/alert'
import {Badge} from '@/components/ui/badge'
import {Button} from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import {Empty, EmptyDescription, EmptyHeader, EmptyMedia, EmptyTitle} from '@/components/ui/empty'
import {Input} from '@/components/ui/input'
import {Skeleton} from '@/components/ui/skeleton'
import {Textarea} from '@/components/ui/textarea'
import {useCrumbLabels} from '@/composables/useCrumbLabels'
import {api, type Schema} from '@/lib/api'
import {formatDate, partOf} from '@/lib/content'
import {lockGitHubRepos, refreshGitHubRepos} from '@/lib/integrations'
import {submissionStatusKey} from '@/lib/status'
import {resolveStudentHelp} from '@/lib/support'
import {checkClass, checkLabel, gradeBadgeVariant, gradeLabel, statusLabel} from '@/lib/submissions'

type Filter = 'all'
  | 'due'
  | 'needsWork'
  | 'notStarted'
  | 'needsHelp'
  | 'supportNotStarted'
  | 'supportNoPush'
  | 'supportRedChecks'
  | 'supportHelp'

interface Stat {
  label: string
  value: number
  suffix: string
  class: string
}

const route = useRoute()
const {setCrumbLabel} = useCrumbLabels()
const router = useRouter()
const courseId = computed(() => String(route.params.courseId))
const linkId = computed(() => String(route.params.linkId))

const weekTitle = ref('')
const assignment = ref<Schema<'ModuleContentItem'> | null>(null)
const content = ref<Schema<'ContentDto'> | null>(null)
const submissions = ref<Schema<'SubmissionDto'>[]>([])
const loading = ref(true)
const error = ref('')
const notFound = ref(false)
const filter = ref<Filter>('all')
const search = ref('')
const refreshing = ref(false)
const locking = ref(false)
const actionError = ref('')
const cloneOpen = ref(false)

const tab = computed(() => route.query.tab === 'instructions'
  ? 'instructions'
  : 'students')

function nameOf(submission: Schema<'SubmissionDto'>): string {
  return submission.studentName
}

function countOf(status: Schema<'SubmissionStatus'>): number {
  return submissions.value.filter((submission) => submission.status === status).length
}

function supportReasons(submission: Schema<'SubmissionDto'>): string[] {
  const support = submission.support
  if (!support) {
    return []
  }

  const reasons: string[] = []
  if (support.helpRequested) {
    reasons.push('Elevflagga')
  }
  if (support.notStarted) {
    reasons.push('Inte startat')
  }
  if (support.noRecentPush) {
    reasons.push('Ingen push')
  }
  if (support.redChecks) {
    reasons.push('Röda tester')
  }

  return reasons
}

function needsHelp(submission: Schema<'SubmissionDto'>): boolean {
  return supportReasons(submission).length > 0
}

const needsHelpCount = computed(() => submissions.value.filter(needsHelp).length)

function supportCount(filter: Filter): number {
  return submissions.value.filter((submission) => {
    const support = submission.support
    if (filter === 'supportNotStarted') {
      return support?.notStarted === true
    }
    if (filter === 'supportNoPush') {
      return support?.noRecentPush === true
    }
    if (filter === 'supportRedChecks') {
      return support?.redChecks === true
    }
    return support?.helpRequested === true
  }).length
}

function supportReasonClass(reason: string): string {
  return reason === 'Elevflagga'
    ? 'border-primary/30 bg-primary/10 text-primary'
    : 'border-warning/30 bg-warning/10 text-warning'
}

async function resolveHelp(submission: Schema<'SubmissionDto'>): Promise<void> {
  actionError.value = ''
  try {
    await resolveStudentHelp(linkId.value, submission.studentId)
    await load()
  } catch {
    actionError.value = 'Kunde inte markera hjälpfrågan som löst.'
  }
}

const stats = computed<Stat[]>(() => [
  {
    label: 'Inlämnade',
    value: countOf('Submitted') + countOf('Graded') + countOf('NeedsWork'),
    suffix: `av ${submissions.value.length}`,
    class: 'bg-card',
  },
  {
    label: 'Att bedöma',
    value: countOf('Submitted'),
    suffix: '',
    class: 'bg-card',
  },
  {
    label: 'Bedömda',
    value: countOf('Graded'),
    suffix: '',
    class: 'bg-card',
  },
  {
    label: 'Komplettering',
    value: countOf('NeedsWork'),
    suffix: '',
    class: 'bg-card',
  },
  ...(submissions.value.some((submission) => submission.repo !== null)
    ? [{
        label: 'Behöver hjälp',
        value: needsHelpCount.value,
        suffix: '',
        class: 'border-warning/20 bg-warning/10 dark:border-warning/30 dark:bg-warning/20',
      }]
    : []),
])

const filters = computed<{value: Filter, label: string}[]>(() => [
  {value: 'all', label: `Alla ${submissions.value.length}`},
  {value: 'due', label: `Att bedöma ${countOf('Submitted')}`},
  {value: 'needsWork', label: `Komplettering ${countOf('NeedsWork')}`},
  {value: 'notStarted', label: `Ej inlämnad ${countOf('NotStarted')}`},
  ...(submissions.value.some((submission) => submission.repo !== null)
    ? [
        {value: 'needsHelp' as const, label: `Behöver hjälp ${needsHelpCount.value}`},
        {value: 'supportNotStarted' as const, label: `Inte startat ${supportCount('supportNotStarted')}`},
        {value: 'supportNoPush' as const, label: `Ingen push ${supportCount('supportNoPush')}`},
        {value: 'supportRedChecks' as const, label: `Röda tester ${supportCount('supportRedChecks')}`},
        {value: 'supportHelp' as const, label: `Elevflagga ${supportCount('supportHelp')}`},
      ]
    : []),
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
    if (filter.value === 'needsHelp' && !needsHelp(submission)) {
      return false
    }
    if (filter.value === 'supportNotStarted' && submission.support?.notStarted !== true) {
      return false
    }
    if (filter.value === 'supportNoPush' && submission.support?.noRecentPush !== true) {
      return false
    }
    if (filter.value === 'supportRedChecks' && submission.support?.redChecks !== true) {
      return false
    }
    if (filter.value === 'supportHelp' && submission.support?.helpRequested !== true) {
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

const cloneCommands = computed(() => submissions.value
  .map((submission) => submission.repo?.repoFullName)
  .filter((repo): repo is string => Boolean(repo))
  .map((repo) => `git clone https://github.com/${repo}.git`)
  .join('\n'))

async function refreshRepos() {
  refreshing.value = true
  actionError.value = ''
  try {
    await refreshGitHubRepos(linkId.value)
    await load()
  } catch {
    actionError.value = 'Kunde inte uppdatera GitHub-statusen.'
  } finally {
    refreshing.value = false
  }
}

async function lockRepos() {
  if (!window.confirm('Lås alla repon nu? Repona blir skrivskyddade och senaste commit sparas som inlämning.')) {
    return
  }

  locking.value = true
  actionError.value = ''
  try {
    await lockGitHubRepos(linkId.value)
    await load()
  } catch {
    actionError.value = 'Kunde inte låsa repon.'
  } finally {
    locking.value = false
  }
}

function pushLabel(submission: Schema<'SubmissionDto'>): string {
  return submission.repo?.lastCommitAt
    ? formatDate(submission.repo.lastCommitAt, true)
    : submission.repo
      ? 'Ingen push'
      : '–'
}

function repoChecksLabel(submission: Schema<'SubmissionDto'>): string {
  const repo = submission.repo
  if (!repo || repo.provisioningState !== 'Ready') {
    return '–'
  }
  return `${repo.checksPassed}/${repo.checksTotal}`
}

function repoUrl(submission: Schema<'SubmissionDto'>): string | null {
  return submission.repo?.repoFullName
    ? `https://github.com/${submission.repo.repoFullName}`
    : null
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
    const [moduleResult, submissionResult] = await Promise.all([
      api.GET('/api/courses/{id}/modules', {params: {path: {id: courseId.value}}}),
      api.GET('/api/links/{id}/submissions', {params: {path: {id: linkId.value}}}),
    ])
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
    setCrumbLabel(`assignment:${linkId.value}`, item.title)
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
    <Alert v-if="actionError" variant="destructive" data-test="github-action-error">
      <AlertDescription>{{ actionError }}</AlertDescription>
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
              <span class="text-muted-foreground text-[13px]">{{ partOf(assignment).label }}</span>
              <StatusBadge
                :status="published
                  ? 'publishing.published'
                  : 'publishing.draft'"
              />
            </div>
            <h1 class="font-heading text-[30px] leading-tight font-semibold">{{ assignment.title }}</h1>
            <p class="text-muted-foreground text-[13px]">{{ summary }}</p>
          </div>
          <div v-if="hasRepos" class="flex flex-wrap items-center gap-2">
            <Button
              type="button"
              variant="outline"
              size="sm"
              :disabled="refreshing || locking"
              data-test="github-refresh"
              @click="refreshRepos"
            >
              <PhArrowsClockwise :class="{'animate-spin': refreshing}"/>
              {{ refreshing
                ? 'Uppdaterar…'
                : 'Uppdatera' }}
            </Button>
            <Button
              type="button"
              variant="outline"
              size="sm"
              :disabled="refreshing || locking || cloneCommands.length === 0"
              data-test="github-clone-all"
              @click="cloneOpen = true"
            >
              <PhCopy/>
              Klona alla
            </Button>
            <Button
              type="button"
              variant="destructive"
              size="sm"
              :disabled="refreshing || locking"
              data-test="github-lock"
              @click="lockRepos"
            >
              <PhLock/>
              {{ locking
                ? 'Låser…'
                : 'Lås repon nu' }}
            </Button>
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
          <span class="text-[13px] text-muted-foreground">{{ stat.label }}</span>
          <span class="font-heading text-2xl font-semibold">
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

      <section v-else aria-label="Elever" class="bg-card overflow-hidden rounded-[11px] border">
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
          <table
            class="w-full text-[13px] max-md:block"
            :class="hasRepos
              ? 'md:min-w-[960px]'
              : 'md:min-w-[760px]'"
          >
            <thead class="max-md:hidden">
              <tr class="bg-muted/50 text-muted-foreground text-left">
                <th scope="col" class="border-b px-4 py-2.5 font-medium">Elev</th>
                <template v-if="hasRepos">
                  <th scope="col" class="border-b px-3 py-2.5 font-medium">Senaste push</th>
                  <th scope="col" class="border-b px-3 py-2.5 font-medium">Commits</th>
                  <th scope="col" class="border-b px-3 py-2.5 font-medium">Tester</th>
                </template>
                <th v-else scope="col" class="border-b px-3 py-2.5 font-medium">Filer</th>
                <th scope="col" class="border-b px-3 py-2.5 font-medium">Inlämnad</th>
                <th scope="col" class="border-b px-3 py-2.5 font-medium">Status</th>
                <th scope="col" class="border-b px-4 py-2.5 text-right font-medium">Betyg</th>
              </tr>
            </thead>
            <tbody class="max-md:block">
              <tr
                v-for="(submission, index) in visible"
                :key="submission.id"
                class="max-md:grid max-md:grid-cols-[minmax(0,1fr)_auto] max-md:gap-x-3 max-md:gap-y-1 max-md:px-4 max-md:py-3"
                :class="index < visible.length - 1
                  ? 'max-md:border-b'
                  : ''"
                data-test="submission-row"
              >
                <td class="px-4 py-3 max-md:border-b-0 max-md:p-0" :class="rowBorder(index)">
                  <div class="font-medium">{{ nameOf(submission) }}</div>
                  <a
                    v-if="repoUrl(submission)"
                    :href="repoUrl(submission)!"
                    target="_blank"
                    rel="noopener noreferrer"
                    class="font-mono text-xs text-muted-foreground underline underline-offset-3"
                  >{{ submission.repo?.repoFullName }}</a>
                  <div v-else-if="submission.repo" class="text-xs text-muted-foreground">
                    {{ submission.repo.provisioningState === 'Failed'
                      ? 'Repot kunde inte skapas'
                      : 'Repot skapas' }}
                  </div>
                  <div v-if="submission.repo?.reopenedAt" class="text-xs text-warning">
                    Öppnat för komplettering
                  </div>
                  <div
                    v-if="supportReasons(submission).length > 0"
                    class="mt-1 flex flex-wrap items-center gap-1"
                    data-test="support-reasons"
                  >
                    <Badge
                      v-for="reason in supportReasons(submission)"
                      :key="reason"
                      variant="outline"
                      class="h-5 rounded-[4px] px-1.5 text-[11px]"
                      :class="supportReasonClass(reason)"
                    >
                      {{ reason }}
                    </Badge>
                    <Button
                      v-if="submission.support?.helpRequested"
                      type="button"
                      variant="ghost"
                      size="sm"
                      class="h-5 px-1.5 text-[11px]"
                      data-test="support-resolve"
                      @click="resolveHelp(submission)"
                    >
                      Markera löst
                    </Button>
                  </div>
                </td>
                <template v-if="hasRepos">
                  <td class="px-3 py-3 max-md:border-b-0 max-md:p-0" :class="rowBorder(index)">
                    {{ pushLabel(submission) }}
                  </td>
                  <td class="px-3 py-3 max-md:border-b-0 max-md:p-0" :class="rowBorder(index)">
                    {{ submission.repo?.commitCount ?? '–' }}
                  </td>
                  <td class="px-3 py-3 max-md:border-b-0 max-md:p-0" :class="rowBorder(index)">
                    <span
                      v-if="submission.repo"
                      :class="checkClass(submission.repo.checkStatus)"
                      :title="checkLabel(submission.repo.checkStatus)"
                    >{{ repoChecksLabel(submission) }}</span>
                    <span v-else class="text-muted-foreground">–</span>
                  </td>
                </template>
                <td v-else class="px-3 py-3 max-md:hidden" :class="rowBorder(index)">
                  {{ submission.files.length > 0
                    ? submission.files.length
                    : '–' }}
                </td>
                <td class="px-3 py-3 max-md:border-b-0 max-md:p-0 max-md:text-muted-foreground" :class="rowBorder(index)">
                  {{ submission.submittedAt
                    ? formatDate(submission.submittedAt, true)
                    : '–' }}
                </td>
                <td class="px-3 py-3 max-md:border-b-0 max-md:p-0" :class="rowBorder(index)">
                  <StatusBadge
                    v-if="submission.status === 'Submitted' || submission.status === 'NeedsWork'"
                    :status="submissionStatusKey(submission.status)"
                  />
                  <span v-else class="text-muted-foreground">{{ statusLabel(submission) }}</span>
                </td>
                <td class="px-4 py-3 text-right max-md:col-start-2 max-md:row-span-2 max-md:row-start-1 max-md:self-center max-md:border-b-0 max-md:p-0" :class="rowBorder(index)">
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
                    >
                      <Badge v-if="submission.grade?.level" :variant="gradeBadgeVariant(submission.grade.level)">
                        {{ gradeLabel(submission.grade.level) }}
                      </Badge>
                      <span v-else class="bg-muted text-muted-foreground flex size-7 items-center justify-center rounded-full">
                        <PhCheck class="size-3.5"/>
                      </span>
                    </RouterLink>
                    <span v-else class="text-muted-foreground">–</span>
                  </div>
                </td>
              </tr>
              <tr v-if="visible.length === 0">
                <td
                  :colspan="hasRepos
                    ? 7
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

    <Dialog v-model:open="cloneOpen">
      <DialogContent class="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>Klona alla repon</DialogTitle>
          <DialogDescription>
            Kör kommandona lokalt för att få hem alla elevrepon.
          </DialogDescription>
        </DialogHeader>
        <Textarea
          :model-value="cloneCommands"
          readonly
          rows="8"
          class="font-mono text-xs"
          data-test="clone-commands"
        />
        <DialogFooter>
          <Button type="button" @click="cloneOpen = false">Stäng</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  </div>
</template>
