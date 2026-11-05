<script setup lang="ts">
import {VisDonut, VisSingleContainer} from '@unovis/vue'
import {computed} from 'vue'

import {ChartContainer, ChartLegendContent, type ChartConfig} from '@/components/ui/chart'
import {gradeDistribution, type AnalyticsSummary, type ChartDatum} from '@/lib/analytics'

const props = defineProps<{summary: AnalyticsSummary}>()

const data = computed(() => gradeDistribution(props.summary))
const config = computed<ChartConfig>(() => Object.fromEntries(data.value.map((item) => [
  item.key,
  {label: item.label, color: item.color},
])))
const total = computed(() => data.value.reduce((sum, item) => sum + item.count, 0))
</script>

<template>
  <ChartContainer v-if="total > 0" :config="config" class="mx-auto h-[260px] w-[260px] max-w-full">
    <VisSingleContainer :data="data" :height="260">
      <VisDonut
        :value="(item: ChartDatum) => item.count"
        :color="(item: ChartDatum) => item.color"
        :arc-width="30"
        :pad-angle="0.02"
        :central-label="String(summary.gradedCount)"
        central-sub-label="bedömda"
      />
    </VisSingleContainer>
    <ChartLegendContent name-key="label"/>
  </ChartContainer>
  <p v-else class="text-muted-foreground py-16 text-center text-sm">
    Inga uppgifter att bedöma än.
  </p>
</template>
