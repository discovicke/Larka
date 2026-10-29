<script setup lang="ts">
import {PhMagnifyingGlass} from '@phosphor-icons/vue'
import {computed, ref} from 'vue'
import type {RouteLocationRaw} from 'vue-router'

import {Alert, AlertDescription} from '@/components/ui/alert'
import {Input} from '@/components/ui/input'
import {Skeleton} from '@/components/ui/skeleton'
import {useCourses} from '@/composables/useCourses'
import {api, type Schema} from '@/lib/api'
import {fetchMe} from '@/lib/me'

interface GroupRow {
  key: string
  name: string
  code: string
  type: string
  period: string
  to?: RouteLocationRaw
}

type Filter = 'all' | 'groups' | 'courses'

const filters: {value: Filter, label: string}[] = [
  {value: 'all', label: 'Alla'},
  {value: 'groups', label: 'Grupper'},
  {value: 'courses', label: 'Kurser'},
]

const groupTypeLabels: Record<Schema<'GroupType'>, string> = {
  Class: 'Klass',
  Committee: 'Kommitté',
  Other: 'Grupp',
}

const {courses, status} = useCourses()

// Användarens egen skola, från /api/me. Grupper och kurser är redan avgränsade till den av API:et.
const school = ref<Schema<'SchoolRefDto'> | null>(null)
const groups = ref<Schema<'GroupDto'>[]>([])
const loading = ref(true)
const error = ref('')
const filter = ref<Filter>('all')
const search = ref('')

const isLoading = computed(() => loading.value || status.value === 'loading')

const rows = computed<GroupRow[]>(() => {
  const schoolRows: GroupRow[] = []
  if (school.value) {
    schoolRows.push({
      key: `school-${school.value.id}`,
      name: school.value.name,
      code: '–',
      type: 'Skola',
      period: '–',
    })
  }

  const groupRows: GroupRow[] = [...groups.value]
    .sort((left, right) => left.name.localeCompare(right.name, 'sv'))
    .map((group) => ({
      key: `group-${group.id}`,
      name: group.name,
      code: '–',
      type: groupTypeLabels[group.type],
      period: group.term || '–',
    }))

  const courseRows: GroupRow[] = [...courses.value]
    .sort((left, right) => left.title.localeCompare(right.title, 'sv'))
    .map((course) => ({
      key: `course-${course.id}`,
      name: course.title,
      code: course.code,
      type: 'Kurs',
      period: '–',
      to: {name: 'course-content', params: {courseId: course.id}},
    }))

  return [...schoolRows, ...groupRows, ...courseRows]
})

const visibleRows = computed(() => {
  const text = search.value.trim().toLowerCase()
  return rows.value.filter((row) => {
    if (filter.value === 'groups' && row.to) {
      return false
    }
    if (filter.value === 'courses' && !row.to) {
      return false
    }
    return !text
      || row.name.toLowerCase().includes(text)
      || row.code.toLowerCase().includes(text)
  })
})

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
            @click="filter = tab.value"
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
      </div>
    </div>

    <Alert v-if="error" variant="destructive">
      <AlertDescription>{{ error }}</AlertDescription>
    </Alert>

    <div v-else-if="isLoading" class="flex flex-col gap-2">
      <Skeleton v-for="i in 4" :key="i" class="h-12 w-full"/>
    </div>

    <section v-else aria-label="Grupper" class="overflow-hidden rounded-[11px] border">
      <div class="overflow-x-auto">
        <table class="w-full min-w-[680px] border-collapse">
          <thead>
            <tr class="bg-muted/50 text-muted-foreground text-left text-[13px]">
              <th scope="col" class="border-b px-4 py-2.5 font-medium">Grupp</th>
              <th scope="col" class="border-b px-3 py-2.5 font-medium">Kod</th>
              <th scope="col" class="border-b px-3 py-2.5 font-medium">Typ</th>
              <th scope="col" class="border-b px-4 py-2.5 font-medium">Period</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="(row, index) in visibleRows" :key="row.key" data-test="group-row">
              <td class="px-4 py-3" :class="rowBorder(index)">
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
                class="px-3 py-3 font-mono text-[13px]"
                :class="[rowBorder(index), {'text-muted-foreground': row.code === '–'}]"
              >
                {{ row.code }}
              </td>
              <td class="px-3 py-3" :class="rowBorder(index)">{{ row.type }}</td>
              <td class="text-muted-foreground px-4 py-3" :class="rowBorder(index)">{{ row.period }}</td>
            </tr>
            <tr v-if="visibleRows.length === 0">
              <td colspan="4" class="text-muted-foreground px-4 py-6 text-center">
                Inga grupper matchar sökningen.
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </section>
  </div>
</template>
