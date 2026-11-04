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
import {api, ApiError, errorMessage, unwrap, type Schema} from '@/lib/api'
import {countLabel} from '@/lib/content'
import {periodLabel} from '@/lib/coursePeriod'
import {
  addDays,
  courseLengthDays,
  suggestCourseCode,
  suggestCourseTitle,
  suggestNewCourseCode,
} from '@/lib/courseSuggestions'
import {addCourseTeacher, createCourse, fetchCourses} from '@/lib/coursesApi'
import {linkCourseToGroup} from '@/lib/groupsApi'
import {fetchCourseModules} from '@/lib/modulesApi'
import {fetchUsers} from '@/lib/usersApi'

const props = defineProps<{groupId: string, groupName: string, schoolId: string}>()
// created kommer när något har sparats, också om ett senare steg misslyckades, så att sidan visar det som finns.
const emit = defineEmits<{created: [notice: string]}>()

const open = defineModel<boolean>('open', {required: true})

const courses = ref<Schema<'CourseDto'>[]>([])
const teachers = ref<Schema<'UserDto'>[]>([])
const weeks = ref<Schema<'ModuleWithContentDto'>[]>([])
const loading = ref(false)
const error = ref('')
const saving = ref(false)
const submitted = ref(false)

// Dialogen börjar med att välja utgångspunkt. Därefter visas formuläret, tomt eller ifyllt från källan.
const step = ref<'pick' | 'form'>('pick')
const source = ref<Schema<'CourseDto'> | null>(null)
const search = ref('')
const form = reactive({title: '', code: '', points: '', startsOn: '', endsOn: ''})
const codeTaken = ref(false)
const selected = ref<string[]>([])
const teacherSearch = ref('')
const addingTeacher = ref(false)
const copyContent = ref(true)
const choosingWeeks = ref(false)
const chosenWeeks = ref<string[]>([])
let suggestedEnd = ''
let suggestedCode = ''

// Stegen som redan är klara, så att ett nytt försök fortsätter där det tog slut i stället för att skapa kursen igen.
let createdCourse: Schema<'CourseDto'> | null = null
let linked = false
let assigned: string[] = []

// Kurser som redan har gått eller pågår står först, senast startade överst. Kommande kurser sist.
const sources = computed(() => {
  const query = search.value.trim().toLocaleLowerCase('sv')
  const today = new Date().toISOString().slice(0, 10)
  const isUpcoming = (course: Schema<'CourseDto'>) => (course.startsOn ?? '') > today
  return [...courses.value]
    .filter((course) => !query
      || course.title.toLocaleLowerCase('sv').includes(query)
      || course.code.toLocaleLowerCase('sv').includes(query))
    .sort((first, second) => Number(isUpcoming(first)) - Number(isUpcoming(second))
      || (second.startsOn ?? '').localeCompare(first.startsOn ?? ''))
})
const sourceLength = computed(() => courseLengthDays(source.value?.startsOn, source.value?.endsOn))
const sourceWeeksLabel = computed(() => sourceLength.value === null
  ? ''
  : countLabel(Math.max(1, Math.round(sourceLength.value / 7)), 'vecka', 'veckor'))
const chosenTeachers = computed(() => teachers.value.filter((teacher) => selected.value.includes(teacher.id)))
const teacherOptions = computed(() => {
  const query = teacherSearch.value.trim().toLocaleLowerCase('sv')
  return teachers.value.filter((teacher) => !selected.value.includes(teacher.id)
    && (!query
      || teacher.name.toLocaleLowerCase('sv').includes(query)
      || teacher.email.toLocaleLowerCase('sv').includes(query)))
})
const includedWeeks = computed(() => weeks.value.filter((week) => chosenWeeks.value.includes(week.id)))
const includedParts = computed(() => includedWeeks.value.reduce((sum, week) => sum + week.content.length, 0))
const copyLabel = computed(() => `Kopiera veckor och delar (${
  countLabel(includedWeeks.value.length, 'vecka', 'veckor')}, ${countLabel(includedParts.value, 'del', 'delar')})`)

const titleError = computed(() => submitted.value && !form.title.trim()
  ? 'Ange ett namn.'
  : '')
const codeError = computed(() => {
  if (codeTaken.value) {
    return 'Skolan har redan en kurs med den koden.'
  }
  return submitted.value && !form.code.trim()
    ? 'Ange en kurskod.'
    : ''
})
const pointsError = computed(() => {
  if (form.points === '') {
    return ''
  }
  const value = Number(form.points)
  return Number.isInteger(value) && value >= 1 && value <= 400
    ? ''
    : 'Ange 1–400 poäng.'
})
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

  step.value = 'pick'
  source.value = null
  search.value = ''
  resetForm()
  error.value = ''
  createdCourse = null
  linked = false
  assigned = []
  loading.value = true
  try {
    const [courseList, userList] = await Promise.all([fetchCourses(), fetchUsers()])
    courses.value = courseList
    // Bara användare med rollen Lärare. Admin undervisar aldrig.
    teachers.value = userList
      .filter((user) => user.role === 'Teacher')
      .sort((first, second) => first.name.localeCompare(second.name, 'sv'))
  } catch (exception) {
    error.value = errorMessage(exception)
  } finally {
    loading.value = false
  }
})

// Kursens längd följer med: slutdatumet föreslås så länge admin inte har valt ett eget.
watch(() => form.startsOn, (startsOn) => {
  if (!startsOn || sourceLength.value === null || (form.endsOn && form.endsOn !== suggestedEnd)) {
    return
  }
  suggestedEnd = addDays(startsOn, sourceLength.value)
  form.endsOn = suggestedEnd
})

// En tom kurs får en kod från namnet. Koden följer namnet så länge admin inte har skrivit en egen kod.
watch(() => form.title, (title) => {
  if (source.value || (form.code !== suggestedCode && form.code.trim() !== '')) {
    return
  }
  suggestedCode = suggestNewCourseCode(props.groupName, title, courses.value.map((course) => course.code))
  form.code = suggestedCode
})

watch(() => form.code, () => {
  codeTaken.value = false
})

function resetForm() {
  Object.assign(form, {title: '', code: '', points: '', startsOn: '', endsOn: ''})
  selected.value = []
  weeks.value = []
  chosenWeeks.value = []
  copyContent.value = true
  choosingWeeks.value = false
  addingTeacher.value = false
  teacherSearch.value = ''
  submitted.value = false
  codeTaken.value = false
  suggestedEnd = ''
  suggestedCode = ''
}

function startEmpty() {
  source.value = null
  resetForm()
  step.value = 'form'
}

async function pickSource(course: Schema<'CourseDto'>) {
  source.value = course
  resetForm()
  form.title = suggestCourseTitle(props.groupName, course.title)
  form.code = suggestCourseCode(props.groupName, course.code)
  form.points = course.points === null || course.points === undefined
    ? ''
    : String(course.points)
  step.value = 'form'
  loading.value = true
  error.value = ''
  try {
    const [staff, modules] = await Promise.all([
      api.GET('/api/courses/{id}/staff', {params: {path: {id: course.id}}}),
      fetchCourseModules(course.id),
    ])
    if (source.value?.id !== course.id) {
      return
    }
    selected.value = unwrap(staff).map((teacher) => teacher.userId)
    weeks.value = modules
    chosenWeeks.value = weeks.value.map((week) => week.id)
  } catch (exception) {
    error.value = errorMessage(exception)
  } finally {
    loading.value = false
  }
}

function backToPick() {
  source.value = null
  error.value = ''
  step.value = 'pick'
}

function addTeacher(userId: string) {
  selected.value = [...selected.value, userId]
  teacherSearch.value = ''
  addingTeacher.value = false
}

function removeTeacher(userId: string) {
  selected.value = selected.value.filter((id) => id !== userId)
}

function toggleWeek(weekId: string, checked: boolean) {
  chosenWeeks.value = checked
    ? [...chosenWeeks.value, weekId]
    : chosenWeeks.value.filter((id) => id !== weekId)
}

function namesLabel(names: string[]): string {
  return names.length > 1
    ? `${names.slice(0, -1).join(', ')} och ${names.at(-1)}`
    : names[0] ?? ''
}

function createdNotice(partsCopied: {modules: number, parts: number} | null): string {
  const teachersLabel = namesLabel(chosenTeachers.value.map((teacher) => teacher.name))
  const content = partsCopied && partsCopied.modules > 0
    ? ` med ${countLabel(partsCopied.modules, 'vecka', 'veckor')} och ${
      countLabel(partsCopied.parts, 'del', 'delar')} som utkast`
    : ''
  return `${form.title.trim()} skapades${content}. ${teachersLabel} ser kursen under Behöver förberedas.`
}

// Att koden redan finns visas vid kodfältet, övriga fel i dialogen.
function showCreateError(exception: unknown) {
  const message = errorMessage(exception)
  if (exception instanceof ApiError && exception.message.includes('already has a course with this code')) {
    codeTaken.value = true
  } else {
    error.value = message
  }
}

const points = computed(() => form.points === ''
  ? null
  : Number(form.points))

async function createFromSource(from: Schema<'CourseDto'>) {
  const copy = copyContent.value && chosenWeeks.value.length > 0
  const run = unwrap(await api.POST('/api/courses/{id}/runs', {
    params: {path: {id: from.id}},
    body: {
      title: form.title.trim(),
      code: form.code.trim(),
      points: points.value,
      groupId: props.groupId,
      startsOn: form.startsOn,
      endsOn: form.endsOn,
      copyContent: copy,
      teacherIds: selected.value,
      moduleIds: chosenWeeks.value,
    },
  }))
  emit('created', createdNotice({modules: run.moduleCount, parts: run.partCount}))
  open.value = false
}

async function createEmpty() {
  let failed = 'Kursen kunde inte skapas.'
  try {
    // Beskrivningen skriver läraren själv, så den följer inte med.
    createdCourse ??= await createCourse({
      schoolId: props.schoolId,
      title: form.title.trim(),
      code: form.code.trim(),
      points: points.value,
      startsOn: form.startsOn,
      endsOn: form.endsOn,
    })
    const created = createdCourse

    failed = `Kursen ${created.title} skapades men kunde inte kopplas till ${props.groupName}.`
    if (!linked) {
      await linkCourseToGroup(props.groupId, created.id)
      linked = true
    }

    for (const userId of selected.value.filter((id) => !assigned.includes(id))) {
      const name = teachers.value.find((teacher) => teacher.id === userId)?.name ?? 'läraren'
      failed = `Kursen ${created.title} skapades och kopplades till ${props.groupName}, men ${name} kunde inte läggas till som lärare.`
      await addCourseTeacher(created.id, userId)
      assigned.push(userId)
    }

    emit('created', createdNotice(null))
    open.value = false
  } catch (exception) {
    if (!createdCourse) {
      throw exception
    }
    error.value = `${failed} ${errorMessage(exception)} Tryck på Skapa kurs igen för att fortsätta, eller lägg till läraren på kursraden.`
    emit('created', '')
  }
}

async function save() {
  submitted.value = true
  if (!form.title.trim() || !form.code.trim() || !form.startsOn || !form.endsOn
    || selected.value.length === 0 || pointsError.value) {
    return
  }

  saving.value = true
  error.value = ''
  try {
    await (source.value
      ? createFromSource(source.value)
      : createEmpty())
  } catch (exception) {
    showCreateError(exception)
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <Dialog v-model:open="open">
    <DialogContent class="max-h-[calc(100dvh-2rem)] grid-cols-[minmax(0,1fr)] overflow-y-auto sm:max-w-xl">
      <div v-if="step === 'pick'" class="flex min-w-0 flex-col gap-4">
        <DialogHeader class="min-w-0">
          <DialogTitle class="break-words">Ny kurs i {{ groupName }}</DialogTitle>
          <DialogDescription>Har kursen gått förut? Utgå från den, så följer det mesta med.</DialogDescription>
        </DialogHeader>

        <Field>
          <FieldLabel for="new-course-source">Utgå från</FieldLabel>
          <Input
            id="new-course-source"
            v-model="search"
            placeholder="Sök kurs eller kod…"
            autocomplete="off"
            data-test="new-course-search"
          />
          <ul class="max-h-64 overflow-y-auto rounded-lg border" data-test="new-course-sources">
            <li v-if="loading" class="text-muted-foreground px-4 py-2.5 text-sm">Hämtar kurser…</li>
            <li v-else-if="sources.length === 0" class="text-muted-foreground px-4 py-2.5 text-sm">
              Inga kurser matchar sökningen.
            </li>
            <li v-for="course in sources" :key="course.id" class="border-b last:border-b-0">
              <button
                type="button"
                class="hover:bg-accent flex min-h-11 w-full flex-wrap items-center justify-between gap-x-3 px-4 py-2 text-left"
                data-test="new-course-source"
                @click="pickSource(course)"
              >
                <span class="flex min-w-0 flex-col">
                  <span class="font-medium break-words">{{ course.title }}</span>
                  <span class="text-muted-foreground font-mono text-xs">{{ course.code }}</span>
                </span>
                <span class="text-muted-foreground text-[13px]">{{ periodLabel(course.startsOn ?? null, course.endsOn ?? null) }}</span>
              </button>
            </li>
          </ul>
        </Field>

        <Alert v-if="error" variant="destructive" data-test="new-course-error">
          <AlertDescription>{{ error }}</AlertDescription>
        </Alert>

        <div class="flex flex-wrap items-center justify-between gap-2">
          <span class="text-muted-foreground text-sm">Ny kurs som inte gått förut?</span>
          <Button variant="outline" data-test="new-course-empty" @click="startEmpty">
            Börja med en tom kurs
          </Button>
        </div>
      </div>

      <form
        v-else
        class="flex min-w-0 flex-col gap-4"
        novalidate
        @submit.prevent="save"
      >
        <DialogHeader class="min-w-0">
          <DialogTitle class="break-words">Ny kurs i {{ groupName }}</DialogTitle>
          <DialogDescription>
            Kursen skapas, kopplas till gruppen och får sina lärare. Läraren bygger sedan ut den.
          </DialogDescription>
        </DialogHeader>

        <FieldGroup>
          <Field>
            <FieldLabel>Utgå från</FieldLabel>
            <div class="flex min-h-8 items-center justify-between gap-3 rounded-lg border px-2.5 py-1 text-sm">
              <span class="min-w-0 break-words" data-test="new-course-chosen-source">
                <template v-if="source">
                  {{ source.title }}
                  <span class="text-muted-foreground font-mono text-xs">{{ source.code }}</span>
                </template>
                <template v-else>Tom kurs</template>
              </span>
              <Button
                type="button"
                variant="ghost"
                size="sm"
                data-test="new-course-change-source"
                @click="backToPick"
              >
                Byt
              </Button>
            </div>
          </Field>

          <div class="grid grid-cols-1 gap-4 sm:grid-cols-[2fr_2fr_1fr]">
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
                :placeholder="`${groupName}_DevOps`"
                autocomplete="off"
                :aria-invalid="codeError
                  ? true
                  : undefined"
                data-test="new-course-code"
              />
              <FieldError v-if="codeError" data-test="new-course-code-error">{{ codeError }}</FieldError>
              <FieldDescription v-else-if="!source">Föreslås från namnet. Skriv en egen kod om du vill.</FieldDescription>
            </Field>
            <Field
              :data-invalid="pointsError
                ? true
                : undefined"
            >
              <FieldLabel for="new-course-points">Omfattning (p)</FieldLabel>
              <Input
                id="new-course-points"
                v-model="form.points"
                inputmode="numeric"
                placeholder="25"
                autocomplete="off"
                :aria-invalid="pointsError
                  ? true
                  : undefined"
                data-test="new-course-points"
              />
              <FieldError v-if="pointsError" data-test="new-course-points-error">{{ pointsError }}</FieldError>
            </Field>
          </div>

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
          <FieldDescription v-if="sourceWeeksLabel" data-test="new-course-length-hint">
            Slutdatumet föreslås utifrån förra kursens längd, {{ sourceWeeksLabel }}.
          </FieldDescription>
          <FieldError v-if="datesError" data-test="new-course-dates-error">{{ datesError }}</FieldError>

          <Field
            :data-invalid="teacherError
              ? true
              : undefined"
          >
            <FieldLabel>Lärare</FieldLabel>
            <div class="flex flex-wrap items-center gap-2" data-test="new-course-teachers">
              <span
                v-for="teacher in chosenTeachers"
                :key="teacher.id"
                class="bg-muted inline-flex min-h-7 items-center gap-1 rounded-full pr-1 pl-3 text-sm"
                data-test="new-course-teacher"
              >
                {{ teacher.name }}
                <Button
                  type="button"
                  variant="ghost"
                  size="icon-sm"
                  class="size-6 rounded-full"
                  :aria-label="`Ta bort ${teacher.name}`"
                  data-test="new-course-teacher-remove"
                  @click="removeTeacher(teacher.id)"
                >
                  ×
                </Button>
              </span>
              <Button
                type="button"
                variant="ghost"
                size="sm"
                data-test="new-course-add-teacher"
                @click="addingTeacher = !addingTeacher"
              >
                Lägg till lärare
              </Button>
            </div>
            <div v-if="addingTeacher" class="flex flex-col gap-2">
              <Input
                v-model="teacherSearch"
                aria-label="Sök lärare"
                placeholder="Sök lärare…"
                autocomplete="off"
                data-test="new-course-teacher-search"
              />
              <ul class="max-h-44 overflow-y-auto rounded-lg border" data-test="new-course-teacher-options">
                <li v-if="teachers.length === 0" class="text-muted-foreground px-4 py-2.5 text-sm">
                  Skolan har inga lärare ännu. Skapa en användare som lärare först.
                </li>
                <li
                  v-else-if="teacherOptions.length === 0"
                  class="text-muted-foreground px-4 py-2.5 text-sm"
                >
                  Inga lärare matchar sökningen.
                </li>
                <li
                  v-for="teacher in teacherOptions"
                  :key="teacher.id"
                  class="border-b last:border-b-0"
                >
                  <button
                    type="button"
                    class="hover:bg-accent flex min-h-11 w-full flex-col justify-center px-4 py-1.5 text-left"
                    data-test="new-course-teacher-option"
                    @click="addTeacher(teacher.id)"
                  >
                    <span class="font-medium break-words">{{ teacher.name }}</span>
                    <span class="text-muted-foreground text-[13px] break-words">{{ teacher.email }}</span>
                  </button>
                </li>
              </ul>
            </div>
            <FieldError v-if="teacherError" data-test="new-course-teacher-error">{{ teacherError }}</FieldError>
          </Field>

          <Field v-if="source && weeks.length > 0">
            <FieldLabel>Innehåll</FieldLabel>
            <div class="flex flex-wrap items-center justify-between gap-2">
              <FieldLabel class="w-auto min-w-0 cursor-pointer items-start gap-2 font-normal">
                <Checkbox
                  :model-value="copyContent"
                  data-test="new-course-copy"
                  @update:model-value="(checked) => copyContent = checked === true"
                />
                <span class="min-w-0 text-sm break-words" data-test="new-course-copy-label">{{ copyLabel }}</span>
              </FieldLabel>
              <Button
                v-if="copyContent"
                type="button"
                variant="ghost"
                size="sm"
                data-test="new-course-choose-weeks"
                @click="choosingWeeks = !choosingWeeks"
              >
                Välj veckor
              </Button>
            </div>
            <div
              v-if="copyContent && choosingWeeks"
              class="flex max-h-44 flex-col gap-1 overflow-y-auto rounded-lg border p-2"
              role="group"
              aria-label="Välj veckor"
            >
              <FieldLabel
                v-for="week in weeks"
                :key="week.id"
                class="w-full min-w-0 cursor-pointer items-center gap-2 px-1 py-1 font-normal"
              >
                <Checkbox
                  :model-value="chosenWeeks.includes(week.id)"
                  :data-test="`new-course-week-${week.id}`"
                  @update:model-value="(checked) => toggleWeek(week.id, checked === true)"
                />
                <span class="min-w-0 flex-1 text-sm break-words">{{ week.title }}</span>
                <span class="text-muted-foreground text-xs">{{ countLabel(week.content.length, 'del', 'delar') }}</span>
              </FieldLabel>
            </div>
            <FieldDescription>
              Allt blir utkast. Eleverna ser inget förrän läraren publicerar. Deadlines flyttas med startdatumet.
            </FieldDescription>
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
