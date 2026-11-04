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
import {Field, FieldError, FieldGroup, FieldLabel} from '@/components/ui/field'
import {Input} from '@/components/ui/input'
import TextLimitNote from '@/components/TextLimitNote.vue'
import {useAsync} from '@/composables/useAsync'
import {type Schema} from '@/lib/api'
import {updateContent} from '@/lib/contentApi'
import {isTooLong, textLimits} from '@/lib/textLimits'

const props = defineProps<{post: Schema<'ContentFeedItem'>}>()
const emit = defineEmits<{saved: []}>()

const open = defineModel<boolean>('open', {required: true})

const titleError = ref(false)
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
    titleError.value = false
    bodyError.value = false
  }
})

async function save() {
  // Fälten kontrolleras här i stället för med required, så att felet står vid fältet.
  titleError.value = form.title.trim() === ''
  bodyError.value = form.bodyMd.trim() === ''
  if (titleError.value || bodyError.value || isTooLong(form.bodyMd, textLimits.contentBody)) {
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
          <Field :data-invalid="titleError">
            <FieldLabel for="news-edit-title">Rubrik</FieldLabel>
            <Input
              id="news-edit-title"
              v-model="form.title"
              :maxlength="textLimits.title"
              placeholder="Studiebesök på tisdag"
              :aria-invalid="titleError"
            />
            <FieldError v-if="titleError">Skriv en rubrik.</FieldError>
          </Field>
          <Field>
            <FieldLabel for="news-edit-body">Text</FieldLabel>
            <MarkdownEditor id="news-edit-body" v-model="form.bodyMd" label="Text"/>
            <TextLimitNote :text="form.bodyMd" :max="textLimits.contentBody"/>
            <FieldError v-if="bodyError">Skriv något i nyheten.</FieldError>
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
