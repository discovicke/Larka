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
import {useAsync} from '@/composables/useAsync'
import {type Schema} from '@/lib/api'
import {updateCourse} from '@/lib/coursesApi'

// Dialogen ändrar en befintlig kurs. Koden är låst efter att kursen skapats.
const props = defineProps<{
  course: Schema<'CourseDto'>
}>()

const open = defineModel<boolean>('open', {required: true})
const emit = defineEmits<{saved: [course: Schema<'CourseDto'>]}>()

const submitted = ref(false)
const form = reactive({title: '', code: '', points: '', startsOn: '', endsOn: ''})

const titleError = computed(() => submitted.value && !form.title.trim()
  ? 'Ange en titel.'
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

interface CourseFields {
  title: string
  points: number | null
  startsOn: string | null
  endsOn: string | null
}

const {run: send, loading: saving, error} = useAsync(async ({title, points, startsOn, endsOn}: CourseFields) => {
  // Beskrivningen skrivs på kursens egen sida, så den följer med oförändrad.
  const saved = await updateCourse(props.course.id, {title, description: props.course.description, points, startsOn, endsOn})
  emit('saved', saved)
  open.value = false
})

watch(open, (isOpen) => {
  if (isOpen) {
    form.title = props.course.title
    form.code = props.course.code
    form.points = props.course.points == null
      ? ''
      : String(props.course.points)
    form.startsOn = props.course.startsOn ?? ''
    form.endsOn = props.course.endsOn ?? ''
    error.value = ''
    submitted.value = false
  }
}, {immediate: true})

async function save() {
  submitted.value = true
  const title = form.title.trim()
  if (!title || pointsError.value) {
    return
  }

  const points = form.points === ''
    ? null
    : Number(form.points)
  await send({title, points, startsOn: form.startsOn || null, endsOn: form.endsOn || null})
}
</script>

<template>
  <Dialog v-model:open="open">
    <DialogContent class="sm:max-w-lg">
      <form class="flex flex-col gap-4" novalidate @submit.prevent="save">
        <DialogHeader>
          <DialogTitle>Redigera kurs</DialogTitle>
          <DialogDescription>
            Ändra titel, omfattning eller datum. Beskrivningen ändras på kursens egen sida.
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
          <Field>
            <FieldLabel for="course-code">Kurskod</FieldLabel>
            <Input
              id="course-code"
              v-model="form.code"
              class="font-mono"
              disabled
              autocomplete="off"
              data-test="course-code"
            />
            <FieldDescription>Koden går inte att ändra.</FieldDescription>
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
            Spara
          </Button>
        </DialogFooter>
      </form>
    </DialogContent>
  </Dialog>
</template>
