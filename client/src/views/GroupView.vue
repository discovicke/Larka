<script setup lang="ts">
import {computed, ref, watch} from 'vue'
import {useRoute} from 'vue-router'

import {PhDotsThree} from '@phosphor-icons/vue'

import {Alert, AlertDescription} from '@/components/ui/alert'
import AddMemberDialog from '@/components/groups/AddMemberDialog.vue'
import ChangeRoleDialog from '@/components/groups/ChangeRoleDialog.vue'
import LinkCourseDialog from '@/components/groups/LinkCourseDialog.vue'
import RemoveMemberDialog from '@/components/groups/RemoveMemberDialog.vue'
import UnlinkCourseDialog from '@/components/groups/UnlinkCourseDialog.vue'
import {Avatar, AvatarFallback} from '@/components/ui/avatar'
import CourseStatusBadge from '@/components/course/CourseStatusBadge.vue'
import CourseTeachersDialog from '@/components/groups/CourseTeachersDialog.vue'
import ImportStudentsDialog from '@/components/groups/ImportStudentsDialog.vue'
import NewCourseDialog from '@/components/groups/NewCourseDialog.vue'
import SetMentorDialog from '@/components/groups/SetMentorDialog.vue'
import StatusBadge from '@/components/StatusBadge.vue'
import {Button} from '@/components/ui/button'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import {Skeleton} from '@/components/ui/skeleton'
import {useCrumbLabels} from '@/composables/useCrumbLabels'
import {useCurrentUser} from '@/composables/useCurrentUser'
import {ApiError, api, unwrap, type Schema} from '@/lib/api'
import {periodLabel} from '@/lib/coursePeriod'
import {courseStatus} from '@/lib/courseStatus'
import {groupRoles as roles} from '@/lib/groupRoles'
import {groupTypeLabels} from '@/lib/groups'
import {fetchGroupCourses, fetchGroupMembers} from '@/lib/groupsApi'
import {initials} from '@/lib/person'
import NotFoundView from '@/views/NotFoundView.vue'

const route = useRoute()
const {can} = useCurrentUser()
const {setCrumbLabel} = useCrumbLabels()
const isAdmin = computed(() => can('admin'))
const groupId = computed(() => String(route.params.groupId))

const group = ref<Schema<'GroupDto'> | null>(null)
const courses = ref<Schema<'CourseDto'>[]>([])
const members = ref<Schema<'GroupMemberDto'>[]>([])
const loading = ref(true)
const loadFailed = ref(false)
const notFound = ref(false)

const addingMember = ref(false)
const memberToRemove = ref<Schema<'GroupMemberDto'> | null>(null)
const linkingCourse = ref(false)
const creatingCourse = ref(false)
const importingStudents = ref(false)
const settingMentor = ref(false)
const courseForTeachers = ref<Schema<'CourseDto'> | null>(null)
// Lärarna per kurs, bara för admin. En kurs som saknas här har inte fått sina lärare hämtade.
const teachersByCourse = ref<Record<string, Schema<'CourseTeacherDto'>[]>>({})
const courseToUnlink = ref<Schema<'CourseDto'> | null>(null)
const memberToChange = ref<Schema<'GroupMemberDto'> | null>(null)

const subtitle = computed(() => {
  if (!group.value) {
    return ''
  }
  // En klass är en årskull: terminen är när den startade, inte en period.
  const term = group.value.term && group.value.type === 'Class'
    ? `startade ${group.value.term}`
    : group.value.term
  return [groupTypeLabels[group.value.type], term]
    .filter((part) => part !== '')
    .join(' · ')
})

// Kurser utan datum räknas som pågående, så de syns överst i stället för att gömmas.
const courseGroups = computed(() => {
  const groups: {key: string, label: string, courses: Schema<'CourseDto'>[]}[] = [
    {key: 'running', label: 'Pågår', courses: []},
    {key: 'upcoming', label: 'Kommande', courses: []},
    {key: 'finished', label: 'Avslutade', courses: []},
  ]
  const byStart = (first: Schema<'CourseDto'>, second: Schema<'CourseDto'>) =>
    (first.startsOn ?? first.endsOn ?? '').localeCompare(second.startsOn ?? second.endsOn ?? '')
  for (const course of [...courses.value].sort(byStart)) {
    const status = courseStatus(course) ?? 'running'
    groups.find((found) => found.key === status)?.courses.push(course)
  }
  groups[2].courses.reverse()
  return groups.filter((found) => found.courses.length > 0)
})

// Mentor är en roll i klassen. Admin ser den alltid på en klass, också utan mentor, för att kunna utse en.
const canSetMentor = computed(() => isAdmin.value && group.value?.type === 'Class')

const membersByRole = computed(() => roles
  .map((role) => ({
    ...role,
    members: members.value.filter((member) => member.role === role.value),
  }))
  .filter((role) => role.members.length > 0 || (role.value === 'Mentor' && canSetMentor.value)))

async function load() {
  const id = groupId.value
  loading.value = true
  loadFailed.value = false
  notFound.value = false
  group.value = null
  try {
    const [groupResult, loadedCourses, loadedMembers] = await Promise.all([
      api.GET('/api/groups/{id}', {params: {path: {id}}}),
      fetchGroupCourses(id),
      fetchGroupMembers(id),
    ])
    if (id !== groupId.value) {
      return
    }
    group.value = unwrap(groupResult)
    setCrumbLabel(`group:${id}`, group.value.name)
    courses.value = loadedCourses
    members.value = loadedMembers
    await loadTeachers(id)
  } catch (exception) {
    if (id !== groupId.value) {
      return
    }
    if (exception instanceof ApiError && exception.status === 404) {
      notFound.value = true
    } else {
      loadFailed.value = true
    }
  } finally {
    if (id === groupId.value) {
      loading.value = false
    }
  }
}

async function loadTeachers(id: string) {
  if (!isAdmin.value) {
    return
  }

  // Ett kurs som inte svarar hoppas över, så att resten av sidan visas.
  const results = await Promise.all(courses.value.map(async (course) => {
    try {
      const result = await api.GET('/api/courses/{id}/teachers', {params: {path: {id: course.id}}})
      return [course.id, result.data] as const
    } catch {
      return [course.id, undefined] as const
    }
  }))
  if (id === groupId.value) {
    const found: Record<string, Schema<'CourseTeacherDto'>[]> = {}
    for (const [courseId, teachers] of results) {
      if (teachers) {
        found[courseId] = teachers
      }
    }
    teachersByCourse.value = found
  }
}

// Efter Ny kurs och Byt mentor hämtas listorna igen, så att sidan visar det som faktiskt sparades.
async function reloadCourses() {
  const id = groupId.value
  try {
    const loaded = await fetchGroupCourses(id)
    if (id === groupId.value) {
      courses.value = loaded
      await loadTeachers(id)
    }
  } catch {
    // Listan är kvar som den var. Nästa besök hämtar den igen.
  }
}

async function reloadMembers() {
  const id = groupId.value
  try {
    const loaded = await fetchGroupMembers(id)
    if (id === groupId.value) {
      members.value = loaded
    }
  } catch {
    // Listan är kvar som den var. Nästa besök hämtar den igen.
  }
}

function setTeachers(courseId: string, teachers: Schema<'CourseTeacherDto'>[]) {
  teachersByCourse.value = {...teachersByCourse.value, [courseId]: teachers}
}

function addMember(member: Schema<'GroupMemberDto'>) {
  members.value = [...members.value, member]
}

function addMembers(added: Schema<'GroupMemberDto'>[]) {
  members.value = [...members.value, ...added]
}

function pasteStudents() {
  addingMember.value = false
  importingStudents.value = true
}

function removeMember(userId: string) {
  members.value = members.value.filter((member) => member.userId !== userId)
}

function addCourse(course: Schema<'CourseDto'>) {
  courses.value = [...courses.value, course]
}

function removeCourse(courseId: string) {
  courses.value = courses.value.filter((course) => course.id !== courseId)
}

watch(groupId, load, {immediate: true})
// Rollen kan komma efter sidan, och då hämtas lärarna först nu.
watch(isAdmin, (admin) => {
  if (admin && !loading.value) {
    loadTeachers(groupId.value)
  }
})
</script>

<template>
  <Alert v-if="loadFailed" variant="destructive">
    <AlertDescription>Kunde inte hämta gruppen.</AlertDescription>
  </Alert>

  <div v-else-if="loading" class="flex flex-col gap-2">
    <Skeleton class="h-10 w-64"/>
    <Skeleton class="h-20 w-full rounded-[11px]"/>
  </div>

  <NotFoundView v-else-if="notFound || !group"/>

  <div v-else class="mx-auto flex w-full max-w-[1120px] flex-col gap-6">
    <div class="flex flex-col gap-1">
      <h1 class="font-heading text-[30px] leading-tight font-semibold">{{ group.name }}</h1>
      <p v-if="subtitle" class="text-muted-foreground" data-test="group-subtitle">{{ subtitle }}</p>
    </div>

    <section
      v-if="courses.length > 0 || isAdmin"
      aria-labelledby="courses-heading"
      class="flex flex-col gap-2.5"
    >
      <div class="flex flex-wrap items-center justify-between gap-3">
        <h2 id="courses-heading" class="text-lg font-semibold">Kurser</h2>
        <div v-if="isAdmin" class="flex items-center gap-2">
          <Button
            variant="outline"
            class="h-8"
            data-test="link-course-button"
            @click="linkingCourse = true"
          >
            Koppla kurs
          </Button>
          <Button
            class="h-8"
            data-test="new-course-button"
            @click="creatingCourse = true"
          >
            Ny kurs
          </Button>
        </div>
      </div>
      <p v-if="courses.length === 0" class="text-muted-foreground text-sm">Gruppen har inga kurser ännu.</p>
      <section
        v-for="part in courseGroups"
        :key="part.key"
        :aria-label="part.label"
        class="flex flex-col gap-2.5"
        data-test="course-group"
      >
        <h3 class="text-muted-foreground text-sm font-medium">{{ part.label }}</h3>
        <div class="overflow-hidden rounded-[11px] border">
          <div
            v-for="course in part.courses"
            :key="course.id"
            class="flex flex-col gap-2 border-b px-4 py-3 last:border-b-0"
            data-test="course-row"
          >
            <div class="flex flex-wrap items-center gap-x-3 gap-y-1.5">
              <RouterLink
                :to="{name: 'course-content', params: {courseId: course.id}}"
                class="mr-auto font-medium underline underline-offset-3 decoration-border hover:decoration-foreground"
              >
                {{ course.title }}
              </RouterLink>
              <div class="flex w-full flex-wrap items-center gap-x-3 gap-y-1 md:w-auto">
                <span class="font-mono text-[13px]">{{ course.code }}</span>
                <span class="text-muted-foreground text-sm" data-test="course-dates">{{ periodLabel(course.startsOn, course.endsOn) }}</span>
                <CourseStatusBadge :status="courseStatus(course)"/>
              </div>
            </div>
            <div
              v-if="isAdmin"
              class="flex flex-wrap items-center justify-between gap-x-3 gap-y-2"
              data-test="course-teachers"
            >
              <template v-if="teachersByCourse[course.id]">
                <StatusBadge
                  v-if="teachersByCourse[course.id].length === 0"
                  status="course.noTeacher"
                />
                <span v-else class="text-sm" data-test="course-teacher-names">
                  <span class="text-muted-foreground">Lärare: </span>{{ teachersByCourse[course.id].map((teacher) => teacher.name).join(', ') }}
                </span>
              </template>
              <span v-else/>
              <div class="flex items-center gap-2">
                <Button
                  v-if="teachersByCourse[course.id]"
                  variant="outline"
                  class="h-8"
                  data-test="edit-teachers"
                  @click="courseForTeachers = course"
                >
                  Ändra lärare
                </Button>
                <Button
                  variant="outline"
                  class="h-8"
                  data-test="unlink-course"
                  @click="courseToUnlink = course"
                >
                  Koppla bort
                </Button>
              </div>
            </div>
          </div>
        </div>
      </section>
    </section>

    <section aria-labelledby="members-heading" class="flex flex-col gap-4">
      <div class="flex flex-wrap items-center justify-between gap-3">
        <h2 id="members-heading" class="text-lg font-semibold">Medlemmar</h2>
        <Button
          v-if="isAdmin"
          variant="outline"
          class="h-8"
          data-test="add-member"
          @click="addingMember = true"
        >
          Lägg till medlem
        </Button>
      </div>

      <section
        v-for="role in membersByRole"
        :key="role.value"
        :aria-label="role.label"
        class="flex flex-col gap-2.5"
        data-test="member-role"
      >
        <div class="flex items-center justify-between gap-3">
          <h3 class="text-muted-foreground text-sm font-medium">{{ role.label }}</h3>
          <Button
            v-if="role.value === 'Mentor' && canSetMentor"
            variant="outline"
            class="h-8"
            data-test="set-mentor"
            @click="settingMentor = true"
          >
            {{ role.members.length > 0
              ? 'Byt mentor'
              : 'Utse mentor' }}
          </Button>
        </div>
        <p v-if="role.members.length === 0" class="text-muted-foreground text-sm" data-test="no-mentor">
          Klassen har ingen mentor ännu.
        </p>
        <div v-else class="overflow-hidden rounded-[11px] border">
          <div
            v-for="member in role.members"
            :key="member.userId"
            class="flex items-center gap-x-3 border-b px-4 py-2 last:border-b-0"
            data-test="member-row"
          >
            <Avatar class="size-10">
              <AvatarFallback class="text-[13px] font-semibold">{{ initials(member.name) }}</AvatarFallback>
            </Avatar>
            <span class="font-medium" data-test="member-name">{{ member.name }}</span>
            <DropdownMenu v-if="isAdmin">
              <DropdownMenuTrigger as-child>
                <Button
                  variant="ghost"
                  size="icon"
                  class="ml-auto size-11 md:size-8"
                  :aria-label="`Fler val för ${member.name}`"
                  data-test="member-menu"
                >
                  <PhDotsThree/>
                </Button>
              </DropdownMenuTrigger>
              <DropdownMenuContent align="end">
                <DropdownMenuItem data-test="change-member-role" @select="memberToChange = member">
                  Ändra roll
                </DropdownMenuItem>
                <DropdownMenuItem
                  variant="destructive"
                  data-test="remove-member"
                  @select="memberToRemove = member"
                >
                  Ta bort
                </DropdownMenuItem>
              </DropdownMenuContent>
            </DropdownMenu>
          </div>
        </div>
      </section>

      <p v-if="members.length === 0" class="text-muted-foreground text-sm">Gruppen har inga medlemmar ännu.</p>
    </section>

    <template v-if="isAdmin">
      <AddMemberDialog
        v-model:open="addingMember"
        :group-id="group.id"
        :group-name="group.name"
        :school-id="group.schoolId"
        :member-ids="members.map((member) => member.userId)"
        @added="addMember"
        @paste="pasteStudents"
      />
      <ImportStudentsDialog
        v-model:open="importingStudents"
        :group-id="group.id"
        :group-name="group.name"
        :member-ids="members.map((member) => member.userId)"
        @added="addMembers"
      />
      <SetMentorDialog
        v-model:open="settingMentor"
        :group-id="group.id"
        :group-name="group.name"
        :members="members"
        @changed="reloadMembers"
      />
      <NewCourseDialog
        v-model:open="creatingCourse"
        :group-id="group.id"
        :group-name="group.name"
        :school-id="group.schoolId"
        @created="reloadCourses"
      />
      <CourseTeachersDialog
        :open="courseForTeachers !== null"
        :course="courseForTeachers"
        :teachers="courseForTeachers
          ? (teachersByCourse[courseForTeachers.id] ?? [])
          : []"
        @update:open="(value) => { if (!value) courseForTeachers = null }"
        @changed="setTeachers"
      />
      <ChangeRoleDialog
        :open="memberToChange !== null"
        :group-id="group.id"
        :group-name="group.name"
        :member="memberToChange"
        :members="members"
        @update:open="(value) => { if (!value) memberToChange = null }"
        @changed="reloadMembers"
      />
      <RemoveMemberDialog
        :open="memberToRemove !== null"
        :group-id="group.id"
        :group-name="group.name"
        :school-id="group.schoolId"
        :member="memberToRemove"
        @update:open="(value) => { if (!value) memberToRemove = null }"
        @removed="removeMember"
      />
      <LinkCourseDialog
        v-model:open="linkingCourse"
        :group-id="group.id"
        :group-name="group.name"
        :school-id="group.schoolId"
        :linked-ids="courses.map((course) => course.id)"
        @linked="addCourse"
      />
      <UnlinkCourseDialog
        :open="courseToUnlink !== null"
        :group-id="group.id"
        :group-name="group.name"
        :school-id="group.schoolId"
        :course="courseToUnlink"
        @update:open="(value) => { if (!value) courseToUnlink = null }"
        @unlinked="removeCourse"
      />
    </template>
  </div>
</template>
