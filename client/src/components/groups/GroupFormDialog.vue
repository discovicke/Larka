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
import {Field, FieldDescription, FieldError, FieldGroup, FieldLabel} from '@/components/ui/field'
import {Input} from '@/components/ui/input'
import {NativeSelect, NativeSelectOption} from '@/components/ui/native-select'
import {useAsync} from '@/composables/useAsync'
import {api, unwrap, type Schema} from '@/lib/api'
import {classCodeFromName} from '@/lib/classCode'
import {groupTypeLabels} from '@/lib/groups'

// Utan group skapas en ny klass eller grupp i skolan, med group ändras den.
const props = withDefaults(defineProps<{
  schoolId: string
  group?: Schema<'GroupDto'>
  kind?: 'class' | 'group'
}>(), {group: undefined, kind: 'group'})

const open = defineModel<boolean>('open', {required: true})
const emit = defineEmits<{saved: [group: Schema<'GroupDto'>]}>()

const submitted = ref(false)
const form = reactive({name: '', type: 'Class' as Schema<'GroupType'>, term: ''})

const isEdit = computed(() => props.group !== undefined)
const isClass = computed(() => form.type === 'Class')
const typeOptions = computed(() => Object.entries(groupTypeLabels)
  .filter(([value]) => isEdit.value || value !== 'Class'))
const nameError = computed(() => submitted.value && !form.name.trim()
  ? 'Ange ett namn.'
  : '')

// Bara klasser fångar elever på adressen. Ett tomt namn visar ingenting förrän det finns något att säga om.
const classCode = computed(() => classCodeFromName(form.name))
const showCodeHint = computed(() => isClass.value && form.name.trim().length > 0)

const {run: send, loading: saving, error} = useAsync(async (name: string, term: string | null) => {
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
})

watch(open, (isOpen) => {
  if (isOpen) {
    form.name = props.group?.name ?? ''
    form.type = props.group?.type ?? (props.kind === 'class'
      ? 'Class'
      : 'Committee')
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

  await send(name, form.term.trim() || null)
}
</script>

<template>
  <Dialog v-model:open="open">
    <DialogContent>
      <form class="flex flex-col gap-4" novalidate @submit.prevent="save">
        <DialogHeader>
          <DialogTitle>
            {{ `${isEdit
              ? 'Redigera'
              : 'Ny'} ${isClass
              ? 'klass'
              : 'grupp'}` }}
          </DialogTitle>
          <DialogDescription>
            <template v-if="isEdit">Ändra namn, typ eller termin. Medlemmarna påverkas inte.</template>
            <template v-else-if="isClass">
              En klass är en årskull. Kurser och elever lägger du till på klassens sida.
            </template>
            <template v-else>Medlemmar och kurser lägger du till på gruppens sida.</template>
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
            <FieldDescription v-if="showCodeHint && classCode" data-test="group-code-hint">
              Elever med adresser som slutar på <strong>.{{ classCode }}@…</strong> hamnar här.
            </FieldDescription>
            <FieldDescription v-else-if="showCodeHint" data-test="group-code-hint-missing">
              Namnet ger ingen kod som adresser kan sluta på, till exempel NET25. Elever hamnar inte här av sig
              själva, du lägger in dem på klassens sida.
            </FieldDescription>
          </Field>
          <Field v-if="isEdit || !isClass">
            <FieldLabel for="group-type">Typ</FieldLabel>
            <NativeSelect
              id="group-type"
              v-model="form.type"
              class="w-full"
              data-test="group-type"
            >
              <NativeSelectOption v-for="[value, label] in typeOptions" :key="value" :value="value">
                {{ label }}
              </NativeSelectOption>
            </NativeSelect>
          </Field>
          <Field>
            <FieldLabel for="group-term">
              {{ isClass
                ? 'Startade (termin)'
                : 'Period/termin' }}
            </FieldLabel>
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
              : isClass
                ? 'Skapa klass'
                : 'Skapa grupp' }}
          </Button>
        </DialogFooter>
      </form>
    </DialogContent>
  </Dialog>
</template>
