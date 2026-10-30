<script setup lang="ts">
import {PhDotsThree, PhMagnifyingGlass, PhPencilSimple, PhPlus, PhTrash} from '@phosphor-icons/vue'
import {computed, ref} from 'vue'
import {type RouteLocationRaw, useRoute, useRouter} from 'vue-router'

import CourseStatusBadge from '@/components/course/CourseStatusBadge.vue'
import DeleteCourseDialog from '@/components/courses/DeleteCourseDialog.vue'
import CourseFormDialog from '@/components/courses/CourseFormDialog.vue'
import DeleteGroupDialog from '@/components/groups/DeleteGroupDialog.vue'
import GroupFormDialog from '@/components/groups/GroupFormDialog.vue'
import {Alert, AlertDescription} from '@/components/ui/alert'
import {Button} from '@/components/ui/button'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import {Empty, EmptyDescription, EmptyHeader, EmptyTitle} from '@/components/ui/empty'
import {Input} from '@/components/ui/input'
import {Skeleton} from '@/components/ui/skeleton'
import {useCourses} from '@/composables/useCourses'
import {useCurrentUser} from '@/composables/useCurrentUser'
import {api, type Schema} from '@/lib/api'
import {periodLabel} from '@/lib/coursePeriod'
import {type CourseStatus, courseStatus} from '@/lib/courseStatus'
import {groupTypeLabels} from '@/lib/groups'
import {fetchMe} from '@/lib/me'

type Group = Schema<'GroupDto'>
type Course = Schema<'CourseDto'>

interface GroupRow {
  key: string
  kind: 'school' | 'group' | 'course'
  name: string
  code: string
  type: string
  period: string
  status: CourseStatus | null
  to?: RouteLocationRaw
  group?: Group
  course?: Course
}

type Filter = 'all' | 'groups' | 'courses'

const filters: {value: Filter, label: string}[] = [
  {value: 'all', label: 'Alla'},
  {value: 'groups', label: 'Grupper'},
  {value: 'courses', label: 'Kurser'},
]

const route = useRoute()
const router = useRouter()
const {courses, status, addCourse, updateCourse, removeCourse} = useCourses()
const {can} = useCurrentUser()
// Bara admin skapar, ändrar och tar bort. Övriga ser sidan som en vanlig lista.
const isAdmin = computed(() => can('admin'))

// Användarens egen skola, från /api/me. Grupper och kurser är redan avgränsade till den av API:et.
const school = ref<Schema<'SchoolRefDto'> | null>(null)
const groups = ref<Group[]>([])
const loading = ref(true)
const error = ref('')
// Filtret ligger i adressen (?filter=courses) så att andra sidor kan länka till en viss lista.
const filter = computed<Filter>(() => {
  const value = route.query.filter
  return value === 'groups' || value === 'courses'
    ? value
    : 'all'
})
const search = ref('')

const isLoading = computed(() => loading.value || status.value === 'loading')

const rows = computed<GroupRow[]>(() => {
  const schoolRows: GroupRow[] = []
  if (school.value) {
    schoolRows.push({
      key: `school-${school.value.id}`,
      kind: 'school',
      name: school.value.name,
      code: '–',
      type: 'Skola',
      period: '–',
      status: null,
    })
  }

  const groupRows: GroupRow[] = [...groups.value]
    .sort((left, right) => left.name.localeCompare(right.name, 'sv'))
    .map((group) => ({
      key: `group-${group.id}`,
      kind: 'group',
      name: group.name,
      code: '–',
      type: groupTypeLabels[group.type],
      period: group.term
        ? group.type === 'Class'
          ? `Startade ${group.term}`
          : group.term
        : '–',
      status: null,
      to: {name: 'group', params: {groupId: group.id}},
      group,
    }))

  // Pågående kurser först, sedan kommande och sist avslutade. Kurser utan datum hamnar bland de pågående.
  const statusOrder: Record<CourseStatus, number> = {running: 0, upcoming: 1, finished: 2}
  const courseRows: GroupRow[] = [...courses.value]
    .map((course) => ({course, status: courseStatus(course)}))
    .sort((left, right) => (statusOrder[left.status ?? 'running'] - statusOrder[right.status ?? 'running'])
      || left.course.title.localeCompare(right.course.title, 'sv'))
    .map(({course, status}) => ({
      key: `course-${course.id}`,
      kind: 'course',
      name: course.title,
      code: course.code,
      type: 'Kurs',
      // Mockupen skriver "från 16 nov 2026" med litet f.
      period: periodLabel(course.startsOn, course.endsOn).replace(/^(Från|Till)/, (word) => word.toLowerCase()) || '–',
      status,
      to: {name: 'course-content', params: {courseId: course.id}},
      course,
    }))

  return [...schoolRows, ...groupRows, ...courseRows]
})

const visibleRows = computed(() => {
  const text = search.value.trim().toLowerCase()
  return rows.value.filter((row) => {
    if (filter.value === 'groups' && row.kind === 'course') {
      return false
    }
    if (filter.value === 'courses' && row.kind !== 'course') {
      return false
    }
    return !text
      || row.name.toLowerCase().includes(text)
      || row.code.toLowerCase().includes(text)
  })
})

// Tomt läge med skapa-knappen, för den lista som filtret visar.
const emptyKind = computed<'group' | 'course' | null>(() => {
  if (!isAdmin.value) {
    return null
  }
  if (filter.value === 'courses') {
    return courses.value.length === 0
      ? 'course'
      : null
  }
  return groups.value.length === 0
    ? 'group'
    : null
})

const showTable = computed(() => !(emptyKind.value && visibleRows.value.length === 0))

const groupFormOpen = ref(false)
const editingGroup = ref<Group>()
const deleteGroupOpen = ref(false)
const deletingGroup = ref<Group>()
const courseFormOpen = ref(false)
const editingCourse = ref<Course>()
const deleteCourseOpen = ref(false)
const deletingCourse = ref<Course>()

function openCreateGroup() {
  editingGroup.value = undefined
  groupFormOpen.value = true
}

function openCreateCourse() {
  editingCourse.value = undefined
  courseFormOpen.value = true
}

function openEdit(row: GroupRow) {
  if (row.group) {
    editingGroup.value = row.group
    groupFormOpen.value = true
  } else if (row.course) {
    editingCourse.value = row.course
    courseFormOpen.value = true
  }
}

function openDelete(row: GroupRow) {
  if (row.group) {
    deletingGroup.value = row.group
    deleteGroupOpen.value = true
  } else if (row.course) {
    deletingCourse.value = row.course
    deleteCourseOpen.value = true
  }
}

// Listorna uppdateras direkt med svaret, utan att hämtas om.
function onGroupSaved(saved: Group) {
  groups.value = groups.value.some((group) => group.id === saved.id)
    ? groups.value.map((group) => group.id === saved.id
        ? saved
        : group)
    : [...groups.value, saved]
}

function onGroupDeleted(removed: Group) {
  groups.value = groups.value.filter((group) => group.id !== removed.id)
}

function onCourseSaved(saved: Course) {
  if (courses.value.some((course) => course.id === saved.id)) {
    updateCourse(saved)
  } else {
    addCourse(saved)
  }
}

function setFilter(value: Filter) {
  void router.replace({query: {...route.query, filter: value === 'all'
    ? undefined
    : value}})
}

function tabClass(value: Filter) {
  return filter.value === value
    ? 'bg-background shadow-sm font-medium'
    : 'text-muted-foreground'
}

function rowBorder(index: number) {
  return index < visibleRows.value.length - 1
    ? 'border-b'
    : ''
}

async function load() {
  loading.value = true
  error.value = ''
  try {
    const [me, groupResult] = await Promise.all([
      fetchMe(),
      api.GET('/api/groups'),
    ])
    school.value = me.school
    groups.value = groupResult.data ?? []
  } catch {
    error.value = 'Kunde inte hämta grupperna.'
  } finally {
    loading.value = false
  }
}

load()
</script>

<template>
  <div class="mx-auto flex w-full max-w-[1120px] flex-col gap-5">
    <div class="flex flex-wrap items-end justify-between gap-3">
      <div class="flex flex-col gap-1">
        <h1 class="font-heading text-[30px] leading-tight font-semibold">Alla grupper</h1>
        <p class="text-muted-foreground">Din skola, dina grupper och skolans kurser</p>
      </div>

      <div class="flex flex-wrap items-center gap-2">
        <div role="tablist" class="bg-muted flex rounded-[7px] p-[3px]">
          <button
            v-for="tab in filters"
            :key="tab.value"
            type="button"
            role="tab"
            :aria-selected="filter === tab.value"
            class="rounded-[5px] px-2.5 py-1 text-[13px]"
            :class="tabClass(tab.value)"
            @click="setFilter(tab.value)"
          >
            {{ tab.label }}
          </button>
        </div>

        <div class="relative w-full sm:w-[220px]">
          <label for="group-search" class="sr-only">Sök grupp</label>
          <PhMagnifyingGlass class="text-muted-foreground pointer-events-none absolute top-1/2 left-2.5 size-4 -translate-y-1/2"/>
          <Input
            id="group-search"
            v-model="search"
            class="pl-8"
            placeholder="Sök…"
          />
        </div>

        <DropdownMenu v-if="isAdmin">
          <DropdownMenuTrigger as-child>
            <Button :disabled="!school" data-test="new-menu">
              <PhPlus/>
              Ny
            </Button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end" class="min-w-48">
            <DropdownMenuItem data-test="new-group" @select="openCreateGroup">
              Ny klass eller grupp
            </DropdownMenuItem>
            <DropdownMenuItem data-test="new-course" @select="openCreateCourse">
              Ny kurs
            </DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>
      </div>
    </div>

    <Alert v-if="error" variant="destructive">
      <AlertDescription>{{ error }}</AlertDescription>
    </Alert>

    <div v-else-if="isLoading" class="flex flex-col gap-2">
      <Skeleton v-for="i in 4" :key="i" class="h-12 w-full"/>
    </div>

    <Empty v-else-if="emptyKind" class="border border-dashed" data-test="groups-empty">
      <EmptyHeader>
        <EmptyTitle>
          {{ emptyKind === 'group'
            ? 'Inga klasser eller grupper ännu'
            : 'Inga kurser ännu' }}
        </EmptyTitle>
        <EmptyDescription>
          {{ emptyKind === 'group'
            ? 'Skapa en klass eller grupp, sedan kan du lägga till medlemmar och koppla kurser.'
            : 'Skapa en kurs, sedan kan du koppla den till grupper.' }}
        </EmptyDescription>
      </EmptyHeader>
      <Button
        v-if="emptyKind === 'group'"
        data-test="empty-new-group"
        @click="openCreateGroup"
      >
        <PhPlus/>
        Skapa skolans första klass
      </Button>
      <Button v-else data-test="empty-new-course" @click="openCreateCourse">
        <PhPlus/>
        Skapa skolans första kurs
      </Button>
    </Empty>

    <section v-if="!error && !isLoading && showTable" aria-label="Grupper" class="bg-card overflow-hidden rounded-[11px] border">
      <div class="overflow-x-auto">
        <table class="w-full border-collapse max-md:block md:min-w-[680px]">
          <thead class="max-md:hidden">
            <tr class="bg-muted/50 text-muted-foreground text-left text-[13px]">
              <th scope="col" class="border-b px-4 py-2.5 font-medium">Grupp</th>
              <th scope="col" class="border-b px-3 py-2.5 font-medium">Kod</th>
              <th scope="col" class="border-b px-3 py-2.5 font-medium">Typ</th>
              <th scope="col" class="border-b px-3 py-2.5 font-medium">Status</th>
              <th scope="col" class="border-b px-4 py-2.5 font-medium">Period</th>
              <th v-if="isAdmin" scope="col" class="border-b px-4 py-2.5 text-right font-medium">
                <span class="sr-only">Åtgärder</span>
              </th>
            </tr>
          </thead>
          <tbody class="max-md:block">
            <tr
              v-for="(row, index) in visibleRows"
              :key="row.key"
              class="max-md:relative max-md:flex max-md:flex-wrap max-md:items-center max-md:gap-x-3 max-md:gap-y-1 max-md:px-4 max-md:py-3"
              :class="index < visibleRows.length - 1
                ? 'max-md:border-b'
                : ''"
              data-test="group-row"
            >
              <td class="px-4 py-3 max-md:w-full max-md:border-b-0 max-md:p-0 max-md:pr-10" :class="rowBorder(index)">
                <RouterLink
                  v-if="row.to"
                  :to="row.to"
                  class="font-medium underline underline-offset-3 decoration-border hover:decoration-foreground"
                >
                  {{ row.name }}
                </RouterLink>
                <span v-else class="font-medium">{{ row.name }}</span>
              </td>
              <td
                class="px-3 py-3 font-mono text-[13px] max-md:border-b-0 max-md:p-0 max-md:text-xs"
                :class="[rowBorder(index), {'text-muted-foreground max-md:hidden': row.code === '–'}]"
              >
                {{ row.code }}
              </td>
              <td class="px-3 py-3 max-md:border-b-0 max-md:p-0 max-md:text-[13px]" :class="rowBorder(index)">{{ row.type }}</td>
              <td
                class="px-3 py-3 max-md:border-b-0 max-md:p-0"
                :class="[rowBorder(index), {'max-md:hidden': !row.status}]"
              >
                <CourseStatusBadge v-if="row.status" :status="row.status"/>
                <span v-else class="text-muted-foreground">–</span>
              </td>
              <td
                class="text-muted-foreground px-4 py-3 max-md:border-b-0 max-md:p-0 max-md:text-[13px]"
                :class="[rowBorder(index), {'max-md:hidden': row.period === '–'}]"
              >
                {{ row.period }}
              </td>
              <td v-if="isAdmin" class="w-12 px-3 py-1.5 text-right max-md:absolute max-md:top-2 max-md:right-2 max-md:w-auto max-md:border-b-0 max-md:p-0" :class="rowBorder(index)">
                <DropdownMenu v-if="row.group || row.course">
                  <DropdownMenuTrigger as-child>
                    <Button
                      variant="ghost"
                      size="icon-sm"
                      :aria-label="`Åtgärder för ${row.name}`"
                      data-test="row-actions"
                    >
                      <PhDotsThree/>
                    </Button>
                  </DropdownMenuTrigger>
                  <DropdownMenuContent align="end" class="min-w-40">
                    <DropdownMenuItem data-test="row-edit" @select="openEdit(row)">
                      <PhPencilSimple/>
                      Redigera
                    </DropdownMenuItem>
                    <DropdownMenuItem variant="destructive" data-test="row-delete" @select="openDelete(row)">
                      <PhTrash/>
                      Ta bort
                    </DropdownMenuItem>
                  </DropdownMenuContent>
                </DropdownMenu>
              </td>
            </tr>
            <tr v-if="visibleRows.length === 0">
              <td
                :colspan="isAdmin
                  ? 6
                  : 5"
                class="text-muted-foreground px-4 py-6 text-center"
              >
                Inga grupper matchar sökningen.
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </section>

    <p v-if="!error && !isLoading && showTable && filter !== 'groups'" class="text-muted-foreground text-[13px]">
      Avslutade kurser finns kvar här med material, betyg och återkoppling.
    </p>

    <template v-if="isAdmin">
      <GroupFormDialog
        v-model:open="groupFormOpen"
        :school-id="school?.id ?? ''"
        :group="editingGroup"
        @saved="onGroupSaved"
      />
      <DeleteGroupDialog
        v-if="deletingGroup"
        v-model:open="deleteGroupOpen"
        :group="deletingGroup"
        @deleted="onGroupDeleted"
      />
      <CourseFormDialog
        v-model:open="courseFormOpen"
        :school-id="school?.id ?? ''"
        :course="editingCourse"
        @saved="onCourseSaved"
      />
      <DeleteCourseDialog
        v-if="deletingCourse"
        v-model:open="deleteCourseOpen"
        :course="deletingCourse"
        @deleted="(removed) => removeCourse(removed.id)"
      />
    </template>
  </div>
</template>
