import api from './api'
import type {
  EmailNotification,
  NotificationPreference,
  NotificationPreferenceCreateDto,
  NotificationSummary,
} from '@/types'

export const notificationService = {
  async getPreferences(): Promise<NotificationPreference> {
    const response = await api.get<NotificationPreference>('/notificationpreferences')
    return response.data
  },

  async savePreferences(data: NotificationPreferenceCreateDto): Promise<NotificationPreference> {
    const response = await api.post<NotificationPreference>('/notificationpreferences', data)
    return response.data
  },

  async getSummary(): Promise<NotificationSummary> {
    const response = await api.get<NotificationSummary>('/notifications/summary')
    return response.data
  },

  async getHistory(limit = 10): Promise<EmailNotification[]> {
    const response = await api.get<EmailNotification[]>('/notifications/history', {
      params: { limit },
    })
    return response.data
  },
}