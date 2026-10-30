<script setup lang="ts">
import StatusBadge from '@/components/StatusBadge.vue'
import {statuses, statusFamilies, type StatusTone, toneMeanings} from '@/lib/status'

const tones = Object.keys(toneMeanings) as StatusTone[]
const families = statusFamilies()
</script>

<template>
  <main class="bg-background text-foreground mx-auto flex max-w-3xl flex-col gap-8 p-6" data-test="status-sheet">
    <header class="flex flex-col gap-2">
      <h1 class="text-2xl font-semibold">Statusblad</h1>
      <p class="text-muted-foreground text-sm">
        Alla lägen i appen i verklig storlek, grupperade per familj. Färgen följer tonen och ingenting annat.
        Betygsstämplarna IG, G och VG ingår inte.
      </p>
    </header>

    <section class="flex flex-col gap-3" aria-labelledby="tones-h">
      <h2 id="tones-h" class="text-lg font-semibold">Toner</h2>
      <ul class="flex flex-col gap-2">
        <li
          v-for="tone in tones"
          :key="tone"
          class="flex items-center gap-3 text-sm"
          :data-test="`tone-${tone}`"
        >
          <span class="w-28 shrink-0 font-mono text-xs">{{ tone }}</span>
          <span class="text-muted-foreground">{{ toneMeanings[tone] }}</span>
        </li>
      </ul>
    </section>

    <section
      v-for="group in families"
      :key="group.family"
      class="flex flex-col gap-3"
      :aria-labelledby="`family-${group.family}`"
      :data-test="`family-${group.family}`"
    >
      <h2 :id="`family-${group.family}`" class="text-lg font-semibold">{{ group.title }}</h2>
      <ul class="divide-y rounded-xl border">
        <li
          v-for="key in group.keys"
          :key="key"
          class="flex items-center gap-3 px-3 py-3 sm:gap-4 sm:px-4"
        >
          <span class="w-32 shrink-0"><StatusBadge :status="key"/></span>
          <span class="text-muted-foreground min-w-0 flex-1 text-xs">
            <span class="font-mono text-[11px] sm:text-xs">{{ key }}</span>
            <span class="block">{{ statuses[key].tone }}</span>
          </span>
        </li>
      </ul>
    </section>
  </main>
</template>
