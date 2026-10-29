<script setup lang="ts">
import {computed} from 'vue'

import {Badge} from '@/components/ui/badge'
import {type CourseStatus, courseStatusLabels} from '@/lib/courseStatus'

const props = defineProps<{status: CourseStatus | null}>()

// Pågår är en fylld bricka, kommande en kontur och avslutad den gröna.
const variant = computed(() => ({
  running: 'default',
  upcoming: 'outline',
  finished: 'success',
}[props.status ?? 'running'] as 'default' | 'outline' | 'success'))
</script>

<template>
  <Badge v-if="status" :variant="variant" data-test="course-status">
    {{ courseStatusLabels[status] }}
  </Badge>
</template>
