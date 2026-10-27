<script setup lang="ts">
import {PhFolder, PhMagnifyingGlass} from '@phosphor-icons/vue'
import {computed, ref, watch} from 'vue'

import {Alert, AlertDescription} from '@/components/ui/alert'
import {Empty, EmptyDescription, EmptyHeader, EmptyMedia, EmptyTitle} from '@/components/ui/empty'
import {Input} from '@/components/ui/input'
import {Skeleton} from '@/components/ui/skeleton'
import {useCourses} from '@/composables/useCourses'
import {api, type Schema} from '@/lib/api'
import {formatDate} from '@/lib/content'
import {fileIcon, formatSize, isThisWeek, materialRows} from '@/lib/material'

const {courses, status} = useCourses()

const groups = ref<Schema<'GroupDto'>[]>([])
const users = ref<Schema<'UserDto'>[]>([])
const feed = ref<Schema<'ContentFeedItem'>[]>([])
const attachments = ref<Schema<'AttachmentDto'>[]>([])
const loading = ref(true)
const loadFailed = ref(false)
const search = ref('')
const target = ref('all')
const now = new Date()

const names = computed(() => {
  const lookup = new Map<string, string>()
  for (const course of courses.value) {
    lookup.set(course.id, course.title)
  }
  for (const group of groups.value) {
    lookup.set(group.id, group.name)
  }
  return lookup
})

const authors = computed(() => new Map(users.value.map((user) => [user.id, user.name])))

const rows = computed(() => materialRows(feed.value, attachments.value, names.value, authors.value))

// En flik per mål som har filer: kurserna först, sedan grupperna.
const targetNames = computed(() => {
  const present = new Set(rows.value.flatMap((row) => row.targets))
  return [
    ...courses.value.map((course) => course.title),
    ...groups.value.map((group) => group.name),
  ].filter((name, index, all) => present.has(name) && all.indexOf(name) === index)
})

const visibleRows = computed(() => {
  const text = search.value.trim().toLowerCase()
  return rows.value.filter((row) => {
    if (target.value !== 'all' && !row.targets.includes(target.value)) {
      return false
    }
    return !text
      || row.attachment.fileName.toLowerCase().includes(text)
      || row.title.toLowerCase().includes(text)
  })
})

const sections = computed(() => [
  {
    heading: 'Nytt den här veckan',
    isWeek: true,
    rows: visibleRows.value.filter((row) => isThisWeek(row.publishAt, now)),
  },
  {
    heading: 'Tidigare',
    isWeek: false,
    rows: visibleRows.value.filter((row) => !isThisWeek(row.publishAt, now)),
  },
].filter((section) => section.rows.length > 0))

function chipClass(name: string) {
  return target.value === name
    ? 'bg-primary text-primary-foreground border-primary'
    : 'bg-background'
}

async function load() {
  loading.value = true
  loadFailed.value = false
  try {
    const [userResult, groupResult] = await Promise.all([
      api.GET('/api/users'),
      api.GET('/api/groups'),
    ])
    users.value = userResult.data ?? []
    groups.value = groupResult.data ?? []

    // Alla flöden hämtas samtidigt, utan typfilter, så att både delar och nyheter kan bära filer.
    const [courseResults, groupResults] = await Promise.all([
      Promise.all(courses.value.map((course) => api.GET('/api/courses/{id}/content', {
        params: {path: {id: course.id}},
      }))),
      Promise.all(groups.value.map((group) => api.GET('/api/groups/{id}/content', {
        params: {path: {id: group.id}},
      }))),
    ])
    feed.value = [...courseResults, ...groupResults]
      .flatMap((result) => result.data ?? [])

    const contentIds = [...new Set(feed.value.map((item) => item.contentId))]
    const attachmentResults = await Promise.all(contentIds.map((contentId) =>
      api.GET('/api/content/{id}/attachments', {params: {path: {id: contentId}}})))
    attachments.value = attachmentResults
      .flatMap((result) => result.data ?? [])
  } catch {
    loadFailed.value = true
    feed.value = []
    attachments.value = []
  } finally {
    loading.value = false
  }
}

// Kurserna hämtas av AppLayout. Flödena kan hämtas först när listan finns.
watch(status, (value) => {
  if (value !== 'loading') {
    load()
  }
}, {immediate: true})
</script>

<template>
  <div class="flex flex-col gap-5">
    <div class="flex flex-wrap items-end justify-between gap-3">
      <div class="flex flex-col gap-1">
        <h1 class="font-heading text-[30px] leading-tight font-semibold">Material</h1>
        <p class="text-muted-foreground">Filer från dina kurser och grupper</p>
      </div>

      <div class="relative w-full flex-[0_1_280px]">
        <label for="material-search" class="sr-only">Sök material</label>
        <PhMagnifyingGlass class="text-muted-foreground pointer-events-none absolute top-1/2 left-2.5 size-4 -translate-y-1/2"/>
        <Input
          id="material-search"
          v-model="search"
          class="pl-8"
          placeholder="Sök i allt material…"
        />
      </div>
    </div>

    <Alert v-if="loadFailed" variant="destructive">
      <AlertDescription>Kunde inte hämta materialet.</AlertDescription>
    </Alert>

    <div v-else-if="loading" class="flex flex-col gap-2">
      <Skeleton v-for="i in 4" :key="i" class="h-12 w-full"/>
    </div>

    <Empty v-else-if="rows.length === 0" class="border border-dashed">
      <EmptyHeader>
        <EmptyMedia variant="icon">
          <PhFolder/>
        </EmptyMedia>
        <EmptyTitle>Inget material än</EmptyTitle>
        <EmptyDescription>Filer som lärarna bifogar i kursernas delar visas här.</EmptyDescription>
      </EmptyHeader>
    </Empty>

    <template v-else>
      <div class="flex flex-wrap gap-1.5">
        <button
          type="button"
          :aria-pressed="target === 'all'"
          class="h-7 rounded-[5px] border px-2 text-xs font-medium"
          :class="chipClass('all')"
          @click="target = 'all'"
        >
          Allt
        </button>
        <button
          v-for="name in targetNames"
          :key="name"
          type="button"
          :aria-pressed="target === name"
          class="h-7 rounded-[5px] border px-2 text-xs font-medium"
          :class="chipClass(name)"
          @click="target = name"
        >
          {{ name }}
        </button>
      </div>

      <section aria-label="Filer" class="overflow-hidden rounded-[11px] border">
        <div class="overflow-x-auto">
          <table class="w-full min-w-[640px] border-collapse text-sm">
            <thead>
              <tr class="bg-muted/50 text-muted-foreground text-left text-[13px]">
                <th scope="col" class="border-b px-4 py-2.5 font-medium">Namn</th>
                <th scope="col" class="border-b px-3 py-2.5 font-medium">Del</th>
                <th scope="col" class="border-b px-3 py-2.5 font-medium">Grupp</th>
                <th scope="col" class="border-b px-3 py-2.5 font-medium">Uppladdat av</th>
                <th scope="col" class="border-b px-4 py-2.5 text-right font-medium">Datum</th>
              </tr>
            </thead>
            <tbody>
              <template v-for="section in sections" :key="section.heading">
                <tr>
                  <td
                    colspan="5"
                    class="text-muted-foreground border-b border-muted px-4 py-2 text-xs font-medium"
                    data-test="material-section"
                  >
                    {{ section.heading }}
                  </td>
                </tr>
                <tr v-for="row in section.rows" :key="row.attachment.id" data-test="material-row">
                  <td class="border-b border-muted px-4 py-2.5">
                    <a
                      :href="row.attachment.url"
                      target="_blank"
                      rel="noopener"
                      class="flex items-center gap-2.5"
                    >
                      <component :is="fileIcon(row.attachment.fileName)" class="text-muted-foreground size-4"/>
                      <span :class="{'font-medium': section.isWeek}">
                        {{ row.attachment.fileName }}
                      </span>
                      <span class="text-muted-foreground text-xs">{{ formatSize(row.attachment.sizeBytes) }}</span>
                    </a>
                  </td>
                  <td class="text-foreground/80 border-b border-muted px-3 py-2.5">{{ row.title }}</td>
                  <td class="border-b border-muted px-3 py-2.5">{{ row.targets.join(' · ') }}</td>
                  <td class="border-b border-muted px-3 py-2.5">{{ row.authorName }}</td>
                  <td class="text-muted-foreground border-b border-muted px-4 py-2.5 text-right">
                    {{ formatDate(row.publishAt) }}
                  </td>
                </tr>
              </template>
              <tr v-if="visibleRows.length === 0">
                <td colspan="5" class="text-muted-foreground px-4 py-6 text-center">
                  Inga filer matchar.
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </section>
    </template>
  </div>
</template>
