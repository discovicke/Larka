<script setup lang="ts">
import {
  PhArrowCounterClockwise,
  PhArrowLeft,
  PhArrowRight,
  PhCheckCircle,
  PhChecks,
  PhClock,
  PhExam,
  PhNewspaper,
  PhQuestion,
  PhTray,
  PhXCircle,
} from '@phosphor-icons/vue'
import {computed, type Component, ref, watch} from 'vue'
import {type RouteLocationRaw, useRoute, useRouter} from 'vue-router'

import InboxRowContent from '@/components/inbox/InboxRowContent.vue'
import PageActions from '@/components/layout/PageActions.vue'
import PillTabs from '@/components/ui/PillTabs.vue'
import NewsArticle from '@/components/news/NewsArticle.vue'
import {Alert, AlertDescription} from '@/components/ui/alert'
import {Button} from '@/components/ui/button'
import {Empty, EmptyDescription, EmptyHeader, EmptyMedia, EmptyTitle} from '@/components/ui/empty'
import {Skeleton} from '@/components/ui/skeleton'
import {useCourses} from '@/composables/useCourses'
import {useMobileDetail} from '@/composables/useMobileDetail'
import {useNotifications} from '@/composables/useNotifications'
import {type Schema} from '@/lib/api'
import {formatDate} from '@/lib/content'
import {fetchCourseContent} from '@/lib/coursesApi'
import {fetchGroupContent, fetchGroups} from '@/lib/groupsApi'
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
import {gradeLabel} from '@/lib/submissions'

const route = useRoute()
const router = useRouter()
const {courses, status} = useCourses()
const {notifications, unreadCount, loadNotifications, markRead, markAllRead} = useNotifications()

const groups = ref<Schema<'GroupDto'>[]>([])
const posts = ref<Schema<'ContentFeedItem'>[]>([])
const loading = ref(true)
const loadFailed = ref(false)
const activeTab = ref<InboxFilter>('all')
const now = new Date()

const kindIcons: Record<InboxKind, Component> = {
  news: PhNewspaper,
  grade: PhCheckCircle,
  needsWork: PhArrowCounterClockwise,
  notSubmitted: PhXCircle,
  deadline: PhClock,
  assignment: PhExam,
  help: PhQuestion,
}

// Nyheter är blå, betyg gröna, komplettering gul. Deadlines och uppgifter har pennans gula ton.
const kindIconClasses: Record<InboxKind, string> = {
  news: 'bg-info/10 text-info dark:bg-info/20',
  grade: 'bg-success/10 text-success dark:bg-success/20',
  needsWork: 'bg-warning/10 text-warning dark:bg-warning/20',
  notSubmitted: 'bg-destructive/10 text-destructive dark:bg-destructive/20',
  deadline: 'bg-primary/25 text-foreground dark:bg-primary/15 dark:text-primary',
  assignment: 'bg-primary/25 text-foreground dark:bg-primary/15 dark:text-primary',
  help: 'bg-warning/10 text-warning dark:bg-warning/20',
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
const selectedNotificationId = computed(() => typeof route.query.message === 'string'
  ? route.query.message
  : '')
const selectedNotification = computed(() => rows.value.find((row) =>
  row.key === `notification-${selectedNotificationId.value}` && row.notification) ?? null)
const hasDetail = computed(() => Boolean(selected.value || selectedNotification.value))
const visibleNews = computed(() => entries.value.filter((entry) =>
  visibleRows.value.some((row) => row.contentId === entry.post.contentId)))
const {isMobile, open} = useMobileDetail(computed(() => selectedId.value || undefined))

// Ett öppnat inlägg slås upp bland alla nyheter, inte bara de som matchar fliken: när en nyhet markeras som läst
// försvinner den ur fliken Olästa, men artikeln ska ligga kvar. På mobil är ingen nyhet vald förrän eleven
// öppnat en; på desktop visas den första synliga. En öppnad notis går före nyhetsvalet, annars skulle
// den senaste nyheten se vald ut samtidigt som meddelandet.
const selected = computed(() => {
  if (selectedNotification.value) {
    return undefined
  }
  return entries.value.find((entry) => entry.post.contentId === selectedId.value)
    ?? (isMobile.value
      ? undefined
      : visibleNews.value[0])
})
const isEmpty = computed(() => !loading.value && !loadFailed.value && rows.value.length === 0)

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
    groups.value = await fetchGroups()

    // Alla flöden hämtas samtidigt; ett fel räcker för att visa felmeddelandet.
    const [courseFeeds, groupFeeds] = await Promise.all([
      Promise.all(courses.value.map((course) => fetchCourseContent(course.id, 'news'))),
      Promise.all(groups.value.map((group) => fetchGroupContent(group.id, 'news'))),
    ])
    posts.value = [...courseFeeds, ...groupFeeds].flat()
    await loadNotifications()
  } catch {
    loadFailed.value = true
    posts.value = []
  } finally {
    loading.value = false
  }
}

function select(contentId: string) {
  open(contentId)
}

function openNotification(row: InboxRow) {
  if (!row.notification) {
    return
  }

  markRowRead(row)
  const location = {query: {message: row.notification.id}}
  void (isMobile.value
    ? router.push(location)
    : router.replace(location))
}

// Meddelandet ska förklara vad notisen gäller, inte bara upprepa radrubriken.
function notificationMessage(row: InboxRow): string {
  const title = row.notification?.title ?? 'uppgiften'
  switch (row.kind) {
    case 'grade':
      return `Du har fått en bedömning på ${title}. Öppna uppgiften för att se betyget och lärarens kommentar.`
    case 'needsWork':
      return `Läraren vill att du kompletterar ${title}. Öppna uppgiften för att se vad som behöver göras.`
    case 'deadline':
      return `${title} ska lämnas in inom ett dygn. Öppna uppgiften för att se detaljerna.`
    case 'help':
      return `En elev har bett om hjälp med ${title}. Öppna uppgiften för att se frågan.`
    default:
      return `En ny uppgift har publicerats: ${title}. Öppna uppgiften för att läsa mer.`
  }
}

function notificationActionLabel(row: InboxRow): string {
  const notification = row.notification
  if (notification?.courseId && notification.linkId) {
    return 'Gå till uppgiften'
  }
  if (row.kind === 'grade' || row.kind === 'needsWork') {
    return 'Gå till betyget'
  }
  return notification?.courseId
    ? 'Gå till kursen'
    : 'Gå till Idag'
}

// Notiser om en del öppnar delen på kurssidan (?del=). Saknas kursen eller delen, som när innehållet
// är borttaget, faller grader och komplettering tillbaka på studieplanen och resten på kursen eller Idag.
function notificationLink(row: InboxRow): RouteLocationRaw {
  const courseId = row.notification?.courseId
  const linkId = row.notification?.linkId
  if (courseId && linkId) {
    return {name: 'course-content', params: {courseId}, query: {del: linkId}}
  }
  if (row.kind === 'grade' || row.kind === 'needsWork' || row.kind === 'notSubmitted') {
    return {name: 'grades'}
  }
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

watch([selectedNotificationId, loading], ([notificationId, isLoading]) => {
  if (!notificationId || isLoading) {
    return
  }
  const row = rows.value.find((candidate) => candidate.notification?.id === notificationId)
  if (row) {
    markRowRead(row)
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
      <div v-if="!isMobile || !hasDetail" class="flex min-w-0 flex-[1_1_420px] flex-col border-r max-md:border-r-0">
        <div class="border-b px-4 py-3">
          <PillTabs v-model="activeTab" :items="tabs"/>
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
                  ? 'bg-accent'
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
              <button
                v-else
                type="button"
                class="flex w-full gap-3 px-4 py-3 text-left hover:bg-muted/40"
                :class="selectedNotification?.key === row.key
                  ? 'bg-accent'
                  : ''"
                :aria-current="selectedNotification?.key === row.key
                  ? 'true'
                  : undefined"
                data-test="notification-item"
                @click="openNotification(row)"
              >
                <InboxRowContent
                  :icon="kindIcons[row.kind]"
                  :icon-class="kindIconClasses[row.kind]"
                  :row="row"
                  :meta="metaOf(row)"
                />
              </button>
            </template>
          </template>
        </template>
      </div>

      <section
        v-if="!isMobile || hasDetail"
        :aria-label="selectedNotification
          ? 'Meddelande'
          : 'Nyhet'"
        class="flex-[999_1_480px] min-w-0 px-[clamp(16px,3vw,40px)] py-8"
      >
        <RouterLink
          v-if="isMobile"
          :to="{query: {}}"
          class="text-muted-foreground hover:text-foreground mb-4 inline-flex items-center gap-1.5 text-sm"
          data-test="back-to-list"
        >
          <PhArrowLeft class="size-4"/>
          Tillbaka till inkorgen
        </RouterLink>
        <div
          v-if="selectedNotification"
          class="bg-card flex max-w-[640px] flex-col gap-4 rounded-xl border p-5"
          data-test="notification-detail"
        >
          <header class="flex flex-wrap items-start justify-between gap-3">
            <div class="flex items-center gap-3">
              <span
                class="flex size-10 shrink-0 items-center justify-center rounded-[9px]"
                :class="kindIconClasses[selectedNotification.kind]"
              >
                <component :is="kindIcons[selectedNotification.kind]" class="size-5"/>
              </span>
              <div class="flex flex-col gap-0.5">
                <span class="text-muted-foreground text-xs">{{ kindLabels[selectedNotification.kind] }}</span>
                <h1 class="text-xl leading-tight font-semibold">{{ selectedNotification.title }}</h1>
              </div>
            </div>
            <span class="text-muted-foreground text-xs">{{ formatDate(selectedNotification.at, true) }}</span>
          </header>

          <p class="text-sm leading-relaxed">{{ notificationMessage(selectedNotification) }}</p>

          <dl class="grid gap-2 text-sm sm:grid-cols-2">
            <div v-if="selectedNotification.where" class="flex flex-col gap-0.5">
              <dt class="text-muted-foreground text-xs">Gäller</dt>
              <dd>{{ selectedNotification.where }}</dd>
            </div>
            <div v-if="selectedNotification.notification?.level" class="flex flex-col gap-0.5">
              <dt class="text-muted-foreground text-xs">Betyg</dt>
              <dd>{{ gradeLabel(selectedNotification.notification.level) }}</dd>
            </div>
          </dl>

          <div>
            <Button as-child>
              <RouterLink
                :to="notificationLink(selectedNotification)"
                data-test="notification-go"
              >
                {{ notificationActionLabel(selectedNotification) }}
                <PhArrowRight/>
              </RouterLink>
            </Button>
          </div>
        </div>

        <div v-else-if="selected" class="max-w-[640px]">
          <NewsArticle
            :post="selected.post"
            :links="selected.links"
            :targets="selected.targets"
            :course-id="selected.post.courseId ?? undefined"
            @changed="load(false)"
            @deleted="afterDelete"
          />
        </div>
      </section>
    </template>
  </div>
</template>
