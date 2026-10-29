<script setup lang="ts">
import {PhPushPin} from '@phosphor-icons/vue'
import type {Component} from 'vue'

import type {InboxRow} from '@/lib/inbox'

// Innehållet i en rad i inkorgen: ikon, rubrik, målet och tiden, och pricken för oläst.
defineProps<{
  row: InboxRow
  meta: string
  icon: Component
  iconClass: string
}>()
</script>

<template>
  <span class="flex size-8 shrink-0 items-center justify-center rounded-[7px]" :class="iconClass">
    <component :is="icon" class="size-4"/>
  </span>
  <span class="flex min-w-0 flex-1 flex-col">
    <span
      :class="row.unread
        ? 'font-semibold'
        : 'font-medium'"
    >{{ row.title }}</span>
    <span class="text-muted-foreground text-[13px]">
      <template v-if="row.pinned">
        <PhPushPin class="mr-1 inline size-3"/>Fäst ·
      </template>
      {{ meta }}
    </span>
  </span>
  <span
    v-if="row.unread"
    class="bg-info mt-1.5 size-2 shrink-0 rounded-full"
    aria-label="Oläst"
  />
</template>
