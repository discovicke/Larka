<script setup lang="ts">
import {ref, watch} from 'vue'

import StatusBadge from '@/components/StatusBadge.vue'
import {Alert, AlertDescription} from '@/components/ui/alert'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import {Skeleton} from '@/components/ui/skeleton'
import {useAsync} from '@/composables/useAsync'
import type {Schema} from '@/lib/api'
import {fetchStudentOverview} from '@/lib/groupsApi'

// Adminens vy av en elev i klassen: läget i varje kurs klassen läser. Bara läsning.
const props = defineProps<{groupId: string, student: Schema<'GroupMemberDto'> | null}>()

const open = defineModel<boolean>('open', {required: true})

const overview = ref<Schema<'StudentOverviewDto'> | null>(null)
const {run: load, loading, error} = useAsync(
  () => fetchStudentOverview(props.groupId, props.student!.userId),
  'Kunde inte hämta elevens läge.',
)

watch(open, async (isOpen) => {
  if (!isOpen || !props.student) {
    return
  }

  overview.value = null
  overview.value = await load() ?? null
})

// Bedömda uppgifter och betygen hittills, t.ex. "Bedömda 3 av 6 (G 2 · VG 1)". IG visas bara när eleven fått något.
function gradedText(course: Schema<'StudentOverviewCourseDto'>): string {
  const grades = [
    course.passCount > 0
      ? `G ${course.passCount}`
      : '',
    course.distinctionCount > 0
      ? `VG ${course.distinctionCount}`
      : '',
    course.failCount > 0
      ? `IG ${course.failCount}`
      : '',
  ].filter((part) => part !== '').join(' · ')
  const graded = `Bedömda ${course.gradedCount} av ${course.assignmentCount}`
  return grades
    ? `${graded} (${grades})`
    : graded
}
</script>

<template>
  <Dialog v-model:open="open">
    <DialogContent class="sm:max-w-xl" data-test="student-overview">
      <DialogHeader>
        <DialogTitle>{{ student?.name }}</DialogTitle>
        <DialogDescription>Läget i klassens kurser. Du kan läsa det men inte ändra något.</DialogDescription>
      </DialogHeader>

      <Alert v-if="error" variant="destructive">
        <AlertDescription>{{ error }}</AlertDescription>
      </Alert>
      <Skeleton v-else-if="loading" class="h-24 w-full rounded-[11px]"/>
      <p
        v-else-if="overview && overview.courses.length === 0"
        class="text-muted-foreground text-sm"
        data-test="overview-empty"
      >
        Klassen läser inga kurser än.
      </p>
      <div v-else-if="overview" class="overflow-hidden rounded-[11px] border">
        <section
          v-for="course in overview.courses"
          :key="course.courseId"
          class="flex flex-col gap-1.5 border-b px-4 py-3 last:border-b-0"
          :aria-label="course.title"
          data-test="overview-course"
        >
          <h3 class="text-sm font-semibold">
            {{ course.title }}
            <span class="text-muted-foreground font-normal">· {{ course.code }}</span>
          </h3>
          <div class="flex flex-wrap items-center gap-x-2 gap-y-1.5 text-[13px]">
            <StatusBadge
              v-if="course.needsWorkCount > 0"
              status="submission.needsWork"
              :label="`Komplettera ${course.needsWorkCount}`"
              data-test="overview-needs-work"
            />
            <StatusBadge
              v-if="course.lateCount > 0"
              status="deadline.overdue"
              :label="`Försenat ${course.lateCount}`"
              data-test="overview-late"
            />
            <StatusBadge
              v-if="course.submittedCount > 0"
              status="submission.submitted"
              :label="`Inlämnat, ej bedömt ${course.submittedCount}`"
              data-test="overview-submitted"
            />
            <span class="text-muted-foreground" data-test="overview-graded">{{ gradedText(course) }}</span>
          </div>
        </section>
      </div>
    </DialogContent>
  </Dialog>
</template>
