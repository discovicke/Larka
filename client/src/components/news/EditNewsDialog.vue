<script setup lang="ts">
import {reactive, ref, watch} from 'vue'

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
import {Textarea} from '@/components/ui/textarea'
import {api, type Schema} from '@/lib/api'

const props = defineProps<{post: Schema<'ContentFeedItem'>}>()
const emit = defineEmits<{saved: []}>()

const open = defineModel<boolean>('open', {required: true})

const saving = ref(false)
const failed = ref(false)
const form = reactive({title: '', bodyMd: ''})

watch(open, (isOpen) => {
  if (isOpen) {
    form.title = props.post.title
    form.bodyMd = props.post.bodyMd
    failed.value = false
  }
})

async function save() {
  saving.value = true
  failed.value = false
  try {
    await api.PUT('/api/content/{id}', {
      params: {path: {id: props.post.contentId}},
      body: {title: form.title, bodyMd: form.bodyMd},
    })
    open.value = false
    emit('saved')
  } catch {
    failed.value = true
  } finally {
    saving.value = false
  }
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
            <Textarea
              id="news-edit-body"
              v-model="form.bodyMd"
              placeholder="Skriv nyheten här."
              rows="6"
              required
            />
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
