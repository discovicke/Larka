<script setup lang="ts">
import {PhListChecks} from '@phosphor-icons/vue'
import {computed, ref, watch} from 'vue'

import {Alert, AlertDescription} from '@/components/ui/alert'
import {Button} from '@/components/ui/button'
import {Empty, EmptyDescription, EmptyHeader, EmptyMedia, EmptyTitle} from '@/components/ui/empty'
import {Skeleton} from '@/components/ui/skeleton'
import {useCourses} from '@/composables/useCourses'
import {api, type Schema} from '@/lib/api'
import {formatDate} from '@/lib/content'

interface GradingRow {
  submission: Schema<'SubmissionDto'>
  courseId: string
  courseTitle: string
  linkId: string
  title: string
  name: string
}

const {courses, status} = useCourses()

const rows = ref<GradingRow[]>([])
const users = ref<Schema<'UserDto'>[]>([])
const loading = ref(false)
const error = ref('')

const names = computed(() => new Map(users.value.map((user) => [user.id, user.name])))

const orderedRows = computed(() => [...rows.value]
  .sort((left, right) => Date.parse(left.submission.submittedAt ?? '')
    - Date.parse(right.submission.submittedAt ?? '')))

function rowBorder(index: number) {
  return index < orderedRows.value.length - 1
    ? 'border-b'
    : ''
}

async function load() {
  loading.value = true
  error.value = ''
  try {
    const [moduleResults, userResult] = await Promise.all([
      Promise.all(courses.value.map((course) => api.GET('/api/courses/{id}/modules', {
        params: {path: {id: course.id}},
      }))),
      api.GET('/api/users'),
    ])
    users.value = userResult.data ?? []

    const assignments = moduleResults.flatMap((result, index) => {
      const course = courses.value[index]
      return (result.data ?? []).flatMap((week) => week.content
        .filter((item) => item.type === 'Assignment')
        .map((item) => ({course, item})))
    })

    const submissionResults = await Promise.all(assignments.map(({item}) => api.GET('/api/links/{id}/submissions', {
      params: {path: {id: item.linkId}},
    })))

    rows.value = assignments.flatMap(({course, item}, index) => (submissionResults[index].data ?? [])
      .filter((submission) => submission.status === 'Submitted')
      .map((submission) => ({
        submission,
        courseId: course.id,
        courseTitle: course.title,
        linkId: item.linkId,
        title: item.title,
        name: names.value.get(submission.studentId) ?? submission.studentId,
      })))
  } catch {
    error.value = 'Kunde inte hämta inlämningarna.'
  } finally {
    loading.value = false
  }
}

watch(status, (value) => {
  if (value !== 'loading') {
    load()
  }
}, {immediate: true})
</script>

<template>
  <div class="flex flex-col gap-5">
    <div class="flex flex-col gap-1">
      <h1 class="font-heading text-[30px] leading-tight font-semibold">Att bedöma</h1>
      <p class="text-muted-foreground">Inlämningar som väntar på bedömning i dina kurser</p>
    </div>

    <Alert v-if="error" variant="destructive">
      <AlertDescription>{{ error }}</AlertDescription>
    </Alert>

    <div v-else-if="loading" class="flex flex-col gap-2">
      <Skeleton v-for="i in 4" :key="i" class="h-12 w-full"/>
    </div>

    <Empty v-else-if="orderedRows.length === 0" class="border border-dashed">
      <EmptyHeader>
        <EmptyMedia variant="icon">
          <PhListChecks/>
        </EmptyMedia>
        <EmptyTitle>Inget att bedöma</EmptyTitle>
        <EmptyDescription>Nya inlämningar från dina elever visas här.</EmptyDescription>
      </EmptyHeader>
    </Empty>

    <section v-else aria-label="Att bedöma" class="overflow-hidden rounded-[11px] border">
      <div class="overflow-x-auto">
        <table class="w-full min-w-[760px] text-[13px]">
          <thead>
            <tr class="bg-muted/50 text-muted-foreground text-left">
              <th scope="col" class="border-b px-4 py-2.5 font-medium">Elev</th>
              <th scope="col" class="border-b px-3 py-2.5 font-medium">Uppgift</th>
              <th scope="col" class="border-b px-3 py-2.5 font-medium">Inlämnad</th>
              <th scope="col" class="border-b px-4 py-2.5 text-right font-medium">Bedöm</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="(row, index) in orderedRows" :key="row.submission.id" data-test="grading-row">
              <td class="px-4 py-3" :class="rowBorder(index)">{{ row.name }}</td>
              <td class="px-3 py-3" :class="rowBorder(index)">
                <div>{{ row.title }}</div>
                <div class="text-muted-foreground text-xs">{{ row.courseTitle }}</div>
              </td>
              <td class="px-3 py-3" :class="rowBorder(index)">
                {{ row.submission.submittedAt
                  ? formatDate(row.submission.submittedAt, true)
                  : '–' }}
              </td>
              <td class="px-4 py-3 text-right" :class="rowBorder(index)">
                <div class="flex justify-end">
                  <Button as-child class="h-7 px-2.5 text-[13px]">
                    <RouterLink
                      :to="{
                        name: 'submission-review',
                        params: {
                          courseId: row.courseId,
                          linkId: row.linkId,
                          submissionId: row.submission.id,
                        },
                      }"
                    >
                      Bedöm
                    </RouterLink>
                  </Button>
                </div>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </section>
  </div>
</template>
