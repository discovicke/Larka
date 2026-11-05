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
import {Select, SelectContent, SelectItem, SelectTrigger, SelectValue} from '@/components/ui/select'
import {useAsync} from '@/composables/useAsync'
import {api, errorMessage, type Schema} from '@/lib/api'
import {addCourseTeacher} from '@/lib/coursesApi'
import {fetchUsers} from '@/lib/usersApi'

const props = defineProps<{
  course: Schema<'CourseDto'> | null
  teachers: Schema<'CourseTeacherDto'>[]
  // Namnen på de andra grupperna som läser kursen. En ändring här gäller också dem.
  sharedWith?: string[]
}>()
const emit = defineEmits<{changed: [courseId: string, teachers: Schema<'CourseTeacherDto'>[]]}>()

const open = defineModel<boolean>('open', {required: true})

const users = ref<Schema<'UserDto'>[]>([])
const userId = ref('')
const busy = ref('')
// Läraren som ska ersättas. Då byter väljaren och knappen betydelse från Lägg till till Ersätt.
const replacing = ref<Schema<'CourseTeacherDto'> | null>(null)
// Den sista läraren tas inte bort utan bekräftelse, eftersom kursen då står utan lärare.
const confirmingLast = ref<Schema<'CourseTeacherDto'> | null>(null)
// Samma felruta visar felet från hämtningen och från ändringarna.
const {run: loadUsers, loading, error} = useAsync(fetchUsers)

// Bara användare med rollen Lärare kan väljas. Admin undervisar aldrig (se docs/roller-och-floden.md), och den
// som redan undervisar i kursen kan inte läggas till igen.
const candidates = computed(() => users.value
  .filter((user) => user.role === 'Teacher')
  .filter((user) => !props.teachers.some((teacher) => teacher.userId === user.id))
  .sort((first, second) => first.name.localeCompare(second.name, 'sv')))

watch(open, async (isOpen) => {
  if (!isOpen) {
    return
  }

  userId.value = ''
  replacing.value = null
  confirmingLast.value = null
  const loaded = await loadUsers()
  if (loaded) {
    users.value = loaded
  }
})

async function add() {
  const user = candidates.value.find((found) => found.id === userId.value)
  if (!props.course || !user) {
    return
  }

  const outgoing = replacing.value
  busy.value = 'add'
  error.value = ''
  let current = props.teachers
  try {
    const added = await addCourseTeacher(props.course.id, user.id)
    current = [...current, added]
    userId.value = ''
    emit('changed', props.course.id, current)
    if (outgoing) {
      // Den nya läggs till först, så att kursen aldrig står utan lärare mitt i bytet.
      await deleteTeacher(props.course.id, outgoing)
      current = current.filter((found) => found.userId !== outgoing.userId)
      replacing.value = null
      emit('changed', props.course.id, current)
    }
  } catch (exception) {
    error.value = outgoing && current.some((found) => found.userId === user.id)
      ? `${user.name} har lagts till, men ${outgoing.name} kunde inte tas bort. ${errorMessage(exception)}`
      : `Kunde inte lägga till ${user.name}. ${errorMessage(exception)}`
  } finally {
    busy.value = ''
  }
}

function deleteTeacher(courseId: string, teacher: Schema<'CourseTeacherDto'>) {
  return api.DELETE('/api/courses/{id}/teachers/{userId}', {
    params: {path: {id: courseId, userId: teacher.userId}},
  })
}

function startRemove(teacher: Schema<'CourseTeacherDto'>) {
  if (props.teachers.length === 1) {
    confirmingLast.value = teacher
    return
  }
  void remove(teacher)
}

function startReplace(teacher: Schema<'CourseTeacherDto'>) {
  replacing.value = replacing.value?.userId === teacher.userId
    ? null
    : teacher
  userId.value = ''
}

async function remove(teacher: Schema<'CourseTeacherDto'>) {
  if (!props.course) {
    return
  }

  confirmingLast.value = null
  busy.value = teacher.userId
  error.value = ''
  try {
    await deleteTeacher(props.course.id, teacher)
    if (replacing.value?.userId === teacher.userId) {
      replacing.value = null
    }
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

        <p v-if="sharedWith?.length" class="text-muted-foreground text-sm" data-test="shared-with">
          Delas med {{ sharedWith.join(', ') }}. Ändringar här gäller också {{ sharedWith.length === 1
            ? 'den klassen'
            : 'de klasserna' }}.
        </p>

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
              :variant="replacing?.userId === teacher.userId
                ? 'secondary'
                : 'outline'"
              class="h-8"
              :disabled="busy !== ''"
              :aria-label="`Ersätt ${teacher.name} som lärare`"
              :aria-pressed="replacing?.userId === teacher.userId"
              data-test="replace-teacher"
              @click="startReplace(teacher)"
            >
              Ersätt
            </Button>
            <Button
              type="button"
              variant="outline"
              class="h-8"
              :disabled="busy !== ''"
              :aria-label="`Ta bort ${teacher.name} som lärare`"
              data-test="remove-teacher"
              @click="startRemove(teacher)"
            >
              Ta bort
            </Button>
          </li>
        </ul>

        <div class="flex min-w-0 flex-col gap-1.5">
          <FieldLabel for="add-teacher">
            {{ replacing
              ? `Ersätt ${replacing.name} med`
              : 'Lägg till lärare' }}
          </FieldLabel>
          <div class="flex min-w-0 gap-2">
            <Select v-model="userId" :disabled="loading || busy !== ''">
              <SelectTrigger id="add-teacher" class="w-full min-w-0" data-test="add-teacher-select">
                <SelectValue
                  :placeholder="candidates.length === 0 && !loading
                    ? 'Inga fler lärare finns'
                    : 'Välj lärare…'"
                />
              </SelectTrigger>
              <SelectContent position="popper">
                <SelectItem v-for="user in candidates" :key="user.id" :value="user.id">
                  {{ user.name }} · {{ user.email }}
                </SelectItem>
              </SelectContent>
            </Select>
            <Button
              type="button"
              :disabled="userId === '' || busy !== ''"
              data-test="add-teacher"
              @click="add"
            >
              {{ replacing
                ? 'Ersätt'
                : 'Lägg till' }}
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

  <Dialog :open="confirmingLast !== null" @update:open="(value) => { if (!value) confirmingLast = null }">
    <DialogContent class="grid-cols-[minmax(0,1fr)] sm:max-w-md" data-test="confirm-last-teacher">
      <DialogHeader class="min-w-0">
        <DialogTitle class="break-words">Ta bort {{ confirmingLast?.name }}?</DialogTitle>
        <DialogDescription>
          {{ confirmingLast?.name }} är kursens enda lärare. Utan lärare kan ingen förbereda eller bedöma kursen.
          Vill du byta lärare väljer du Ersätt i stället.
        </DialogDescription>
      </DialogHeader>
      <DialogFooter>
        <Button type="button" variant="outline" @click="confirmingLast = null">Avbryt</Button>
        <Button
          type="button"
          variant="destructive"
          data-test="confirm-remove-last"
          @click="confirmingLast && remove(confirmingLast)"
        >
          Ta bort
        </Button>
      </DialogFooter>
    </DialogContent>
  </Dialog>
</template>
