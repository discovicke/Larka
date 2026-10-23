import {ref} from 'vue'

import type {FormResult} from '@/components/dev/types'
import {ApiError} from '@/lib/api'

/** Det ett anrop med api returnerar (data finns inte för svar utan body). */
interface CallResult<T> {
  data?: T
  response: Response
}

/** Kör ett formulärs anrop och lägger svaret (status + JSON) eller felet i result. */
export function useDevSubmit() {
  const result = ref<FormResult | null>(null)
  const busy = ref(false)

  async function submit<T>(call: () => Promise<CallResult<T>>): Promise<T | undefined> {
    busy.value = true
    try {
      const {data, response} = await call()
      result.value = {kind: 'response', status: response.status, body: data ?? null}
      return data
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

  /** Visar ett fel utan att något skickas, t.ex. när ett obligatoriskt val saknas. */
  function fail(message: string) {
    result.value = {kind: 'error', message}
  }

  return {result, busy, submit, fail}
}
