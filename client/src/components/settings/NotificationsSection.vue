<script setup lang="ts">
import {onMounted, ref} from 'vue'

import {Alert, AlertDescription} from '@/components/ui/alert'
import {Checkbox} from '@/components/ui/checkbox'
import {errorMessage, type Schema} from '@/lib/api'
import {fetchNotificationSettings, saveNotificationSettings} from '@/lib/notificationSettings'

type NotificationKind = Schema<'NotificationKind'>

// En rad i tabellen kan styra flera slag: betyg och begäran om komplettering hör ihop för eleven.
const notificationRows: {id: string, label: string, kinds: NotificationKind[]}[] = [
  {id: 'feedback', label: 'Betyg och återkoppling', kinds: ['SubmissionGraded', 'SubmissionNeedsWork']},
  {id: 'deadline', label: 'Påminnelse 24 h före deadline', kinds: ['DeadlineSoon']},
  {id: 'personalDeadline', label: 'Ny personlig deadline', kinds: ['PersonalDeadlineSet']},
  {id: 'assignments', label: 'Nya uppgifter', kinds: ['AssignmentPublished']},
  {id: 'help', label: 'Elev ber om hjälp', kinds: ['StudentHelpRequested']},
  {id: 'news', label: 'Nyheter', kinds: ['NewsPublished']},
]

const muted = ref<NotificationKind[]>([])
const notificationsLoaded = ref(false)
const notificationError = ref('')

function isEnabled(kinds: NotificationKind[]) {
  return !kinds.some((kind) => muted.value.includes(kind))
}

async function toggleNotification(kinds: NotificationKind[], enabled: boolean) {
  const previous = muted.value
  muted.value = enabled
    ? previous.filter((kind) => !kinds.includes(kind))
    : [...new Set([...previous, ...kinds])]
  notificationError.value = ''
  try {
    muted.value = (await saveNotificationSettings({muted: muted.value})).muted
  } catch (exception) {
    muted.value = previous
    notificationError.value = errorMessage(exception)
  }
}

async function loadNotificationSettings() {
  try {
    muted.value = (await fetchNotificationSettings()).muted
    notificationsLoaded.value = true
  } catch (exception) {
    notificationError.value = errorMessage(exception)
  }
}

onMounted(loadNotificationSettings)
</script>

<template>
  <section
    id="aviseringar"
    class="bg-card scroll-mt-6 overflow-hidden rounded-[11px] border"
    data-test="notifications"
  >
    <header class="flex flex-col gap-0.5 px-5 pt-5 pb-3">
      <h2 class="text-base font-semibold">Aviseringar</h2>
      <p class="text-muted-foreground text-[13px]">Välj vad du vill få i appen</p>
    </header>
    <Alert
      v-if="notificationError"
      variant="destructive"
      class="mx-5 mb-3 w-auto"
      data-test="notifications-error"
    >
      <AlertDescription>{{ notificationError }}</AlertDescription>
    </Alert>
    <table class="w-full text-sm">
      <thead>
        <tr class="text-muted-foreground text-[13px]">
          <th scope="col" class="px-5 py-2 text-left font-normal">Händelse</th>
          <th scope="col" class="w-[90px] px-5 py-2 text-center font-normal">I appen</th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="row in notificationRows" :key="row.id" class="border-t">
          <td class="px-5 py-2.5">{{ row.label }}</td>
          <td class="px-5 py-2.5">
            <div class="flex justify-center">
              <Checkbox
                :model-value="isEnabled(row.kinds)"
                :disabled="!notificationsLoaded"
                :aria-label="`${row.label} i appen`"
                :data-test="`notify-${row.id}`"
                @update:model-value="(checked) => toggleNotification(row.kinds, checked === true)"
              />
            </div>
          </td>
        </tr>
      </tbody>
    </table>
  </section>
</template>
