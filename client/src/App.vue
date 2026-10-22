<script setup lang="ts">
import { onMounted, ref } from 'vue'

import { api } from '@/lib/api'

interface VersionResponse {
  version: string
  commit: string
}

const status = ref<'loading' | 'ok' | 'error'>('loading')
const version = ref<VersionResponse | null>(null)

onMounted(async () => {
  try {
    version.value = await api.get<VersionResponse>('/version')
    status.value = 'ok'
  } catch {
    status.value = 'error'
  }
})
</script>

<template>
  <div>
    <h1>Welcome to your new project</h1>
    <p v-if="status === 'loading'" data-test="api-status">Connecting to the API...</p>
    <p v-else-if="status === 'ok' && version" data-test="api-status">
      API: OK ({{ version.version }}, {{ version.commit }})
    </p>
    <p v-else data-test="api-status">API: could not reach the backend</p>
  </div>
</template>
