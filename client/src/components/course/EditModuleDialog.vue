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
import {Field, FieldDescription, FieldGroup, FieldLabel} from '@/components/ui/field'
import {Input} from '@/components/ui/input'
import {api, errorMessage, type Schema} from '@/lib/api'
import {isoOrNull, orNull, toDateTimeLocal} from '@/lib/form'

const props = defineProps<{
  courseId: string
  module: Schema<'ModuleWithContentDto'>
}>()

const open = defineModel<boolean>('open', {required: true})
const emit = defineEmits<{saved: [module: Schema<'ModuleDto'>]}>()

const saving = ref(false)
const error = ref('')
const form = reactive({title: '', description: '', publishAt: ''})

watch(open, (isOpen) => {
  if (isOpen) {
    form.title = props.module.title
    form.description = props.module.description
    form.publishAt = toDateTimeLocal(props.module.publishAt)
    error.value = ''
  }
}, {immediate: true})

async function save() {
  if (!form.title.trim()) {
    error.value = 'Ange en titel.'
    return
  }

  saving.value = true
  error.value = ''
  try {
    const {data} = await api.PUT('/api/courses/{id}/modules/{moduleId}', {
      params: {path: {id: props.courseId, moduleId: props.module.id}},
      body: {
        title: form.title,
        description: orNull(form.description),
        publishAt: isoOrNull(form.publishAt),
      },
    })
    if (data) {
      emit('saved', data)
    }
    open.value = false
  } catch (exception) {
    error.value = errorMessage(exception)
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
          <DialogTitle>Redigera vecka</DialogTitle>
          <DialogDescription>
            Ändra namnet eller bestäm när hela veckan ska öppna för eleverna.
          </DialogDescription>
        </DialogHeader>

        <FieldGroup>
          <Field>
            <FieldLabel for="edit-module-title">Titel</FieldLabel>
            <Input
              id="edit-module-title"
              v-model="form.title"
              data-test="edit-module-title"
              required
            />
          </Field>
          <Field>
            <FieldLabel for="edit-module-description">Beskrivning</FieldLabel>
            <Input
              id="edit-module-description"
              v-model="form.description"
              data-test="edit-module-description"
            />
          </Field>
          <Field>
            <FieldLabel for="edit-module-publish-at">Veckan öppnar</FieldLabel>
            <Input
              id="edit-module-publish-at"
              v-model="form.publishAt"
              type="datetime-local"
              data-test="edit-module-publish-at"
            />
            <FieldDescription>
              Tomt = veckan är öppen direkt. Datumet gäller hela veckan, även om delarna har
              tidigare publiceringsdatum.
            </FieldDescription>
          </Field>
        </FieldGroup>

        <Alert v-if="error" variant="destructive">
          <AlertDescription>{{ error }}</AlertDescription>
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
          <Button type="submit" :disabled="saving" data-test="save-module">
            Spara
          </Button>
        </DialogFooter>
      </form>
    </DialogContent>
  </Dialog>
</template>
