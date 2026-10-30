<script setup lang="ts">
import DatePicker from '@/components/DatePicker.vue'
import {PhEnvelopeSimple, PhPencilSimple, PhTrash} from '@phosphor-icons/vue'
import {computed, nextTick, reactive, ref, watch} from 'vue'
import {useRoute, useRouter} from 'vue-router'

import DeleteCourseDialog from '@/components/courses/DeleteCourseDialog.vue'
import MarkdownEditor from '@/components/editor/MarkdownEditor.vue'
import MarkdownView from '@/components/editor/MarkdownView.vue'
import {Alert, AlertDescription} from '@/components/ui/alert'
import {Avatar, AvatarFallback} from '@/components/ui/avatar'
import {Button} from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import {Field, FieldGroup, FieldLabel} from '@/components/ui/field'
import {Input} from '@/components/ui/input'
import {Skeleton} from '@/components/ui/skeleton'
import {useCourses} from '@/composables/useCourses'
import {useCurrentUser} from '@/composables/useCurrentUser'
import {api, errorMessage, type Schema} from '@/lib/api'
import {initials} from '@/lib/news'
import NotFoundView from '@/views/NotFoundView.vue'

const route = useRoute()
const router = useRouter()
const courseId = computed(() => String(route.params.courseId))
const {findCourse, updateCourse, removeCourse} = useCourses()
const {can} = useCurrentUser()

const course = computed(() => findCourse(courseId.value))
// Beskrivningen är lärarens text. Admin ändrar kursens fakta (titel, datum, poäng) och tar bort kursen.
const canEditDescription = computed(() => can('editCourse', courseId.value))
const canDelete = computed(() => can('manageCourse'))
const canEdit = computed(() => canEditDescription.value || canDelete.value)
const deleteOpen = ref(false)

// Kursen finns inte längre, så sidan lämnas för listan där den skapades.
async function onDeleted(removed: Schema<'CourseDto'>) {
  await router.push({name: 'groups'})
  removeCourse(removed.id)
}

const staff = ref<Schema<'CourseStaffDto'>[]>([])
const loading = ref(true)
const loadFailed = ref(false)

const hasDescription = computed(() => (course.value?.description ?? '').trim() !== '')

const period = computed(() => {
  if (!course.value) {
    return '–'
  }
  if (course.value.startsOn && course.value.endsOn) {
    return `${course.value.startsOn} – ${course.value.endsOn}`
  }
  return course.value.startsOn ?? course.value.endsOn ?? '–'
})

const pointsText = computed(() => {
  const points = course.value?.points
  return points === null || points === undefined
    ? '–'
    : `${points} p`
})

const editOpen = ref(false)
const saving = ref(false)
const saveError = ref('')
const titleMissing = ref(false)
const form = reactive({title: '', description: '', points: '', startsOn: '', endsOn: ''})

async function loadStaff() {
  loading.value = true
  loadFailed.value = false
  try {
    const {data} = await api.GET('/api/courses/{id}/staff', {params: {path: {id: courseId.value}}})
    staff.value = data ?? []
  } catch {
    loadFailed.value = true
  } finally {
    loading.value = false
    // Först när personalen har laddats står sektionen på sin slutliga plats.
    scrollToStaff()
  }
}

function openEdit() {
  if (!course.value) {
    return
  }
  form.title = course.value.title
  form.description = course.value.description
  form.points = course.value.points === null
    ? ''
    : String(course.value.points)
  form.startsOn = course.value.startsOn ?? ''
  form.endsOn = course.value.endsOn ?? ''
  saveError.value = ''
  titleMissing.value = false
  editOpen.value = true
}

async function save() {
  titleMissing.value = form.title.trim() === ''
  if (titleMissing.value) {
    return
  }

  saving.value = true
  saveError.value = ''
  try {
    const {data} = await api.PUT('/api/courses/{id}', {
      params: {path: {id: courseId.value}},
      body: {
        title: form.title,
        description: form.description,
        points: form.points === ''
          ? null
          : Number(form.points),
        startsOn: form.startsOn === ''
          ? null
          : form.startsOn,
        endsOn: form.endsOn === ''
          ? null
          : form.endsOn,
      },
    })
    if (data) {
      updateCourse(data)
    }
    editOpen.value = false
  } catch (exception) {
    saveError.value = errorMessage(exception)
  } finally {
    saving.value = false
  }
}

// Personal-fliken visar samma sida som Om, men scrollar till personalen.
function scrollToStaff() {
  if (route.name !== 'course-staff') {
    return
  }
  nextTick(() => {
    const section = document.getElementById('personal')
    if (section && typeof section.scrollIntoView === 'function') {
      section.scrollIntoView({behavior: 'smooth', block: 'start'})
    }
  })
}

watch(courseId, () => {
  staff.value = []
  loadStaff()
}, {immediate: true})

watch(() => route.name, scrollToStaff)
</script>

<template>
  <NotFoundView v-if="!course"/>
  <template v-else>
    <div class="flex flex-wrap items-start gap-6">
      <div class="flex min-w-0 flex-[999_1_520px] flex-col gap-6">
        <section aria-labelledby="description-heading" class="flex flex-col gap-2.5">
          <div class="flex flex-wrap items-center justify-between gap-2">
            <h2 id="description-heading" class="text-lg font-semibold">Beskrivning</h2>
            <div v-if="canEdit" class="flex items-center gap-2">
              <Button
                variant="outline"
                data-test="edit-course"
                @click="openEdit"
              >
                <PhPencilSimple/>
                Redigera
              </Button>
              <Button
                v-if="canDelete"
                variant="outline"
                data-test="delete-course"
                @click="deleteOpen = true"
              >
                <PhTrash/>
                Ta bort kursen
              </Button>
            </div>
          </div>
          <div class="max-w-[68ch]">
            <MarkdownView v-if="hasDescription" :markdown="course.description"/>
            <p v-else class="text-muted-foreground">Ingen kursbeskrivning ännu.</p>
          </div>
        </section>

        <section id="personal" aria-labelledby="staff-heading" class="flex scroll-mt-6 flex-col gap-2.5">
          <h2 id="staff-heading" class="text-lg font-semibold">Personal</h2>

          <Skeleton v-if="loading" class="h-20 w-full rounded-[11px]"/>
          <Alert v-else-if="loadFailed" variant="destructive">
            <AlertDescription>Kunde inte hämta personalen.</AlertDescription>
          </Alert>
          <div v-else-if="staff.length > 0" class="bg-card overflow-hidden rounded-[11px] border">
            <div
              v-for="member in staff"
              :key="member.userId"
              class="flex flex-wrap items-center gap-3 border-b px-4 py-3.5 last:border-b-0"
              data-test="staff-row"
            >
              <Avatar class="size-10">
                <AvatarFallback class="text-[13px] font-semibold">{{ initials(member.name) }}</AvatarFallback>
              </Avatar>
              <div class="flex min-w-0 flex-1 basis-[200px] flex-col">
                <span class="font-medium">{{ member.name }}</span>
                <span class="text-muted-foreground text-[13px]">{{ member.groups.join(' · ') }}</span>
              </div>
              <Button variant="outline" as-child>
                <a :href="`mailto:${member.email}`">
                  <PhEnvelopeSimple/>
                  Meddelande
                </a>
              </Button>
            </div>
          </div>
          <p v-else class="text-muted-foreground text-sm">Ingen personal är kopplad till kursen ännu.</p>
        </section>
      </div>

      <aside class="flex min-w-0 flex-[1_1_300px] flex-col gap-4">
        <section
          aria-labelledby="facts-heading"
          class="bg-card flex flex-col gap-2 rounded-[11px] border p-4 text-[13px]"
        >
          <h2 id="facts-heading" class="text-sm font-semibold">Fakta</h2>
          <dl class="flex flex-col gap-2">
            <div class="flex justify-between gap-2">
              <dt class="text-muted-foreground">Kurskod</dt>
              <dd class="font-mono text-xs">{{ course.code }}</dd>
            </div>
            <div class="flex justify-between gap-2">
              <dt class="text-muted-foreground">Period</dt>
              <dd>{{ period }}</dd>
            </div>
            <div class="flex justify-between gap-2">
              <dt class="text-muted-foreground">Omfattning</dt>
              <dd>{{ pointsText }}</dd>
            </div>
            <div class="flex justify-between gap-2">
              <dt class="text-muted-foreground">Kursbetyg</dt>
              <dd>IG – G – VG</dd>
            </div>
          </dl>
        </section>
      </aside>
    </div>

    <DeleteCourseDialog
      v-if="canDelete"
      v-model:open="deleteOpen"
      :course="course"
      @deleted="onDeleted"
    />

    <Dialog v-model:open="editOpen">
      <DialogContent class="sm:max-w-lg">
        <form class="flex flex-col gap-4" @submit.prevent="save">
          <DialogHeader>
            <DialogTitle>Redigera kursen</DialogTitle>
            <DialogDescription>
              {{ canEditDescription
                ? 'Ändringarna syns på fliken Om kursen.'
                : 'Beskrivningen är lärarens och ändras inte här.' }}
            </DialogDescription>
          </DialogHeader>

          <FieldGroup>
            <Field>
              <FieldLabel for="course-edit-title">Rubrik</FieldLabel>
              <Input id="course-edit-title" v-model="form.title" required/>
              <p v-if="titleMissing" class="text-destructive text-sm">Skriv en rubrik.</p>
            </Field>
            <Field v-if="canEditDescription">
              <FieldLabel for="course-edit-description">Beskrivning</FieldLabel>
              <MarkdownEditor
                id="course-edit-description"
                v-model="form.description"
                label="Beskrivning"
              />
            </Field>
            <div class="grid grid-cols-1 gap-4 sm:grid-cols-2">
              <Field>
                <FieldLabel for="course-edit-points">Omfattning (p)</FieldLabel>
                <Input
                  id="course-edit-points"
                  v-model="form.points"
                  type="number"
                  min="1"
                  max="400"
                  placeholder="25"
                />
              </Field>
              <Field>
                <FieldLabel for="course-edit-start">Startdatum</FieldLabel>
                <DatePicker
                  id="course-edit-start"
                  v-model="form.startsOn"
                  clearable
                  :max="form.endsOn || undefined"
                />
              </Field>
              <Field>
                <FieldLabel for="course-edit-end">Slutdatum</FieldLabel>
                <DatePicker
                  id="course-edit-end"
                  v-model="form.endsOn"
                  clearable
                  :min="form.startsOn || undefined"
                />
              </Field>
            </div>
          </FieldGroup>

          <Alert v-if="saveError" variant="destructive">
            <AlertDescription>{{ saveError }}</AlertDescription>
          </Alert>

          <DialogFooter>
            <Button
              type="button"
              variant="outline"
              :disabled="saving"
              @click="editOpen = false"
            >
              Avbryt
            </Button>
            <Button type="submit" :disabled="saving">Spara</Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  </template>
</template>
