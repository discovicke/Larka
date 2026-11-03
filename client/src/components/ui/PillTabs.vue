<script setup lang="ts" generic="T extends string">
defineProps<{
  items: readonly {value: T, label: string, dataTest?: string}[]
  ariaLabel?: string
}>()

const model = defineModel<T>({required: true})
</script>

<template>
  <div role="tablist" :aria-label="ariaLabel" class="bg-muted flex flex-wrap rounded-[7px] p-[3px]">
    <button
      v-for="item in items"
      :key="item.value"
      type="button"
      role="tab"
      :aria-selected="model === item.value"
      :data-test="item.dataTest"
      class="rounded-[5px] px-2.5 py-1 text-[13px]"
      :class="model === item.value
        ? 'bg-background font-medium shadow-sm'
        : 'text-muted-foreground'"
      @click="model = item.value"
    >
      {{ item.label }}
    </button>
  </div>
</template>
