<script setup lang="ts">
import {PhPlus} from '@phosphor-icons/vue'
import {reactive, ref} from 'vue'

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
import {Textarea} from '@/components/ui/textarea'
import {api} from '@/lib/api'

const props = defineProps<{courseId: string}>()
const emit = defineEmits<{created: [linkId: string]}>()

const open = ref(false)
const saving = ref(false)
const failed = ref(false)
const form = reactive({title: '', bodyMd: '', isPinned: false})

async function save() {
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
            <Textarea
              id="news-body"
              v-model="form.bodyMd"
              placeholder="Skriv nyheten här."
              rows="6"
              required
            />
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
