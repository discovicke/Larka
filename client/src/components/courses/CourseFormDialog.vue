<script setup lang="ts">
import {computed, reactive, ref, watch} from 'vue'

import DatePicker from '@/components/DatePicker.vue'
import {Alert, AlertDescription} from '@/components/ui/alert'
import {Button} from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import {Field, FieldDescription, FieldError, FieldGroup, FieldLabel} from '@/components/ui/field'
import {Input} from '@/components/ui/input'
import {api, errorMessage, unwrap, type Schema} from '@/lib/api'

// Utan course skapas en ny kurs i skolan, med course ändras den. Koden är låst efter att kursen skapats.
const props = defineProps<{
  schoolId: string
  course?: Schema<'CourseDto'>
}>()

const open = defineModel<boolean>('open', {required: true})
const emit = defineEmits<{saved: [course: Schema<'CourseDto'>]}>()

const saving = ref(false)
const error = ref('')
const submitted = ref(false)
const form = reactive({title: '', code: '', points: '', startsOn: '', endsOn: ''})

const isEdit = computed(() => props.course !== undefined)
const titleError = computed(() => submitted.value && !form.title.trim()
  ? 'Ange en titel.'
  : '')
const codeError = computed(() => submitted.value && !isEdit.value && !form.code.trim()
  ? 'Ange en kurskod.'
  : '')
const pointsError = computed(() => {
  if (form.points === '') {
    return ''
  }
  const points = Number(form.points)
  return Number.isInteger(points) && points >= 1 && points <= 400
    ? ''
    : 'Omfattningen ska vara mellan 1 och 400 poäng.'
})

watch(open, (isOpen) => {
  if (isOpen) {
    form.title = props.course?.title ?? ''
    form.code = props.course?.code ?? ''
    form.points = props.course?.points == null
      ? ''
      : String(props.course.points)
    form.startsOn = props.course?.startsOn ?? ''
    form.endsOn = props.course?.endsOn ?? ''
    error.value = ''
    submitted.value = false
  }
}, {immediate: true})

async function save() {
  submitted.value = true
  const title = form.title.trim()
  const code = form.code.trim()
  if (!title || (!isEdit.value && !code) || pointsError.value) {
    return
  }

  saving.value = true
  error.value = ''
  const points = form.points === ''
    ? null
    : Number(form.points)
  const startsOn = form.startsOn || null
  const endsOn = form.endsOn || null
  try {
    const saved = props.course
      // Beskrivningen skrivs på kursens egen sida, så den följer med oförändrad.
      ? unwrap(await api.PUT('/api/courses/{id}', {
          params: {path: {id: props.course.id}},
          body: {title, description: props.course.description, points, startsOn, endsOn},
        }))
      : unwrap(await api.POST('/api/courses', {
          body: {schoolId: props.schoolId, title, code, points, startsOn, endsOn},
        }))
    emit('saved', saved)
    open.value = false
  } catch (exception) {
    error.value = errorMessage(exception)
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <Dialog v-model:open="open">
    <DialogContent class="sm:max-w-lg">
      <form class="flex flex-col gap-4" novalidate @submit.prevent="save">
        <DialogHeader>
          <DialogTitle>
            {{ isEdit
              ? 'Redigera kurs'
              : 'Ny kurs' }}
          </DialogTitle>
          <DialogDescription>
            {{ isEdit
              ? 'Ändra titel, omfattning eller datum. Beskrivningen ändras på kursens egen sida.'
              : 'Skapa en kurs i skolan. Grupper kopplas till kursen från gruppens sida.' }}
          </DialogDescription>
        </DialogHeader>

        <FieldGroup>
          <Field
            :data-invalid="titleError
              ? true
              : undefined"
          >
            <FieldLabel for="course-title">Titel</FieldLabel>
            <Input
              id="course-title"
              v-model="form.title"
              placeholder="DevOps"
              :aria-invalid="titleError
                ? true
                : undefined"
              autocomplete="off"
              data-test="course-title"
            />
            <FieldError v-if="titleError" data-test="course-title-error">{{ titleError }}</FieldError>
          </Field>
          <Field
            :data-invalid="codeError
              ? true
              : undefined"
          >
            <FieldLabel for="course-code">Kurskod</FieldLabel>
            <Input
              id="course-code"
              v-model="form.code"
              class="font-mono"
              placeholder="DEVOPS25"
              :disabled="isEdit"
              :aria-invalid="codeError
                ? true
                : undefined"
              autocomplete="off"
              data-test="course-code"
            />
            <FieldDescription v-if="isEdit">Koden går inte att ändra.</FieldDescription>
            <FieldError v-if="codeError" data-test="course-code-error">{{ codeError }}</FieldError>
          </Field>
          <Field
            :data-invalid="pointsError
              ? true
              : undefined"
          >
            <FieldLabel for="course-points">Omfattning (p)</FieldLabel>
            <Input
              id="course-points"
              v-model="form.points"
              type="number"
              min="1"
              max="400"
              placeholder="25"
              :aria-invalid="pointsError
                ? true
                : undefined"
              data-test="course-points"
            />
            <FieldError v-if="pointsError" data-test="course-points-error">{{ pointsError }}</FieldError>
          </Field>
          <div class="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <Field>
              <FieldLabel for="course-start">Startdatum</FieldLabel>
              <DatePicker
                id="course-start"
                v-model="form.startsOn"
                clearable
                :max="form.endsOn || undefined"
              />
            </Field>
            <Field>
              <FieldLabel for="course-end">Slutdatum</FieldLabel>
              <DatePicker
                id="course-end"
                v-model="form.endsOn"
                clearable
                :min="form.startsOn || undefined"
              />
            </Field>
          </div>
        </FieldGroup>

        <Alert v-if="error" variant="destructive" data-test="course-form-error">
          <AlertDescription>{{ error }}</AlertDescription>
        </Alert>

        <DialogFooter>
          <Button
            type="button"
            variant="outline"
            :disabled="saving"
            @click="open = false"
          >
            Avbryt
          </Button>
          <Button type="submit" :disabled="saving" data-test="save-course">
            {{ isEdit
              ? 'Spara'
              : 'Skapa kurs' }}
          </Button>
        </DialogFooter>
      </form>
    </DialogContent>
  </Dialog>
</template>
