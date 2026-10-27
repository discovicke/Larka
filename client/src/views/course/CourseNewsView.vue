<script setup lang="ts">
import {PhNewspaper, PhPushPin} from '@phosphor-icons/vue'
import {computed, ref, watch} from 'vue'
import {useRoute, useRouter} from 'vue-router'

import NewsPostDialog from '@/components/course/NewsPostDialog.vue'
import {Alert, AlertDescription} from '@/components/ui/alert'
import {Avatar, AvatarFallback} from '@/components/ui/avatar'
import {Button} from '@/components/ui/button'
import {Empty, EmptyContent, EmptyDescription, EmptyHeader, EmptyMedia, EmptyTitle} from '@/components/ui/empty'
import {Skeleton} from '@/components/ui/skeleton'
import {Textarea} from '@/components/ui/textarea'
import {api, type Schema} from '@/lib/api'
import {formatDate} from '@/lib/content'

const route = useRoute()
const router = useRouter()
const courseId = computed(() => String(route.params.courseId))

const users = ref<Schema<'UserDto'>[]>([])
const posts = ref<Schema<'ContentFeedItem'>[]>([])
const loading = ref(true)
const loadFailed = ref(false)

const comments = ref<Schema<'CommentDto'>[]>([])
const commentsLoading = ref(false)
const commentBody = ref('')
const sending = ref(false)
const commentFailed = ref(false)

// Flödet sorteras om här också, så att ordningen blir rätt även om backend ändrar sig.
const sortedPosts = computed(() => [...posts.value].sort((left, right) => {
  if (left.isPinned !== right.isPinned) {
    return left.isPinned
      ? -1
      : 1
  }
  return Date.parse(right.publishAt) - Date.parse(left.publishAt)
}))

const selectedLinkId = computed(() => typeof route.query.post === 'string'
  ? route.query.post
  : '')
const selected = computed(() =>
  sortedPosts.value.find((post) => post.linkId === selectedLinkId.value) ?? sortedPosts.value[0])

const commentCount = computed(() => comments.value.length === 1
  ? '1 kommentar'
  : `${comments.value.length} kommentarer`)

function nameOf(authorId: string): string {
  return users.value.find((user) => user.id === authorId)?.name ?? 'Okänd'
}

function initials(name: string): string {
  return name
    .split(' ')
    .filter((part) => part !== '')
    .slice(0, 2)
    .map((part) => part[0].toUpperCase())
    .join('') || '?'
}

function paragraphs(bodyMd: string): string[] {
  return bodyMd
    .split(/\n\s*\n/)
    .map((paragraph) => paragraph.trim())
    .filter((paragraph) => paragraph !== '')
}

async function loadPosts() {
  loadFailed.value = false
  loading.value = true
  try {
    const {data} = await api.GET('/api/courses/{id}/content', {
      params: {path: {id: courseId.value}, query: {type: 'news'}},
    })
    posts.value = data ?? []
  } catch {
    loadFailed.value = true
  } finally {
    loading.value = false
  }
}

async function loadUsers() {
  try {
    const {data} = await api.GET('/api/users')
    users.value = data ?? []
  } catch {
    // Namnen är ett komplement. Utan dem visas "Okänd" i stället för authorId.
  }
}

async function loadComments(contentId: string) {
  comments.value = []
  commentsLoading.value = true
  commentFailed.value = false
  try {
    const {data} = await api.GET('/api/content/{id}/comments', {params: {path: {id: contentId}}})
    comments.value = data ?? []
  } catch {
    // Kommentarerna är sekundära: går hämtningen fel visas inga kommentarer.
  } finally {
    commentsLoading.value = false
  }
}

function select(post: Schema<'ContentFeedItem'>) {
  router.replace({query: {post: post.linkId}})
}

async function submitComment() {
  const post = selected.value
  const bodyMd = commentBody.value.trim()
  if (!post || bodyMd === '') {
    return
  }

  sending.value = true
  commentFailed.value = false
  try {
    const {data} = await api.POST('/api/content/{id}/comments', {
      params: {path: {id: post.contentId}},
      body: {bodyMd},
    })
    if (data) {
      comments.value = [...comments.value, data]
    }
    commentBody.value = ''
  } catch {
    commentFailed.value = true
  } finally {
    sending.value = false
  }
}

async function created(linkId: string) {
  await loadPosts()
  await router.replace({query: {post: linkId}})
}

watch(courseId, () => {
  users.value = []
  loadPosts()
  loadUsers()
}, {immediate: true})

watch(() => selected.value?.contentId, (contentId) => {
  if (contentId) {
    loadComments(contentId)
  }
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
      <EmptyContent>
        <NewsPostDialog :course-id="courseId" @created="created"/>
      </EmptyContent>
    </Empty>

    <template v-else-if="!loadFailed && posts.length > 0">
      <div class="flex justify-end">
        <NewsPostDialog :course-id="courseId" @created="created"/>
      </div>

      <div class="flex flex-wrap overflow-hidden rounded-[11px] border">
        <div class="flex-[1_1_360px] min-w-0 border-r max-md:border-r-0 max-md:border-b">
          <button
            v-for="post in sortedPosts"
            :key="post.linkId"
            type="button"
            class="flex w-full flex-col gap-1 border-b border-muted px-5 py-3.5 text-left hover:bg-muted/50"
            :class="selected?.linkId === post.linkId
              ? 'bg-muted shadow-[inset_2px_0_0_var(--color-primary)]'
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
            <span class="text-muted-foreground text-[13px]">{{ nameOf(post.authorId) }}</span>
          </button>
        </div>

        <article class="flex-[999_1_520px] min-w-0 px-[clamp(16px,3vw,40px)] pt-7 pb-12">
          <div v-if="selected" class="flex max-w-[680px] flex-col gap-5">
            <div class="flex flex-col gap-3">
              <h2 class="font-heading text-[28px] leading-tight font-semibold">{{ selected.title }}</h2>
              <div class="flex items-center gap-3">
                <Avatar class="size-8">
                  <AvatarFallback class="text-xs font-semibold">{{ initials(nameOf(selected.authorId)) }}</AvatarFallback>
                </Avatar>
                <div class="flex flex-col">
                  <span class="font-medium">{{ nameOf(selected.authorId) }}</span>
                  <span class="text-muted-foreground text-xs">{{ formatDate(selected.publishAt, true) }}</span>
                </div>
              </div>
            </div>

            <p v-for="(paragraph, index) in paragraphs(selected.bodyMd)" :key="index" class="whitespace-pre-line">
              {{ paragraph }}
            </p>

            <section aria-labelledby="comments-heading" class="flex flex-col gap-4 border-t pt-3">
              <h3 id="comments-heading" class="text-base font-semibold">{{ commentCount }}</h3>

              <Skeleton v-if="commentsLoading" class="h-16 w-full"/>
              <ul v-else class="flex flex-col gap-4">
                <li
                  v-for="comment in comments"
                  :key="comment.id"
                  class="flex gap-3"
                  data-test="comment"
                >
                  <Avatar class="size-8">
                    <AvatarFallback class="text-xs font-semibold">{{ initials(nameOf(comment.authorId)) }}</AvatarFallback>
                  </Avatar>
                  <div class="flex min-w-0 flex-col gap-1">
                    <div class="flex flex-wrap items-baseline gap-1.5">
                      <span class="font-medium">{{ nameOf(comment.authorId) }}</span>
                      <span class="text-muted-foreground text-xs">· {{ formatDate(comment.createdAt, true) }}</span>
                    </div>
                    <p class="text-foreground/80 whitespace-pre-line">{{ comment.bodyMd }}</p>
                  </div>
                </li>
              </ul>

              <form class="flex flex-col gap-2" @submit.prevent="submitComment">
                <label class="sr-only" for="comment-body">Skriv en kommentar</label>
                <Textarea
                  id="comment-body"
                  v-model="commentBody"
                  placeholder="Skriv en kommentar…"
                  rows="2"
                />
                <div class="flex flex-wrap items-center justify-between gap-2">
                  <span class="text-muted-foreground text-xs">Synlig för alla i kursen</span>
                  <Button
                    type="submit"
                    size="sm"
                    :disabled="commentBody.trim() === '' || sending"
                  >
                    Kommentera
                  </Button>
                </div>
              </form>

              <Alert v-if="commentFailed" variant="destructive">
                <AlertDescription>Kunde inte skicka kommentaren.</AlertDescription>
              </Alert>
            </section>
          </div>
        </article>
      </div>
    </template>
  </div>
</template>
