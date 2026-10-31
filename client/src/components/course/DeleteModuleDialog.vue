<script setup lang="ts">
import {PhWarning} from '@phosphor-icons/vue'
import {computed, ref, watch} from 'vue'

import {Alert, AlertDescription, AlertTitle} from '@/components/ui/alert'
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
import {api, type Schema} from '@/lib/api'

const props = defineProps<{
  courseId: string
  module: Schema<'ModuleWithContentDto'>
}>()

const open = defineModel<boolean>('open', {required: true})
const emit = defineEmits<{deleted: []}>()

const typed = ref('')

const activityParts = computed(() =>
  props.module.content.filter((part) => part.hasStudentActivity))
const publishedSubmissions = computed(() => props.module.content.filter((part) =>
  part.type === 'Assignment'
  && part.status === 'Published'
  && part.gradeScale !== null
  && part.gradeScale !== 'None'))
const hasActivity = computed(() => activityParts.value.length > 0)
const canDelete = computed(() =>
  !hasActivity.value && typed.value === props.module.title)

watch(open, (isOpen) => {
  if (isOpen) {
    typed.value = ''
    error.value = ''
  }
})

const {run: remove, loading: deleting, error} = useAsync(async () => {
  if (!canDelete.value) {
    return
  }

  await api.DELETE('/api/courses/{id}/modules/{moduleId}', {
    params: {path: {id: props.courseId, moduleId: props.module.id}},
  })
  emit('deleted')
  open.value = false
})
</script>

<template>
  <Dialog v-model:open="open">
    <DialogContent>
      <form class="flex flex-col gap-4" @submit.prevent="remove">
        <DialogHeader>
          <DialogTitle>Ta bort {{ module.title }}?</DialogTitle>
          <DialogDescription>
            Veckan och dess delar tas bort. Det går inte att ångra.
          </DialogDescription>
        </DialogHeader>

        <Alert v-if="hasActivity" variant="destructive" data-test="delete-week-activity">
          <PhWarning/>
          <AlertTitle>Veckan innehåller elevarbete</AlertTitle>
          <AlertDescription>
            {{ activityParts.map((part) => part.title).join(', ') }} har inlämningar, betyg
            eller klarmarkeringar. Veckan kan därför inte tas bort.
          </AlertDescription>
        </Alert>

        <Alert v-if="publishedSubmissions.length > 0" data-test="delete-week-published">
          <PhWarning/>
          <AlertTitle>Publicerad inlämning</AlertTitle>
          <AlertDescription>
            {{ publishedSubmissions.map((part) => part.title).join(', ') }} är publicerad och
            tas bort tillsammans med veckan. Inlämningar som inte påbörjats raderas också.
          </AlertDescription>
        </Alert>

        <div v-if="module.content.length > 0" class="bg-muted/30 flex flex-col gap-1 rounded-lg border p-3 text-sm">
          <span class="text-muted-foreground text-xs font-medium tracking-wide uppercase">Delarna i veckan</span>
          <span v-for="part in module.content" :key="part.linkId" class="truncate">
            {{ part.title }}
          </span>
        </div>

        <FieldGroup>
          <Field>
            <FieldLabel for="delete-module-title">
              Skriv veckans titel för att bekräfta
            </FieldLabel>
            <Input
              id="delete-module-title"
              v-model="typed"
              :placeholder="module.title"
              :disabled="hasActivity"
              autocomplete="off"
              data-test="delete-week-confirm"
            />
          </Field>
        </FieldGroup>

        <Alert v-if="error" variant="destructive">
          <AlertDescription>{{ error }}</AlertDescription>
        </Alert>

        <DialogFooter>
          <Button
            type="button"
            variant="outline"
            :disabled="deleting"
            @click="open = false"
          >
            Avbryt
          </Button>
          <Button
            type="submit"
            variant="destructive"
            :disabled="deleting || !canDelete"
            data-test="confirm-delete-week"
          >
            Ta bort veckan
          </Button>
        </DialogFooter>
      </form>
    </DialogContent>
  </Dialog>
</template>
