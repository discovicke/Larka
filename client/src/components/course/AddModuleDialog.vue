<script setup lang="ts">
import DateTimePicker from '@/components/DateTimePicker.vue'
import {PhPlus} from '@phosphor-icons/vue'
import {reactive, ref} from 'vue'

import {Alert, AlertDescription} from '@/components/ui/alert'
import {Button} from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@/components/ui/dialog'
import {Field, FieldDescription, FieldGroup, FieldLabel} from '@/components/ui/field'
import {Input} from '@/components/ui/input'
import {useAsync} from '@/composables/useAsync'
import {api} from '@/lib/api'
import {isoOrNull, orNull} from '@/lib/form'

const props = defineProps<{courseId: string}>()
const emit = defineEmits<{created: []}>()

const open = ref(false)
const form = reactive({title: '', description: '', publishAt: ''})

const {run: save, loading: saving, error} = useAsync(async () => {
  await api.POST('/api/courses/{id}/modules', {
    params: {path: {id: props.courseId}},
    body: {
      title: form.title,
      description: orNull(form.description),
      publishAt: isoOrNull(form.publishAt),
    },
  })
  Object.assign(form, {title: '', description: '', publishAt: ''})
  open.value = false
  emit('created')
})
</script>

<template>
  <Dialog v-model:open="open">
    <DialogTrigger as-child>
      <Button variant="outline" class="self-start" data-test="add-module">
        <PhPlus/>
        Lägg till vecka eller avsnitt
      </Button>
    </DialogTrigger>
    <DialogContent>
      <form class="flex flex-col gap-4" @submit.prevent="save">
        <DialogHeader>
          <DialogTitle>Ny vecka eller avsnitt</DialogTitle>
          <DialogDescription>Hamnar sist i kursen. Ordningen går att ändra efteråt.</DialogDescription>
        </DialogHeader>
        <FieldGroup>
          <Field>
            <FieldLabel for="module-title">Titel</FieldLabel>
            <Input
              id="module-title"
              v-model="form.title"
              placeholder="Vecka 3 – Containrar"
              required
            />
          </Field>
          <Field>
            <FieldLabel for="module-description">Beskrivning</FieldLabel>
            <Input
              id="module-description"
              v-model="form.description"
              placeholder="Utbildningsdagar: måndag, tisdag."
            />
          </Field>
          <Field>
            <FieldLabel for="module-publish-at">Veckan öppnar</FieldLabel>
            <DateTimePicker
              id="module-publish-at"
              v-model="form.publishAt"
              clearable
              data-test="module-publish-at"
            />
            <FieldDescription>
              Tomt = veckan är öppen direkt. Du kan schemalägga veckan redan nu och fylla på
              delar senare.
            </FieldDescription>
          </Field>
        </FieldGroup>
        <Alert v-if="error" variant="destructive">
          <AlertDescription>{{ error }}</AlertDescription>
        </Alert>
        <DialogFooter>
          <Button type="submit" :disabled="saving">Lägg till</Button>
        </DialogFooter>
      </form>
    </DialogContent>
  </Dialog>
</template>
