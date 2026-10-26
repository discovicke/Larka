<script setup lang="ts">
import {NativeSelect, NativeSelectOption} from '@/components/ui/native-select'

import DevField from './DevField.vue'

defineProps<{
  id: string
  label: string
  values: readonly string[]
  /** Ger ett tomt val först, t.ex. "No grade". Utan den måste ett värde väljas. */
  emptyLabel?: string
}>()

const model = defineModel<string>({required: true})
</script>

<template>
  <DevField :id="id" :label="label">
    <NativeSelect :id="id" v-model="model" class="w-full">
      <NativeSelectOption v-if="emptyLabel !== undefined" value="">
        {{ emptyLabel }}
      </NativeSelectOption>
      <NativeSelectOption v-for="value in values" :key="value" :value="value">
        {{ value }}
      </NativeSelectOption>
    </NativeSelect>
  </DevField>
</template>
