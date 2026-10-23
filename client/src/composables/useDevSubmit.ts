import {ref} from 'vue'

import type {FormResult} from '@/components/dev/types'
import {api, ApiError, type Method} from '@/lib/api'

/** Skickar ett formulär och lägger svaret (status + JSON) eller felet i result. */
export function useDevSubmit() {
  const result = ref<FormResult | null>(null)
  const busy = ref(false)

  async function send<T>(method: Method, path: string, body?: unknown): Promise<T | undefined> {
    busy.value = true
    try {
      const response = await api.send<T>(method, path, body)
      result.value = {kind: 'response', status: response.status, body: response.data ?? null}
      return response.data
    } catch (error) {
      result.value = error instanceof ApiError
        ? {kind: 'error', status: error.status, message: error.message}
        : {kind: 'error', message: error instanceof Error
            ? error.message
            : String(error)}
      return undefined
    } finally {
      busy.value = false
    }
  }

  return {result, busy, send}
}
