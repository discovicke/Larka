<script setup lang="ts">
import {computed, ref} from 'vue'

import {Badge} from '@/components/ui/badge'
import PillTabs from '@/components/ui/PillTabs.vue'
import {Button} from '@/components/ui/button'
import SearchField from '@/components/ui/SearchField.vue'
import {studentProgressStatus, type StudentAnalyticsRow} from '@/lib/analytics'

const props = defineProps<{rows: StudentAnalyticsRow[], scopeFinished: boolean}>()
const emit = defineEmits<{open: [studentId: string]}>()

const filter = ref<'all' | 'risk'>('all')
const search = ref('')

const riskCount = computed(() => props.rows.filter((row) => row.needsSupport).length)

function statusOf(row: StudentAnalyticsRow): 'Behöver stöttning' | 'Klar' | 'På väg' {
  const status = studentProgressStatus(row, props.scopeFinished)
  return status === 'needsSupport'
    ? 'Behöver stöttning'
    : status === 'complete'
      ? 'Klar'
      : 'På väg'
}

function statusVariant(row: StudentAnalyticsRow): 'destructive' | 'success' | 'secondary' {
  const status = studentProgressStatus(row, props.scopeFinished)
  return status === 'needsSupport'
    ? 'destructive'
    : status === 'complete'
      ? 'success'
      : 'secondary'
}
const visibleRows = computed(() => {
  const text = search.value.trim().toLowerCase()
  return props.rows.filter((row) =>
    (filter.value === 'all' || row.needsSupport)
    && (!text || row.name.toLowerCase().includes(text)))
})
</script>

<template>
  <div class="flex flex-col gap-3">
    <div class="flex flex-wrap items-center justify-between gap-3">
      <PillTabs
        v-model="filter"
        :items="[
          {value: 'all', label: 'Alla elever'},
          {value: 'risk', label: `Behöver stöttning ${riskCount}`, dataTest: 'risk-filter'},
        ]"
      />

      <SearchField
        id="analytics-student-search"
        v-model="search"
        class="sm:w-[220px]"
        label="Sök elev"
        placeholder="Sök elev…"
      />
    </div>

    <div v-if="visibleRows.length === 0" class="text-muted-foreground rounded-[11px] border border-dashed p-6 text-center text-sm">
      {{ filter === 'risk'
        ? 'Inga elever behöver stöttning just nu.'
        : 'Inga elever matchar sökningen.' }}
    </div>

    <div v-else class="bg-card overflow-hidden rounded-[11px] border">
      <div class="overflow-x-auto">
        <table class="w-full border-collapse text-sm" data-test="analytics-students">
          <thead>
            <tr class="bg-muted/50 text-muted-foreground text-left text-[13px]">
              <th scope="col" class="border-b px-4 py-2.5 font-medium">Elev</th>
              <th scope="col" class="border-b px-3 py-2.5 text-right font-medium">Bedömda</th>
              <th scope="col" class="border-b px-3 py-2.5 font-medium">Resultat</th>
              <th scope="col" class="border-b px-3 py-2.5 font-medium">Kvar</th>
              <th scope="col" class="border-b px-3 py-2.5 font-medium">Status</th>
              <th scope="col" class="border-b px-4 py-2.5 text-right font-medium">
                <span class="sr-only">Visa</span>
              </th>
            </tr>
          </thead>
          <tbody>
            <tr
              v-for="row in visibleRows"
              :key="row.studentId"
              class="border-b last:border-b-0"
              data-test="analytics-student"
            >
              <td class="px-4 py-2.5 font-medium">{{ row.name }}</td>
              <td class="px-3 py-2.5 text-right whitespace-nowrap tabular-nums">
                {{ row.gradedCount }} av {{ row.assignmentCount }}
              </td>
              <td class="px-3 py-2.5 whitespace-nowrap">
                <div class="flex items-center gap-3">
                  <span class="text-grade-pass">{{ row.passCount }} G</span>
                  <span class="text-grade-distinction">{{ row.distinctionCount }} VG</span>
                  <span
                    :class="row.failCount > 0
                      ? 'text-grade-fail font-semibold'
                      : 'text-muted-foreground'"
                    :title="row.autoFailCount > 0
                      ? `${row.autoFailCount} automatiskt IG utan inlämning`
                      : undefined"
                  >
                    {{ row.failCount }} IG
                  </span>
                </div>
              </td>
              <td class="px-3 py-2.5 whitespace-nowrap">
                <div class="flex items-center gap-3">
                  <span v-if="row.needsWorkCount > 0" class="text-warning">{{ row.needsWorkCount }} kompl.</span>
                  <span v-if="row.overdueCount > 0" class="text-destructive">
                    {{ row.overdueCount }} försenat
                  </span>
                  <span
                    v-if="row.needsWorkCount === 0 && row.overdueCount === 0"
                    class="text-muted-foreground"
                  >–</span>
                </div>
              </td>
              <td class="px-3 py-2.5">
                <Badge :variant="statusVariant(row)">
                  {{ statusOf(row) }}
                </Badge>
              </td>
              <td class="px-4 py-2.5 text-right">
                <Button
                  variant="ghost"
                  size="sm"
                  data-test="analytics-student-open"
                  @click="emit('open', row.studentId)"
                >
                  Visa
                </Button>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>
  </div>
</template>
