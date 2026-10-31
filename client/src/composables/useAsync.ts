import {ref} from 'vue'

import {errorMessage} from '@/lib/api'

/**
 * Kör en asynkron uppgift och håller reda på `loading` och `error`, så att vyn slipper egna
 * laddnings- och felvariabler. Felet blir en text via errorMessage, eller `failureText` om den ges.
 *
 * `run` rensar felet när uppgiften startar och returnerar uppgiftens resultat, eller `undefined`
 * om den misslyckades. Startas uppgiften igen innan den förra är klar (t.ex. när ruttens
 * parameter byts) gäller bara den senaste: den äldres resultat och fel kastas bort och den
 * äldre `run` returnerar `undefined`.
 */
export function useAsync<Args extends unknown[], Result>(
  task: (...args: Args) => Promise<Result>,
  failureText?: string,
) {
  const loading = ref(false)
  const error = ref('')
  let latest = 0

  async function run(...args: Args): Promise<Result | undefined> {
    const call = ++latest
    loading.value = true
    error.value = ''
    try {
      const result = await task(...args)
      return call === latest
        ? result
        : undefined
    } catch (exception) {
      if (call === latest) {
        error.value = failureText ?? errorMessage(exception)
      }
      return undefined
    } finally {
      if (call === latest) {
        loading.value = false
      }
    }
  }

  return {run, loading, error}
}
