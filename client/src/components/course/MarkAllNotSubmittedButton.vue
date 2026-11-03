<script setup lang="ts">
import {ref} from 'vue'

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
import {errorMessage} from '@/lib/api'
import {markAllNotSubmitted} from '@/lib/submissionsApi'

// "Markera alla som ej inlämnat": en knapp med en bekräftelse. Efter deadlinen, för en uppgift åt gången.
// Eleverna får en notis och kan fortfarande lämna in till nästa deadline.
const props = defineProps<{linkId: string, count?: number}>()

const emit = defineEmits<{done: [count: number]}>()

const open = ref(false)
const working = ref(false)
const error = ref('')

async function confirm() {
  working.value = true
  error.value = ''
  try {
    const marked = await markAllNotSubmitted(props.linkId)
    open.value = false
    emit('done', marked)
  } catch (exception) {
    error.value = errorMessage(exception)
  } finally {
    working.value = false
  }
}
</script>

<template>
  <Button
    type="button"
    variant="outline"
    class="h-9 px-3.5 text-sm font-semibold"
    data-test="mark-all-not-submitted"
    @click="open = true"
  >
    Markera alla som ej inlämnat
  </Button>

  <Dialog v-model:open="open">
    <DialogContent class="sm:max-w-md" data-test="mark-all-dialog">
      <DialogHeader>
        <DialogTitle>Markera alla som ej inlämnat?</DialogTitle>
        <DialogDescription>
          <template v-if="count">
            {{ count }} {{ count === 1
              ? 'elev'
              : 'elever' }} utan inlämning markeras som Ej inlämnat och får en notis.
          </template>
          <template v-else>
            Alla som inte lämnat in markeras som Ej inlämnat och får en notis.
          </template>
          Eleverna kan fortfarande lämna in till nästa deadline.
        </DialogDescription>
      </DialogHeader>
      <Alert v-if="error" variant="destructive" data-test="mark-all-error">
        <AlertDescription>{{ error }}</AlertDescription>
      </Alert>
      <DialogFooter>
        <Button
          type="button"
          variant="outline"
          :disabled="working"
          @click="open = false"
        >
          Avbryt
        </Button>
        <Button
          type="button"
          :disabled="working"
          data-test="mark-all-confirm"
          @click="confirm"
        >
          {{ working
            ? 'Markerar…'
            : 'Markera alla' }}
        </Button>
      </DialogFooter>
    </DialogContent>
  </Dialog>
</template>
