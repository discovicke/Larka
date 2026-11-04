<script setup lang="ts">
import {computed, ref} from 'vue'
import {RouterLink, type RouteLocationRaw} from 'vue-router'

import ClassAnalytics from '@/components/analytics/ClassAnalytics.vue'
import AdminQuickActions from '@/components/today/AdminQuickActions.vue'
import ClassTimelines from '@/components/today/ClassTimelines.vue'
import SetPasswordDialog from '@/components/users/SetPasswordDialog.vue'
import {Alert, AlertDescription} from '@/components/ui/alert'
import {Skeleton} from '@/components/ui/skeleton'
import {useAsync} from '@/composables/useAsync'
import {useCurrentUser} from '@/composables/useCurrentUser'
import {api, type Schema, unwrap} from '@/lib/api'
import {countLabel} from '@/lib/content'
import {dateLine, firstName, greeting} from '@/lib/today'

type Overview = Schema<'AdminOverviewDto'>

const now = new Date()
const {me} = useCurrentUser()

const overview = ref<Overview | null>(null)

const name = computed(() => firstName(me.value?.name))
const mobileGreeting = computed(() => name.value
  ? `Hej ${name.value}`
  : 'Hej')

// Klasserna där någon saknar lösenord. Användare utan klass räknas som en egen rad.
const passwordClasses = computed(() =>
  new Set((overview.value?.usersWithoutPassword ?? []).map((user) => user.groupId ?? '')).size)

// Räknas per kurs och per klass, inte per användare: en klass där tolv saknar lösenord är en sak att ordna.
// Elever utan klass är en sak, eftersom de ordnas på samma ställe.
const attentionCount = computed(() => (overview.value?.coursesWithoutTeacher.length ?? 0)
  + ((overview.value?.studentsWithoutClass.length ?? 0) > 0
    ? 1
    : 0)
  + passwordClasses.value)

// Sätt lösenord öppnas direkt från listan Kan inte logga in. Sedan hämtas översikten igen, så att raden försvinner.
const passwordOpen = ref(false)
const passwordUser = ref<{id: string, name: string}>()

function openSetPassword(user: Schema<'AdminUserWithoutPasswordDto'>) {
  passwordUser.value = {id: user.userId, name: user.name}
  passwordOpen.value = true
}

const summary = computed(() => attentionCount.value === 0
  ? 'Inget behöver ordnas'
  : `${countLabel(attentionCount.value, 'sak', 'saker')} att ordna`)

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

// Efter ett sparat lösenord hämtas översikten utan skelett, så att sidan står kvar medan raden försvinner.
async function refresh() {
  try {
    overview.value = unwrap(await api.GET('/api/me/admin-overview'))
  } catch {
    // Listan står kvar som den var tills sidan laddas om.
  }
}

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
      <p class="text-muted-foreground flex flex-wrap items-center gap-2 text-sm md:text-base">
        <span>{{ dateLine(now) }}</span>
        <span class="bg-border h-3 w-px" aria-hidden="true"/>
        <span>{{ summary }}</span>
      </p>
    </div>

    <AdminQuickActions
      :courses-without-teacher="overview.coursesWithoutTeacher"
      @changed="load"
    />

    <ClassTimelines :classes="overview.classTimelines"/>

    <ClassAnalytics/>

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

    <section
      v-if="overview.usersWithoutPassword.length"
      aria-labelledby="a-no-password-heading"
      class="flex flex-col gap-1.5"
      data-test="no-password"
    >
      <h2 id="a-no-password-heading" class="text-base">Kan inte logga in</h2>
      <p class="text-muted-foreground text-sm">
        De här användarna har inget lösenord än. Ge dem ett tillfälligt lösenord.
      </p>
      <ul class="flex flex-col">
        <li
          v-for="user in overview.usersWithoutPassword"
          :key="user.userId"
          class="flex min-h-11 flex-wrap items-center gap-x-3"
          data-test="no-password-item"
        >
          <span class="font-medium">{{ user.name }}</span>
          <RouterLink
            v-if="user.groupId"
            :to="classLink(user.groupId)"
            class="text-muted-foreground text-sm underline-offset-3 hover:underline"
          >
            {{ user.groupName }}
          </RouterLink>
          <span v-else class="text-muted-foreground text-sm">Utan klass</span>
          <button
            type="button"
            class="ml-auto inline-flex min-h-11 items-center font-medium underline underline-offset-3 decoration-border hover:decoration-foreground"
            :data-test="`set-password-${user.userId}`"
            @click="openSetPassword(user)"
          >
            Sätt lösenord
          </button>
        </li>
      </ul>
    </section>

    <p v-if="attentionCount === 0" class="text-muted-foreground text-sm" data-test="admin-all-good">
      Alla kurser har lärare, alla elever har en klass och alla användare kan logga in.
    </p>

    <SetPasswordDialog
      v-if="passwordUser"
      v-model:open="passwordOpen"
      :user="passwordUser"
      @saved="refresh"
    />
  </div>
</template>
