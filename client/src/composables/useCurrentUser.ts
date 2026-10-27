import {ref} from 'vue'

import {fetchMe, type Me} from '@/lib/me'
import {canDo, isStudentEverywhere as isStudentEverywhereRule, type Action} from '@/lib/permissions'

// Den inloggade användaren, på samma sätt som useCourses: en delad modul som AppLayout hämtar en gång.
const me = ref<Me | null>(null)
const status = ref<'loading' | 'loaded' | 'failed'>('loading')

// Både routen och layouten kan fråga samtidigt; då räcker det med ett anrop.
let pending: Promise<void> | null = null

async function loadMe(): Promise<void> {
  if (status.value === 'loaded') {
    return
  }
  if (pending) {
    return pending
  }

  status.value = 'loading'
  pending = fetchMe()
    .then((user) => {
      me.value = user
      status.value = 'loaded'
    })
    .catch(() => {
      // Utan svar nekas allt; routen släpper ändå igenom och API:et svarar 403.
      me.value = null
      status.value = 'failed'
    })
    .finally(() => {
      pending = null
    })
  return pending
}

function can(action: Action, courseId?: string): boolean {
  return canDo(me.value, action, courseId)
}

function isStudentEverywhere(): boolean {
  return isStudentEverywhereRule(me.value)
}

/** Testerna byter användare mellan fallen, så det delade tillståndet måste börja om. */
function resetCurrentUser(): void {
  me.value = null
  status.value = 'loading'
  pending = null
}

export function useCurrentUser() {
  return {me, status, loadMe, can, isStudentEverywhere, resetCurrentUser}
}
