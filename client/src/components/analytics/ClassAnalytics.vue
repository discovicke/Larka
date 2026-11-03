<script setup lang="ts">
import {PhChartBar, PhGraduationCap, PhUsersThree, PhWarningCircle} from '@phosphor-icons/vue'
import {computed, onMounted, ref, watch} from 'vue'
import {useRoute, useRouter} from 'vue-router'

import CourseGradeChart from '@/components/analytics/CourseGradeChart.vue'
import GradeDistributionChart from '@/components/analytics/GradeDistributionChart.vue'
import StatusDistributionChart from '@/components/analytics/StatusDistributionChart.vue'
import StudentProgressTable from '@/components/analytics/StudentProgressTable.vue'
import StudentOverviewDialog from '@/components/groups/StudentOverviewDialog.vue'
import {Alert, AlertDescription} from '@/components/ui/alert'
import {Card, CardContent, CardDescription, CardHeader, CardTitle} from '@/components/ui/card'
import {Empty, EmptyDescription, EmptyHeader, EmptyTitle} from '@/components/ui/empty'
import {NativeSelect, NativeSelectOption} from '@/components/ui/native-select'
import {Skeleton} from '@/components/ui/skeleton'
import {useAsync} from '@/composables/useAsync'
import {errorMessage, type Schema} from '@/lib/api'
import {percent, studentRows, summarize, type GroupAnalytics, type StudentAnalyticsRow} from '@/lib/analytics'
import {courseStatus, type CourseStatus} from '@/lib/courseStatus'
import {fetchGroupAnalytics, fetchGroups} from '@/lib/groupsApi'

const route = useRoute()
const router = useRouter()

const groups = ref<Schema<'GroupDto'>[]>([])
const groupsLoading = ref(true)
const groupsError = ref('')
const selectedGroupId = ref('')
const selectedCourseId = ref('')
const analytics = ref<GroupAnalytics | null>(null)
const selectedStudent = ref<Schema<'GroupMemberDto'> | null>(null)
const studentOpen = ref(false)

const {run: loadAnalytics, loading, error} = useAsync(
  (groupId: string) => fetchGroupAnalytics(groupId),
  'Kunde inte hämta klassens statistik.',
)

const classes = computed(() => groups.value
  .filter((group) => group.type === 'Class')
  .sort((left, right) => left.name.localeCompare(right.name, 'sv')))
const selectedGroup = computed(() => classes.value.find((group) => group.id === selectedGroupId.value) ?? null)
const courses = computed(() => analytics.value?.courses ?? [])
const selectedCourse = computed(() => courses.value.find((course) => course.courseId === selectedCourseId.value) ?? null)
const summary = computed(() => analytics.value
  ? summarize(analytics.value, selectedCourseId.value)
  : null)
const rows = computed<StudentAnalyticsRow[]>(() => analytics.value
  ? studentRows(analytics.value, selectedCourseId.value)
  : [])

const selectionLabel = computed(() => selectedCourse.value
  ? `${selectedCourse.value.title} (${selectedCourse.value.code})`
  : 'Alla klassens kurser')
const scopeFinished = computed(() => selectedCourse.value
  ? courseStatus(selectedCourse.value) === 'finished'
  : courses.value.length > 0 && courses.value.every((course) => courseStatus(course) === 'finished'))

const courseStatusLabels: Record<CourseStatus, string> = {
  running: 'Pågår',
  upcoming: 'Kommande',
  finished: 'Avslutad',
}

function courseStatusLabel(course: Schema<'GroupAnalyticsCourseDto'>): string {
  const status = courseStatus(course)
  return status
    ? courseStatusLabels[status]
    : 'Utan datum'
}

const stats = computed(() => summary.value
  ? [
      {
        key: 'students',
        label: 'Elever',
        value: summary.value.studentCount,
        hint: 'i klassen',
        icon: PhUsersThree,
      },
      {
        key: 'pass',
        label: 'Godkända bedömningar',
        value: percent(summary.value.passRate),
        hint: `${summary.value.passCount + summary.value.distinctionCount} av ${summary.value.gradedCount} bedömda`,
        icon: PhGraduationCap,
      },
      {
        key: 'risk',
        label: 'Behöver stöttning',
        value: summary.value.atRiskStudentCount,
        hint: 'elever att följa upp',
        icon: PhWarningCircle,
      },
      {
        key: 'missing',
        label: 'IG (ingen inlämning)/försenat',
        value: summary.value.autoFailCount + summary.value.overdueCount,
        hint: 'uppgifter',
        icon: PhChartBar,
      },
    ]
  : [])

function syncQuery() {
  const query = {...route.query}
  if (selectedGroupId.value) {
    query.group = selectedGroupId.value
  } else {
    delete query.group
  }
  if (selectedCourseId.value) {
    query.course = selectedCourseId.value
  } else {
    delete query.course
  }
  void router.replace({query})
}

function applyCourseFromQuery(loaded: GroupAnalytics) {
  const wanted = typeof route.query.course === 'string'
    ? route.query.course
    : ''
  selectedCourseId.value = loaded.courses.some((course) => course.courseId === wanted)
    ? wanted
    : ''
}

watch(selectedGroupId, async (groupId) => {
  selectedCourseId.value = ''
  analytics.value = null
  syncQuery()
  if (!groupId) {
    return
  }
  const loaded = await loadAnalytics(groupId)
  if (loaded) {
    analytics.value = loaded
    applyCourseFromQuery(loaded)
    syncQuery()
  }
})

watch(selectedCourseId, syncQuery)

function openStudent(studentId: string) {
  const student = rows.value.find((row) => row.studentId === studentId)
  if (!student) {
    return
  }
  selectedStudent.value = {userId: student.studentId, name: student.name, role: 'Student'}
  studentOpen.value = true
}

async function loadGroups() {
  groupsLoading.value = true
  groupsError.value = ''
  try {
    groups.value = await fetchGroups()
    const wanted = typeof route.query.group === 'string'
      ? route.query.group
      : ''
    selectedGroupId.value = classes.value.some((group) => group.id === wanted)
      ? wanted
      : (classes.value[0]?.id ?? '')
  } catch (exception) {
    groups.value = []
    groupsError.value = errorMessage(exception)
  } finally {
    groupsLoading.value = false
  }
}

onMounted(loadGroups)
</script>

<template>
  <section aria-labelledby="class-analytics-heading" class="flex flex-col gap-4" data-test="class-analytics">
    <div class="flex flex-wrap items-end justify-between gap-3">
      <div class="flex flex-col gap-1">
        <h2 id="class-analytics-heading" class="text-base">Följ upp klass</h2>
        <p class="text-muted-foreground text-sm">Betyg och status per kurs, med elever som behöver stöttning.</p>
      </div>

      <div class="flex flex-wrap items-center gap-2">
        <NativeSelect
          v-model="selectedGroupId"
          class="w-[170px]"
          aria-label="Välj klass"
          :disabled="groupsLoading || classes.length === 0"
          data-test="analytics-group"
        >
          <NativeSelectOption v-if="classes.length === 0" value="">Inga klasser</NativeSelectOption>
          <NativeSelectOption v-for="group in classes" :key="group.id" :value="group.id">
            {{ group.name }}
          </NativeSelectOption>
        </NativeSelect>

        <NativeSelect
          v-model="selectedCourseId"
          class="w-[220px]"
          aria-label="Välj kurs"
          :disabled="!analytics || courses.length === 0"
          data-test="analytics-course"
        >
          <NativeSelectOption value="">Alla kurser</NativeSelectOption>
          <NativeSelectOption v-for="course in courses" :key="course.courseId" :value="course.courseId">
            {{ course.title }} ({{ course.code }}) — {{ courseStatusLabel(course) }}
          </NativeSelectOption>
        </NativeSelect>
      </div>
    </div>

    <Alert v-if="groupsError" variant="destructive">
      <AlertDescription>{{ groupsError }}</AlertDescription>
    </Alert>
    <Alert v-else-if="error" variant="destructive">
      <AlertDescription>{{ error }}</AlertDescription>
    </Alert>

    <div v-else-if="groupsLoading || loading" class="flex flex-col gap-3">
      <div class="grid grid-cols-2 gap-3 lg:grid-cols-4">
        <Skeleton v-for="index in 4" :key="index" class="h-24 w-full rounded-xl"/>
      </div>
      <Skeleton class="h-[260px] w-full rounded-xl"/>
    </div>

    <Empty v-else-if="classes.length === 0" class="border border-dashed">
      <EmptyHeader>
        <EmptyTitle>Inga klasser än</EmptyTitle>
        <EmptyDescription>Skapa en klass först, sedan kan du följa upp kurserna här.</EmptyDescription>
      </EmptyHeader>
    </Empty>

    <template v-else-if="analytics && summary">
      <div class="grid grid-cols-2 gap-3 lg:grid-cols-4">
        <Card
          v-for="stat in stats"
          :key="stat.key"
          size="sm"
          class="gap-2 py-4"
        >
          <CardHeader class="gap-1 px-4">
            <CardDescription class="flex items-center gap-1.5 text-[13px]">
              <component :is="stat.icon" class="size-4 shrink-0"/>
              {{ stat.label }}
            </CardDescription>
            <CardTitle class="text-2xl font-semibold tabular-nums" :data-test="`analytics-stat-${stat.key}`">
              {{ stat.value }}
            </CardTitle>
          </CardHeader>
          <CardContent class="text-muted-foreground px-4 text-xs">{{ stat.hint }}</CardContent>
        </Card>
      </div>

      <div class="grid gap-4 lg:grid-cols-2">
        <Card size="sm" class="gap-2 py-4">
          <CardHeader class="gap-1 px-4">
            <CardTitle class="text-base">Betygsfördelning</CardTitle>
            <CardDescription>{{ selectionLabel }}</CardDescription>
          </CardHeader>
          <CardContent class="px-4" data-test="grade-chart">
            <GradeDistributionChart :summary="summary"/>
          </CardContent>
        </Card>

        <Card size="sm" class="gap-2 py-4">
          <CardHeader class="gap-1 px-4">
            <CardTitle class="text-base">Status på uppgifterna</CardTitle>
            <CardDescription>Antal uppgifter per status: {{ selectionLabel }}</CardDescription>
          </CardHeader>
          <CardContent class="px-4" data-test="status-chart">
            <StatusDistributionChart :summary="summary"/>
          </CardContent>
        </Card>
      </div>

      <Card v-if="!selectedCourseId && courses.length > 0" size="sm" class="gap-2 py-4">
        <CardHeader class="gap-1 px-4">
          <CardTitle class="text-base">Kursjämförelse</CardTitle>
          <CardDescription>Bedömda uppgifter per kurs. Välj en kurs för att se eleverna.</CardDescription>
        </CardHeader>
        <CardContent class="px-4">
          <CourseGradeChart :courses="courses"/>
        </CardContent>
      </Card>

      <div class="flex flex-col gap-2">
        <div>
          <h3 class="text-base">Elever</h3>
          <p class="text-muted-foreground text-sm">{{ selectionLabel }}</p>
        </div>
        <StudentProgressTable :rows="rows" :scope-finished="scopeFinished" @open="openStudent"/>
      </div>
    </template>

    <StudentOverviewDialog
      v-if="selectedGroup"
      v-model:open="studentOpen"
      :group-id="selectedGroup.id"
      :student="selectedStudent"
    />
  </section>
</template>
