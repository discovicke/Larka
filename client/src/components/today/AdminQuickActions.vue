<script setup lang="ts">
import {
  PhBookOpen,
  PhChalkboardTeacher,
  PhPlus,
  PhUserGear,
  PhUsersThree,
} from '@phosphor-icons/vue'
import {computed, ref, watch} from 'vue'
import {RouterLink, useRouter} from 'vue-router'

import CourseTeachersDialog from '@/components/groups/CourseTeachersDialog.vue'
import GroupFormDialog from '@/components/groups/GroupFormDialog.vue'
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
import {useCourses} from '@/composables/useCourses'
import {useCurrentUser} from '@/composables/useCurrentUser'
import {api, errorMessage, unwrap, type Schema} from '@/lib/api'
import {fetchGroups} from '@/lib/groupsApi'
import {groupTypeLabels} from '@/lib/groups'

const props = defineProps<{
  coursesWithoutTeacher: Schema<'AdminCourseWithoutTeacherDto'>[]
}>()
const emit = defineEmits<{changed: []}>()

const router = useRouter()
const {me} = useCurrentUser()
const {courses} = useCourses()

const schoolId = computed(() => me.value?.school?.id ?? '')

const groupOpen = ref(false)
const groupKind = ref<'class' | 'group'>('class')

function openGroup(kind: 'class' | 'group') {
  groupKind.value = kind
  groupOpen.value = true
}

function onGroupSaved(group: Schema<'GroupDto'>) {
  const notice = `${group.name} skapades. Lägg till det ${group.type === 'Class'
    ? 'klassen'
    : 'gruppen'} behöver.`
  void router.push({name: 'group', params: {groupId: group.id}, state: {notice}})
}

// En kurs skapas på en klass eller grupp, så snabbåtgärden väljer mål först och öppnar sedan
// den vanliga kursdialogen där.
const coursePickerOpen = ref(false)
const groups = ref<Schema<'GroupDto'>[]>([])
const groupsLoading = ref(false)
const groupsError = ref('')
const selectedGroupId = ref('')

const groupOptions = computed(() => [...groups.value]
  .sort((left, right) => Number(right.type === 'Class') - Number(left.type === 'Class')
    || left.name.localeCompare(right.name, 'sv')))

watch(coursePickerOpen, async (isOpen) => {
  if (!isOpen) {
    return
  }

  selectedGroupId.value = ''
  groupsError.value = ''
  groupsLoading.value = true
  try {
    groups.value = await fetchGroups()
  } catch (exception) {
    groups.value = []
    groupsError.value = errorMessage(exception)
  } finally {
    groupsLoading.value = false
  }
})

function startCourse() {
  const group = groups.value.find((candidate) => candidate.id === selectedGroupId.value)
  if (!group) {
    return
  }

  coursePickerOpen.value = false
  void router.push({name: 'group', params: {groupId: group.id}, query: {newCourse: '1'}})
}

const noTeacherIds = computed(() => new Set(props.coursesWithoutTeacher.map((course) => course.courseId)))
const courseOptions = computed(() => [...courses.value]
  .sort((left, right) => Number(noTeacherIds.value.has(right.id)) - Number(noTeacherIds.value.has(left.id))
    || left.title.localeCompare(right.title, 'sv')))

const teacherPickerOpen = ref(false)
const selectedCourseId = ref('')
const teacherError = ref('')
const teacherBusy = ref(false)
const teacherCourse = ref<Schema<'CourseDto'> | null>(null)
const teacherList = ref<Schema<'CourseTeacherDto'>[]>([])

function openTeacherPicker() {
  selectedCourseId.value = ''
  teacherError.value = ''
  teacherPickerOpen.value = true
}

async function openTeachers() {
  const course = courses.value.find((candidate) => candidate.id === selectedCourseId.value)
  if (!course) {
    return
  }

  teacherBusy.value = true
  teacherError.value = ''
  try {
    const teachers = unwrap(await api.GET('/api/courses/{id}/teachers', {
      params: {path: {id: course.id}},
    }))
    teacherCourse.value = course
    teacherList.value = teachers
    teacherPickerOpen.value = false
  } catch (exception) {
    teacherError.value = errorMessage(exception)
  } finally {
    teacherBusy.value = false
  }
}

function teachersChanged(_courseId: string, teachers: Schema<'CourseTeacherDto'>[]) {
  teacherList.value = teachers
  emit('changed')
}
</script>

<template>
  <section aria-label="Snabbåtgärder" class="flex flex-wrap items-center justify-between gap-2 border-b pb-4" data-test="admin-quick-actions">
    <div class="flex flex-wrap gap-2">
      <Button
        variant="outline"
        class="h-8"
        :disabled="!schoolId"
        data-test="quick-new-class"
        @click="openGroup('class')"
      >
        <PhUsersThree/>
        Ny klass
      </Button>
      <Button
        variant="outline"
        class="h-8"
        :disabled="!schoolId"
        data-test="quick-new-group"
        @click="openGroup('group')"
      >
        <PhPlus/>
        Ny grupp
      </Button>
      <Button
        variant="outline"
        class="h-8"
        data-test="quick-new-course"
        @click="coursePickerOpen = true"
      >
        <PhBookOpen/>
        Skapa kurs
      </Button>
    </div>
    <div class="flex flex-wrap gap-2">
      <Button variant="outline" class="h-8" as-child>
        <RouterLink :to="{name: 'users'}">
          <PhUserGear/>
          Användare
        </RouterLink>
      </Button>
      <Button
        variant="outline"
        class="h-8"
        :disabled="courses.length === 0"
        data-test="quick-assign-teacher"
        @click="openTeacherPicker"
      >
        <PhChalkboardTeacher/>
        Tilldela lärare
      </Button>
    </div>

    <GroupFormDialog
      v-if="schoolId"
      v-model:open="groupOpen"
      :school-id="schoolId"
      :kind="groupKind"
      @saved="onGroupSaved"
    />

    <Dialog v-model:open="coursePickerOpen">
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Skapa kurs</DialogTitle>
          <DialogDescription>
            Välj klassen eller gruppen som ska läsa kursen. Sedan öppnas kursformuläret där du väljer lärare.
          </DialogDescription>
        </DialogHeader>

        <div class="flex flex-col gap-1.5">
          <FieldLabel for="quick-course-group">Klass eller grupp</FieldLabel>
          <Select v-model="selectedGroupId" :disabled="groupsLoading">
            <SelectTrigger id="quick-course-group" class="w-full" data-test="quick-course-group">
              <SelectValue
                :placeholder="groupsLoading
                  ? 'Hämtar…'
                  : groupOptions.length === 0
                    ? 'Det finns inga klasser eller grupper'
                    : 'Välj klass eller grupp…'"
              />
            </SelectTrigger>
            <SelectContent position="popper">
              <SelectItem v-for="group in groupOptions" :key="group.id" :value="group.id">
                {{ group.name }} ({{ groupTypeLabels[group.type] }})
              </SelectItem>
            </SelectContent>
          </Select>
        </div>

        <Alert v-if="groupsError" variant="destructive">
          <AlertDescription>{{ groupsError }}</AlertDescription>
        </Alert>

        <DialogFooter>
          <Button variant="outline" @click="coursePickerOpen = false">Avbryt</Button>
          <Button :disabled="selectedGroupId === ''" data-test="quick-course-continue" @click="startCourse">
            Fortsätt
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>

    <Dialog v-model:open="teacherPickerOpen">
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Tilldela lärare</DialogTitle>
          <DialogDescription>Välj kursen där du vill lägga till, byta eller ta bort lärare.</DialogDescription>
        </DialogHeader>

        <div class="flex flex-col gap-1.5">
          <FieldLabel for="quick-teacher-course">Kurs</FieldLabel>
          <Select v-model="selectedCourseId">
            <SelectTrigger id="quick-teacher-course" class="w-full" data-test="quick-teacher-course">
              <SelectValue placeholder="Välj kurs…"/>
            </SelectTrigger>
            <SelectContent position="popper">
              <SelectItem v-for="course in courseOptions" :key="course.id" :value="course.id">
                {{ course.title }} ({{ course.code }}){{ noTeacherIds.has(course.id)
                  ? ', saknar lärare'
                  : '' }}
              </SelectItem>
            </SelectContent>
          </Select>
        </div>

        <Alert v-if="teacherError" variant="destructive">
          <AlertDescription>{{ teacherError }}</AlertDescription>
        </Alert>

        <DialogFooter>
          <Button variant="outline" @click="teacherPickerOpen = false">Avbryt</Button>
          <Button
            :disabled="selectedCourseId === '' || teacherBusy"
            data-test="quick-teacher-continue"
            @click="openTeachers"
          >
            Öppna
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>

    <CourseTeachersDialog
      :open="teacherCourse !== null"
      :course="teacherCourse"
      :teachers="teacherList"
      @update:open="(value) => { if (!value) teacherCourse = null }"
      @changed="teachersChanged"
    />
  </section>
</template>
