<script setup lang="ts">
import {PhCheckCircle, PhQuestion} from '@phosphor-icons/vue'
import {ref, watch} from 'vue'

import {Badge} from '@/components/ui/badge'
import {Button} from '@/components/ui/button'
import {errorMessage} from '@/lib/api'
import {
  fetchStudentHelp,
  requestStudentHelp,
  withdrawStudentHelp,
  type StudentHelpStatus,
} from '@/lib/support'

const props = defineProps<{linkId: string}>()

const status = ref<StudentHelpStatus | null>(null)
const loading = ref(false)
const error = ref('')

async function load() {
  error.value = ''
  try {
    status.value = await fetchStudentHelp(props.linkId)
  } catch (exception) {
    error.value = errorMessage(exception)
  }
}

async function request() {
  loading.value = true
  error.value = ''
  try {
    status.value = await requestStudentHelp(props.linkId)
  } catch (exception) {
    error.value = errorMessage(exception)
  } finally {
    loading.value = false
  }
}

async function withdraw() {
  loading.value = true
  error.value = ''
  try {
    await withdrawStudentHelp(props.linkId)
    await load()
  } catch (exception) {
    error.value = errorMessage(exception)
  } finally {
    loading.value = false
  }
}

watch(() => props.linkId, load, {immediate: true})
</script>

<template>
  <section class="bg-card flex flex-col gap-3 rounded-xl border p-4" data-test="student-help">
    <div class="flex items-start gap-2.5">
      <PhQuestion class="text-muted-foreground mt-0.5 size-4 shrink-0"/>
      <div class="flex min-w-0 flex-1 flex-col gap-1">
        <h2 class="text-sm font-semibold">Behöver du hjälp?</h2>
        <p class="text-muted-foreground text-xs">
          Flagga uppgiften så ser din lärare att du behöver stöd.
        </p>
      </div>
      <Badge v-if="status?.requested" variant="default" class="shrink-0">
        <PhCheckCircle/>
        Skickad
      </Badge>
    </div>

    <div class="flex flex-wrap items-center gap-2">
      <Button
        v-if="!status?.requested"
        type="button"
        size="sm"
        variant="outline"
        :disabled="loading"
        data-test="student-help-request"
        @click="request"
      >
        <PhQuestion/>
        Jag behöver hjälp
      </Button>
      <Button
        v-else
        type="button"
        size="sm"
        variant="ghost"
        :disabled="loading"
        data-test="student-help-withdraw"
        @click="withdraw"
      >
        Ta tillbaka
      </Button>
      <span v-if="status?.requested" class="text-muted-foreground text-xs">
        Läraren har fått din förfrågan.
      </span>
    </div>

    <p v-if="error" class="text-destructive text-xs">{{ error }}</p>
  </section>
</template>
