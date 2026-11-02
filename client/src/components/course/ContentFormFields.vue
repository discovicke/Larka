<script setup lang="ts">
import type {Component} from 'vue'
import {PhPlus, PhX} from '@phosphor-icons/vue'
import {computed} from 'vue'

import DateTimePicker from '@/components/DateTimePicker.vue'
import MarkdownEditor from '@/components/editor/MarkdownEditor.vue'
import {Button} from '@/components/ui/button'
import {Checkbox} from '@/components/ui/checkbox'
import {
  Field,
  FieldContent,
  FieldDescription,
  FieldError,
  FieldLabel,
  FieldLegend,
  FieldSet,
  FieldTitle,
} from '@/components/ui/field'
import {Input} from '@/components/ui/input'
import {NativeSelect, NativeSelectOption} from '@/components/ui/native-select'
import {RadioGroup, RadioGroupItem} from '@/components/ui/radio-group'
import type {Schema} from '@/lib/api'
import {partTypes} from '@/lib/content'
import {type ContentForm, type FormErrors, isAssignment, isGraded, maxResubmitDueAt} from '@/lib/createContent'

// Delens fält som inte rör GitHub: typ, grundinfo och sättet att lämna in. Formuläret, felen och
// listan med inlämningssätt ägs av vyn runtom.

defineProps<{
  isEditing: boolean
  errors: FormErrors
  submissionModes: {value: Schema<'SubmissionMode'>, label: string, description: string, icon: Component}[]
}>()

const form = defineModel<ContentForm>('form', {required: true})

const gradeScales: {value: Schema<'GradeScale'>, label: string}[] = [
  {value: 'PassFail', label: 'Icke godkänd / Godkänd'},
  {value: 'PassFailDistinction', label: 'Icke godkänd / Godkänd / Väl godkänd'},
]

// Valt kort: mörk ram som i mockupen, i stället för shadcns tonade bakgrund.
const choiceCard = 'has-data-checked:border-primary has-data-checked:bg-transparent has-data-checked:ring-1 has-data-checked:ring-primary'

const currentPartType = computed(() =>
  partTypes.find((candidate) => candidate.value === form.value.partType) ?? partTypes[0])

function toggleMode(mode: Schema<'SubmissionMode'>, checked: boolean) {
  form.value.submissionModes = checked
    ? [...form.value.submissionModes, mode]
    : form.value.submissionModes.filter((selected) => selected !== mode)
}
</script>

<template>
  <FieldSet>
    <FieldLegend>Typ av del</FieldLegend>
    <div
      v-if="isEditing"
      class="bg-muted/40 flex items-center gap-3 rounded-[11px] border p-3"
      data-test="fixed-part-type"
    >
      <component :is="currentPartType.icon" class="size-5"/>
      <div class="flex flex-col">
        <span class="font-medium">{{ currentPartType.label }}</span>
        <span class="text-muted-foreground text-xs">
          Typen kan inte ändras efter att delen skapats.
        </span>
      </div>
    </div>
    <RadioGroup
      v-else
      v-model="form.partType"
      class="grid grid-cols-[repeat(auto-fit,minmax(min(150px,100%),1fr))] gap-2.5"
    >
      <FieldLabel
        v-for="partType in partTypes"
        :key="partType.value"
        :for="`part-${partType.value}`"
        :class="choiceCard"
      >
        <Field orientation="horizontal">
          <FieldContent>
            <component :is="partType.icon" class="size-5"/>
            <FieldTitle>{{ partType.label }}</FieldTitle>
            <FieldDescription class="text-xs">{{ partType.description }}</FieldDescription>
          </FieldContent>
          <RadioGroupItem :id="`part-${partType.value}`" :value="partType.value"/>
        </Field>
      </FieldLabel>
    </RadioGroup>
  </FieldSet>

  <section class="bg-card flex flex-col gap-4 rounded-[11px] border p-5">
    <h2 class="text-base">Grundinfo</h2>
    <Field :data-invalid="!!errors.title">
      <FieldLabel for="title">Titel</FieldLabel>
      <Input id="title" v-model="form.title" :aria-invalid="!!errors.title"/>
      <FieldError v-if="errors.title">{{ errors.title }}</FieldError>
    </Field>
    <Field>
      <FieldLabel for="description">Beskrivning</FieldLabel>
      <MarkdownEditor id="description" v-model="form.bodyMd" label="Beskrivning"/>
    </Field>
    <div class="grid grid-cols-[repeat(auto-fit,minmax(min(200px,100%),1fr))] gap-4">
      <Field>
        <FieldLabel for="publish-at">Publiceras</FieldLabel>
        <DateTimePicker id="publish-at" v-model="form.publishAt" clearable/>
        <FieldDescription class="text-xs">Tomt = direkt.</FieldDescription>
      </Field>
      <Field v-if="isAssignment(form)" :data-invalid="!!errors.dueAt">
        <FieldLabel for="due-at">Deadline</FieldLabel>
        <DateTimePicker
          id="due-at"
          v-model="form.dueAt"
          clearable
          default-time="23:59"
          :invalid="!!errors.dueAt"
        />
        <FieldError v-if="errors.dueAt">{{ errors.dueAt }}</FieldError>
      </Field>
      <Field v-if="isAssignment(form)" :data-invalid="!!errors.resubmitDueAt" data-test="resubmit-field">
        <FieldLabel>Kompletteringsdeadlines</FieldLabel>
        <div v-for="(_, index) in form.resubmitDueAt" :key="index" class="flex items-center gap-2">
          <DateTimePicker
            :id="`resubmit-due-at-${index}`"
            v-model="form.resubmitDueAt[index]"
            :min="form.dueAt"
            :invalid="!!errors.resubmitDueAt"
          />
          <Button
            type="button"
            variant="ghost"
            size="icon"
            aria-label="Ta bort kompletteringsdeadline"
            data-test="resubmit-remove"
            @click="form.resubmitDueAt.splice(index, 1)"
          >
            <PhX/>
          </Button>
        </div>
        <div>
          <Button
            type="button"
            variant="outline"
            size="sm"
            :disabled="!form.dueAt || form.resubmitDueAt.length >= maxResubmitDueAt"
            data-test="resubmit-add"
            @click="form.resubmitDueAt.push('')"
          >
            <PhPlus/>
            Lägg till kompletteringsdeadline
          </Button>
        </div>
        <FieldDescription class="text-xs">
          Extra tillfällen att lämna in igen efter sista dagen. Kräver en sista dag.
        </FieldDescription>
        <FieldError v-if="errors.resubmitDueAt">{{ errors.resubmitDueAt }}</FieldError>
      </Field>
      <Field v-if="isGraded(form)">
        <FieldLabel for="grade-scale">Betygsskala</FieldLabel>
        <NativeSelect id="grade-scale" v-model="form.gradeScale" class="w-full">
          <NativeSelectOption v-for="scale in gradeScales" :key="scale.value" :value="scale.value">
            {{ scale.label }}
          </NativeSelectOption>
        </NativeSelect>
      </Field>
    </div>
  </section>

  <section v-if="isAssignment(form)" class="bg-card flex flex-col gap-3.5 rounded-[11px] border p-5">
    <div class="flex flex-col">
      <h2 class="text-base">Hur lämnar eleven in?</h2>
      <span class="text-muted-foreground text-[13px]">Välj ett eller flera sätt, eller inget</span>
    </div>
    <div class="grid grid-cols-[repeat(auto-fit,minmax(min(190px,100%),1fr))] gap-2.5">
      <FieldLabel
        v-for="mode in submissionModes"
        :key="mode.value"
        :for="`mode-${mode.value}`"
        :class="choiceCard"
      >
        <Field orientation="horizontal">
          <Checkbox
            :id="`mode-${mode.value}`"
            :model-value="form.submissionModes.includes(mode.value)"
            @update:model-value="(checked) => toggleMode(mode.value, checked === true)"
          />
          <FieldContent>
            <FieldTitle>
              <component :is="mode.icon"/>
              {{ mode.label }}
            </FieldTitle>
            <FieldDescription class="text-xs">{{ mode.description }}</FieldDescription>
          </FieldContent>
        </Field>
      </FieldLabel>
    </div>
  </section>
</template>
