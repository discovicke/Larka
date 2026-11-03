<script setup lang="ts">
import {PhMagnifyingGlass} from '@phosphor-icons/vue'

import {Input} from '@/components/ui/input'

defineProps<{
  id: string
  label: string
  placeholder: string
  dataTest?: string
}>()

const model = defineModel<string>({required: true})
</script>

<template>
  <div class="relative w-full">
    <label :for="id" class="sr-only">{{ label }}</label>
    <PhMagnifyingGlass class="text-muted-foreground pointer-events-none absolute top-1/2 left-2.5 size-4 -translate-y-1/2"/>
    <Input
      :id="id"
      v-model="model"
      class="pl-8"
      :placeholder="placeholder"
      :data-test="dataTest"
    />
  </div>
</template>
