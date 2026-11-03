<script setup lang="ts">
import {computed, ref} from 'vue'

import DateTimePicker from '@/components/DateTimePicker.vue'
import {Alert, AlertDescription} from '@/components/ui/alert'
import {Button} from '@/components/ui/button'
import {useAsync} from '@/composables/useAsync'
import type {Schema} from '@/lib/api'
import {formatDate} from '@/lib/content'
import {earliestPersonalDate, isoOrNull} from '@/lib/form'
import {setPersonalDueAt} from '@/lib/submissionsApi'

// Läraren ger en enskild elev en extra deadline. Den räknas med bland uppgiftens egna deadlines (GradingWindow i
// backend). Visas bara när uppgiften har en deadline och eleven kan lämna in igen: inte när inlämningen
// är bedömd, och inte när den väntar på bedömning (då begär läraren komplettering i stället).
const props = defineProps<{
  linkId: string
  studentId: string
  /** Elevens nuvarande personliga deadline. */
  personalDueAt: string | null
  /** Klassens sista deadline från inlämningens läge. Utan deadline finns ingen deadlinelogik, så åtgärden döljs. */
  lastClassDeadline: string | null
  status: Schema<'SubmissionStatus'>
}>()

const emit = defineEmits<{saved: []}>()

const picked = ref('')
const available = computed(() => Boolean(props.lastClassDeadline)
  && (props.status === 'NotStarted' || props.status === 'NeedsWork'))
// Datumet gör bara skillnad efter klassens sista deadline, så tidigare dagar går inte att välja.
const minDate = computed(() => earliestPersonalDate(props.lastClassDeadline))

const {run: save, loading: saving, error} = useAsync(async (value: string | null) => {
  await setPersonalDueAt(props.linkId, props.studentId, value)
  picked.value = ''
  emit('saved')
})
</script>

<template>
  <section v-if="available" class="flex flex-col gap-2" data-test="personal-due-at">
    <h2 class="text-sm font-semibold">Ny deadline för eleven</h2>
    <p v-if="personalDueAt" class="text-[13px]" data-test="personal-due-at-current">
      Eleven har deadline {{ formatDate(personalDueAt, true) }}.
    </p>
    <DateTimePicker
      v-model="picked"
      class="bg-background"
      :min="minDate"
      default-time="23:59"
      data-test="personal-due-at-picker"
    />
    <div class="flex flex-wrap gap-2">
      <Button
        type="button"
        size="sm"
        :disabled="saving || !picked"
        data-test="personal-due-at-save"
        @click="save(isoOrNull(picked))"
      >
        Spara deadline
      </Button>
      <Button
        v-if="personalDueAt"
        type="button"
        variant="outline"
        size="sm"
        :disabled="saving"
        data-test="personal-due-at-remove"
        @click="save(null)"
      >
        Ta bort
      </Button>
    </div>
    <p v-if="!personalDueAt" class="text-muted-foreground text-[13px]">
      Eleven får en egen extra deadline och ett meddelande om den.
      <template v-if="lastClassDeadline">Den ska ligga efter uppgiftens sista deadline, {{ formatDate(lastClassDeadline, true) }}.</template>
    </p>
    <Alert v-if="error" variant="destructive" data-test="personal-due-at-error">
      <AlertDescription>{{ error }}</AlertDescription>
    </Alert>
  </section>
</template>
