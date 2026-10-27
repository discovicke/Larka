<script setup lang="ts">
import {PhNewspaper, PhPushPin, PhTray} from '@phosphor-icons/vue'
import {computed, ref, watch} from 'vue'
import {useRoute, useRouter} from 'vue-router'

import NewsArticle from '@/components/news/NewsArticle.vue'
import {Alert, AlertDescription} from '@/components/ui/alert'
import {Empty, EmptyDescription, EmptyHeader, EmptyMedia, EmptyTitle} from '@/components/ui/empty'
import {Skeleton} from '@/components/ui/skeleton'
import {useCourses} from '@/composables/useCourses'
import {api, type Schema} from '@/lib/api'
import {formatDate} from '@/lib/content'
import {dayGroup, mergeNews} from '@/lib/news'

type InboxTab = 'all' | 'news'

// Betyg, svar och enkäter kommer med #113/#46. Tills dess visar båda flikarna samma nyhetslista.
const tabs: {value: InboxTab, label: string}[] = [
  {value: 'all', label: 'Alla'},
  {value: 'news', label: 'Nyheter'},
]

const route = useRoute()
const router = useRouter()
const {courses, status} = useCourses()

const groups = ref<Schema<'GroupDto'>[]>([])
const users = ref<Schema<'UserDto'>[]>([])
const posts = ref<Schema<'ContentFeedItem'>[]>([])
const loading = ref(true)
const loadFailed = ref(false)
const activeTab = ref<InboxTab>('all')
const now = new Date()

const dayHeadings = ['Idag', 'Igår', 'Tidigare'] as const

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

// En post per innehåll, även när den är publicerad i både en kurs och en grupp.
// Inkorgens lista är kronologisk, så fästa nyheter flyttas inte upp här.
const entries = computed(() => [...mergeNews(posts.value, names.value)]
  .sort((left, right) => Date.parse(right.post.publishAt) - Date.parse(left.post.publishAt)))

const sections = computed(() => dayHeadings
  .map((heading) => ({
    heading,
    entries: entries.value.filter((entry) => dayGroup(entry.post.publishAt, now) === heading),
  }))
  .filter((section) => section.entries.length > 0))

const selectedId = computed(() => typeof route.query.post === 'string'
  ? route.query.post
  : '')
const selected = computed(() =>
  entries.value.find((entry) => entry.post.contentId === selectedId.value) ?? entries.value[0])
const isEmpty = computed(() => !loading.value && !loadFailed.value && entries.value.length === 0)

function tabClass(value: InboxTab) {
  return activeTab.value === value
    ? 'bg-background font-medium shadow-sm'
    : 'text-muted-foreground'
}

function timeLabel(publishAt: string): string {
  return dayGroup(publishAt, now) === 'Tidigare'
    ? formatDate(publishAt)
    : new Date(publishAt).toLocaleTimeString('sv-SE', {hour: '2-digit', minute: '2-digit'})
}

async function load(showSkeleton = true) {
  if (showSkeleton) {
    loading.value = true
  }
  loadFailed.value = false
  try {
    const [userResult, groupResult] = await Promise.all([
      api.GET('/api/users'),
      api.GET('/api/groups'),
    ])
    users.value = userResult.data ?? []
    groups.value = groupResult.data ?? []

    // Alla flöden hämtas samtidigt; ett fel räcker för att visa felmeddelandet.
    const [courseResults, groupResults] = await Promise.all([
      Promise.all(courses.value.map((course) => api.GET('/api/courses/{id}/content', {
        params: {path: {id: course.id}, query: {type: 'news'}},
      }))),
      Promise.all(groups.value.map((group) => api.GET('/api/groups/{id}/content', {
        params: {path: {id: group.id}, query: {type: 'news'}},
      }))),
    ])
    posts.value = [...courseResults, ...groupResults]
      .flatMap((result) => result.data ?? [])
  } catch {
    loadFailed.value = true
    posts.value = []
  } finally {
    loading.value = false
  }
}

function select(contentId: string) {
  router.replace({query: {post: contentId}})
}

async function afterDelete() {
  await load(false)
  await router.replace({query: {}})
}

// Kurserna hämtas av AppLayout. Flödena kan hämtas först när listan finns.
watch(status, (value) => {
  if (value !== 'loading') {
    load()
  }
}, {immediate: true})
</script>

<template>
  <div class="flex min-w-0 flex-1 flex-wrap items-stretch">
    <template v-if="isEmpty">
      <Empty class="m-auto border border-dashed">
        <EmptyHeader>
          <EmptyMedia variant="icon">
            <PhTray/>
          </EmptyMedia>
          <EmptyTitle>Inkorgen är tom</EmptyTitle>
          <EmptyDescription>Nyheter från dina kurser och grupper visas här.</EmptyDescription>
        </EmptyHeader>
      </Empty>
    </template>

    <template v-else>
      <div class="flex min-w-0 flex-[1_1_420px] flex-col border-r max-md:border-r-0 max-md:border-b">
        <div class="border-b px-4 py-3">
          <div role="tablist" class="bg-muted inline-flex flex-wrap rounded-[7px] p-[3px]">
            <button
              v-for="tab in tabs"
              :key="tab.value"
              type="button"
              role="tab"
              :aria-selected="activeTab === tab.value"
              class="rounded-[5px] px-2.5 py-1 text-[13px]"
              :class="tabClass(tab.value)"
              @click="activeTab = tab.value"
            >
              {{ tab.label }}
            </button>
          </div>
        </div>

        <Alert v-if="loadFailed" variant="destructive" class="m-4">
          <AlertDescription>Kunde inte hämta nyheterna.</AlertDescription>
        </Alert>

        <div v-if="loading" class="flex flex-col">
          <Skeleton v-for="i in 4" :key="i" class="mx-4 my-2 h-14"/>
        </div>

        <template v-else-if="!loadFailed">
          <template v-for="section in sections" :key="section.heading">
            <div
              class="text-muted-foreground px-4 pt-2.5 pb-1 text-xs font-medium"
              data-test="news-day"
            >
              {{ section.heading }}
            </div>
            <button
              v-for="entry in section.entries"
              :key="entry.post.contentId"
              type="button"
              class="flex w-full gap-3 px-4 py-3 text-left hover:bg-muted/40"
              :class="selected?.post.contentId === entry.post.contentId
                ? 'bg-muted shadow-[inset_2px_0_0_var(--color-primary)]'
                : ''"
              :aria-current="selected?.post.contentId === entry.post.contentId
                ? 'true'
                : undefined"
              data-test="news-item"
              @click="select(entry.post.contentId)"
            >
              <span class="bg-muted flex size-8 shrink-0 items-center justify-center rounded-[7px]">
                <PhNewspaper class="size-4"/>
              </span>
              <span class="flex min-w-0 flex-1 flex-col">
                <span class="font-medium">{{ entry.post.title }}</span>
                <span class="text-muted-foreground text-[13px]">
                  <template v-if="entry.post.isPinned">
                    <PhPushPin class="mr-1 inline size-3"/>Fäst ·
                  </template>
                  {{ entry.targets.join(' · ') }} · Nyhet · {{ timeLabel(entry.post.publishAt) }}
                </span>
              </span>
            </button>
          </template>
        </template>
      </div>

      <section
        aria-label="Nyhet"
        class="flex-[999_1_480px] min-w-0 px-[clamp(16px,3vw,40px)] py-8"
      >
        <div v-if="selected" class="max-w-[640px]">
          <NewsArticle
            :post="selected.post"
            :links="selected.links"
            :targets="selected.targets"
            :users="users"
            @changed="load(false)"
            @deleted="afterDelete"
          />
        </div>
      </section>
    </template>
  </div>
</template>
