<script setup lang="ts">
import {PhArrowClockwise} from '@phosphor-icons/vue'
import {computed, onMounted} from 'vue'

import {Button} from '@/components/ui/button'
import {NativeSelect, NativeSelectOption} from '@/components/ui/native-select'
import {type OptionSource, useDevOptions} from '@/composables/useDevOptions'

import DevField from './DevField.vue'

const props = defineProps<{
  id: string
  label: string
  source: OptionSource
  /** Visar ett "ingen"-val, för fält som får vara tomma. */
  optional?: boolean
}>()

const model = defineModel<string>({default: ''})

const {options, loading, refresh} = useDevOptions()

const items = computed(() => options[props.source])

onMounted(() => refresh(props.source))
const placeholder = computed(() => {
  if (props.optional) {
    return '(none)'
  }
  return items.value.length === 0
    ? 'Nothing to pick yet, try refresh'
    : 'Pick one'
})
</script>

<template>
  <DevField :id="id" :label="label">
    <div class="flex gap-2">
      <NativeSelect :id="id" v-model="model" class="w-full">
        <NativeSelectOption value="">{{ placeholder }}</NativeSelectOption>
        <NativeSelectOption v-for="item in items" :key="item.value" :value="item.value">
          {{ item.label }}
        </NativeSelectOption>
      </NativeSelect>
      <Button
        type="button"
        variant="outline"
        size="icon"
        :disabled="loading[source]"
        :aria-label="`Refresh ${label.toLowerCase()}`"
        @click="refresh(source)"
      >
        <PhArrowClockwise/>
      </Button>
    </div>
  </DevField>
</template>
