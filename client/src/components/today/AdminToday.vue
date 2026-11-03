<script setup lang="ts">
import {computed, ref} from 'vue'
import {RouterLink, type RouteLocationRaw} from 'vue-router'

import {Alert, AlertDescription} from '@/components/ui/alert'
import {Skeleton} from '@/components/ui/skeleton'
import {useAsync} from '@/composables/useAsync'
import {useCurrentUser} from '@/composables/useCurrentUser'
import {api, type Schema, unwrap} from '@/lib/api'
import {countLabel} from '@/lib/content'
import {dateLine, firstName, greeting} from '@/lib/today'

type Overview = Schema<'AdminOverviewDto'>

interface ClassCount {
  groupId: string | null
  name: string
  count: number
}

const now = new Date()
const {me} = useCurrentUser()

const overview = ref<Overview | null>(null)

const name = computed(() => firstName(me.value?.name))
const mobileGreeting = computed(() => name.value
  ? `Hej ${name.value}`
  : 'Hej')

// Användare utan lösenord samlas per klass, så att listan blir kort och länken leder dit lösenordet ordnas.
const withoutPassword = computed<ClassCount[]>(() => {
  const byClass = new Map<string, ClassCount>()
  for (const user of overview.value?.usersWithoutPassword ?? []) {
    const key = user.groupId ?? ''
    const existing = byClass.get(key)
    if (existing) {
      existing.count += 1
    } else {
      byClass.set(key, {groupId: user.groupId ?? null, name: user.groupName ?? 'Utan klass', count: 1})
    }
  }
  return [...byClass.values()]
})

// Räknas per kurs och per klass, inte per användare: en klass där tolv saknar lösenord är en sak att ordna.
// Elever utan klass är en sak, eftersom de ordnas på samma ställe.
const attentionCount = computed(() => (overview.value?.coursesWithoutTeacher.length ?? 0)
  + withoutPassword.value.length
  + ((overview.value?.studentsWithoutClass.length ?? 0) > 0
    ? 1
    : 0))

const summary = computed(() => attentionCount.value === 0
  ? 'Inget behöver ordnas'
  : `${countLabel(attentionCount.value, 'sak', 'saker')} att ordna`)

const courseCounts = computed(() => {
  const courses = overview.value?.courses
  if (!courses) {
    return ''
  }
  const parts = [
    `${courses.running} pågår`,
    `${courses.upcoming} kommande`,
    `${courses.finished} avslutade`,
  ]
  if (courses.undated > 0) {
    parts.push(`${courses.undated} utan datum`)
  }
  return parts.join(' · ')
})

function classLink(groupId: string): RouteLocationRaw {
  return {name: 'group', params: {groupId}}
}

function courseLink(course: Schema<'AdminCourseWithoutTeacherDto'>): RouteLocationRaw {
  // Lärare läggs till från klassens sida. En kurs som ingen klass läser öppnas på kursen.
  return course.groupId
    ? classLink(course.groupId)
    : {name: 'course-content', params: {courseId: course.courseId}}
}

const {run: load, loading, error} = useAsync(async () => {
  overview.value = unwrap(await api.GET('/api/me/admin-overview'))
})
const loadFailed = computed(() => error.value !== '')

const linkClass = 'inline-flex min-h-11 items-center gap-2 font-medium underline underline-offset-3 decoration-border hover:decoration-foreground'

load()
</script>

<template>
  <Alert v-if="loadFailed" variant="destructive">
    <AlertDescription>Kunde inte hämta din översikt.</AlertDescription>
  </Alert>

  <div v-else-if="loading" class="flex flex-col gap-4" data-test="admin-loading">
    <Skeleton class="h-9 w-72"/>
    <Skeleton class="h-40 w-full"/>
  </div>

  <div
    v-else-if="overview"
    class="flex flex-col gap-5 max-md:-mt-3 max-md:-mb-10 md:gap-6"
    data-test="admin-dashboard"
  >
    <div class="flex flex-col gap-0.5 md:gap-1">
      <h1 class="hidden text-[30px] leading-[1.15] md:block">
        {{ greeting(now) }}<template v-if="name">, {{ name }}</template>
      </h1>
      <h1 class="text-2xl leading-[1.2] md:hidden">{{ mobileGreeting }}</h1>
      <p class="text-muted-foreground text-sm md:text-base">{{ dateLine(now) }} · {{ summary }}</p>
    </div>

    <section aria-labelledby="a-courses-heading" class="flex flex-col gap-1.5">
      <h2 id="a-courses-heading" class="text-base">Kurser</h2>
      <p class="text-muted-foreground text-sm" data-test="course-counts">{{ courseCounts }}</p>
      <RouterLink :to="{name: 'groups', query: {filter: 'courses'}}" :class="linkClass">
        Visa alla kurser
      </RouterLink>
    </section>

    <section
      v-if="overview.coursesWithoutTeacher.length"
      aria-labelledby="a-no-teacher-heading"
      class="flex flex-col gap-1.5"
      data-test="no-teacher"
    >
      <h2 id="a-no-teacher-heading" class="text-base">Kurser utan lärare</h2>
      <p class="text-muted-foreground text-sm">Lägg till en lärare, så kan kursen förberedas.</p>
      <ul class="flex flex-col">
        <li v-for="course in overview.coursesWithoutTeacher" :key="course.courseId" data-test="no-teacher-item">
          <RouterLink :to="courseLink(course)" :class="linkClass">
            {{ course.title }}
            <span class="text-muted-foreground font-mono text-xs font-normal">{{ course.code }}</span>
          </RouterLink>
        </li>
      </ul>
    </section>

    <section
      v-if="withoutPassword.length"
      aria-labelledby="a-no-password-heading"
      class="flex flex-col gap-1.5"
      data-test="no-password"
    >
      <h2 id="a-no-password-heading" class="text-base">Kan inte logga in</h2>
      <p class="text-muted-foreground text-sm">De här användarna har inget lösenord än.</p>
      <ul class="flex flex-col">
        <li v-for="item in withoutPassword" :key="item.groupId ?? 'none'" data-test="no-password-item">
          <RouterLink v-if="item.groupId" :to="classLink(item.groupId)" :class="linkClass">
            {{ item.name }}
            <span class="text-muted-foreground text-xs font-normal">{{ item.count }} användare</span>
          </RouterLink>
          <RouterLink
            v-else
            :to="{name: 'users'}"
            :class="linkClass"
            data-test="no-password-unplaced"
          >
            {{ item.name }}
            <span class="text-muted-foreground text-xs font-normal">{{ item.count }} användare</span>
          </RouterLink>
        </li>
      </ul>
    </section>

    <section
      v-if="overview.studentsWithoutClass.length"
      aria-labelledby="a-no-class-heading"
      class="flex flex-col gap-1.5"
      data-test="no-class"
    >
      <h2 id="a-no-class-heading" class="text-base">Elever utan klass</h2>
      <p class="text-muted-foreground text-sm">
        Lägg in dem i en klass med Lägg till medlem på klassens sida, eller gör dem till lärare under Användare.
      </p>
      <ul class="flex flex-col">
        <li data-test="no-class-item">
          <RouterLink :to="{name: 'users', query: {filter: 'Student'}}" :class="linkClass">
            {{ countLabel(overview.studentsWithoutClass.length, 'elev', 'elever') }}
          </RouterLink>
        </li>
      </ul>
      <div class="flex flex-wrap gap-x-5">
        <RouterLink :to="{name: 'groups', query: {filter: 'groups'}}" :class="linkClass">Visa klasserna</RouterLink>
      </div>
    </section>

    <p v-if="attentionCount === 0" class="text-muted-foreground text-sm" data-test="admin-all-good">
      Alla kurser har lärare, alla elever har en klass och alla användare kan logga in.
    </p>
  </div>
</template>
