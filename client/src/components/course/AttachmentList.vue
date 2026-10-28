<script setup lang="ts">
import {PhTrash, PhUploadSimple} from '@phosphor-icons/vue'
import {ref, watch} from 'vue'

import {Alert, AlertDescription} from '@/components/ui/alert'
import {Button} from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import {api, multipart, type Schema} from '@/lib/api'
import {fileIcon, formatSize} from '@/lib/material'

const props = defineProps<{
  contentId: string
  // Eleven får se filerna men inte ladda upp eller ta bort dem.
  canManage: boolean
}>()

const attachments = ref<Schema<'AttachmentDto'>[]>([])
const loading = ref(true)
const uploading = ref(false)
const uploadFailed = ref(false)
const fileInput = ref<HTMLInputElement | null>(null)
const deleteOpen = ref(false)
const fileToDelete = ref<Schema<'AttachmentDto'> | null>(null)
const deleting = ref(false)
const deleteError = ref('')

async function load() {
  loading.value = true
  try {
    const {data} = await api.GET('/api/content/{id}/attachments', {
      params: {path: {id: props.contentId}},
    })
    attachments.value = data ?? []
  } catch {
    attachments.value = []
  } finally {
    loading.value = false
  }
}

async function onFileChanged(event: Event) {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  if (!file) {
    return
  }

  const body = new FormData()
  body.append('file', file, file.name)

  uploading.value = true
  uploadFailed.value = false
  try {
    // Formuläret skickas som multipart. OpenAPI-typen beskriver filen som en sträng, så FormData läggs in här.
    const {data} = await api.POST('/api/content/{id}/attachments', {
      params: {path: {id: props.contentId}},
      body: multipart<{file: Schema<'IFormFile'>}>(body),
    })
    if (data) {
      attachments.value = [...attachments.value, data]
    }
  } catch {
    uploadFailed.value = true
  } finally {
    uploading.value = false
    input.value = ''
  }
}

function askDelete(attachment: Schema<'AttachmentDto'>) {
  fileToDelete.value = attachment
  deleteError.value = ''
  deleteOpen.value = true
}

async function remove() {
  const attachment = fileToDelete.value
  if (!attachment) {
    return
  }

  deleting.value = true
  deleteError.value = ''
  try {
    await api.DELETE('/api/content/{id}/attachments/{attachmentId}', {
      params: {path: {id: props.contentId, attachmentId: attachment.id}},
    })
    attachments.value = attachments.value.filter((item) => item.id !== attachment.id)
    deleteOpen.value = false
    fileToDelete.value = null
  } catch {
    deleteError.value = 'Kunde inte ta bort filen.'
  } finally {
    deleting.value = false
  }
}

watch(() => props.contentId, load, {immediate: true})
</script>

<template>
  <section class="flex flex-col gap-4 rounded-[11px] border p-5" data-test="attachments">
    <h2 class="text-base">Bifogade filer</h2>

    <Alert v-if="uploadFailed" variant="destructive">
      <AlertDescription>Kunde inte ladda upp filen.</AlertDescription>
    </Alert>

    <div v-if="attachments.length > 0" class="flex flex-col gap-2">
      <div
        v-for="attachment in attachments"
        :key="attachment.id"
        class="flex items-center gap-2.5 rounded-[7px] border px-3 py-2"
        data-test="attachment-row"
      >
        <component :is="fileIcon(attachment.fileName)" class="text-muted-foreground size-4"/>
        <a
          :href="attachment.url"
          target="_blank"
          rel="noopener"
          class="font-medium underline-offset-3 hover:underline"
        >
          {{ attachment.fileName }}
        </a>
        <span class="text-muted-foreground text-xs">{{ formatSize(attachment.sizeBytes) }}</span>
        <Button
          v-if="canManage"
          type="button"
          variant="ghost"
          size="icon-sm"
          class="ml-auto"
          :aria-label="`Ta bort ${attachment.fileName}`"
          @click="askDelete(attachment)"
        >
          <PhTrash/>
        </Button>
      </div>
    </div>

    <p v-else-if="!loading" class="text-muted-foreground text-[13px]">Inga filer bifogade än.</p>

    <div v-if="canManage">
      <Button
        type="button"
        variant="outline"
        :disabled="uploading"
        @click="fileInput?.click()"
      >
        <PhUploadSimple/>
        {{ uploading
          ? 'Laddar upp…'
          : 'Ladda upp fil' }}
      </Button>
      <input
        ref="fileInput"
        type="file"
        class="hidden"
        data-test="attachment-file"
        @change="onFileChanged"
      />
    </div>

    <Dialog v-model:open="deleteOpen">
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Ta bort filen?</DialogTitle>
          <DialogDescription>
            {{ fileToDelete?.fileName }} tas bort från delen. Det går inte att ångra.
          </DialogDescription>
        </DialogHeader>
        <Alert v-if="deleteError" variant="destructive">
          <AlertDescription>{{ deleteError }}</AlertDescription>
        </Alert>
        <DialogFooter>
          <Button
            type="button"
            variant="outline"
            :disabled="deleting"
            @click="deleteOpen = false"
          >
            Avbryt
          </Button>
          <Button
            type="button"
            variant="destructive"
            :disabled="deleting"
            @click="remove"
          >
            Ta bort
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  </section>
</template>
