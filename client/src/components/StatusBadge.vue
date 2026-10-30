<script setup lang="ts">
import {computed} from 'vue'

import {Badge} from '@/components/ui/badge'
import {statuses, type StatusKey, statusVariant} from '@/lib/status'

// label ersätter texten när lägets text behöver en detalj, t.ex. "Schemalagd 3 nov".
const props = withDefaults(defineProps<{
  status: StatusKey
  label?: string
  icon?: boolean
}>(), {label: undefined, icon: true})

const definition = computed(() => statuses[props.status])
const variant = computed(() => statusVariant(props.status))
</script>

<template>
  <Badge
    :variant="variant"
    :data-status="status"
    :data-tone="definition.tone"
    data-test="status-badge"
  >
    <component :is="definition.icon" v-if="icon && definition.icon" aria-hidden="true"/>
    {{ label ?? definition.label }}
  </Badge>
</template>
