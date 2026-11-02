<script setup lang="ts">
import {computed, ref, watch} from 'vue'

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
import {FieldLabel} from '@/components/ui/field'
import {Input} from '@/components/ui/input'
import {NativeSelect, NativeSelectOption} from '@/components/ui/native-select'
import {useAsync} from '@/composables/useAsync'
import {ApiError, errorMessage, type Schema} from '@/lib/api'
import {splitFinished} from '@/lib/courseStatus'
import {fetchCourses} from '@/lib/coursesApi'
import {linkCourseToGroup} from '@/lib/groupsApi'

const props = defineProps<{groupId: string, groupName: string, linkedIds: string[]}>()
const emit = defineEmits<{linked: [course: Schema<'CourseDto'>]}>()

const open = defineModel<boolean>('open', {required: true})

const courses = ref<Schema<'CourseDto'>[]>([])
const courseId = ref('')
const search = ref('')
const saving = ref(false)
// Samma felruta visar felet från hämtningen och från sparandet.
const {run: loadCourses, loading, error} = useAsync(fetchCourses)

// Avslutade kurser sist, så att de pågående ligger överst.
const available = computed(() => {
  const {current, finished} = splitFinished(courses.value.filter((course) => !props.linkedIds.includes(course.id)))
  return [...current, ...finished]
})

// Sökfältet filtrerar listan på titel och kod.
const visible = computed(() => {
  const query = search.value.trim().toLowerCase()
  return query === ''
    ? available.value
    : available.value.filter((course) =>
        course.title.toLowerCase().includes(query) || course.code.toLowerCase().includes(query))
})

watch(open, async (isOpen) => {
  if (!isOpen) {
    return
  }

  courseId.value = ''
  search.value = ''
  const loaded = await loadCourses()
  if (loaded) {
    courses.value = loaded
  }
})

async function link() {
  const course = courses.value.find((found) => found.id === courseId.value)
  if (!course) {
    return
  }

  saving.value = true
  error.value = ''
  try {
    await linkCourseToGroup(props.groupId, course.id)
    emit('linked', course)
    open.value = false
  } catch (exception) {
    error.value = exception instanceof ApiError && exception.status === 409
      ? 'Kursen är redan kopplad till gruppen.'
      : errorMessage(exception)
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <Dialog v-model:open="open">
    <DialogContent class="grid-cols-[minmax(0,1fr)] sm:max-w-lg">
      <form class="flex min-w-0 flex-col gap-4" @submit.prevent="link">
        <DialogHeader class="min-w-0">
          <DialogTitle class="break-words">Koppla kurs till {{ groupName }}</DialogTitle>
          <DialogDescription>Gruppens medlemmar får tillgång till kursen.</DialogDescription>
        </DialogHeader>

        <div class="flex min-w-0 flex-col gap-1.5">
          <FieldLabel for="link-course-search">Sök kurs</FieldLabel>
          <Input
            id="link-course-search"
            v-model="search"
            type="search"
            placeholder="Titel eller kod"
            :disabled="loading"
            data-test="link-course-search"
          />
          <FieldLabel for="link-course" class="mt-1.5">Kurs</FieldLabel>
          <NativeSelect
            id="link-course"
            v-model="courseId"
            class="w-full"
            required
            :disabled="loading"
            data-test="link-course"
          >
            <NativeSelectOption value="">
              {{ available.length === 0 && !loading
                ? 'Det finns inga kurser att koppla'
                : visible.length === 0 && !loading
                  ? 'Ingen kurs matchar sökningen'
                  : 'Välj kurs…' }}
            </NativeSelectOption>
            <NativeSelectOption v-for="course in visible" :key="course.id" :value="course.id">
              {{ course.title }} · {{ course.code }}
            </NativeSelectOption>
          </NativeSelect>
        </div>

        <Alert v-if="error" variant="destructive">
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
          <Button type="submit" :disabled="saving || courseId === ''" data-test="confirm-link-course">
            Koppla kurs
          </Button>
        </DialogFooter>
      </form>
    </DialogContent>
  </Dialog>
</template>
