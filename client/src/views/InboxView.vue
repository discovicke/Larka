<script setup lang="ts">
import {
  PhArrowCounterClockwise,
  PhCheckCircle,
  PhChecks,
  PhClock,
  PhExam,
  PhNewspaper,
  PhTray,
} from '@phosphor-icons/vue'
import {computed, type Component, ref, watch} from 'vue'
import {type RouteLocationRaw, useRoute, useRouter} from 'vue-router'

import InboxRowContent from '@/components/inbox/InboxRowContent.vue'
import PageActions from '@/components/layout/PageActions.vue'
import NewsArticle from '@/components/news/NewsArticle.vue'
import {Alert, AlertDescription} from '@/components/ui/alert'
import {Button} from '@/components/ui/button'
import {Empty, EmptyDescription, EmptyHeader, EmptyMedia, EmptyTitle} from '@/components/ui/empty'
import {Skeleton} from '@/components/ui/skeleton'
import {useCourses} from '@/composables/useCourses'
import {useNotifications} from '@/composables/useNotifications'
import {api, type Schema} from '@/lib/api'
import {formatDate} from '@/lib/content'
import {
  type InboxFilter,
  type InboxKind,
  type InboxRow,
  inboxKindOf,
  kindLabels,
  matchesFilter,
  notificationRow,
  sortRows,
} from '@/lib/inbox'
import {dayGroup, mergeNews} from '@/lib/news'

const route = useRoute()
const router = useRouter()
const {courses, status} = useCourses()
const {notifications, unreadCount, loadNotifications, markRead, markAllRead} = useNotifications()

const groups = ref<Schema<'GroupDto'>[]>([])
const users = ref<Schema<'UserDto'>[]>([])
const posts = ref<Schema<'ContentFeedItem'>[]>([])
const loading = ref(true)
const loadFailed = ref(false)
const activeTab = ref<InboxFilter>('all')
const now = new Date()

const kindIcons: Record<InboxKind, Component> = {
  news: PhNewspaper,
  grade: PhCheckCircle,
  needsWork: PhArrowCounterClockwise,
  deadline: PhClock,
  assignment: PhExam,
}

// Betyg är grönt som i mockupen, komplettering gult. Resten är dämpat.
const kindIconClasses: Record<InboxKind, string> = {
  news: 'bg-muted',
  grade: 'bg-emerald-50 text-emerald-700 dark:bg-emerald-950 dark:text-emerald-300',
  needsWork: 'bg-amber-50 text-amber-700 dark:bg-amber-950 dark:text-amber-300',
  deadline: 'bg-muted',
  assignment: 'bg-muted',
}

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

// Nyheter som har en oläst notis är olästa. Äldre nyheter, från före notiserna, räknas som lästa.
const unreadNewsIds = computed(() => new Set(notifications.value
  .filter((notification) => notification.kind === 'NewsPublished' && notification.readAt === null)
  .map((notification) => notification.contentId)))

const rows = computed(() => sortRows([
  ...entries.value.map((entry): InboxRow => ({
    key: `news-${entry.post.contentId}`,
    kind: 'news',
    at: entry.post.publishAt,
    unread: unreadNewsIds.value.has(entry.post.contentId),
    title: entry.post.title,
    where: entry.targets.join(' · '),
    pinned: entry.post.isPinned,
    contentId: entry.post.contentId,
  })),
  ...notifications.value.flatMap((notification) => {
    const kind = inboxKindOf(notification)
    return kind
      ? [notificationRow(notification, kind)]
      : []
  }),
]))

const visibleRows = computed(() => rows.value.filter((row) => matchesFilter(row, activeTab.value)))
const unreadRows = computed(() => rows.value.filter((row) => row.unread).length)

const tabs = computed<{value: InboxFilter, label: string}[]>(() => [
  {value: 'all', label: 'Alla'},
  {
    value: 'unread',
    label: unreadRows.value > 0
      ? `Olästa ${unreadRows.value}`
      : 'Olästa',
  },
  {value: 'grades', label: 'Betyg'},
  {value: 'news', label: 'Nyheter'},
])

const emptyTexts: Record<InboxFilter, string> = {
  all: 'Inget här än.',
  unread: 'Du har läst allt.',
  grades: 'Inga betyg än.',
  news: 'Inga nyheter.',
}

const sections = computed(() => dayHeadings
  .map((heading) => ({
    heading,
    rows: visibleRows.value.filter((row) => dayGroup(row.at, now) === heading),
  }))
  .filter((section) => section.rows.length > 0))

const selectedId = computed(() => typeof route.query.post === 'string'
  ? route.query.post
  : '')
const visibleNews = computed(() => entries.value.filter((entry) =>
  visibleRows.value.some((row) => row.contentId === entry.post.contentId)))
const selected = computed(() =>
  visibleNews.value.find((entry) => entry.post.contentId === selectedId.value) ?? visibleNews.value[0])
const isEmpty = computed(() => !loading.value && !loadFailed.value && rows.value.length === 0)

function tabClass(value: InboxFilter) {
  return activeTab.value === value
    ? 'bg-background font-medium shadow-sm'
    : 'text-muted-foreground'
}

// "DevOps · Betyg · 11:20". Fäst först för nyheter.
function metaOf(row: InboxRow): string {
  return [row.where, kindLabels[row.kind], timeLabel(row.at)]
    .filter((part) => part !== '')
    .join(' · ')
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
    await loadNotifications()
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

// Grader och komplettering leder till studieplanen, uppgifter och deadlines till kursen.
function notificationLink(row: InboxRow): RouteLocationRaw {
  if (row.kind === 'grade' || row.kind === 'needsWork') {
    return {name: 'grades'}
  }
  const courseId = row.notification?.courseId
  return courseId
    ? {name: 'course-content', params: {courseId}}
    : {name: 'today'}
}

function markRowRead(row: InboxRow) {
  if (row.notification) {
    markRead([row.notification.id])
  }
}

// Nyheten är läst när den öppnats i detaljen, oavsett om den valdes här eller via en länk (?post=).
function markNewsRead(contentId: string) {
  markRead(notifications.value
    .filter((notification) => notification.kind === 'NewsPublished' && notification.contentId === contentId)
    .map((notification) => notification.id))
}

async function afterDelete() {
  await load(false)
  await router.replace({query: {}})
}

watch([selectedId, loading], ([contentId, isLoading]) => {
  if (contentId && !isLoading) {
    markNewsRead(contentId)
  }
})

// Kurserna hämtas av AppLayout. Flödena kan hämtas först när listan finns.
watch(status, (value) => {
  if (value !== 'loading') {
    load()
  }
}, {immediate: true})
</script>

<template>
  <PageActions>
    <Button
      variant="outline"
      class="h-8"
      :disabled="unreadCount === 0"
      data-test="mark-all-read"
      @click="markAllRead"
    >
      <PhChecks/>
      Markera alla som lästa
    </Button>
  </PageActions>

  <div class="flex min-w-0 flex-1 flex-wrap items-stretch">
    <template v-if="isEmpty">
      <Empty class="m-auto border border-dashed">
        <EmptyHeader>
          <EmptyMedia variant="icon">
            <PhTray/>
          </EmptyMedia>
          <EmptyTitle>Inkorgen är tom</EmptyTitle>
          <EmptyDescription>Nyheter, betyg och påminnelser från dina kurser och grupper visas här.</EmptyDescription>
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
          <p
            v-if="visibleRows.length === 0"
            class="text-muted-foreground px-4 py-6 text-center text-[13px]"
            data-test="tab-empty"
          >
            {{ emptyTexts[activeTab] }}
          </p>

          <template v-for="section in sections" :key="section.heading">
            <div
              class="text-muted-foreground px-4 pt-2.5 pb-1 text-xs font-medium"
              data-test="news-day"
            >
              {{ section.heading }}
            </div>
            <template v-for="row in section.rows" :key="row.key">
              <button
                v-if="row.kind === 'news' && row.contentId"
                type="button"
                class="flex w-full gap-3 px-4 py-3 text-left hover:bg-muted/40"
                :class="selected?.post.contentId === row.contentId
                  ? 'bg-muted shadow-[inset_2px_0_0_var(--color-primary)]'
                  : ''"
                :aria-current="selected?.post.contentId === row.contentId
                  ? 'true'
                  : undefined"
                data-test="news-item"
                @click="select(row.contentId)"
              >
                <InboxRowContent
                  :icon="kindIcons[row.kind]"
                  :icon-class="kindIconClasses[row.kind]"
                  :row="row"
                  :meta="metaOf(row)"
                />
              </button>
              <RouterLink
                v-else
                :to="notificationLink(row)"
                class="flex w-full gap-3 px-4 py-3 text-left hover:bg-muted/40"
                data-test="notification-item"
                @click="markRowRead(row)"
              >
                <InboxRowContent
                  :icon="kindIcons[row.kind]"
                  :icon-class="kindIconClasses[row.kind]"
                  :row="row"
                  :meta="metaOf(row)"
                />
              </RouterLink>
            </template>
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
            :course-id="selected.post.courseId ?? undefined"
            @changed="load(false)"
            @deleted="afterDelete"
          />
        </div>
      </section>
    </template>
  </div>
</template>
