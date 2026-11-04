<script setup lang="ts">
import {computed, ref, watch} from 'vue'
import {useRoute, useRouter} from 'vue-router'

import {PhCaretDown, PhCheckCircle, PhCircle, PhDotsThree} from '@phosphor-icons/vue'

import {Alert, AlertDescription} from '@/components/ui/alert'
import AddMemberDialog from '@/components/groups/AddMemberDialog.vue'
import ChangeRoleDialog from '@/components/groups/ChangeRoleDialog.vue'
import LinkCourseDialog from '@/components/groups/LinkCourseDialog.vue'
import RemoveMemberDialog from '@/components/groups/RemoveMemberDialog.vue'
import UnlinkCourseDialog from '@/components/groups/UnlinkCourseDialog.vue'
import {Avatar, AvatarFallback, AvatarImage} from '@/components/ui/avatar'
import CourseStatusBadge from '@/components/course/CourseStatusBadge.vue'
import CourseTeachersDialog from '@/components/groups/CourseTeachersDialog.vue'
import NewCourseDialog from '@/components/groups/NewCourseDialog.vue'
import StudentOverviewDialog from '@/components/groups/StudentOverviewDialog.vue'
import StatusBadge from '@/components/StatusBadge.vue'
import {Button} from '@/components/ui/button'
import {Collapsible, CollapsibleContent, CollapsibleTrigger} from '@/components/ui/collapsible'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import {Skeleton} from '@/components/ui/skeleton'
import {useCourses} from '@/composables/useCourses'
import {useCrumbLabels} from '@/composables/useCrumbLabels'
import {useCurrentUser} from '@/composables/useCurrentUser'
import {useToast} from '@/composables/useToast'
import {ApiError, api, unwrap, type Schema} from '@/lib/api'
import {periodLabel} from '@/lib/coursePeriod'
import {courseStatus} from '@/lib/courseStatus'
import {groupRoles as roles} from '@/lib/groupRoles'
import {groupTypeLabels} from '@/lib/groups'
import {fetchGroupCourses, fetchGroupMembers} from '@/lib/groupsApi'
import {initials} from '@/lib/person'
import NotFoundView from '@/views/NotFoundView.vue'

const route = useRoute()
const router = useRouter()
const {can} = useCurrentUser()
const {setCrumbLabel} = useCrumbLabels()
const {loadCourses} = useCourses()
const {showToast} = useToast()
const isAdmin = computed(() => can('admin'))
const groupId = computed(() => String(route.params.groupId))

const group = ref<Schema<'GroupDto'> | null>(null)
const courses = ref<Schema<'CourseDto'>[]>([])
const members = ref<Schema<'GroupMemberDto'>[]>([])
const loading = ref(true)
const loadFailed = ref(false)
const notFound = ref(false)
const notice = ref('')

const addingMember = ref(false)
const memberToRemove = ref<Schema<'GroupMemberDto'> | null>(null)
const linkingCourse = ref(false)
const creatingCourse = ref(false)
const courseForTeachers = ref<Schema<'CourseDto'> | null>(null)
// Lärarna per kurs, bara för admin. En kurs som saknas här har inte fått sina lärare hämtade.
const teachersByCourse = ref<Record<string, Schema<'CourseTeacherDto'>[]>>({})
// Namnen på de andra grupperna som läser kursen, så att admin ser att en ändring gäller flera klasser.
const sharedByCourse = ref<Record<string, string[]>>({})
const courseToUnlink = ref<Schema<'CourseDto'> | null>(null)
const memberToChange = ref<Schema<'GroupMemberDto'> | null>(null)
const studentToOverview = ref<Schema<'GroupMemberDto'> | null>(null)
const overviewOpen = ref(false)

// Snabbåtgärden Skapa kurs går hit med ?newCourse=1. När gruppen är hämtad öppnas kursdialogen och
// parametern tas bort, så att bakåtknappen inte öppnar den igen.
watch([() => route.query.newCourse, group, isAdmin], ([newCourse, loadedGroup, admin]) => {
  if (newCourse !== '1' || !loadedGroup || !admin) {
    return
  }
  creatingCourse.value = true
  const query = {...route.query}
  delete query.newCourse
  void router.replace({query})
}, {immediate: true})

const subtitle = computed(() => group.value
  ? groupTypeLabels[group.value.type]
  : '')

const groupNoun = computed(() => group.value?.type === 'Class'
  ? 'Klassen'
  : 'Gruppen')

// Vad som återstår för en ny klass. Listan försvinner när allt är gjort.
const setupSteps = computed(() => {
  if (!isAdmin.value || group.value?.type !== 'Class') {
    return []
  }
  const steps = [
    {label: 'Lägg till kurser', done: courses.value.length > 0},
    {label: 'Lägg in elever', done: members.value.some((member) => member.role === 'Student')},
  ]
  return steps.every((step) => step.done)
    ? []
    : steps
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

// Avslutade kurser är hopfällda tills man öppnar dem. Kurserna finns kvar och går att nå.
const openParts = ref<Record<string, boolean>>({running: true, upcoming: true, finished: false})

// Admin kan öppna en elevs läge i klassens kurser.
function showOverview(member: Schema<'GroupMemberDto'>) {
  studentToOverview.value = member
  overviewOpen.value = true
}

const membersByRole = computed(() => roles
  .map((role) => ({
    ...role,
    members: members.value.filter((member) => member.role === role.value),
  }))
  .filter((role) => role.members.length > 0))

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

  // Lärarna för alla kurser kommer i ett anrop. Svarar det inte visas resten av sidan utan lärarraderna.
  let rows: Schema<'GroupCourseTeachersDto'>[] = []
  try {
    rows = (await api.GET('/api/groups/{id}/course-teachers', {params: {path: {id}}})).data ?? []
  } catch {
    // Raderna visas utan lärare och utan Ändra lärare tills sidan laddas om.
  }
  if (id === groupId.value) {
    teachersByCourse.value = Object.fromEntries(rows.map((row) => [row.courseId, row.teachers]))
    sharedByCourse.value = Object.fromEntries(rows.map((row) => [row.courseId, row.sharedWith]))
  }
}

// Efter Ny kurs hämtas listan igen, så att sidan visar det som faktiskt sparades.
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
  showToast(`${member.name} är tillagd i ${group.value?.name}.`)
}

async function roleChanged() {
  await reloadMembers()
  showToast('Rollen är ändrad.')
}

function removeMember(userId: string) {
  const removed = members.value.find((member) => member.userId === userId)
  if (removed) {
    showToast(`${removed.name} är borttagen ur ${group.value?.name}.`)
  }
  members.value = members.value.filter((member) => member.userId !== userId)
}

// Den kopplade kursens lärare hämtas direkt, så att raden inte står utan dem tills sidan laddas om.
async function addCourse(course: Schema<'CourseDto'>) {
  courses.value = [...courses.value, course]
  showToast(`${course.title} är kopplad till ${group.value?.name}.`)
  await loadTeachers(groupId.value)
}

// Sidomenyn visar skolans kurser, så också den listan hämtas om.
async function courseCreated(message: string) {
  notice.value = message
  await Promise.all([reloadCourses(), loadCourses()])
}

function removeCourse(courseId: string) {
  const removed = courses.value.find((course) => course.id === courseId)
  if (removed) {
    showToast(`${removed.title} är frånkopplad från ${group.value?.name}.`)
  }
  courses.value = courses.value.filter((course) => course.id !== courseId)
}

watch(groupId, () => {
  // Kommer man från Ny klass väntar ett meddelande i historiken. Det tas bort så att det bara visas en gång.
  notice.value = typeof history.state?.notice === 'string'
    ? history.state.notice
    : ''
  if (notice.value) {
    history.replaceState({...history.state, notice: undefined}, '')
  }
  void load()
}, {immediate: true})
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
    <div class="flex flex-wrap items-start justify-between gap-3">
      <div class="flex flex-col gap-1">
        <h1 class="font-heading text-[30px] leading-tight font-semibold">{{ group.name }}</h1>
        <p v-if="subtitle" class="text-muted-foreground" data-test="group-subtitle">{{ subtitle }}</p>
      </div>
      <Button
        v-if="isAdmin"
        as-child
        variant="outline"
        class="h-8"
        data-test="group-analytics"
      >
        <RouterLink :to="{name: 'today', query: {group: group.id}}">
          Följ upp klassen
        </RouterLink>
      </Button>
    </div>

    <Alert v-if="notice" data-test="group-notice">
      <AlertDescription>{{ notice }}</AlertDescription>
    </Alert>

    <section
      v-if="setupSteps.length > 0"
      aria-labelledby="setup-heading"
      class="flex flex-col gap-2.5"
      data-test="group-setup"
    >
      <h2 id="setup-heading" class="text-lg font-semibold">Att ordna</h2>
      <ul class="flex flex-col gap-1">
        <li
          v-for="step in setupSteps"
          :key="step.label"
          class="flex min-h-8 items-center gap-2.5"
          :class="{'text-muted-foreground': step.done}"
          data-test="group-setup-step"
        >
          <PhCheckCircle
            v-if="step.done"
            class="text-success size-5 shrink-0"
            weight="fill"
            aria-label="Klart"
          />
          <PhCircle v-else class="text-muted-foreground size-5 shrink-0" aria-hidden="true"/>
          {{ step.label }}
        </li>
      </ul>
    </section>

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
      <p v-if="courses.length === 0" class="text-muted-foreground text-sm">{{ groupNoun }} har inga kurser ännu.</p>
      <section
        v-for="part in courseGroups"
        :key="part.key"
        :aria-label="part.label"
        class="flex flex-col gap-2.5"
        data-test="course-group"
      >
        <Collapsible v-model:open="openParts[part.key]" class="flex flex-col gap-2.5">
          <CollapsibleTrigger v-if="part.key === 'finished'" as-child>
            <button
              type="button"
              class="text-muted-foreground flex w-fit items-center gap-1.5 text-sm font-medium"
              data-test="finished-courses-toggle"
            >
              <PhCaretDown class="transition-transform" :class="{'-rotate-90': !openParts[part.key]}"/>
              {{ part.label }} ({{ part.courses.length }})
            </button>
          </CollapsibleTrigger>
          <h3 v-else class="text-muted-foreground text-sm font-medium">{{ part.label }}</h3>
          <CollapsibleContent>
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
          </CollapsibleContent>
        </Collapsible>
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
        <h3 class="text-muted-foreground text-sm font-medium">{{ role.label }}</h3>
        <div class="overflow-hidden rounded-[11px] border">
          <div
            v-for="member in role.members"
            :key="member.userId"
            class="flex items-center gap-x-3 border-b px-4 py-2 last:border-b-0"
            data-test="member-row"
          >
            <Avatar class="size-10">
              <AvatarImage
                v-if="member.avatarUrl"
                :src="member.avatarUrl"
                alt=""
                data-test="member-avatar-image"
              />
              <AvatarFallback class="text-[13px] font-semibold">{{ initials(member.name) }}</AvatarFallback>
            </Avatar>
            <span class="font-medium" data-test="member-name">{{ member.name }}</span>
            <div class="ml-auto flex items-center gap-1">
              <Button
                v-if="isAdmin && member.role === 'Student'"
                variant="outline"
                size="sm"
                :aria-label="`Visa läget för ${member.name}`"
                data-test="member-overview"
                @click="showOverview(member)"
              >
                Visa läge
              </Button>
              <DropdownMenu v-if="isAdmin">
                <DropdownMenuTrigger as-child>
                  <Button
                    variant="ghost"
                    size="icon"
                    class="size-11 md:size-8"
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
        </div>
      </section>

      <p v-if="members.length === 0" class="text-muted-foreground text-sm">{{ groupNoun }} har inga medlemmar ännu.</p>
    </section>

    <StudentOverviewDialog
      v-model:open="overviewOpen"
      :group-id="groupId"
      :student="studentToOverview"
    />

    <template v-if="isAdmin">
      <AddMemberDialog
        v-model:open="addingMember"
        :group-id="group.id"
        :group-name="group.name"
        :school-id="group.schoolId"
        :member-ids="members.map((member) => member.userId)"
        @added="addMember"
      />
      <NewCourseDialog
        v-model:open="creatingCourse"
        :group-id="group.id"
        :group-name="group.name"
        :school-id="group.schoolId"
        @created="courseCreated"
      />
      <CourseTeachersDialog
        :open="courseForTeachers !== null"
        :course="courseForTeachers"
        :teachers="courseForTeachers
          ? (teachersByCourse[courseForTeachers.id] ?? [])
          : []"
        :shared-with="courseForTeachers
          ? (sharedByCourse[courseForTeachers.id] ?? [])
          : []"
        @update:open="(value) => { if (!value) courseForTeachers = null }"
        @changed="setTeachers"
      />
      <ChangeRoleDialog
        :open="memberToChange !== null"
        :group-id="group.id"
        :group-name="group.name"
        :member="memberToChange"
        @update:open="(value) => { if (!value) memberToChange = null }"
        @changed="roleChanged"
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
