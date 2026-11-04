import {useRouter} from 'vue-router'

import {useToast} from '@/composables/useToast'
import {ApiError} from '@/lib/api'

/** Texten när en sida kräver en behörighet som användaren saknar. */
export const noAccessMessage = 'Du har inte behörighet till den sidan.'

/**
 * Sant när API:et nekar (403) eller inte hittar (404) det sidan visar. API:et svarar 404 för det
 * användaren inte får se, så de två behandlas lika och sidan avslöjar inte vad som finns.
 */
export function isNoAccess(exception: unknown): boolean {
  return exception instanceof ApiError && (exception.status === 403 || exception.status === 404)
}

// En kurs, del eller grupp som användaren inte får öppna: hen hamnar på Idag med en toast, som när
// en sida kräver en behörighet hen saknar (beforeEach i router.ts).
export function useNoAccess() {
  const router = useRouter()

  async function leave() {
    useToast().showToast(noAccessMessage)
    await router.replace({name: 'today'})
  }

  return {leave}
}
