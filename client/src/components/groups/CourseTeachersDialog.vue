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
import {NativeSelect, NativeSelectOption} from '@/components/ui/native-select'
import {api, errorMessage, unwrap, type Schema} from '@/lib/api'

const props = defineProps<{course: Schema<'CourseDto'> | null, teachers: Schema<'CourseTeacherDto'>[]}>()
const emit = defineEmits<{changed: [courseId: string, teachers: Schema<'CourseTeacherDto'>[]]}>()

const open = defineModel<boolean>('open', {required: true})

const users = ref<Schema<'UserDto'>[]>([])
const userId = ref('')
const loading = ref(false)
const busy = ref('')
const error = ref('')

// Den som redan undervisar i kursen kan inte läggas till igen, och elever kan inte vara lärare.
const candidates = computed(() => users.value
  .filter((user) => user.role !== 'Student')
  .filter((user) => !props.teachers.some((teacher) => teacher.userId === user.id))
  .sort((first, second) => first.name.localeCompare(second.name, 'sv')))

watch(open, async (isOpen) => {
  if (!isOpen) {
    return
  }

  userId.value = ''
  error.value = ''
  loading.value = true
  try {
    users.value = unwrap(await api.GET('/api/users'))
  } catch (exception) {
    error.value = errorMessage(exception)
  } finally {
    loading.value = false
  }
})

async function add() {
  const user = candidates.value.find((found) => found.id === userId.value)
  if (!props.course || !user) {
    return
  }

  busy.value = 'add'
  error.value = ''
  try {
    const added = unwrap(await api.POST('/api/courses/{id}/teachers', {
      params: {path: {id: props.course.id}},
      body: {userId: user.id},
    }))
    userId.value = ''
    emit('changed', props.course.id, [...props.teachers, added])
  } catch (exception) {
    error.value = `Kunde inte lägga till ${user.name}. ${errorMessage(exception)}`
  } finally {
    busy.value = ''
  }
}

async function remove(teacher: Schema<'CourseTeacherDto'>) {
  if (!props.course) {
    return
  }

  busy.value = teacher.userId
  error.value = ''
  try {
    await api.DELETE('/api/courses/{id}/teachers/{userId}', {
      params: {path: {id: props.course.id, userId: teacher.userId}},
    })
    emit('changed', props.course.id, props.teachers.filter((found) => found.userId !== teacher.userId))
  } catch (exception) {
    error.value = `Kunde inte ta bort ${teacher.name}. ${errorMessage(exception)}`
  } finally {
    busy.value = ''
  }
}
</script>

<template>
  <Dialog v-model:open="open">
    <DialogContent class="max-h-[calc(100dvh-2rem)] grid-cols-[minmax(0,1fr)] overflow-y-auto sm:max-w-lg">
      <div class="flex min-w-0 flex-col gap-4">
        <DialogHeader class="min-w-0">
          <DialogTitle class="break-words">Lärare i {{ course?.title }}</DialogTitle>
          <DialogDescription>
            Lärarna skriver beskrivningen, bygger veckorna och bedömer. Lärare i klassen får ingen behörighet i kursen.
          </DialogDescription>
        </DialogHeader>

        <p v-if="teachers.length === 0" class="text-muted-foreground text-sm" data-test="no-teachers">
          Kursen har ingen lärare ännu.
        </p>
        <ul v-else class="rounded-[11px] border" data-test="course-teacher-list">
          <li
            v-for="teacher in teachers"
            :key="teacher.userId"
            class="flex items-center gap-3 border-b px-4 py-2.5 last:border-b-0"
            data-test="course-teacher"
          >
            <span class="flex min-w-0 flex-1 flex-col">
              <span class="font-medium break-words">{{ teacher.name }}</span>
              <span class="text-muted-foreground text-[13px] break-words">{{ teacher.email }}</span>
            </span>
            <Button
              type="button"
              variant="outline"
              class="h-8"
              :disabled="busy !== ''"
              :aria-label="`Ta bort ${teacher.name} som lärare`"
              data-test="remove-teacher"
              @click="remove(teacher)"
            >
              Ta bort
            </Button>
          </li>
        </ul>

        <div class="flex min-w-0 flex-col gap-1.5">
          <FieldLabel for="add-teacher">Lägg till lärare</FieldLabel>
          <div class="flex min-w-0 gap-2">
            <NativeSelect
              id="add-teacher"
              v-model="userId"
              class="w-full min-w-0"
              :disabled="loading || busy !== ''"
              data-test="add-teacher-select"
            >
              <NativeSelectOption value="">
                {{ candidates.length === 0 && !loading
                  ? 'Inga fler lärare finns'
                  : 'Välj lärare…' }}
              </NativeSelectOption>
              <NativeSelectOption v-for="user in candidates" :key="user.id" :value="user.id">
                {{ user.name }} · {{ user.email }}
              </NativeSelectOption>
            </NativeSelect>
            <Button
              type="button"
              :disabled="userId === '' || busy !== ''"
              data-test="add-teacher"
              @click="add"
            >
              Lägg till
            </Button>
          </div>
        </div>

        <Alert v-if="error" variant="destructive">
          <AlertDescription>{{ error }}</AlertDescription>
        </Alert>

        <DialogFooter>
          <Button type="button" variant="outline" @click="open = false">Stäng</Button>
        </DialogFooter>
      </div>
    </DialogContent>
  </Dialog>
</template>
