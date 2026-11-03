<script setup lang="ts">
import {VisAxis, VisStackedBar, VisXYContainer} from '@unovis/vue'
import {computed} from 'vue'

import {ChartContainer, ChartLegendContent, type ChartConfig} from '@/components/ui/chart'
import {chartColors, courseGradeData, type AnalyticsCourse} from '@/lib/analytics'

const props = defineProps<{courses: AnalyticsCourse[]}>()

type CourseDatum = ReturnType<typeof courseGradeData>[number]

const data = computed(() => courseGradeData(props.courses))
const config = {
  fail: {label: 'IG', color: chartColors.fail},
  pass: {label: 'G', color: chartColors.pass},
  distinction: {label: 'VG', color: chartColors.distinction},
} satisfies ChartConfig

function xValue(_item: CourseDatum, index: number): number {
  return index
}

function xLabel(value: number): string {
  return data.value[Math.round(value)]?.code ?? ''
}
</script>

<template>
  <ChartContainer :config="config" class="aspect-auto h-[260px] w-full">
    <VisXYContainer :data="data" :height="260">
      <VisStackedBar
        :x="xValue"
        :y="[
          (item: CourseDatum) => item.fail,
          (item: CourseDatum) => item.pass,
          (item: CourseDatum) => item.distinction,
        ]"
        :color="[chartColors.fail, chartColors.pass, chartColors.distinction]"
        :rounded-corners="3"
        bar-padding="0.2"
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
    <ChartLegendContent/>
  </ChartContainer>
</template>
