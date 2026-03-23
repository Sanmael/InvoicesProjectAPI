import api from './api'
import type {
  EmailPasswordUpdateDto,
  EmailSettings,
  EmailSettingsCreateDto,
  EmailTestDto,
  EmailTestResult,
} from '@/types'

export const adminService = {
  async getEmailSettings(): Promise<EmailSettings | null> {
    try {
      const response = await api.get<EmailSettings>('/admin/email-settings')
      return response.data
    } catch (error: any) {
      if (error.response?.status === 404) {
        return null
      }
      throw error
    }
  },

  async saveEmailSettings(data: EmailSettingsCreateDto): Promise<EmailSettings> {
    const response = await api.post<EmailSettings>('/admin/email-settings', data)
    return response.data
  },

  async updateEmailPassword(data: EmailPasswordUpdateDto): Promise<void> {
    await api.put('/admin/email-settings/password', data)
  },

  async testEmailSettings(data: EmailTestDto): Promise<EmailTestResult> {
    const response = await api.post<EmailTestResult>('/admin/email-settings/test', data)
    return response.data
  },

  async processAllNotifications(): Promise<void> {
    await api.post('/admin/notifications/process-all')
  },
}