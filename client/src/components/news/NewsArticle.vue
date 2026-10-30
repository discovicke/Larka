<script setup lang="ts">
import {
  PhDotsThree,
  PhNewspaper,
  PhPencilSimple,
  PhPushPin,
  PhPushPinSlash,
  PhTrash,
} from '@phosphor-icons/vue'
import {computed, ref, watch} from 'vue'

import MarkdownView from '@/components/editor/MarkdownView.vue'
import EditNewsDialog from '@/components/news/EditNewsDialog.vue'
import {Alert, AlertDescription} from '@/components/ui/alert'
import {Avatar, AvatarFallback} from '@/components/ui/avatar'
import {Button} from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import {Skeleton} from '@/components/ui/skeleton'
import {Textarea} from '@/components/ui/textarea'
import {useCurrentUser} from '@/composables/useCurrentUser'
import {api, type Schema} from '@/lib/api'
import {formatDate} from '@/lib/content'
import {initials} from '@/lib/news'

const props = defineProps<{
  post: Schema<'ContentFeedItem'>
  links?: Schema<'ContentFeedItem'>[]
  targets?: string[]
  // Kursen nyheten visas i. Gruppnyheter i inkorgen har ingen kurs, och då krävs admin.
  courseId?: string
}>()

const emit = defineEmits<{
  changed: []
  deleted: []
}>()

const {can} = useCurrentUser()
const canPublish = computed(() => props.courseId
  ? can('publishNews', props.courseId)
  : can('admin'))

// Nyheten kan vara publicerad i flera kurser och grupper; då ändras alla länkar samtidigt.
const links = computed(() => props.links?.length
  ? props.links
  : [props.post])
const isPinned = computed(() => links.value.some((link) => link.isPinned))

const comments = ref<Schema<'CommentDto'>[]>([])
const commentsLoading = ref(false)
const commentBody = ref('')
const sending = ref(false)
const commentFailed = ref(false)

const editOpen = ref(false)
const deleteOpen = ref(false)
const deleting = ref(false)
const actionError = ref('')

const commentCount = computed(() => comments.value.length === 1
  ? '1 kommentar'
  : `${comments.value.length} kommentarer`)

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

async function submitComment() {
  const bodyMd = commentBody.value.trim()
  if (bodyMd === '') {
    return
  }

  sending.value = true
  commentFailed.value = false
  try {
    const {data} = await api.POST('/api/content/{id}/comments', {
      params: {path: {id: props.post.contentId}},
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

async function togglePin() {
  actionError.value = ''
  const pinned = !isPinned.value
  try {
    for (const link of links.value) {
      await api.PUT('/api/content/{id}/links/{linkId}', {
        params: {path: {id: link.contentId, linkId: link.linkId}},
        body: {publishAt: link.publishAt, dueAt: null, isPinned: pinned},
      })
    }
    emit('changed')
  } catch {
    actionError.value = 'Kunde inte ändra nyheten.'
  }
}

async function remove() {
  deleting.value = true
  actionError.value = ''
  try {
    for (const link of links.value) {
      await api.DELETE('/api/content/{id}/links/{linkId}', {
        params: {path: {id: link.contentId, linkId: link.linkId}},
      })
    }
    deleteOpen.value = false
    emit('deleted')
  } catch {
    deleteOpen.value = false
    actionError.value = 'Kunde inte ta bort nyheten.'
  } finally {
    deleting.value = false
  }
}

watch(() => props.post.contentId, (contentId) => {
  if (contentId) {
    loadComments(contentId)
  }
}, {immediate: true})

watch(() => props.post.linkId, () => {
  actionError.value = ''
})
</script>

<template>
  <div class="flex max-w-[680px] flex-col gap-5">
    <Alert v-if="actionError" variant="destructive">
      <AlertDescription>{{ actionError }}</AlertDescription>
    </Alert>

    <div class="flex flex-col gap-3">
      <div v-if="targets?.length" class="flex items-center gap-3.5">
        <span class="bg-muted flex size-14 shrink-0 items-center justify-center rounded-[11px] border">
          <PhNewspaper class="size-6"/>
        </span>
        <div class="flex flex-col gap-0.5">
          <span class="text-muted-foreground text-[13px]">{{ targets.join(' · ') }} · Nyhet</span>
          <h1 class="font-heading text-2xl leading-tight font-semibold">{{ post.title }}</h1>
        </div>
      </div>
      <h2 v-else class="font-heading text-[28px] leading-tight font-semibold">{{ post.title }}</h2>
      <div class="flex items-center gap-3">
        <Avatar class="size-8">
          <AvatarFallback class="text-xs font-semibold">{{ initials(post.authorName) }}</AvatarFallback>
        </Avatar>
        <div class="flex flex-col">
          <span class="font-medium">{{ post.authorName }}</span>
          <span class="text-muted-foreground text-xs">{{ formatDate(post.publishAt, true) }}</span>
        </div>
        <DropdownMenu v-if="canPublish">
          <DropdownMenuTrigger as-child>
            <Button
              variant="ghost"
              size="icon-sm"
              class="ml-auto"
              aria-label="Fler val för nyheten"
              data-test="news-menu"
            >
              <PhDotsThree/>
            </Button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end">
            <DropdownMenuItem @select="editOpen = true">
              <PhPencilSimple/>
              Redigera
            </DropdownMenuItem>
            <DropdownMenuItem @select="togglePin">
              <PhPushPin v-if="!isPinned"/>
              <PhPushPinSlash v-else/>
              {{ isPinned
                ? 'Lossa'
                : 'Fäst överst' }}
            </DropdownMenuItem>
            <DropdownMenuItem variant="destructive" class="text-destructive" @select="deleteOpen = true">
              <PhTrash/>
              Ta bort
            </DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>
      </div>
    </div>

    <MarkdownView :markdown="post.bodyMd"/>

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
            <AvatarFallback class="text-xs font-semibold">{{ initials(comment.authorName) }}</AvatarFallback>
          </Avatar>
          <div class="flex min-w-0 flex-col gap-1">
            <div class="flex flex-wrap items-baseline gap-1.5">
              <span class="font-medium">{{ comment.authorName }}</span>
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

    <EditNewsDialog v-model:open="editOpen" :post="post" @saved="emit('changed')"/>

    <Dialog v-model:open="deleteOpen">
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Ta bort nyheten?</DialogTitle>
          <DialogDescription v-if="links.length > 1">
            Nyheten tas bort från alla kurser och grupper den är publicerad i.
          </DialogDescription>
          <DialogDescription v-else>
            Nyheten tas bort från kursen. Kommentarerna försvinner om den inte finns kvar någon annanstans.
          </DialogDescription>
        </DialogHeader>
        <DialogFooter>
          <Button variant="outline" :disabled="deleting" @click="deleteOpen = false">
            Avbryt
          </Button>
          <Button variant="destructive" :disabled="deleting" @click="remove">
            Ta bort
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  </div>
</template>
