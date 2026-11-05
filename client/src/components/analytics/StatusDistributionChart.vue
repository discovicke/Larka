<script setup lang="ts">
import {VisAxis, VisGroupedBar, VisXYContainer} from '@unovis/vue'
import {computed} from 'vue'

import {ChartContainer, type ChartConfig} from '@/components/ui/chart'
import {statusDistribution, type AnalyticsSummary, type ChartDatum} from '@/lib/analytics'

const props = defineProps<{summary: AnalyticsSummary}>()

const data = computed(() => statusDistribution(props.summary))
const config = computed<ChartConfig>(() => Object.fromEntries(data.value.map((item) => [
  item.key,
  {label: item.label, color: item.color},
])))

function xValue(_item: ChartDatum, index: number): number {
  return index
}

function xLabel(value: number): string {
  return data.value[Math.round(value)]?.label ?? ''
}

// Varje stapel får sin egen statusfärg i stället för en gemensam blå.
function barColor(item: ChartDatum): string {
  return item.color
}
</script>

<template>
  <div class="flex flex-col gap-2">
    <ChartContainer :config="config" class="aspect-auto h-[210px] w-full">
      <VisXYContainer :data="data" :height="210">
        <VisGroupedBar
          :x="xValue"
          :y="[(item: ChartDatum) => item.count]"
          :color="barColor"
          :rounded-corners="3"
          :bar-min-height="2"
          bar-padding="0.18"
        />
        <VisAxis
          type="x"
          :x="xValue"
          :tick-values="data.map((_, index) => index)"
          :tick-format="xLabel"
          :tick-line="false"
          :domain-line="false"
          :grid-line="false"
        />
        <VisAxis
          type="y"
          :tick-line="false"
          :domain-line="false"
          :grid-line="true"
        />
      </VisXYContainer>
    </ChartContainer>
    <div class="text-muted-foreground flex flex-wrap justify-center gap-x-3 gap-y-1 text-[11px]" data-test="status-legend">
      <span v-for="item in data" :key="item.key" class="inline-flex items-center gap-1">
        <span class="size-2 rounded-xs" :style="{backgroundColor: item.color}"/>
        {{ item.label }}: {{ item.count }} st
      </span>
    </div>
  </div>
</template>
