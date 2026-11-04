<script setup lang="ts">
import {computed, ref, watch} from 'vue'

import SearchPicker from '@/components/SearchPicker.vue'
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

// Sökningen matchar titel och kod.
const items = computed(() => available.value.map((course) => ({id: course.id, title: course.title, detail: course.code})))

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

        <SearchPicker
          id="link-course"
          v-model="courseId"
          v-model:search="search"
          label="Kurs"
          placeholder="Sök på titel eller kod"
          :items="items"
          :nouns="['kurs', 'kurser']"
          empty-text="Det finns inga kurser att koppla."
          :loading="loading"
          data-test="link-course"
        />

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
