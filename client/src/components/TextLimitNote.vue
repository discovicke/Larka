<script setup lang="ts">
import {computed} from 'vue'

import {textLimitNote} from '@/lib/textLimits'

// Räknaren under ett textfält. Tom långt under gränsen, ett fel när texten är för lång.
const props = defineProps<{text: string | null | undefined, max: number}>()

const note = computed(() => textLimitNote(props.text, props.max))
</script>

<template>
  <p
    v-if="note.message"
    class="text-xs"
    :class="note.tooLong
      ? 'text-destructive'
      : 'text-muted-foreground'"
    :role="note.tooLong
      ? 'alert'
      : undefined"
    data-test="text-limit"
  >
    {{ note.message }}
  </p>
</template>
