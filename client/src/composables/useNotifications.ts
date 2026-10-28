import {computed, ref} from 'vue'

import {api, type Schema} from '@/lib/api'

type Notification = Schema<'NotificationDto'>

// Notiserna och antalet olästa, delade av sidomenyns märke och inkorgen. AppLayout hämtar antalet en gång,
// inkorgen hämtar hela listan. Ett fel lämnar det som redan finns, så att inkorgen ändå visar nyheterna.
const notifications = ref<Notification[]>([])
const unreadCount = ref(0)

const unreadIds = computed(() => notifications.value
  .filter((notification) => notification.readAt === null)
  .map((notification) => notification.id))

async function loadUnreadCount(): Promise<void> {
  try {
    const result = await api.GET('/api/notifications/unread-count')
    unreadCount.value = result.data?.count ?? unreadCount.value
  } catch {
    // Märket i sidomenyn är bara en hjälp; sidan fungerar utan det.
  }
}

/** Hämtar de senaste notiserna. Returnerar false om det inte gick. */
async function loadNotifications(): Promise<boolean> {
  try {
    const result = await api.GET('/api/notifications', {params: {query: {limit: 200}}})
    if (!result.data) {
      return false
    }
    notifications.value = result.data.items
    unreadCount.value = result.data.unreadCount
    return true
  } catch {
    notifications.value = []
    return false
  }
}

/** Markerar notiserna som lästa direkt i listan och i märket, och sedan på servern. */
async function markRead(ids: string[]): Promise<void> {
  const unread = notifications.value.filter((notification) =>
    ids.includes(notification.id) && notification.readAt === null)
  if (unread.length === 0) {
    return
  }

  const readAt = new Date().toISOString()
  for (const notification of unread) {
    notification.readAt = readAt
  }
  unreadCount.value = Math.max(0, unreadCount.value - unread.length)

  try {
    await Promise.all(unread.map((notification) =>
      api.POST('/api/notifications/{id}/read', {params: {path: {id: notification.id}}})))
  } catch {
    await loadNotifications()
  }
}

async function markAllRead(): Promise<void> {
  const readAt = new Date().toISOString()
  for (const notification of notifications.value) {
    notification.readAt ??= readAt
  }
  unreadCount.value = 0

  try {
    await api.POST('/api/notifications/read-all')
  } catch {
    await loadNotifications()
  }
}

/** Testerna byter användare mellan fallen, så det delade tillståndet måste börja om. */
function resetNotifications(): void {
  notifications.value = []
  unreadCount.value = 0
}

export function useNotifications() {
  return {
    notifications,
    unreadCount,
    unreadIds,
    loadUnreadCount,
    loadNotifications,
    markRead,
    markAllRead,
    resetNotifications,
  }
}
