<script setup lang="ts">
import {computed, reactive, ref, watch} from 'vue'

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
import {NativeSelect, NativeSelectOption} from '@/components/ui/native-select'
import {api, errorMessage, unwrap, type Schema} from '@/lib/api'
import {groupTypeLabels} from '@/lib/groups'

// Utan group skapas en ny grupp i skolan, med group ändras den.
const props = defineProps<{
  schoolId: string
  group?: Schema<'GroupDto'>
}>()

const open = defineModel<boolean>('open', {required: true})
const emit = defineEmits<{saved: [group: Schema<'GroupDto'>]}>()

const saving = ref(false)
const error = ref('')
const submitted = ref(false)
const form = reactive({name: '', type: 'Class' as Schema<'GroupType'>, term: ''})

const isEdit = computed(() => props.group !== undefined)
const nameError = computed(() => submitted.value && !form.name.trim()
  ? 'Ange ett namn.'
  : '')

watch(open, (isOpen) => {
  if (isOpen) {
    form.name = props.group?.name ?? ''
    form.type = props.group?.type ?? 'Class'
    form.term = props.group?.term ?? ''
    error.value = ''
    submitted.value = false
  }
}, {immediate: true})

async function save() {
  submitted.value = true
  const name = form.name.trim()
  if (!name) {
    return
  }

  saving.value = true
  error.value = ''
  const term = form.term.trim() || null
  try {
    const saved = props.group
      ? unwrap(await api.PUT('/api/groups/{id}', {
          params: {path: {id: props.group.id}},
          body: {type: form.type, name, term},
        }))
      : unwrap(await api.POST('/api/groups', {
          body: {schoolId: props.schoolId, type: form.type, name, term},
        }))
    emit('saved', saved)
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
      <form class="flex flex-col gap-4" novalidate @submit.prevent="save">
        <DialogHeader>
          <DialogTitle>
            {{ isEdit
              ? 'Redigera grupp'
              : 'Ny grupp' }}
          </DialogTitle>
          <DialogDescription>
            {{ isEdit
              ? 'Ändra namn, typ eller period. Medlemmarna påverkas inte.'
              : 'Skapa en klass eller grupp i skolan. Medlemmar läggs till efteråt.' }}
          </DialogDescription>
        </DialogHeader>

        <FieldGroup>
          <Field
            :data-invalid="nameError
              ? true
              : undefined"
          >
            <FieldLabel for="group-name">Namn</FieldLabel>
            <Input
              id="group-name"
              v-model="form.name"
              placeholder="NET25"
              :aria-invalid="nameError
                ? true
                : undefined"
              autocomplete="off"
              data-test="group-name"
            />
            <FieldError v-if="nameError" data-test="group-name-error">{{ nameError }}</FieldError>
          </Field>
          <Field>
            <FieldLabel for="group-type">Typ</FieldLabel>
            <NativeSelect
              id="group-type"
              v-model="form.type"
              class="w-full"
              data-test="group-type"
            >
              <NativeSelectOption v-for="(label, value) in groupTypeLabels" :key="value" :value="value">
                {{ label }}
              </NativeSelectOption>
            </NativeSelect>
          </Field>
          <Field>
            <FieldLabel for="group-term">Period</FieldLabel>
            <Input
              id="group-term"
              v-model="form.term"
              placeholder="HT25"
              autocomplete="off"
              data-test="group-term"
            />
          </Field>
        </FieldGroup>

        <Alert v-if="error" variant="destructive" data-test="group-form-error">
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
          <Button type="submit" :disabled="saving" data-test="save-group">
            {{ isEdit
              ? 'Spara'
              : 'Skapa grupp' }}
          </Button>
        </DialogFooter>
      </form>
    </DialogContent>
  </Dialog>
</template>
