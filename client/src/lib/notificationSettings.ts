import {api, type Schema, unwrap} from './api'

/** Svaret från GET /api/me/notification-settings: slagen av notiser användaren stängt av. */
export type NotificationSettings = Schema<'NotificationSettingsDto'>

export async function fetchNotificationSettings(): Promise<NotificationSettings> {
  return unwrap(await api.GET('/api/me/notification-settings'))
}

export async function saveNotificationSettings(settings: NotificationSettings): Promise<NotificationSettings> {
  return unwrap(await api.PUT('/api/me/notification-settings', {body: settings}))
}
