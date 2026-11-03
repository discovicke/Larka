<script setup lang="ts">
import {PhCaretDown, PhMagnifyingGlass, PhPlus} from '@phosphor-icons/vue'
import {computed, ref, watch} from 'vue'
import {useRoute, useRouter} from 'vue-router'

import DeleteCourseDialog from '@/components/courses/DeleteCourseDialog.vue'
import CourseFormDialog from '@/components/courses/CourseFormDialog.vue'
import DeleteGroupDialog from '@/components/groups/DeleteGroupDialog.vue'
import GroupFormDialog from '@/components/groups/GroupFormDialog.vue'
import GroupsTable from '@/components/groups/GroupsTable.vue'
import {Alert, AlertDescription} from '@/components/ui/alert'
import {Button} from '@/components/ui/button'
import {Collapsible, CollapsibleContent, CollapsibleTrigger} from '@/components/ui/collapsible'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import {Empty, EmptyDescription, EmptyHeader, EmptyTitle} from '@/components/ui/empty'
import {Input} from '@/components/ui/input'
import {Skeleton} from '@/components/ui/skeleton'
import {useAsync} from '@/composables/useAsync'
import {useCourses} from '@/composables/useCourses'
import {useCurrentUser} from '@/composables/useCurrentUser'
import {type Schema} from '@/lib/api'
import {periodLabel} from '@/lib/coursePeriod'
import {type CourseStatus, courseStatus, isGroupFinished} from '@/lib/courseStatus'
import {type GroupRow} from '@/lib/groupRows'
import {groupTypeLabels} from '@/lib/groups'
import {fetchGroups} from '@/lib/groupsApi'
import {fetchMe} from '@/lib/me'

type Group = Schema<'GroupDto'>
type Course = Schema<'CourseDto'>

type Filter = 'all' | 'groups' | 'courses'

const filters: {value: Filter, label: string}[] = [
  {value: 'all', label: 'Alla'},
  {value: 'groups', label: 'Grupper'},
  {value: 'courses', label: 'Kurser'},
]

const route = useRoute()
const router = useRouter()
const {courses, status, updateCourse, removeCourse} = useCourses()
const {can} = useCurrentUser()
// Bara admin skapar, ändrar och tar bort. Övriga ser sidan som en vanlig lista.
const isAdmin = computed(() => can('admin'))

// Användarens egen skola, från /api/me. Grupper och kurser är redan avgränsade till den av API:et.
const school = ref<Schema<'SchoolRefDto'> | null>(null)
const groups = ref<Group[]>([])
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
      finished: false,
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
      period: '–',
      status: null,
      // Bara en klass kan vara avslutad. Servern räknar ut den sista slutdagen bland kurserna.
      finished: group.type === 'Class' && isGroupFinished(group),
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
      finished: status === 'finished',
      to: {name: 'course-content', params: {courseId: course.id}},
      course,
    }))

  return [...schoolRows, ...groupRows, ...courseRows]
})

const matchingRows = computed(() => {
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

// Pågående rader ligger i tabellen. Avslutade kurser och klasser ligger i en hopfälld sektion under den.
const visibleRows = computed(() => matchingRows.value.filter((row) => !row.finished))
const finishedRows = computed(() => matchingRows.value.filter((row) => row.finished))

// Sektionen är hopfälld tills man öppnar den, men en sökträff fäller ut den så att träffen syns.
const finishedOpen = ref(false)
watch(() => search.value.trim() !== '' && finishedRows.value.length > 0, (hasMatch) => {
  if (hasMatch) {
    finishedOpen.value = true
  }
})

// Finns träffar bara bland de avslutade döljs den tomma tabellen med pågående rader.
const hideCurrent = computed(() => visibleRows.value.length === 0 && finishedRows.value.length > 0
  && search.value.trim() !== '')
const currentEmptyText = computed(() => search.value.trim() === '' && finishedRows.value.length > 0
  ? 'Inga pågående kurser.'
  : 'Inga grupper matchar sökningen.')

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

const showTable = computed(() => !(emptyKind.value && matchingRows.value.length === 0))

const groupFormOpen = ref(false)
const groupFormKind = ref<'class' | 'group'>('class')
const editingGroup = ref<Group>()
const deleteGroupOpen = ref(false)
const deletingGroup = ref<Group>()
const courseFormOpen = ref(false)
const editingCourse = ref<Course>()
const deleteCourseOpen = ref(false)
const deletingCourse = ref<Course>()

function openCreate(kind: 'class' | 'group') {
  editingGroup.value = undefined
  groupFormKind.value = kind
  groupFormOpen.value = true
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
// En ny klass eller grupp öppnas direkt, för det är där nästa steg finns. Meddelandet följer med i historiken.
function onGroupSaved(saved: Group) {
  if (groups.value.some((group) => group.id === saved.id)) {
    groups.value = groups.value.map((group) => group.id === saved.id
      ? saved
      : group)
    return
  }

  groups.value = [...groups.value, saved]
  const notice = `${saved.name} skapades. Lägg till det ${saved.type === 'Class'
    ? 'klassen'
    : 'gruppen'} behöver.`
  void router.push({name: 'group', params: {groupId: saved.id}, state: {notice}})
}

function onGroupDeleted(removed: Group) {
  groups.value = groups.value.filter((group) => group.id !== removed.id)
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

const {run: load, loading, error} = useAsync(async () => {
  const [me, loadedGroups] = await Promise.all([fetchMe(), fetchGroups()])
  school.value = me.school
  groups.value = loadedGroups
}, 'Kunde inte hämta grupperna.')

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
            <DropdownMenuItem data-test="new-class" @select="openCreate('class')">
              Ny klass
            </DropdownMenuItem>
            <DropdownMenuItem data-test="new-group" @select="openCreate('group')">
              Ny grupp
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
            : 'Kurser skapas på klassens sida. Öppna en klass och välj Ny kurs.' }}
        </EmptyDescription>
      </EmptyHeader>
      <Button
        v-if="emptyKind === 'group'"
        data-test="empty-new-group"
        @click="openCreate('class')"
      >
        <PhPlus/>
        Skapa skolans första klass
      </Button>
    </Empty>

    <GroupsTable
      v-if="!error && !isLoading && showTable && !hideCurrent"
      :rows="visibleRows"
      :is-admin="isAdmin"
      :empty-text="currentEmptyText"
      @edit="openEdit"
      @delete="openDelete"
    />

    <Collapsible
      v-if="!error && !isLoading && finishedRows.length"
      v-model:open="finishedOpen"
      class="flex flex-col gap-2.5"
      data-test="finished-section"
    >
      <CollapsibleTrigger as-child>
        <button
          type="button"
          class="text-muted-foreground flex w-fit items-center gap-1.5 text-sm font-medium"
          data-test="finished-toggle"
        >
          <PhCaretDown class="transition-transform" :class="{'-rotate-90': !finishedOpen}"/>
          Avslutade ({{ finishedRows.length }})
        </button>
      </CollapsibleTrigger>
      <CollapsibleContent class="flex flex-col gap-2.5">
        <p class="text-muted-foreground text-[13px]">
          Avslutade kurser finns kvar här med material, betyg och återkoppling.
        </p>
        <GroupsTable
          :rows="finishedRows"
          :is-admin="isAdmin"
          @edit="openEdit"
          @delete="openDelete"
        />
      </CollapsibleContent>
    </Collapsible>

    <template v-if="isAdmin">
      <GroupFormDialog
        v-model:open="groupFormOpen"
        :school-id="school?.id ?? ''"
        :group="editingGroup"
        :kind="groupFormKind"
        @saved="onGroupSaved"
      />
      <DeleteGroupDialog
        v-if="deletingGroup"
        v-model:open="deleteGroupOpen"
        :group="deletingGroup"
        @deleted="onGroupDeleted"
      />
      <CourseFormDialog
        v-if="editingCourse"
        v-model:open="courseFormOpen"
        :course="editingCourse"
        @saved="updateCourse"
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
