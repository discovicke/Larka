<script setup lang="ts">
import {PhListChecks} from '@phosphor-icons/vue'
import {computed, ref, watch} from 'vue'

import MarkAllNotSubmittedButton from '@/components/course/MarkAllNotSubmittedButton.vue'
import StatusBadge from '@/components/StatusBadge.vue'
import {Alert, AlertDescription} from '@/components/ui/alert'
import {Button} from '@/components/ui/button'
import {Empty, EmptyDescription, EmptyHeader, EmptyMedia, EmptyTitle} from '@/components/ui/empty'
import {Skeleton} from '@/components/ui/skeleton'
import {useAsync} from '@/composables/useAsync'
import {useCourses} from '@/composables/useCourses'
import {useFlash} from '@/composables/useFlash'
import {formatDate} from '@/lib/content'
import {
  deadlineLine,
  fetchAssignmentRows,
  type GradingGroup,
  type GradingRow,
  type GradingTab,
  groupByAssignment,
  groupRowsByTab,
} from '@/lib/gradingQueue'
import {nextDeadline} from '@/lib/gradingWindow'
import {submissionStatusKey} from '@/lib/status'
import {markedNotSubmittedMessage} from '@/lib/submissionsApi'

const {courses, status} = useCourses()

const rows = ref<GradingRow[]>([])
// Beskedet från bedömningen som skickade läraren hit, t.ex. "Bedömningen är sparad."
const notice = ref(useFlash().takeFlash())
const activeTab = ref<GradingTab>('submitted')

const tabs: {value: GradingTab, label: string, description: string}[] = [
  {value: 'submitted', label: 'Inlämnat', description: 'Inlämningar som väntar på ett betyg eller en komplettering.'},
  {
    value: 'missed',
    label: 'Inte inlämnat efter deadline',
    description: 'Deadline har passerat och inget har kommit in. Eleverna kan fortfarande lämna in till nästa deadline: markeringen Ej inlämnat säger bara att inget kom i tid.',
  },
  {
    value: 'out',
    label: 'Chanserna slut',
    description: 'Sista deadline har passerat och eleven är inte godkänd. Sätt IG, en i taget.',
  },
  {
    value: 'waiting',
    label: 'Väntar på eleven',
    description: 'Bara för överblick. Eleven har en chans kvar och du behöver inte göra något.',
  },
]

const actionLabels: Record<GradingTab, string> = {
  submitted: 'Bedöm',
  missed: 'Markera med kommentar',
  out: 'Sätt IG',
  waiting: '',
}

const byTab = computed(() => groupRowsByTab(rows.value))
const activeRows = computed(() => byTab.value[activeTab.value])
const groups = computed(() => groupByAssignment(activeRows.value))
const activeDescription = computed(() => tabs.find((tab) => tab.value === activeTab.value)?.description ?? '')
// Överblicken är en tabell över elever och inte per uppgift, så den sorteras på namn.
const waitingRows = computed(() => [...byTab.value.waiting]
  .sort((left, right) => left.name.localeCompare(right.name, 'sv')))

const total = computed(() => tabs.reduce((sum, tab) => sum + byTab.value[tab.value].length, 0))

function tabClass(value: GradingTab) {
  return activeTab.value === value
    ? 'bg-primary-foreground text-background hover:bg-primary-foreground/90 hover:text-background'
    : ''
}

function reviewLink(row: GradingRow) {
  return {
    name: 'submission-review',
    params: {courseId: row.courseId, linkId: row.linkId, submissionId: row.submission.id},
  }
}

function line(group: GradingGroup) {
  return deadlineLine(group)
}

function chanceUntil(row: GradingRow): string {
  const until = nextDeadline(row)
  return until
    ? `Chans till ${formatDate(until)}`
    : '–'
}

const {run: loadRows, loading, error} = useAsync(
  () => fetchAssignmentRows(courses.value),
  'Kunde inte hämta inlämningarna.',
)

async function load() {
  const loaded = await loadRows()
  if (loaded) {
    rows.value = loaded
  }
}

async function markedAll(count: number) {
  await load()
  notice.value = markedNotSubmittedMessage(count)
}

watch(status, (value) => {
  if (value !== 'loading') {
    load()
  }
}, {immediate: true})
</script>

<template>
  <div class="flex flex-col gap-6">
    <div class="flex flex-col gap-1">
      <h1 class="font-heading text-[30px] leading-tight font-semibold">Att bedöma</h1>
      <p class="text-muted-foreground">Det som väntar på dig i dina kurser, uppgift för uppgift</p>
    </div>

    <Alert v-if="notice" data-test="grading-notice">
      <AlertDescription>{{ notice }}</AlertDescription>
    </Alert>

    <Alert v-if="error" variant="destructive">
      <AlertDescription>{{ error }}</AlertDescription>
    </Alert>

    <div v-else-if="loading" class="flex flex-col gap-2">
      <Skeleton v-for="i in 4" :key="i" class="h-12 w-full"/>
    </div>

    <Empty v-else-if="total === 0" class="border border-dashed">
      <EmptyHeader>
        <EmptyMedia variant="icon">
          <PhListChecks/>
        </EmptyMedia>
        <EmptyTitle>Inget att bedöma</EmptyTitle>
        <EmptyDescription>Nya inlämningar från dina elever visas här.</EmptyDescription>
      </EmptyHeader>
    </Empty>

    <template v-else>
      <div role="tablist" aria-label="Vad som väntar" class="flex flex-wrap gap-2">
        <Button
          v-for="tab in tabs"
          :key="tab.value"
          type="button"
          role="tab"
          variant="outline"
          class="h-9 px-3.5 text-sm"
          :class="tabClass(tab.value)"
          :aria-selected="activeTab === tab.value"
          :data-test="`grading-tab-${tab.value}`"
          @click="activeTab = tab.value"
        >
          {{ tab.label }}
          <span class="font-normal opacity-80" data-test="grading-tab-count">{{ byTab[tab.value].length }}</span>
        </Button>
      </div>

      <p class="text-muted-foreground -mt-2" data-test="grading-tab-description">{{ activeDescription }}</p>

      <Empty v-if="activeRows.length === 0" class="border border-dashed" data-test="grading-tab-empty">
        <EmptyHeader>
          <EmptyMedia variant="icon">
            <PhListChecks/>
          </EmptyMedia>
          <EmptyTitle>Inget här just nu</EmptyTitle>
        </EmptyHeader>
      </Empty>

      <section
        v-else-if="activeTab === 'waiting'"
        aria-label="Väntar på eleven"
        class="overflow-hidden rounded-[11px] border"
      >
        <div class="overflow-x-auto">
          <table class="w-full min-w-[640px] text-[13px]">
            <tbody>
              <tr
                v-for="(row, index) in waitingRows"
                :key="row.submission.id"
                data-test="grading-row"
                :class="index < waitingRows.length - 1
                  ? 'border-b'
                  : ''"
              >
                <td class="px-5 py-3 font-medium">{{ row.name }}</td>
                <td class="text-muted-foreground px-3 py-3">{{ row.title }} · {{ row.courseTitle }}</td>
                <td class="px-3 py-3">
                  <StatusBadge :status="submissionStatusKey(row.submission.status)" :icon="false"/>
                </td>
                <td class="text-muted-foreground px-5 py-3">{{ chanceUntil(row) }}</td>
              </tr>
            </tbody>
          </table>
        </div>
      </section>

      <template v-else>
        <section
          v-for="group in groups"
          :key="group.linkId"
          :aria-label="group.title"
          class="overflow-hidden rounded-[11px] border"
          data-test="grading-group"
        >
          <div class="bg-muted/50 flex flex-wrap items-center justify-between gap-3 border-b px-5 py-3.5">
            <div class="flex flex-col gap-0.5">
              <span class="text-[15px] font-semibold">
                {{ group.title }}
                <span class="text-muted-foreground font-normal">· {{ group.courseTitle }}</span>
              </span>
              <span
                class="text-[13px]"
                :class="line(group).last
                  ? 'text-destructive font-medium'
                  : 'text-muted-foreground'"
                data-test="grading-deadline"
              >
                {{ line(group).text }}
              </span>
            </div>
            <Button v-if="activeTab === 'submitted'" as-child class="h-9 px-3.5 text-sm font-semibold">
              <RouterLink :to="reviewLink(group.rows[0])" data-test="grade-in-order">
                Bedöm {{ group.rows.length }} i ordning
              </RouterLink>
            </Button>
            <div v-else-if="activeTab === 'missed'" data-test="grading-group-actions">
              <MarkAllNotSubmittedButton
                :link-id="group.linkId"
                :count="group.rows.length"
                @done="markedAll"
              />
            </div>
          </div>

          <div class="overflow-x-auto">
            <table class="w-full min-w-[640px] text-[13px]">
              <tbody>
                <tr
                  v-for="(row, index) in group.rows"
                  :key="row.submission.id"
                  data-test="grading-row"
                  :class="index < group.rows.length - 1
                    ? 'border-b'
                    : ''"
                >
                  <td class="px-5 py-3 font-medium">{{ row.name }}</td>
                  <td v-if="activeTab === 'submitted'" class="px-3 py-3">
                    <StatusBadge
                      :status="row.previousGrade
                        ? 'submission.again'
                        : 'submission.first'"
                      :icon="false"
                    />
                  </td>
                  <td v-else-if="activeTab === 'out'" class="px-3 py-3">
                    <StatusBadge :status="submissionStatusKey(row.submission.status)" :icon="false"/>
                  </td>
                  <td v-if="activeTab === 'submitted'" class="text-muted-foreground px-3 py-3">
                    {{ row.submission.submittedAt
                      ? `Inlämnad ${formatDate(row.submission.submittedAt, true)}`
                      : '–' }}
                  </td>
                  <td class="px-5 py-3 text-right">
                    <Button as-child variant="link" class="h-7 px-0 text-[13px]">
                      <RouterLink :to="reviewLink(row)">
                        {{ actionLabels[activeTab] }}
                      </RouterLink>
                    </Button>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </section>
      </template>
    </template>
  </div>
</template>
