<script setup lang="ts">
import {PhCircleHalf, PhMoon, PhSun} from '@phosphor-icons/vue'
import type {Component} from 'vue'

import {Field, FieldContent, FieldDescription, FieldLabel, FieldLegend, FieldSet} from '@/components/ui/field'
import {RadioGroup, RadioGroupItem} from '@/components/ui/radio-group'
import {useTheme} from '@/composables/useTheme'
import type {ThemePreference} from '@/lib/theme'

const {preference, resolvedTheme} = useTheme()

const options: {
  value: ThemePreference
  label: string
  description: string
  icon: Component
}[] = [
  {
    value: 'auto',
    label: 'Automatiskt',
    description: 'Följer systemets inställning för ljust eller mörkt.',
    icon: PhCircleHalf,
  },
  {
    value: 'light',
    label: 'Ljust',
    description: 'Använd ljust tema oavsett systeminställning.',
    icon: PhSun,
  },
  {
    value: 'dark',
    label: 'Mörkt',
    description: 'Använd mörkt tema oavsett systeminställning.',
    icon: PhMoon,
  },
]
</script>

<template>
  <section id="utseende" class="bg-card scroll-mt-6 rounded-xl border p-5">
    <FieldSet>
      <FieldLegend>Utseende</FieldLegend>
      <FieldDescription>
        Välj om appen ska följa systemet eller alltid använda ljust eller mörkt tema.
      </FieldDescription>

      <RadioGroup v-model="preference" aria-label="Tema" class="gap-2">
        <FieldLabel
          v-for="option in options"
          :key="option.value"
          :for="`theme-${option.value}`"
          class="has-data-checked:border-primary has-data-checked:ring-primary hover:bg-muted/50 w-full cursor-pointer items-center gap-3 rounded-lg border p-3 font-normal transition-colors has-data-checked:ring-1"
        >
          <Field orientation="horizontal">
            <FieldContent class="gap-0.5">
              <span class="flex items-center gap-2">
                <component :is="option.icon" class="text-muted-foreground size-4 shrink-0"/>
                <span class="font-medium">{{ option.label }}</span>
              </span>
              <FieldDescription>{{ option.description }}</FieldDescription>
            </FieldContent>
            <RadioGroupItem
              :id="`theme-${option.value}`"
              :value="option.value"
              :data-test="`theme-${option.value}`"
            />
          </Field>
        </FieldLabel>
      </RadioGroup>
    </FieldSet>

    <p class="text-muted-foreground mt-4 text-sm">
      Just nu används
      <strong class="text-foreground" data-test="resolved-theme">
        {{ resolvedTheme === 'dark'
          ? 'mörkt tema'
          : 'ljust tema' }}
      </strong>.
    </p>
  </section>
</template>
