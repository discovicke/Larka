<script setup lang="ts">
import {computed, ref, watch} from 'vue'
import {useRoute} from 'vue-router'

import {Alert, AlertDescription} from '@/components/ui/alert'
import {Avatar, AvatarFallback} from '@/components/ui/avatar'
import {Skeleton} from '@/components/ui/skeleton'
import {ApiError, api, unwrap, type Schema} from '@/lib/api'
import {groupTypeLabels} from '@/lib/groups'
import {initials} from '@/lib/news'
import NotFoundView from '@/views/NotFoundView.vue'

// Lärare först, sedan mentorer och elever.
const roles: {value: Schema<'GroupRole'>, label: string}[] = [
  {value: 'Teacher', label: 'Lärare'},
  {value: 'Mentor', label: 'Mentor'},
  {value: 'Student', label: 'Elev'},
]

const route = useRoute()
const groupId = computed(() => String(route.params.groupId))

const group = ref<Schema<'GroupDto'> | null>(null)
const courses = ref<Schema<'CourseDto'>[]>([])
const members = ref<Schema<'GroupMemberDto'>[]>([])
const loading = ref(true)
const loadFailed = ref(false)
const notFound = ref(false)

const subtitle = computed(() => {
  if (!group.value) {
    return ''
  }
  return [groupTypeLabels[group.value.type], group.value.term]
    .filter((part) => part !== '')
    .join(' · ')
})

const membersByRole = computed(() => roles
  .map((role) => ({
    ...role,
    members: members.value.filter((member) => member.role === role.value),
  }))
  .filter((role) => role.members.length > 0))

async function load() {
  const id = groupId.value
  loading.value = true
  loadFailed.value = false
  notFound.value = false
  group.value = null
  try {
    const params = {params: {path: {id}}}
    const [groupResult, courseResult, memberResult] = await Promise.all([
      api.GET('/api/groups/{id}', params),
      api.GET('/api/groups/{id}/courses', params),
      api.GET('/api/groups/{id}/members', params),
    ])
    if (id !== groupId.value) {
      return
    }
    group.value = unwrap(groupResult)
    courses.value = courseResult.data ?? []
    members.value = memberResult.data ?? []
  } catch (exception) {
    if (id !== groupId.value) {
      return
    }
    if (exception instanceof ApiError && exception.status === 404) {
      notFound.value = true
    } else {
      loadFailed.value = true
    }
  } finally {
    if (id === groupId.value) {
      loading.value = false
    }
  }
}

watch(groupId, load, {immediate: true})
</script>

<template>
  <Alert v-if="loadFailed" variant="destructive">
    <AlertDescription>Kunde inte hämta gruppen.</AlertDescription>
  </Alert>

  <div v-else-if="loading" class="flex flex-col gap-2">
    <Skeleton class="h-10 w-64"/>
    <Skeleton class="h-20 w-full rounded-[11px]"/>
  </div>

  <NotFoundView v-else-if="notFound || !group"/>

  <div v-else class="mx-auto flex w-full max-w-[1120px] flex-col gap-6">
    <div class="flex flex-col gap-1">
      <h1 class="font-heading text-[30px] leading-tight font-semibold">{{ group.name }}</h1>
      <p v-if="subtitle" class="text-muted-foreground" data-test="group-subtitle">{{ subtitle }}</p>
    </div>

    <section aria-labelledby="members-heading" class="flex flex-col gap-4">
      <h2 id="members-heading" class="text-lg font-semibold">Medlemmar</h2>

      <section
        v-for="role in membersByRole"
        :key="role.value"
        :aria-label="role.label"
        class="flex flex-col gap-2.5"
        data-test="member-role"
      >
        <h3 class="text-muted-foreground text-sm font-medium">{{ role.label }}</h3>
        <div class="overflow-hidden rounded-[11px] border">
          <div
            v-for="member in role.members"
            :key="member.userId"
            class="flex items-center gap-3 border-b px-4 py-3.5 last:border-b-0"
            data-test="member-row"
          >
            <Avatar class="size-10">
              <AvatarFallback class="text-[13px] font-semibold">{{ initials(member.name) }}</AvatarFallback>
            </Avatar>
            <span class="font-medium" data-test="member-name">{{ member.name }}</span>
          </div>
        </div>
      </section>

      <p v-if="members.length === 0" class="text-muted-foreground text-sm">Gruppen har inga medlemmar ännu.</p>
    </section>

    <section v-if="courses.length > 0" aria-labelledby="courses-heading" class="flex flex-col gap-2.5">
      <h2 id="courses-heading" class="text-lg font-semibold">Kurser</h2>
      <div class="overflow-hidden rounded-[11px] border">
        <div
          v-for="course in courses"
          :key="course.id"
          class="flex items-center justify-between gap-3 border-b px-4 py-3 last:border-b-0"
          data-test="course-row"
        >
          <RouterLink
            :to="{name: 'course-content', params: {courseId: course.id}}"
            class="font-medium underline underline-offset-3 decoration-border hover:decoration-foreground"
          >
            {{ course.title }}
          </RouterLink>
          <span class="font-mono text-[13px]">{{ course.code }}</span>
        </div>
      </div>
    </section>
  </div>
</template>
