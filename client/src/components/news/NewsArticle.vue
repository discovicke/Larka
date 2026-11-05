<script setup lang="ts">
import {
  PhDotsThree,
  PhNewspaper,
  PhPencilSimple,
  PhPushPin,
  PhPushPinSlash,
  PhTrash,
} from '@phosphor-icons/vue'
import {computed, nextTick, ref, watch} from 'vue'

import ConfirmDialog from '@/components/ConfirmDialog.vue'
import MarkdownView from '@/components/editor/MarkdownView.vue'
import CommentText from '@/components/news/CommentText.vue'
import EditNewsDialog from '@/components/news/EditNewsDialog.vue'
import MentionTextarea from '@/components/news/MentionTextarea.vue'
import {Alert, AlertDescription} from '@/components/ui/alert'
import {Avatar, AvatarFallback, AvatarImage} from '@/components/ui/avatar'
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
import TextLimitNote from '@/components/TextLimitNote.vue'
import {useAsync} from '@/composables/useAsync'
import {useToast} from '@/composables/useToast'
import {useCurrentUser} from '@/composables/useCurrentUser'
import {api, type Schema} from '@/lib/api'
import {deleteContentLink, updateContentLink} from '@/lib/contentApi'
import {formatDate} from '@/lib/content'
import {type MentionMap, toEditable, toStored} from '@/lib/mentions'
import {initials} from '@/lib/person'
import {isTooLong, textLimits} from '@/lib/textLimits'

const props = defineProps<{
  post: Schema<'ContentFeedItem'>
  links?: Schema<'ContentFeedItem'>[]
  targets?: string[]
  // Kursen nyheten visas i. Gruppnyheter i inkorgen har ingen kurs, och då krävs en lärarroll.
  courseId?: string
  // Kommentaren att skrolla fram till och markera, när nyheten öppnas från en notis om den.
  focusCommentId?: string
}>()

const emit = defineEmits<{
  changed: []
  deleted: []
}>()

const {can} = useCurrentUser()
const canPublish = computed(() => can('publishNews', props.courseId))

// Nyheten kan vara publicerad i flera kurser och grupper; då ändras alla länkar samtidigt.
const links = computed(() => props.links?.length
  ? props.links
  : [props.post])
const isPinned = computed(() => links.value.some((link) => link.isPinned))

const comments = ref<Schema<'CommentDto'>[]>([])
// Fälten visar @Namn. Vem namnen är hålls i en egen lista och läggs in i texten när den skickas.
const commentBody = ref('')
const commentMentions = ref<MentionMap>(new Map())
const storedComment = computed(() => toStored(commentBody.value.trim(), commentMentions.value))

const editOpen = ref(false)
const deleteOpen = ref(false)
const deleting = ref(false)
const actionError = ref('')

const commentCount = computed(() => comments.value.length === 1
  ? '1 kommentar'
  : `${comments.value.length} kommentarer`)

// Kommentarerna är sekundära: går hämtningen fel visas inga kommentarer, och inget fel.
const {run: fetchComments, loading: commentsLoading} = useAsync(async (contentId: string) => {
  const {data} = await api.GET('/api/content/{id}/comments', {params: {path: {id: contentId}}})
  return data ?? []
})

const {showToast} = useToast()
const {run: sendComment, loading: sending, error: sendError} = useAsync(async (bodyMd: string) => {
  const {data} = await api.POST('/api/content/{id}/comments', {
    params: {path: {id: props.post.contentId}},
    body: {bodyMd},
  })
  if (data) {
    comments.value = [...comments.value, data]
  }
  commentBody.value = ''
  commentMentions.value = new Map()
  showToast('Kommentaren är skickad.')
})
const commentTooLong = computed(() => isTooLong(storedComment.value, textLimits.comment))

// Författaren ändrar sin kommentar på plats. Läraren i kursen och författaren tar bort den.
const editingId = ref<string | null>(null)
const editBody = ref('')
const editMentions = ref<MentionMap>(new Map())
const storedEdit = computed(() => toStored(editBody.value.trim(), editMentions.value))
const editTooLong = computed(() => isTooLong(storedEdit.value, textLimits.comment))
const removingComment = ref<Schema<'CommentDto'> | null>(null)
const removeOpen = computed({
  get: () => removingComment.value !== null,
  set: (open: boolean) => {
    if (!open) {
      removingComment.value = null
    }
  },
})

const {run: saveEdit, loading: savingEdit, error: editError} = useAsync(async () => {
  const commentId = editingId.value
  const bodyMd = storedEdit.value
  if (!commentId || bodyMd === '') {
    return
  }
  const {data} = await api.PUT('/api/content/{id}/comments/{commentId}', {
    params: {path: {id: props.post.contentId, commentId}},
    body: {bodyMd},
  })
  if (data) {
    comments.value = comments.value.map((comment) => comment.id === commentId
      ? data
      : comment)
  }
  editingId.value = null
  showToast('Kommentaren är ändrad.')
})

function startEdit(comment: Schema<'CommentDto'>) {
  editingId.value = comment.id
  const editable = toEditable(comment.bodyMd)
  editBody.value = editable.text
  editMentions.value = editable.mentions
  editError.value = ''
}

async function removeComment() {
  const comment = removingComment.value
  if (!comment) {
    return
  }
  await api.DELETE('/api/content/{id}/comments/{commentId}', {
    params: {path: {id: props.post.contentId, commentId: comment.id}},
  })
  comments.value = comments.value.filter((kept) => kept.id !== comment.id)
  showToast('Kommentaren är borttagen.')
}

async function loadComments(contentId: string) {
  comments.value = []
  sendError.value = ''
  editingId.value = null
  const loaded = await fetchComments(contentId)
  if (loaded) {
    comments.value = loaded
    await showFocusedComment()
  }
}

async function showFocusedComment() {
  if (!props.focusCommentId) {
    return
  }
  await nextTick()
  document.getElementById(`comment-${props.focusCommentId}`)?.scrollIntoView({block: 'center'})
}

async function submitComment() {
  const bodyMd = storedComment.value
  if (bodyMd === '' || commentTooLong.value) {
    return
  }

  await sendComment(bodyMd)
}

async function togglePin() {
  actionError.value = ''
  const pinned = !isPinned.value
  try {
    for (const link of links.value) {
      await updateContentLink(link.contentId, link.linkId, {publishAt: link.publishAt, dueAt: null, isPinned: pinned})
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
      await deleteContentLink(link.contentId, link.linkId)
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

// En ny notis om samma nyhet byter bara kommentaren, inte nyheten.
watch(() => props.focusCommentId, () => {
  if (!commentsLoading.value) {
    void showFocusedComment()
  }
})

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
          <AvatarImage
            v-if="post.authorAvatarUrl"
            :src="post.authorAvatarUrl"
            alt=""
            data-test="author-avatar-image"
          />
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
          :id="`comment-${comment.id}`"
          :key="comment.id"
          :class="[
            'flex scroll-mt-6 gap-3 rounded-lg',
            comment.id === focusCommentId
              ? 'bg-primary/5 -mx-2 px-2 py-1.5 ring-1 ring-primary/20'
              : '',
          ]"
          data-test="comment"
        >
          <Avatar class="size-8">
            <AvatarImage
              v-if="comment.authorAvatarUrl"
              :src="comment.authorAvatarUrl"
              alt=""
              data-test="comment-avatar-image"
            />
            <AvatarFallback class="text-xs font-semibold">{{ initials(comment.authorName) }}</AvatarFallback>
          </Avatar>
          <div class="flex min-w-0 flex-1 flex-col gap-1">
            <div class="flex items-start gap-1.5">
              <div class="flex min-w-0 flex-1 flex-wrap items-baseline gap-1.5">
                <span class="font-medium break-words">{{ comment.authorName }}</span>
                <span class="text-muted-foreground text-xs">· {{ formatDate(comment.createdAt, true) }}</span>
              </div>
              <DropdownMenu v-if="(comment.canEdit || comment.canDelete) && editingId !== comment.id">
                <DropdownMenuTrigger as-child>
                  <Button
                    variant="ghost"
                    size="icon-sm"
                    class="-mt-1 shrink-0"
                    aria-label="Fler val för kommentaren"
                    data-test="comment-menu"
                  >
                    <PhDotsThree/>
                  </Button>
                </DropdownMenuTrigger>
                <DropdownMenuContent align="end">
                  <DropdownMenuItem v-if="comment.canEdit" data-test="comment-edit" @select="startEdit(comment)">
                    <PhPencilSimple/>
                    Redigera
                  </DropdownMenuItem>
                  <DropdownMenuItem
                    v-if="comment.canDelete"
                    variant="destructive"
                    class="text-destructive"
                    data-test="comment-delete"
                    @select="removingComment = comment"
                  >
                    <PhTrash/>
                    Ta bort
                  </DropdownMenuItem>
                </DropdownMenuContent>
              </DropdownMenu>
            </div>
            <form
              v-if="editingId === comment.id"
              class="flex flex-col gap-2"
              data-test="comment-edit-form"
              @submit.prevent="saveEdit()"
            >
              <label class="sr-only" :for="`comment-edit-${comment.id}`">Ändra kommentaren</label>
              <MentionTextarea
                :id="`comment-edit-${comment.id}`"
                v-model="editBody"
                v-model:mentions="editMentions"
                :content-id="post.contentId"
                rows="2"
              />
              <TextLimitNote :text="storedEdit" :max="textLimits.comment"/>
              <Alert v-if="editError" variant="destructive">
                <AlertDescription>{{ editError }}</AlertDescription>
              </Alert>
              <div class="flex flex-wrap gap-2">
                <Button
                  type="submit"
                  size="sm"
                  :disabled="editBody.trim() === '' || editTooLong || savingEdit"
                  data-test="comment-edit-save"
                >
                  Spara
                </Button>
                <Button
                  type="button"
                  size="sm"
                  variant="ghost"
                  :disabled="savingEdit"
                  @click="editingId = null"
                >
                  Avbryt
                </Button>
              </div>
            </form>
            <CommentText v-else :body-md="comment.bodyMd"/>
          </div>
        </li>
      </ul>

      <form class="flex flex-col gap-2" @submit.prevent="submitComment">
        <label class="sr-only" for="comment-body">Skriv en kommentar</label>
        <MentionTextarea
          id="comment-body"
          v-model="commentBody"
          v-model:mentions="commentMentions"
          :content-id="post.contentId"
          placeholder="Skriv en kommentar… Nämn någon med @"
          rows="2"
        />
        <TextLimitNote :text="storedComment" :max="textLimits.comment"/>
        <div class="flex flex-wrap items-center justify-between gap-2">
          <span class="text-muted-foreground text-xs">Synlig för alla i kursen</span>
          <Button
            type="submit"
            size="sm"
            :disabled="commentBody.trim() === '' || commentTooLong || sending"
          >
            Kommentera
          </Button>
        </div>
      </form>

      <Alert v-if="sendError" variant="destructive">
        <AlertDescription>{{ sendError }}</AlertDescription>
      </Alert>
    </section>

    <ConfirmDialog
      v-model:open="removeOpen"
      title="Ta bort kommentaren?"
      description="Kommentaren tas bort för alla som läser nyheten."
      confirm-label="Ta bort"
      confirm-test="comment-delete-confirm"
      :action="removeComment"
    />

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
