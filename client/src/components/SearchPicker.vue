<script setup lang="ts">
import {computed, watch} from 'vue'

import SearchField from '@/components/ui/SearchField.vue'

// Sök och välj en sak: träffarna står som en lista under sökfältet, och man klickar på en rad. Listan visar alltid
// vad sökningen ger, så det syns att fältet söker. Raderna är radioknappar, så piltangenterna flyttar valet.
export interface PickerItem {
  id: string
  title: string
  detail?: string
}

const props = withDefaults(defineProps<{
  id: string
  label: string
  placeholder: string
  items: PickerItem[]
  // Räknaren: singular och plural, till exempel ['kurs', 'kurser'] ger "1 kurs" och "3 av 24 kurser".
  nouns: [string, string]
  // Visas när det inte finns något alls att välja.
  emptyText: string
  loading?: boolean
  dataTest: string
}>(), {loading: false})

const selected = defineModel<string>({required: true})
const search = defineModel<string>('search', {required: true})

const matches = computed(() => {
  const query = search.value.trim().toLowerCase()
  return query === ''
    ? props.items
    : props.items.filter((item) =>
        item.title.toLowerCase().includes(query) || (item.detail ?? '').toLowerCase().includes(query))
})

const count = computed(() => {
  const total = props.items.length
  const noun = props.nouns[total === 1
    ? 0
    : 1]
  return search.value.trim() === ''
    ? `${total} ${noun}`
    : `${matches.value.length} av ${total} ${noun}`
})

// Ett val som sökningen döljer ska inte skickas.
watch(matches, (list) => {
  if (selected.value !== '' && !list.some((item) => item.id === selected.value)) {
    selected.value = ''
  }
})
</script>

<template>
  <div class="flex min-w-0 flex-col gap-1.5" :data-test="dataTest">
    <span class="text-sm font-medium" aria-hidden="true">{{ label }}</span>
    <SearchField
      :id="`${id}-search`"
      v-model="search"
      :label="label"
      :placeholder="placeholder"
      :data-test="`${dataTest}-search`"
    />
    <div
      role="radiogroup"
      :aria-label="label"
      class="max-h-56 overflow-y-auto rounded-lg border"
      :data-test="`${dataTest}-options`"
    >
      <p v-if="loading" class="text-muted-foreground px-4 py-2.5 text-sm">Laddar…</p>
      <p v-else-if="items.length === 0" class="text-muted-foreground px-4 py-2.5 text-sm" :data-test="`${dataTest}-empty`">
        {{ emptyText }}
      </p>
      <p v-else-if="matches.length === 0" class="text-muted-foreground px-4 py-2.5 text-sm" :data-test="`${dataTest}-no-match`">
        Inget matchar sökningen.
      </p>
      <label
        v-for="item in matches"
        :key="item.id"
        class="hover:bg-accent has-[:checked]:bg-accent flex min-h-11 cursor-pointer items-center gap-3 border-b px-4 py-1.5 last:border-b-0"
        :data-test="`${dataTest}-option`"
      >
        <input
          v-model="selected"
          type="radio"
          :name="id"
          :value="item.id"
          class="accent-primary size-4 shrink-0"
          :data-test="`${dataTest}-radio`"
        />
        <span class="flex min-w-0 flex-col">
          <span class="font-medium break-words">{{ item.title }}</span>
          <span v-if="item.detail" class="text-muted-foreground text-[13px] break-words">{{ item.detail }}</span>
        </span>
      </label>
    </div>
    <p v-if="!loading && items.length > 0" class="text-muted-foreground text-[13px]" :data-test="`${dataTest}-count`">
      {{ count }}
    </p>
  </div>
</template>
