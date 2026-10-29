<script setup lang="ts">
import {computed, ref, watch} from 'vue'
import {useRoute} from 'vue-router'

import {Alert, AlertDescription} from '@/components/ui/alert'
import AddMemberDialog from '@/components/groups/AddMemberDialog.vue'
import LinkCourseDialog from '@/components/groups/LinkCourseDialog.vue'
import RemoveMemberDialog from '@/components/groups/RemoveMemberDialog.vue'
import UnlinkCourseDialog from '@/components/groups/UnlinkCourseDialog.vue'
import {Avatar, AvatarFallback} from '@/components/ui/avatar'
import CourseStatusBadge from '@/components/course/CourseStatusBadge.vue'
import {Button} from '@/components/ui/button'
import {NativeSelect, NativeSelectOption} from '@/components/ui/native-select'
import {Skeleton} from '@/components/ui/skeleton'
import {useCrumbLabels} from '@/composables/useCrumbLabels'
import {useCurrentUser} from '@/composables/useCurrentUser'
import {ApiError, api, errorMessage, unwrap, type Schema} from '@/lib/api'
import {periodLabel} from '@/lib/coursePeriod'
import {courseStatus} from '@/lib/courseStatus'
import {groupRoles as roles} from '@/lib/groupRoles'
import {groupTypeLabels} from '@/lib/groups'
import {initials} from '@/lib/news'
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
const courseToUnlink = ref<Schema<'CourseDto'> | null>(null)
const changingRole = ref('')
const memberError = ref('')
// Byts vid fel, så att listan ritas om med den gamla rollen.
const roleRevision = ref(0)

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
    const params = {params: {path: {id}}}
    const [groupResult, courseResult, memberResult] = await Promise.all([
      api.GET('/api/groups/{id}', params),
      api.GET('/api/groups/{id}/courses', params),
      api.GET('/api/groups/{id}/members', params),
    ])
    if (id !== groupId.value) {
      return
    }
    group.value = unwrap(groupResult)
    setCrumbLabel(`group:${id}`, group.value.name)
    courses.value = courseResult.data ?? []
    members.value = memberResult.data ?? []
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

function addMember(member: Schema<'GroupMemberDto'>) {
  members.value = [...members.value, member]
}

function removeMember(userId: string) {
  members.value = members.value.filter((member) => member.userId !== userId)
}

async function changeRole(member: Schema<'GroupMemberDto'>, role: Schema<'GroupRole'>) {
  if (role === member.role) {
    return
  }

  changingRole.value = member.userId
  memberError.value = ''
  try {
    await api.PUT('/api/groups/{id}/members/{userId}', {
      params: {path: {id: groupId.value, userId: member.userId}},
      body: {role},
    })
    members.value = members.value.map((found) => found.userId === member.userId
      ? {...found, role}
      : found)
  } catch (exception) {
    roleRevision.value++
    memberError.value = `Kunde inte ändra rollen för ${member.name}. ${errorMessage(exception)}`
  } finally {
    changingRole.value = ''
  }
}

function addCourse(course: Schema<'CourseDto'>) {
  courses.value = [...courses.value, course]
}

function removeCourse(courseId: string) {
  courses.value = courses.value.filter((course) => course.id !== courseId)
}

watch(groupId, load, {immediate: true})
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

      <Alert v-if="memberError" variant="destructive">
        <AlertDescription>{{ memberError }}</AlertDescription>
      </Alert>

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
            class="flex items-center gap-3 border-b px-4 py-3.5 last:border-b-0"
            data-test="member-row"
          >
            <Avatar class="size-10">
              <AvatarFallback class="text-[13px] font-semibold">{{ initials(member.name) }}</AvatarFallback>
            </Avatar>
            <span class="font-medium" data-test="member-name">{{ member.name }}</span>
            <div v-if="isAdmin" class="ml-auto flex items-center gap-2">
              <NativeSelect
                :key="`${member.userId}-${member.role}-${roleRevision}`"
                :model-value="member.role"
                :aria-label="`Roll för ${member.name}`"
                :disabled="changingRole === member.userId"
                class="w-32"
                data-test="member-role-select"
                @change="(event: Event) => changeRole(member, (event.target as HTMLSelectElement).value as Schema<'GroupRole'>)"
              >
                <NativeSelectOption v-for="option in roles" :key="option.value" :value="option.value">
                  {{ option.label }}
                </NativeSelectOption>
              </NativeSelect>
              <Button
                variant="outline"
                class="h-8"
                :disabled="changingRole === member.userId"
                data-test="remove-member"
                @click="memberToRemove = member"
              >
                Ta bort
              </Button>
            </div>
          </div>
        </div>
      </section>

      <p v-if="members.length === 0" class="text-muted-foreground text-sm">Gruppen har inga medlemmar ännu.</p>
    </section>

    <section
      v-if="courses.length > 0 || isAdmin"
      aria-labelledby="courses-heading"
      class="flex flex-col gap-2.5"
    >
      <div class="flex flex-wrap items-center justify-between gap-3">
        <h2 id="courses-heading" class="text-lg font-semibold">Kurser</h2>
        <Button
          v-if="isAdmin"
          variant="outline"
          class="h-8"
          data-test="link-course-button"
          @click="linkingCourse = true"
        >
          Koppla kurs
        </Button>
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
            class="flex flex-wrap items-center justify-between gap-x-3 gap-y-1 border-b px-4 py-3 last:border-b-0"
            data-test="course-row"
          >
            <RouterLink
              :to="{name: 'course-content', params: {courseId: course.id}}"
              class="font-medium underline underline-offset-3 decoration-border hover:decoration-foreground"
            >
              {{ course.title }}
            </RouterLink>
            <div class="flex items-center gap-3">
              <span class="font-mono text-[13px]">{{ course.code }}</span>
              <span class="text-muted-foreground text-sm" data-test="course-dates">{{ periodLabel(course.startsOn, course.endsOn) }}</span>
              <CourseStatusBadge :status="courseStatus(course)"/>
              <Button
                v-if="isAdmin"
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
      </section>
    </section>

    <template v-if="isAdmin">
      <AddMemberDialog
        v-model:open="addingMember"
        :group-id="group.id"
        :group-name="group.name"
        :school-id="group.schoolId"
        :member-ids="members.map((member) => member.userId)"
        @added="addMember"
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
