<script setup lang="ts">
import {
  PhArrowsClockwise,
  PhCaretDown,
  PhCaretRight,
  PhCheck,
  PhCopy,
  PhLock,
  PhMagnifyingGlass,
  PhUsersThree,
} from '@phosphor-icons/vue'
import {computed, ref} from 'vue'
import {RouterLink} from 'vue-router'

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
import {Textarea} from '@/components/ui/textarea'
import {type Schema} from '@/lib/api'
import {formatDate} from '@/lib/content'
import {lockGitHubRepos, refreshGitHubRepos} from '@/lib/integrations'
import {submissionStatusKey} from '@/lib/status'
import {resolveStudentHelp} from '@/lib/support'
import {checkClass, checkLabel, gradeBadgeVariant, gradeLabel, gradeName, statusLabel} from '@/lib/submissions'

type Filter = 'all'
  | 'due'
  | 'needsWork'
  | 'notStarted'
  | 'autoFail'
  | 'needsHelp'
  | 'supportNotStarted'
  | 'supportNoPush'
  | 'supportRedChecks'
  | 'supportHelp'

type Submission = Schema<'SubmissionDto'>
type Group = Schema<'AssignmentGroupDto'>

interface TrackingRow {
  key: string
  name: string
  group: Group | null
  submissions: Submission[]
  primary: Submission
  status: Schema<'SubmissionStatus'>
  /** Sista deadline har passerat utan inlämning eller komplettering: automatiskt IG. */
  autoFail: boolean
  /** Elevens egen extra deadline, för en elevrad. */
  personalDueAt: string | null
  repo: Submission['repo']
  submittedAt: string | null
  files: number
  grade: Submission['grade']
  gradedCount: number
  isMember: boolean
  supportReasons: string[]
}

const props = defineProps<{
  courseId: string
  linkId: string
  submissions: Submission[]
  /** Bedöms på plats: läraren kan sätta betyg utan inlämning. */
  onSite?: boolean
}>()

const emit = defineEmits<{reload: []}>()

const filter = ref<Filter>('all')
const search = ref('')
const refreshing = ref(false)
const locking = ref(false)
const actionError = ref('')
const cloneOpen = ref(false)
const expanded = ref<string[]>([])

function aggregateStatus(submissions: Submission[]): Schema<'SubmissionStatus'> {
  if (submissions.length > 0 && submissions.every((submission) => submission.status === 'Graded')) {
    return 'Graded'
  }
  if (submissions.some((submission) => submission.status === 'NeedsWork')) {
    return 'NeedsWork'
  }
  if (submissions.some((submission) => submission.status === 'Submitted' || submission.status === 'Graded')) {
    return 'Submitted'
  }
  return 'NotStarted'
}

// Backend räknar automatiskt IG per elev. En grupp har det först när alla medlemmar har det.
function groupAutoFail(submissions: Submission[]): boolean {
  return submissions.length > 0 && submissions.every((submission) => submission.deadline?.autoFail === true)
}

function reasonsFor(submissions: Submission[]): string[] {
  const reasons = new Set<string>()
  for (const submission of submissions) {
    const support = submission.support
    if (!support) {
      continue
    }
    if (support.helpRequested) {
      reasons.add('Elevflagga')
    }
    if (support.notStarted) {
      reasons.add('Inte startat')
    }
    if (support.noRecentPush) {
      reasons.add('Ingen push')
    }
    if (support.redChecks) {
      reasons.add('Röda tester')
    }
  }
  return [...reasons]
}

function preferredSubmission(submissions: Submission[]): Submission {
  return submissions.find((submission) => submission.status === 'Submitted')
    ?? submissions.find((submission) => submission.status === 'NeedsWork')
    ?? submissions[0]
}

function submittedAtOf(submissions: Submission[]): string | null {
  return submissions
    .map((submission) => submission.submittedAt)
    .filter((value): value is string => Boolean(value))
    .sort()
    .at(-1) ?? null
}

function sharedGrade(submissions: Submission[]): Submission['grade'] {
  if (submissions.length === 0 || submissions.some((submission) => submission.grade === null)) {
    return null
  }
  const level = submissions[0].grade?.level ?? null
  return submissions.every((submission) => (submission.grade?.level ?? null) === level)
    ? submissions[0].grade
    : null
}

function toStudentRow(submission: Submission, isMember = false): TrackingRow {
  return {
    key: `student:${submission.id}`,
    name: submission.studentName,
    group: null,
    submissions: [submission],
    primary: submission,
    status: submission.status,
    autoFail: submission.deadline?.autoFail === true,
    personalDueAt: submission.personalDueAt ?? null,
    repo: submission.repo,
    submittedAt: submission.submittedAt,
    files: submission.files.length,
    grade: submission.grade,
    gradedCount: submission.grade
      ? 1
      : 0,
    isMember,
    supportReasons: reasonsFor([submission]),
  }
}

function toGroupRow(group: Group, submissions: Submission[]): TrackingRow {
  return {
    key: `group:${group.id}`,
    name: group.name,
    group,
    submissions,
    primary: preferredSubmission(submissions),
    status: aggregateStatus(submissions),
    // Gruppens medlemmar kan ha olika personliga deadlines, så gruppen räknar med den senaste.
    autoFail: groupAutoFail(submissions),
    personalDueAt: null,
    repo: submissions.find((submission) => submission.repo)?.repo ?? null,
    submittedAt: submittedAtOf(submissions),
    files: submissions.reduce((sum, submission) => sum + submission.files.length, 0),
    grade: sharedGrade(submissions),
    gradedCount: submissions.filter((submission) => submission.grade).length,
    isMember: false,
    supportReasons: reasonsFor(submissions),
  }
}

const rows = computed<TrackingRow[]>(() => {
  const groups = new Map<string, {group: Group, submissions: Submission[]}>()
  const students: TrackingRow[] = []
  for (const submission of props.submissions) {
    if (submission.group) {
      const existing = groups.get(submission.group.id)
      if (existing) {
        existing.submissions.push(submission)
      } else {
        groups.set(submission.group.id, {group: submission.group, submissions: [submission]})
      }
    } else {
      students.push(toStudentRow(submission))
    }
  }
  return [
    ...students,
    ...[...groups.values()].map(({group, submissions}) => toGroupRow(group, submissions)),
  ]
})

const hasGroups = computed(() => rows.value.some((row) => row.group !== null))
const unit = computed(() => {
  const count = rows.value.length
  if (hasGroups.value) {
    return count === 1
      ? 'grupp'
      : 'grupper'
  }
  return count === 1
    ? 'elev'
    : 'elever'
})
const hasRepos = computed(() => props.submissions.some((submission) => submission.repo !== null))

function countOf(status: Schema<'SubmissionStatus'>): number {
  return rows.value.filter((row) => row.status === status && !row.autoFail).length
}

const autoFailCount = computed(() => rows.value.filter((row) => row.autoFail).length)

function needsHelp(row: TrackingRow): boolean {
  return row.supportReasons.length > 0
}

const needsHelpCount = computed(() => rows.value.filter(needsHelp).length)

function supportCount(filterValue: Filter): number {
  return rows.value.filter((row) => {
    if (filterValue === 'supportNotStarted') {
      return row.supportReasons.includes('Inte startat')
    }
    if (filterValue === 'supportNoPush') {
      return row.supportReasons.includes('Ingen push')
    }
    if (filterValue === 'supportRedChecks') {
      return row.supportReasons.includes('Röda tester')
    }
    return row.supportReasons.includes('Elevflagga')
  }).length
}

function supportReasonClass(reason: string): string {
  return reason === 'Elevflagga'
    ? 'border-primary/30 bg-primary/10 text-primary'
    : 'border-warning/30 bg-warning/10 text-warning'
}

function hasHelp(row: TrackingRow): boolean {
  return row.group
    ? row.submissions.some((submission) => submission.support?.helpRequested === true)
    : row.primary.support?.helpRequested === true
}

async function resolveHelp(row: TrackingRow): Promise<void> {
  actionError.value = ''
  const targets = row.group
    ? row.submissions.filter((submission) => submission.support?.helpRequested)
    : [row.primary]
  try {
    for (const target of targets) {
      await resolveStudentHelp(props.linkId, target.studentId)
    }
    emit('reload')
  } catch {
    actionError.value = 'Kunde inte markera hjälpfrågan som löst.'
  }
}

const filters = computed<{value: Filter, label: string}[]>(() => [
  {value: 'all', label: `Alla ${rows.value.length}`},
  {value: 'due', label: `Att bedöma ${countOf('Submitted')}`},
  {value: 'needsWork', label: `Komplettering ${countOf('NeedsWork')}`},
  {value: 'notStarted', label: `Inte inlämnad ${countOf('NotStarted')}`},
  ...(autoFailCount.value > 0
    ? [{value: 'autoFail' as const, label: `IG automatiskt ${autoFailCount.value}`}]
    : []),
  ...(hasRepos.value
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

const sorted = computed(() => [...rows.value]
  .sort((left, right) => statusOrder[left.status] - statusOrder[right.status]
    || left.name.localeCompare(right.name, 'sv')))

const visibleRows = computed(() => {
  const text = search.value.trim().toLowerCase()
  return sorted.value.filter((row) => {
    if (filter.value === 'due' && row.status !== 'Submitted') {
      return false
    }
    if (filter.value === 'needsWork' && (row.status !== 'NeedsWork' || row.autoFail)) {
      return false
    }
    if (filter.value === 'autoFail' && !row.autoFail) {
      return false
    }
    if (filter.value === 'notStarted' && (row.status !== 'NotStarted' || row.autoFail)) {
      return false
    }
    if (filter.value === 'needsHelp' && !needsHelp(row)) {
      return false
    }
    if (filter.value === 'supportNotStarted' && !row.supportReasons.includes('Inte startat')) {
      return false
    }
    if (filter.value === 'supportNoPush' && !row.supportReasons.includes('Ingen push')) {
      return false
    }
    if (filter.value === 'supportRedChecks' && !row.supportReasons.includes('Röda tester')) {
      return false
    }
    if (filter.value === 'supportHelp' && !row.supportReasons.includes('Elevflagga')) {
      return false
    }
    return !text
      || row.name.toLowerCase().includes(text)
      || row.submissions.some((submission) => submission.studentName.toLowerCase().includes(text))
  })
})

const displayRows = computed(() => visibleRows.value.flatMap((row) => row.group
  ? [
      row,
      ...(expanded.value.includes(row.key)
        ? row.submissions.map((submission) => toStudentRow(submission, true))
        : []),
    ]
  : [row]))

function toggleGroup(key: string) {
  expanded.value = expanded.value.includes(key)
    ? expanded.value.filter((candidate) => candidate !== key)
    : [...expanded.value, key]
}

function isExpanded(key: string): boolean {
  return expanded.value.includes(key)
}

function filterClass(value: Filter) {
  return filter.value === value
    ? 'bg-background shadow-sm font-medium'
    : 'text-muted-foreground'
}

const cloneCommands = computed(() => [...new Set(props.submissions
  .map((submission) => submission.repo?.repoFullName)
  .filter((repo): repo is string => Boolean(repo)))]
  .map((repo) => `git clone https://github.com/${repo}.git`)
  .join('\n'))

async function refreshRepos() {
  refreshing.value = true
  actionError.value = ''
  try {
    await refreshGitHubRepos(props.linkId)
    emit('reload')
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
    await lockGitHubRepos(props.linkId)
    emit('reload')
  } catch {
    actionError.value = 'Kunde inte låsa repon.'
  } finally {
    locking.value = false
  }
}

function hasOwnCommit(row: TrackingRow): boolean {
  const repo = row.repo
  if (!repo) {
    return false
  }
  return repo.initialSha
    ? repo.lastSha !== repo.initialSha
    : repo.commitCount > 1
}

function pushLabel(row: TrackingRow): string {
  return hasOwnCommit(row) && row.repo?.lastCommitAt
    ? formatDate(row.repo.lastCommitAt, true)
    : 'Ingen push från deltagarna'
}

function commitLabel(row: TrackingRow): number | string {
  if (!row.repo) {
    return '–'
  }
  return Math.max(0, row.repo.commitCount - (row.repo.initialSha
    ? 1
    : 0))
}

function repoChecksLabel(row: TrackingRow): string {
  const repo = row.repo
  if (!repo || repo.provisioningState !== 'Ready') {
    return '–'
  }
  return `${repo.checksPassed}/${repo.checksTotal}`
}

function repoUrl(row: TrackingRow): string | null {
  return row.repo?.repoFullName
    ? `https://github.com/${row.repo.repoFullName}`
    : null
}

// Komplettera väntar på eleven, så där är det inget att bedöma än.
// Bedöm när det finns något att bedöma: en inlämning, eller en elev utan inlämning när uppgiften bedöms på plats.
function canAssess(row: TrackingRow): boolean {
  return row.status === 'Submitted' || (Boolean(props.onSite) && !row.group && row.status === 'NotStarted')
}

// En elev utan inlämning går att öppna, för att bedöma på plats eller ge eleven en egen deadline.
function canOpenEmpty(row: TrackingRow): boolean {
  return !row.group && row.status === 'NotStarted'
}

function reviewRoute(row: TrackingRow) {
  return {
    name: 'submission-review',
    params: {courseId: props.courseId, linkId: props.linkId, submissionId: row.primary.id},
  }
}

function rowBorder(index: number) {
  return index < displayRows.value.length - 1
    ? 'border-b'
    : ''
}
</script>

<template>
  <div class="flex flex-col gap-4">
    <Alert v-if="actionError" variant="destructive" data-test="github-action-error">
      <AlertDescription>{{ actionError }}</AlertDescription>
    </Alert>

    <div v-if="hasRepos" class="flex flex-wrap items-center justify-end gap-2">
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

    <Empty v-if="submissions.length === 0" class="border border-dashed">
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

        <div role="tablist" class="bg-muted flex flex-wrap rounded-[7px] p-[3px]">
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
              <th scope="col" class="border-b px-4 py-2.5 font-medium">Elev / grupp</th>
              <template v-if="hasRepos">
                <th scope="col" class="border-b px-3 py-2.5 font-medium">Senaste push</th>
                <th scope="col" class="border-b px-3 py-2.5 font-medium">Egna commits</th>
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
              v-for="(row, index) in displayRows"
              :key="row.key"
              class="max-md:grid max-md:grid-cols-[minmax(0,1fr)_auto] max-md:gap-x-3 max-md:gap-y-1 max-md:px-4 max-md:py-3"
              :class="[
                row.isMember && 'bg-muted/20',
                index < displayRows.length - 1
                  ? 'max-md:border-b'
                  : '',
              ]"
              :data-test="row.group
                ? 'group-row'
                : row.isMember
                  ? 'member-row'
                  : 'submission-row'"
            >
              <td class="px-4 py-3 max-md:border-b-0 max-md:p-0" :class="rowBorder(index)">
                <div class="flex flex-wrap items-center gap-1.5">
                  <Button
                    v-if="row.group"
                    type="button"
                    variant="ghost"
                    size="sm"
                    class="h-6 gap-1 px-1.5"
                    :aria-expanded="isExpanded(row.key)"
                    data-test="group-toggle"
                    @click="toggleGroup(row.key)"
                  >
                    <component
                      :is="isExpanded(row.key)
                        ? PhCaretDown
                        : PhCaretRight"
                      class="size-3.5"
                    />
                    <span class="font-medium" data-test="group-name">{{ row.name }}</span>
                  </Button>
                  <span v-else class="font-medium">{{ row.name }}</span>
                  <Badge
                    v-if="row.group || row.isMember"
                    variant="outline"
                    class="h-5 rounded-[4px] px-1.5 text-[11px]"
                  >
                    {{ row.group
                      ? `${row.submissions.length} medlemmar`
                      : 'Gruppmedlem' }}
                  </Badge>
                </div>
                <div
                  v-if="row.personalDueAt"
                  class="text-muted-foreground text-xs"
                  data-test="personal-due-at-hint"
                >
                  Egen deadline {{ formatDate(row.personalDueAt, true) }}
                </div>
                <div v-if="row.group" class="text-muted-foreground text-xs">
                  {{ row.submissions.map((submission) => submission.studentName).join(', ') }}
                </div>
                <a
                  v-if="repoUrl(row)"
                  :href="repoUrl(row)!"
                  target="_blank"
                  rel="noopener noreferrer"
                  class="font-mono text-xs text-muted-foreground underline underline-offset-3"
                >{{ row.repo?.repoFullName }}</a>
                <div v-else-if="row.repo" class="text-xs text-muted-foreground">
                  {{ row.repo.provisioningState === 'Failed'
                    ? 'Repot kunde inte skapas'
                    : 'Repot skapas' }}
                </div>
                <div v-if="row.repo?.reopenedAt" class="text-xs text-warning">
                  Öppnat för komplettering
                </div>
                <div
                  v-if="row.supportReasons.length > 0"
                  class="mt-1 flex flex-wrap items-center gap-1"
                  data-test="support-reasons"
                >
                  <Badge
                    v-for="reason in row.supportReasons"
                    :key="reason"
                    variant="outline"
                    class="h-5 rounded-[4px] px-1.5 text-[11px]"
                    :class="supportReasonClass(reason)"
                  >
                    {{ reason }}
                  </Badge>
                  <Button
                    v-if="hasHelp(row)"
                    type="button"
                    variant="ghost"
                    size="sm"
                    class="h-5 px-1.5 text-[11px]"
                    data-test="support-resolve"
                    @click="resolveHelp(row)"
                  >
                    Markera löst
                  </Button>
                </div>
              </td>
              <template v-if="hasRepos">
                <td class="px-3 py-3 max-md:border-b-0 max-md:p-0" :class="rowBorder(index)">
                  {{ row.isMember
                    ? ''
                    : pushLabel(row) }}
                </td>
                <td class="px-3 py-3 max-md:border-b-0 max-md:p-0" :class="rowBorder(index)">
                  {{ row.isMember
                    ? ''
                    : commitLabel(row) }}
                </td>
                <td class="px-3 py-3 max-md:border-b-0 max-md:p-0" :class="rowBorder(index)">
                  <span
                    v-if="row.repo && !row.isMember"
                    :class="checkClass(row.repo.checkStatus)"
                    :title="checkLabel(row.repo.checkStatus)"
                  >{{ repoChecksLabel(row) }}</span>
                  <span v-else-if="!row.isMember" class="text-muted-foreground">–</span>
                </td>
              </template>
              <td v-else class="px-3 py-3 max-md:hidden" :class="rowBorder(index)">
                {{ row.files > 0
                  ? row.files
                  : '–' }}
              </td>
              <td class="px-3 py-3 max-md:border-b-0 max-md:p-0 max-md:text-muted-foreground" :class="rowBorder(index)">
                {{ row.isMember
                  ? ''
                  : row.submittedAt
                    ? formatDate(row.submittedAt, true)
                    : '–' }}
              </td>
              <td class="px-3 py-3 max-md:border-b-0 max-md:p-0" :class="rowBorder(index)">
                <StatusBadge
                  v-if="row.autoFail || row.status === 'Submitted' || row.status === 'NeedsWork'"
                  :status="submissionStatusKey(row.status, row.autoFail)"
                />
                <span v-else class="text-muted-foreground">{{ statusLabel(row) }}</span>
              </td>
              <td class="px-4 py-3 text-right max-md:col-start-2 max-md:row-span-2 max-md:row-start-1 max-md:self-center max-md:border-b-0 max-md:p-0" :class="rowBorder(index)">
                <div class="flex justify-end">
                  <Button
                    v-if="row.status === 'Submitted' || row.status === 'NeedsWork' || canOpenEmpty(row)"
                    as-child
                    :variant="canAssess(row)
                      ? 'default'
                      : 'outline'"
                    class="h-7 px-2.5 text-[13px]"
                  >
                    <RouterLink :to="reviewRoute(row)">
                      {{ canAssess(row)
                        ? 'Bedöm'
                        : 'Visa' }}
                    </RouterLink>
                  </Button>
                  <RouterLink
                    v-else-if="row.status === 'Graded'"
                    :to="reviewRoute(row)"
                  >
                    <Badge
                      v-if="row.grade?.level"
                      :variant="gradeBadgeVariant(row.grade.level)"
                      role="img"
                      :aria-label="gradeName(row.grade.level)"
                      :title="gradeName(row.grade.level)"
                    >
                      {{ gradeLabel(row.grade.level) }}
                    </Badge>
                    <span
                      v-else-if="row.group && row.gradedCount > 0"
                      class="text-muted-foreground text-xs"
                    >{{ row.gradedCount }}/{{ row.submissions.length }} bedömda</span>
                    <span v-else class="bg-muted text-muted-foreground flex size-7 items-center justify-center rounded-full">
                      <PhCheck class="size-3.5"/>
                    </span>
                  </RouterLink>
                  <span v-else class="text-muted-foreground">–</span>
                </div>
              </td>
            </tr>
            <tr v-if="visibleRows.length === 0">
              <td
                :colspan="hasRepos
                  ? 7
                  : 5"
                class="text-muted-foreground px-4 py-6 text-center"
              >
                Inga {{ unit }} matchar sökningen.
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      <div class="text-muted-foreground border-t px-4 py-2.5 text-[13px]">
        Visar {{ visibleRows.length }} av {{ rows.length }} {{ unit }}
      </div>
    </section>

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
