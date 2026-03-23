import api from './api'
import type { Receivable, CreateReceivableDto, CreateRecurringReceivableDto, UpdateReceivableDto } from '@/types'

export const receivableService = {
  async getAll(): Promise<Receivable[]> {
    const response = await api.get<Receivable[]>('/receivables')
    return response.data
  },

  async getPending(): Promise<Receivable[]> {
    const response = await api.get<Receivable[]>('/receivables/pending')
    return response.data
  },

  async getById(id: string): Promise<Receivable> {
    const response = await api.get<Receivable>(`/receivables/${id}`)
    return response.data
  },

  async create(data: CreateReceivableDto): Promise<Receivable> {
    const response = await api.post<Receivable>('/receivables', data)
    return response.data
  },

  async createRecurring(data: CreateRecurringReceivableDto): Promise<Receivable[]> {
    const response = await api.post<Receivable[]>('/receivables/recurring', data)
    return response.data
  },

  async update(id: string, data: UpdateReceivableDto): Promise<Receivable> {
    const response = await api.put<Receivable>(`/receivables/${id}`, data)
    return response.data
  },

  async markAsReceived(id: string): Promise<Receivable> {
    const response = await api.post<Receivable>(`/receivables/${id}/receive`)
    return response.data
  },

  async delete(id: string): Promise<void> {
    await api.delete(`/receivables/${id}`)
  },

  async deleteGroup(groupId: string): Promise<void> {
    await api.delete(`/receivables/group/${groupId}`)
  },
}
