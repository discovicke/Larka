<script setup lang="ts">
import {computed, reactive, ref, watch} from 'vue'

import MarkdownEditor from '@/components/editor/MarkdownEditor.vue'
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
import {Field, FieldGroup, FieldLabel} from '@/components/ui/field'
import {Input} from '@/components/ui/input'
import {useAsync} from '@/composables/useAsync'
import {type Schema} from '@/lib/api'
import {updateContent} from '@/lib/contentApi'

const props = defineProps<{post: Schema<'ContentFeedItem'>}>()
const emit = defineEmits<{saved: []}>()

const open = defineModel<boolean>('open', {required: true})

const bodyError = ref(false)
const form = reactive({title: '', bodyMd: ''})

const {run: send, loading: saving, error} = useAsync(async () => {
  await updateContent(props.post.contentId, {title: form.title, bodyMd: form.bodyMd})
  open.value = false
  emit('saved')
}, 'Kunde inte spara nyheten.')
const failed = computed(() => error.value !== '')

watch(open, (isOpen) => {
  if (isOpen) {
    form.title = props.post.title
    form.bodyMd = props.post.bodyMd
    error.value = ''
    bodyError.value = false
  }
})

async function save() {
  // MarkdownEditor har ingen required, så texten kontrolleras här.
  bodyError.value = form.bodyMd.trim() === ''
  if (bodyError.value) {
    return
  }

  await send()
}
</script>

<template>
  <Dialog v-model:open="open">
    <DialogContent>
      <form class="flex flex-col gap-4" @submit.prevent="save">
        <DialogHeader>
          <DialogTitle>Redigera nyheten</DialogTitle>
          <DialogDescription>Ändringen syns direkt i kursens nyhetsflöde.</DialogDescription>
        </DialogHeader>
        <FieldGroup>
          <Field>
            <FieldLabel for="news-edit-title">Rubrik</FieldLabel>
            <Input
              id="news-edit-title"
              v-model="form.title"
              placeholder="Studiebesök på tisdag"
              required
            />
          </Field>
          <Field>
            <FieldLabel for="news-edit-body">Text</FieldLabel>
            <MarkdownEditor id="news-edit-body" v-model="form.bodyMd" label="Text"/>
            <p v-if="bodyError" class="text-destructive text-sm">Skriv en text.</p>
          </Field>
        </FieldGroup>
        <Alert v-if="failed" variant="destructive">
          <AlertDescription>Kunde inte spara nyheten.</AlertDescription>
        </Alert>
        <DialogFooter>
          <Button
            type="button"
            variant="outline"
            :disabled="saving"
            @click="open = false"
          >
            Avbryt
          </Button>
          <Button type="submit" :disabled="saving">Spara</Button>
        </DialogFooter>
      </form>
    </DialogContent>
  </Dialog>
</template>
