<script setup lang="ts">
import {PhCalendarBlank, PhCalendarCheck, PhCaretLeft, PhCaretRight, PhFlag} from '@phosphor-icons/vue'
import {computed, ref, watch} from 'vue'
import {RouterLink, type RouteLocationRaw} from 'vue-router'

import {Alert, AlertDescription} from '@/components/ui/alert'
import {Button} from '@/components/ui/button'
import {Empty, EmptyDescription, EmptyHeader, EmptyMedia, EmptyTitle} from '@/components/ui/empty'
import {NativeSelect} from '@/components/ui/native-select'
import {Skeleton} from '@/components/ui/skeleton'
import {useAsync} from '@/composables/useAsync'
import {useCourses} from '@/composables/useCourses'
import {useCurrentUser} from '@/composables/useCurrentUser'
import {api, type Schema, unwrap} from '@/lib/api'
import {
  addDays,
  dateKey,
  groupByDate,
  isSameDay,
  monthGrid,
  monthHeading,
  startOfWeek,
  timeText,
  weekDays,
  weekHeading,
  weekLabel,
  weekdayNames,
} from '@/lib/calendar'

type Item = Schema<'CalendarItemDto'>

const maxChipsPerDay = 3

const {courses} = useCourses()
const {can} = useCurrentUser()

const view = ref<'week' | 'month'>('week')
const anchor = ref(new Date())
const items = ref<Item[]>([])
const courseFilter = ref('all')
const today = new Date()

const weekStart = computed(() => startOfWeek(anchor.value))

// Veckan visar 7 dagar, månaden hela rutnätet om 6 veckor.
const days = computed(() => view.value === 'week'
  ? weekDays(weekStart.value)
  : monthGrid(anchor.value))

const heading = computed(() => view.value === 'week'
  ? weekHeading(weekStart.value)
  : monthHeading(anchor.value))

const subHeading = computed(() => view.value === 'week'
  ? weekLabel(weekStart.value)
  : null)

const visibleItems = computed(() => courseFilter.value === 'all'
  ? items.value
  : items.value.filter((item) => item.courseId === courseFilter.value))

const byDate = computed(() => groupByDate(visibleItems.value))

function itemsOn(day: Date): Item[] {
  return byDate.value.get(dateKey(day)) ?? []
}

function shownOn(day: Date): Item[] {
  return itemsOn(day).slice(0, maxChipsPerDay)
}

const rangeLabel = computed(() => view.value === 'week'
  ? 'veckan'
  : 'månaden')

const hasItems = computed(() => visibleItems.value.length > 0)

function step(direction: -1 | 1) {
  anchor.value = view.value === 'week'
    ? addDays(anchor.value, 7 * direction)
    : new Date(anchor.value.getFullYear(), anchor.value.getMonth() + direction, 1)
}

function setView(next: 'week' | 'month') {
  view.value = next
}

function weekdayName(index: number): string {
  return weekdayNames[index]
}

function isOtherMonth(day: Date): boolean {
  return view.value === 'month' && day.getMonth() !== anchor.value.getMonth()
}

function linkFor(item: Item): RouteLocationRaw | null {
  if (!item.courseId) {
    return null
  }
  if (item.contentType === 'Assignment' && can('readSubmissions', item.courseId)) {
    return {name: 'assignment-submissions', params: {courseId: item.courseId, linkId: item.linkId}}
  }
  // Eleven landar på delen i kursvyn. Kurssidan skickar alla andra vidare till delens egen sida.
  return {name: 'course-content', params: {courseId: item.courseId}, query: {del: item.linkId}}
}

function chipClass(item: Item): string {
  return item.kind === 'Deadline'
    ? 'border-destructive/20 bg-destructive/10 text-destructive'
    : 'bg-background'
}

function chipLabel(item: Item): string {
  return item.kind === 'Deadline'
    ? 'Deadline'
    : 'Publiceras'
}

const {run: fetchItems, loading, error} = useAsync(async (from: Date, to: Date) => unwrap(await api.GET('/api/calendar', {
  params: {query: {from: from.toISOString(), to: to.toISOString()}},
})))
const loadFailed = computed(() => error.value !== '')

async function load() {
  const first = days.value[0]
  const last = days.value[days.value.length - 1]
  const loaded = await fetchItems(first, addDays(last, 1))
  if (loaded) {
    items.value = loaded
  } else if (error.value) {
    items.value = []
  }
}

// Hämtar om så fort det synliga intervallet ändras.
watch([view, anchor], load, {immediate: true})
</script>

<template>
  <div class="flex flex-col gap-4">
    <div class="flex flex-wrap items-center gap-3">
      <div class="flex gap-1">
        <Button variant="outline" size="sm" @click="anchor = new Date()">Idag</Button>
        <Button
          variant="outline"
          size="icon-sm"
          :aria-label="view === 'week'
            ? 'Föregående vecka'
            : 'Föregående månad'"
          @click="step(-1)"
        >
          <PhCaretLeft/>
        </Button>
        <Button
          variant="outline"
          size="icon-sm"
          :aria-label="view === 'week'
            ? 'Nästa vecka'
            : 'Nästa månad'"
          @click="step(1)"
        >
          <PhCaretRight/>
        </Button>
      </div>

      <h1 class="font-heading text-[22px] font-semibold">
        {{ heading }}
        <span v-if="subHeading" class="text-muted-foreground text-base font-normal">· {{ subHeading }}</span>
      </h1>

      <div class="ml-auto flex flex-wrap gap-2">
        <label for="calendar-course" class="sr-only">Kurs</label>
        <NativeSelect id="calendar-course" v-model="courseFilter" size="sm">
          <option value="all">Alla kurser</option>
          <option v-for="course in courses" :key="course.id" :value="course.id">{{ course.title }}</option>
        </NativeSelect>

        <div role="tablist" class="bg-muted flex rounded-[7px] p-[3px]">
          <button
            type="button"
            role="tab"
            :aria-selected="view === 'week'"
            class="text-muted-foreground rounded-[5px] px-2.5 py-1 text-[13px] aria-selected:bg-background aria-selected:text-foreground aria-selected:font-medium aria-selected:shadow-sm"
            @click="setView('week')"
          >
            Vecka
          </button>
          <button
            type="button"
            role="tab"
            :aria-selected="view === 'month'"
            class="text-muted-foreground rounded-[5px] px-2.5 py-1 text-[13px] aria-selected:bg-background aria-selected:text-foreground aria-selected:font-medium aria-selected:shadow-sm"
            @click="setView('month')"
          >
            Månad
          </button>
        </div>
      </div>
    </div>

    <Alert v-if="loadFailed" variant="destructive">
      <AlertDescription>Kunde inte hämta kalendern.</AlertDescription>
    </Alert>

    <div v-else-if="loading" class="flex flex-col gap-2" data-test="calendar-loading">
      <Skeleton v-for="i in 4" :key="i" class="h-12 w-full"/>
    </div>

    <Empty v-else-if="!hasItems" class="border border-dashed">
      <EmptyHeader>
        <EmptyMedia variant="icon">
          <PhCalendarBlank/>
        </EmptyMedia>
        <EmptyTitle>Inget i kalendern den här {{ rangeLabel }}</EmptyTitle>
        <EmptyDescription>Deadlines och publiceringstider för dina kurser visas här.</EmptyDescription>
      </EmptyHeader>
    </Empty>

    <section v-else aria-label="Kalender" class="bg-card overflow-hidden rounded-[11px] border">
      <div
        v-if="view === 'month'"
        class="bg-muted/50 text-muted-foreground hidden grid-cols-7 border-b text-[13px] md:grid"
      >
        <div v-for="index in 7" :key="index" class="px-2 py-2">{{ weekdayName(index - 1) }}</div>
      </div>

      <div
        class="grid grid-cols-1 md:grid-cols-7"
        :data-test="view === 'week'
          ? 'week-grid'
          : 'month-grid'"
      >
        <div
          v-for="day in days"
          :key="dateKey(day)"
          :data-test="`day-${dateKey(day)}`"
          class="flex min-w-0 flex-col gap-1 border-b p-1 md:border-l md:first:border-l-0"
          :class="[
            isSameDay(day, today)
              ? 'bg-muted/40'
              : '',
            isOtherMonth(day)
              ? 'opacity-50'
              : '',
            view === 'month'
              ? 'md:min-h-24'
              : 'md:min-h-40',
            !itemsOn(day).length && view === 'month'
              ? 'max-md:hidden'
              : '',
          ]"
        >
          <div class="flex items-center gap-1.5 px-1 py-1 text-[13px]">
            <span
              :class="isSameDay(day, today)
                ? 'font-medium'
                : 'text-muted-foreground'"
            >
              {{ weekdayName((day.getDay() + 6) % 7) }}
            </span>
            <span
              class="font-semibold"
              :class="isSameDay(day, today)
                ? 'bg-foreground text-background flex size-6 items-center justify-center rounded-full text-sm'
                : 'text-base'"
            >
              {{ day.getDate() }}
            </span>
          </div>

          <template
            v-for="item in (view === 'week'
              ? itemsOn(day)
              : shownOn(day))"
            :key="`${item.kind}-${item.linkId}`"
          >
            <component
              :is="linkFor(item)
                ? RouterLink
                : 'div'"
              :to="linkFor(item) ?? undefined"
              data-test="calendar-chip"
              class="flex min-w-0 flex-col rounded-[5px] border px-1.5 py-1 text-xs"
              :class="chipClass(item)"
            >
              <span class="flex min-w-0 items-center gap-1 font-medium">
                <PhFlag v-if="item.kind === 'Deadline'" class="size-3.5 shrink-0"/>
                <PhCalendarCheck v-else class="size-3.5 shrink-0"/>
                <span class="shrink-0">{{ timeText(item.at) }}</span>
                <span class="truncate">{{ item.title }}</span>
              </span>
              <span v-if="view === 'week'" class="text-muted-foreground truncate">
                {{ chipLabel(item) }}<template v-if="item.courseTitle"> · {{ item.courseTitle }}</template>
              </span>
            </component>
          </template>

          <span
            v-if="view === 'month' && itemsOn(day).length > maxChipsPerDay"
            class="text-muted-foreground px-1 text-xs"
            data-test="calendar-more"
          >
            +{{ itemsOn(day).length - maxChipsPerDay }} till
          </span>
        </div>
      </div>
    </section>
  </div>
</template>
