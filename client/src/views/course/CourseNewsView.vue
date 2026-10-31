<script setup lang="ts">
import {PhArrowLeft, PhNewspaper, PhPushPin} from '@phosphor-icons/vue'
import {computed, ref, watch} from 'vue'
import {useRoute, useRouter} from 'vue-router'

import NewsArticle from '@/components/news/NewsArticle.vue'
import NewsPostDialog from '@/components/news/NewsPostDialog.vue'
import {Alert, AlertDescription} from '@/components/ui/alert'
import {Empty, EmptyContent, EmptyDescription, EmptyHeader, EmptyMedia, EmptyTitle} from '@/components/ui/empty'
import {Skeleton} from '@/components/ui/skeleton'
import {useCurrentUser} from '@/composables/useCurrentUser'
import {useMobileDetail} from '@/composables/useMobileDetail'
import {type Schema} from '@/lib/api'
import {formatDate} from '@/lib/content'
import {fetchCourseContent} from '@/lib/coursesApi'
import {sortNews} from '@/lib/news'

const route = useRoute()
const router = useRouter()
const courseId = computed(() => String(route.params.courseId))
const {can} = useCurrentUser()
const canPublish = computed(() => can('publishNews', courseId.value))

const posts = ref<Schema<'ContentFeedItem'>[]>([])
const loading = ref(true)
const loadFailed = ref(false)

// Flödet sorteras om här också, så att ordningen blir rätt även om backend ändrar sig.
const sortedPosts = computed(() => sortNews(posts.value))

const selectedLinkId = computed(() => typeof route.query.post === 'string'
  ? route.query.post
  : '')
const {isMobile, open} = useMobileDetail(computed(() => selectedLinkId.value || undefined))

// På mobil är ingen nyhet vald förrän eleven öppnat en; på desktop visas den första.
const selected = computed(() =>
  sortedPosts.value.find((post) => post.linkId === selectedLinkId.value)
  ?? (isMobile.value
    ? undefined
    : sortedPosts.value[0]))

async function loadPosts() {
  loadFailed.value = false
  try {
    posts.value = await fetchCourseContent(courseId.value, 'news')
  } catch {
    loadFailed.value = true
  } finally {
    loading.value = false
  }
}

function select(post: Schema<'ContentFeedItem'>) {
  open(post.linkId)
}

async function created(linkId: string) {
  await loadPosts()
  await router.replace({query: {post: linkId}})
}

async function afterDelete() {
  await loadPosts()
  await router.replace({query: {}})
}

watch(courseId, () => {
  loading.value = true
  loadPosts()
}, {immediate: true})
</script>

<template>
  <div class="flex flex-col gap-4">
    <Alert v-if="loadFailed" variant="destructive">
      <AlertDescription>Kunde inte hämta nyheterna.</AlertDescription>
    </Alert>

    <Skeleton v-if="loading" class="h-64 w-full rounded-[11px]"/>

    <Empty v-else-if="posts.length === 0 && !loadFailed" class="border border-dashed">
      <EmptyHeader>
        <EmptyMedia variant="icon">
          <PhNewspaper/>
        </EmptyMedia>
        <EmptyTitle>Inga nyheter än</EmptyTitle>
        <EmptyDescription>Nyheter som läraren publicerar i kursen visas här.</EmptyDescription>
      </EmptyHeader>
      <EmptyContent v-if="canPublish">
        <NewsPostDialog :course-id="courseId" @created="created"/>
      </EmptyContent>
    </Empty>

    <template v-else-if="!loadFailed && posts.length > 0">
      <div v-if="canPublish" class="flex justify-end">
        <NewsPostDialog :course-id="courseId" @created="created"/>
      </div>

      <div class="bg-card flex flex-wrap overflow-hidden rounded-[11px] border">
        <div v-if="!isMobile || !selected" class="flex-[1_1_360px] min-w-0 border-r max-md:border-r-0">
          <button
            v-for="post in sortedPosts"
            :key="post.linkId"
            type="button"
            class="flex w-full flex-col gap-1 border-b border-muted px-5 py-3.5 text-left hover:bg-muted/50"
            :class="selected?.linkId === post.linkId
              ? 'bg-accent'
              : ''"
            :aria-current="selected?.linkId === post.linkId
              ? 'true'
              : undefined"
            data-test="news-item"
            @click="select(post)"
          >
            <span class="text-muted-foreground flex justify-between text-xs">
              <span v-if="post.isPinned" class="inline-flex items-center gap-1">
                <PhPushPin class="size-3"/>
                Fäst
              </span>
              <span v-else/>
              <span>{{ formatDate(post.publishAt) }}</span>
            </span>
            <span class="font-semibold">{{ post.title }}</span>
            <span class="text-muted-foreground text-[13px]">{{ post.authorName }}</span>
          </button>
        </div>

        <article v-if="!isMobile || selected" class="flex-[999_1_520px] min-w-0 px-[clamp(16px,3vw,40px)] pt-7 pb-12">
          <RouterLink
            v-if="isMobile"
            :to="{query: {}}"
            class="text-muted-foreground hover:text-foreground mb-4 inline-flex items-center gap-1.5 text-sm"
            data-test="back-to-list"
          >
            <PhArrowLeft class="size-4"/>
            Alla nyheter
          </RouterLink>
          <NewsArticle
            v-if="selected"
            :post="selected"
            :course-id="courseId"
            @changed="loadPosts"
            @deleted="afterDelete"
          />
        </article>
      </div>
    </template>
  </div>
</template>
