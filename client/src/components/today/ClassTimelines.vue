<script setup lang="ts">
import {computed} from 'vue'

import {Card, CardContent, CardDescription, CardHeader, CardTitle} from '@/components/ui/card'
import type {Schema} from '@/lib/api'
import {timelineDate, timelineProgress} from '@/lib/timeline'

type ClassTimeline = Schema<'AdminClassTimelineDto'>
type CourseTimeline = Schema<'AdminClassCourseTimelineDto'>

const props = defineProps<{classes: ClassTimeline[]}>()

const today = new Date()

const sortedClasses = computed(() => [...props.classes].sort((left, right) => {
  const leftDays = nextDaysLeft(left)
  const rightDays = nextDaysLeft(right)
  if (leftDays === null && rightDays === null) {
    return left.name.localeCompare(right.name, 'sv')
  }
  if (leftDays === null) {
    return 1
  }
  if (rightDays === null) {
    return -1
  }
  return leftDays - rightDays || left.name.localeCompare(right.name, 'sv')
}))

function progress(course: CourseTimeline) {
  return timelineProgress(course.startsOn, course.endsOn, today)
}

function nextDaysLeft(group: ClassTimeline): number | null {
  const days = group.currentCourses
    .map((course) => progress(course).daysLeft)
    .filter((value): value is number => value !== null)
  return days.length > 0
    ? Math.min(...days)
    : null
}

function courseLabel(course: CourseTimeline): string {
  return course.title
}
</script>

<template>
  <section aria-labelledby="class-timelines-heading" class="flex flex-col gap-3" data-test="class-timelines">
    <div class="flex flex-col gap-0.5">
      <h2 id="class-timelines-heading" class="text-base">Klasser och pågående kurser</h2>
      <p class="text-muted-foreground text-sm">Tidslinjen visar hur långt klassen kommit i sin nuvarande kurs.</p>
    </div>

    <div class="grid gap-3 md:grid-cols-2">
      <Card
        v-for="group in sortedClasses"
        :key="group.groupId"
        size="sm"
        class="gap-3 py-4"
        data-test="class-timeline"
      >
        <CardHeader class="px-4">
          <CardTitle class="text-lg">{{ group.name }}</CardTitle>
          <CardDescription v-if="group.currentCourses.length === 0">Ingen pågående kurs</CardDescription>
        </CardHeader>
        <CardContent class="flex flex-col gap-4 px-4">
          <div
            v-for="course in group.currentCourses"
            :key="course.courseId"
            class="flex flex-col gap-1.5"
            data-test="class-timeline-course"
          >
            <div class="flex flex-wrap items-center gap-2">
              <p class="text-sm font-medium">{{ courseLabel(course) }}</p>
              <span class="text-muted-foreground font-mono text-[11px]">{{ course.code }}</span>
            </div>
            <div
              class="bg-muted h-2 overflow-hidden rounded-full"
              role="progressbar"
              :aria-valuenow="progress(course).percent"
              aria-valuemin="0"
              aria-valuemax="100"
              :aria-label="`Kursens tidslinje för ${course.title}`"
            >
              <div
                class="h-full rounded-full transition-[width]"
                :class="progress(course).nearEnd
                  ? 'bg-warning'
                  : 'bg-primary'"
                :style="{width: `${progress(course).percent}%`}"
              />
            </div>
            <div class="text-muted-foreground flex items-center justify-between gap-2 text-[11px]">
              <span>{{ timelineDate(course.startsOn) }}</span>
              <span v-if="progress(course).nearEnd" class="text-warning font-medium" data-test="timeline-warning">
                {{ progress(course).daysLeft }} dagar kvar
              </span>
              <span>{{ timelineDate(course.endsOn) }}</span>
            </div>
          </div>
        </CardContent>
      </Card>
    </div>
  </section>
</template>
