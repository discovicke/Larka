import {readonly, ref} from 'vue'

// Ett kort besked efter en åtgärd, till exempel "Delen är klarmarkerad." Det visas av <AppToaster>
// i appens layout, försvinner av sig självt och ligger kvar medan man går vidare till nästa sida.
// Sidor som redan visar en ruta för samma åtgärd ska inte visa en toast också.
export interface Toast {
  id: number
  message: string
}

/** Hur länge en toast ligger kvar, i millisekunder. */
export const toastDuration = 5000

const toasts = ref<Toast[]>([])
const timers = new Map<number, ReturnType<typeof setTimeout>>()
let nextId = 1

function dismissToast(id: number) {
  clearTimeout(timers.get(id))
  timers.delete(id)
  toasts.value = toasts.value.filter((toast) => toast.id !== id)
}

/** Visar en toast. Samma text två gånger i rad ger en toast, inte två. */
function showToast(message: string) {
  const existing = toasts.value.find((toast) => toast.message === message)
  if (existing) {
    dismissToast(existing.id)
  }
  const id = nextId++
  toasts.value = [...toasts.value, {id, message}]
  timers.set(id, setTimeout(() => dismissToast(id), toastDuration))
}

/** Tar bort alla toasts, för testerna. */
export function resetToasts() {
  for (const timer of timers.values()) {
    clearTimeout(timer)
  }
  timers.clear()
  toasts.value = []
}

export function useToast() {
  return {toasts: readonly(toasts), showToast, dismissToast}
}
