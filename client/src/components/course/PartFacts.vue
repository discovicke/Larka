<script setup lang="ts">
// Delens fakta i panelen bredvid innehållet: etikett till vänster, värde till höger. Rubriken under titeln
// visar bara deadlinen, så betygsskala, inlämningssätt och extra datum står här. Tomma värden hoppas över.

export interface PartFact {
  key: string
  label: string
  value: string
  /** Hela texten när värdet är förkortat, t.ex. betygsskalan. */
  title?: string
}

const props = defineProps<{facts: PartFact[]}>()
</script>

<template>
  <dl v-if="props.facts.some((fact) => fact.value)" class="flex flex-col gap-2 text-sm" data-test="part-facts">
    <template v-for="fact in props.facts" :key="fact.key">
      <div v-if="fact.value" class="flex items-baseline justify-between gap-3" :data-test="`fact-${fact.key}`">
        <dt class="text-muted-foreground shrink-0">{{ fact.label }}</dt>
        <dd class="text-right" :title="fact.title">{{ fact.value }}</dd>
      </div>
    </template>
  </dl>
</template>
