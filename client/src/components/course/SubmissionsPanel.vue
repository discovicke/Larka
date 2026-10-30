<script setup lang="ts">
import {
  PhArrowsClockwise,
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
import {useCurrentUser} from '@/composables/useCurrentUser'
import {type Schema} from '@/lib/api'
import {formatDate} from '@/lib/content'
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

const props = defineProps<{
  courseId: string
  linkId: string
  submissions: Schema<'SubmissionDto'>[]
}>()

const emit = defineEmits<{reload: []}>()

// Mentorn läser inlämningarna men bedömer inte.
const {can} = useCurrentUser()
const canGrade = computed(() => can('grade', props.courseId))

const filter = ref<Filter>('all')
const search = ref('')
const refreshing = ref(false)
const locking = ref(false)
const actionError = ref('')
const cloneOpen = ref(false)

function nameOf(submission: Schema<'SubmissionDto'>): string {
  return submission.studentName
}

function countOf(status: Schema<'SubmissionStatus'>): number {
  return props.submissions.filter((submission) => submission.status === status).length
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

const needsHelpCount = computed(() => props.submissions.filter(needsHelp).length)

function supportCount(filter: Filter): number {
  return props.submissions.filter((submission) => {
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
    await resolveStudentHelp(props.linkId, submission.studentId)
    emit('reload')
  } catch {
    actionError.value = 'Kunde inte markera hjälpfrågan som löst.'
  }
}

const filters = computed<{value: Filter, label: string}[]>(() => [
  {value: 'all', label: `Alla ${props.submissions.length}`},
  {value: 'due', label: `Att bedöma ${countOf('Submitted')}`},
  {value: 'needsWork', label: `Komplettering ${countOf('NeedsWork')}`},
  {value: 'notStarted', label: `Ej inlämnad ${countOf('NotStarted')}`},
  ...(props.submissions.some((submission) => submission.repo !== null)
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

const sorted = computed(() => [...props.submissions]
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

const hasRepos = computed(() => props.submissions.some((submission) => submission.repo !== null))

function filterClass(value: Filter) {
  return filter.value === value
    ? 'bg-background shadow-sm font-medium'
    : 'text-muted-foreground'
}

const cloneCommands = computed(() => props.submissions
  .map((submission) => submission.repo?.repoFullName)
  .filter((repo): repo is string => Boolean(repo))
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

// Komplettera väntar på eleven, så där är det inget att bedöma än.
function canAssess(submission: Schema<'SubmissionDto'>): boolean {
  return canGrade.value && submission.status === 'Submitted'
}

function reviewRoute(submission: Schema<'SubmissionDto'>) {
  return {
    name: 'submission-review',
    params: {courseId: props.courseId, linkId: props.linkId, submissionId: submission.id},
  }
}

function rowBorder(index: number) {
  return index < visible.value.length - 1
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
                    :variant="canAssess(submission)
                      ? 'default'
                      : 'outline'"
                    class="h-7 px-2.5 text-[13px]"
                  >
                    <RouterLink :to="reviewRoute(submission)">
                      {{ canAssess(submission)
                        ? 'Bedöm'
                        : 'Visa' }}
                    </RouterLink>
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
