<script setup lang="ts">
import {PhPlus} from '@phosphor-icons/vue'
import {reactive, ref} from 'vue'

import MarkdownEditor from '@/components/editor/MarkdownEditor.vue'
import {Alert, AlertDescription} from '@/components/ui/alert'
import {Button} from '@/components/ui/button'
import {Checkbox} from '@/components/ui/checkbox'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@/components/ui/dialog'
import {Field, FieldGroup, FieldLabel} from '@/components/ui/field'
import {Input} from '@/components/ui/input'
import {api} from '@/lib/api'

const props = defineProps<{courseId: string}>()
const emit = defineEmits<{created: [linkId: string]}>()

const open = ref(false)
const saving = ref(false)
const failed = ref(false)
const bodyError = ref(false)
const form = reactive({title: '', bodyMd: '', isPinned: false})

async function save() {
  // MarkdownEditor har ingen required, så texten kontrolleras här.
  bodyError.value = form.bodyMd.trim() === ''
  if (bodyError.value) {
    return
  }

  saving.value = true
  failed.value = false
  try {
    const {data: content} = await api.POST('/api/content', {
      body: {type: 'News', title: form.title, bodyMd: form.bodyMd, status: 'Published'},
    })
    const {data: link} = await api.POST('/api/content/{id}/links', {
      params: {path: {id: content!.id}},
      body: {courseId: props.courseId, isPinned: form.isPinned},
    })
    Object.assign(form, {title: '', bodyMd: '', isPinned: false})
    bodyError.value = false
    open.value = false
    emit('created', link!.id)
  } catch {
    failed.value = true
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <Dialog v-model:open="open">
    <DialogTrigger as-child>
      <Button data-test="new-news">
        <PhPlus/>
        Ny nyhet
      </Button>
    </DialogTrigger>
    <DialogContent>
      <form class="flex flex-col gap-4" @submit.prevent="save">
        <DialogHeader>
          <DialogTitle>Ny nyhet</DialogTitle>
          <DialogDescription>Publiceras direkt i kursens nyhetsflöde.</DialogDescription>
        </DialogHeader>
        <FieldGroup>
          <Field>
            <FieldLabel for="news-title">Rubrik</FieldLabel>
            <Input
              id="news-title"
              v-model="form.title"
              placeholder="Studiebesök på tisdag"
              required
            />
          </Field>
          <Field>
            <FieldLabel for="news-body">Text</FieldLabel>
            <MarkdownEditor id="news-body" v-model="form.bodyMd" label="Text"/>
            <p v-if="bodyError" class="text-destructive text-sm">Skriv en text.</p>
          </Field>
          <Field orientation="horizontal">
            <Checkbox
              id="news-pinned"
              :model-value="form.isPinned"
              @update:model-value="(checked) => (form.isPinned = checked === true)"
            />
            <FieldLabel for="news-pinned">Fäst överst</FieldLabel>
          </Field>
        </FieldGroup>
        <Alert v-if="failed" variant="destructive">
          <AlertDescription>Kunde inte publicera nyheten.</AlertDescription>
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
          <Button type="submit" :disabled="saving">Publicera</Button>
        </DialogFooter>
      </form>
    </DialogContent>
  </Dialog>
</template>
