<script setup lang="ts">
import {computed, reactive, ref, watch} from 'vue'

import DatePicker from '@/components/DatePicker.vue'
import {Alert, AlertDescription} from '@/components/ui/alert'
import {Button} from '@/components/ui/button'
import {Checkbox} from '@/components/ui/checkbox'
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

const props = defineProps<{groupId: string, groupName: string, schoolId: string}>()
// created kommer när något har sparats, också om ett senare steg misslyckades, så att sidan visar det som finns.
const emit = defineEmits<{created: []}>()

const open = defineModel<boolean>('open', {required: true})

const teachers = ref<Schema<'UserDto'>[]>([])
const loading = ref(false)
const saving = ref(false)
const error = ref('')
const submitted = ref(false)
const form = reactive({title: '', code: '', startsOn: '', endsOn: ''})
const selected = ref<string[]>([])

// Stegen som redan är klara, så att ett nytt försök fortsätter där det tog slut i stället för att skapa kursen igen.
let course: Schema<'CourseDto'> | null = null
let linked = false
let assigned: string[] = []

const titleError = computed(() => submitted.value && !form.title.trim()
  ? 'Ange ett namn.'
  : '')
const codeError = computed(() => submitted.value && !form.code.trim()
  ? 'Ange en kurskod.'
  : '')
const datesError = computed(() => submitted.value && (!form.startsOn || !form.endsOn)
  ? 'Ange både start- och slutdatum.'
  : '')
const teacherError = computed(() => submitted.value && selected.value.length === 0
  ? 'Välj minst en lärare.'
  : '')

watch(open, async (isOpen) => {
  if (!isOpen) {
    return
  }

  Object.assign(form, {title: '', code: '', startsOn: '', endsOn: ''})
  selected.value = []
  error.value = ''
  submitted.value = false
  course = null
  linked = false
  assigned = []
  loading.value = true
  try {
    const users = unwrap(await api.GET('/api/users'))
    teachers.value = users
      .filter((user) => user.role !== 'Student')
      .sort((first, second) => first.name.localeCompare(second.name, 'sv'))
  } catch (exception) {
    error.value = errorMessage(exception)
  } finally {
    loading.value = false
  }
})

function toggle(userId: string, checked: boolean) {
  selected.value = checked
    ? [...selected.value, userId]
    : selected.value.filter((id) => id !== userId)
}

// Svaren från API:et är på engelska, så de vanligaste översätts.
function createError(exception: unknown): string {
  const message = errorMessage(exception)
  return message.includes('already has a course with this code')
    ? 'Skolan har redan en kurs med den koden.'
    : message
}

async function save() {
  submitted.value = true
  if (!form.title.trim() || !form.code.trim() || !form.startsOn || !form.endsOn || selected.value.length === 0) {
    return
  }

  saving.value = true
  error.value = ''
  let step = 'Kursen kunde inte skapas.'
  try {
    // Beskrivningen skriver läraren själv, så den följer inte med.
    course ??= unwrap(await api.POST('/api/courses', {
      body: {
        schoolId: props.schoolId,
        title: form.title.trim(),
        code: form.code.trim(),
        points: null,
        startsOn: form.startsOn,
        endsOn: form.endsOn,
      },
    }))
    const created = course

    step = `Kursen ${created.title} skapades men kunde inte kopplas till ${props.groupName}.`
    if (!linked) {
      await api.POST('/api/groups/{id}/courses/{courseId}', {
        params: {path: {id: props.groupId, courseId: created.id}},
      })
      linked = true
    }

    for (const userId of selected.value.filter((id) => !assigned.includes(id))) {
      const name = teachers.value.find((teacher) => teacher.id === userId)?.name ?? 'läraren'
      step = `Kursen ${created.title} skapades och kopplades till ${props.groupName}, men ${name} kunde inte läggas till som lärare.`
      await api.POST('/api/courses/{id}/teachers', {
        params: {path: {id: created.id}},
        body: {userId},
      })
      assigned.push(userId)
    }

    emit('created')
    open.value = false
  } catch (exception) {
    error.value = course
      ? `${step} ${errorMessage(exception)} Tryck på Skapa kurs igen för att fortsätta, eller lägg till läraren på kursraden.`
      : createError(exception)
    if (course) {
      emit('created')
    }
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <Dialog v-model:open="open">
    <DialogContent class="max-h-[calc(100dvh-2rem)] grid-cols-[minmax(0,1fr)] overflow-y-auto sm:max-w-lg">
      <form class="flex min-w-0 flex-col gap-4" novalidate @submit.prevent="save">
        <DialogHeader class="min-w-0">
          <DialogTitle class="break-words">Ny kurs i {{ groupName }}</DialogTitle>
          <DialogDescription>
            Kursen skapas, kopplas till klassen och får sina lärare. Läraren skriver beskrivningen.
          </DialogDescription>
        </DialogHeader>

        <FieldGroup>
          <Field
            :data-invalid="titleError
              ? true
              : undefined"
          >
            <FieldLabel for="new-course-title">Namn</FieldLabel>
            <Input
              id="new-course-title"
              v-model="form.title"
              placeholder="DevOps"
              autocomplete="off"
              :aria-invalid="titleError
                ? true
                : undefined"
              data-test="new-course-title"
            />
            <FieldError v-if="titleError" data-test="new-course-title-error">{{ titleError }}</FieldError>
          </Field>
          <Field
            :data-invalid="codeError
              ? true
              : undefined"
          >
            <FieldLabel for="new-course-code">Kurskod</FieldLabel>
            <Input
              id="new-course-code"
              v-model="form.code"
              class="font-mono"
              placeholder="DEVOPS25"
              autocomplete="off"
              :aria-invalid="codeError
                ? true
                : undefined"
              data-test="new-course-code"
            />
            <FieldError v-if="codeError" data-test="new-course-code-error">{{ codeError }}</FieldError>
          </Field>
          <div class="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <Field
              :data-invalid="datesError && !form.startsOn
                ? true
                : undefined"
            >
              <FieldLabel for="new-course-start">Startdatum</FieldLabel>
              <DatePicker
                id="new-course-start"
                v-model="form.startsOn"
                :max="form.endsOn || undefined"
                :invalid="datesError !== '' && !form.startsOn"
              />
            </Field>
            <Field
              :data-invalid="datesError && !form.endsOn
                ? true
                : undefined"
            >
              <FieldLabel for="new-course-end">Slutdatum</FieldLabel>
              <DatePicker
                id="new-course-end"
                v-model="form.endsOn"
                :min="form.startsOn || undefined"
                :invalid="datesError !== '' && !form.endsOn"
              />
            </Field>
          </div>
          <FieldError v-if="datesError" data-test="new-course-dates-error">{{ datesError }}</FieldError>

          <Field
            :data-invalid="teacherError
              ? true
              : undefined"
          >
            <FieldLabel>Lärare</FieldLabel>
            <ul
              class="max-h-44 overflow-y-auto rounded-[11px] border"
              role="group"
              aria-label="Lärare"
              data-test="new-course-teachers"
            >
              <li v-if="loading" class="text-muted-foreground px-4 py-2.5 text-sm">Hämtar lärare…</li>
              <li
                v-else-if="teachers.length === 0"
                class="text-muted-foreground px-4 py-2.5 text-sm"
              >
                Skolan har inga lärare ännu. Skapa en användare som lärare först.
              </li>
              <li
                v-for="teacher in teachers"
                :key="teacher.id"
                class="border-b last:border-b-0"
              >
                <label class="flex min-h-11 cursor-pointer items-center gap-3 px-4 py-2">
                  <Checkbox
                    :model-value="selected.includes(teacher.id)"
                    :aria-label="teacher.name"
                    data-test="new-course-teacher"
                    @update:model-value="(checked) => toggle(teacher.id, checked === true)"
                  />
                  <span class="flex min-w-0 flex-col">
                    <span class="font-medium break-words">{{ teacher.name }}</span>
                    <span class="text-muted-foreground text-[13px] break-words">{{ teacher.email }}</span>
                  </span>
                </label>
              </li>
            </ul>
            <FieldDescription>Välj en eller flera. Fler lärare kan läggas till på kursraden.</FieldDescription>
            <FieldError v-if="teacherError" data-test="new-course-teacher-error">{{ teacherError }}</FieldError>
          </Field>
        </FieldGroup>

        <Alert v-if="error" variant="destructive" data-test="new-course-error">
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
          <Button type="submit" :disabled="saving || loading" data-test="save-new-course">
            Skapa kurs
          </Button>
        </DialogFooter>
      </form>
    </DialogContent>
  </Dialog>
</template>
