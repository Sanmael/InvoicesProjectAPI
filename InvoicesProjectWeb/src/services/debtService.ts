import api from './api'
import type { Debt, CreateDebtDto, CreateInstallmentDebtDto, CreateRecurringDebtDto, UpdateDebtDto } from '@/types'

export const debtService = {
  async getAll(): Promise<Debt[]> {
    const response = await api.get<Debt[]>('/debts')
    return response.data
  },

  async getPending(): Promise<Debt[]> {
    const response = await api.get<Debt[]>('/debts/pending')
    return response.data
  },

  async getById(id: string): Promise<Debt> {
    const response = await api.get<Debt>(`/debts/${id}`)
    return response.data
  },

  async create(data: CreateDebtDto): Promise<Debt> {
    const response = await api.post<Debt>('/debts', data)
    return response.data
  },

  async createInstallment(data: CreateInstallmentDebtDto): Promise<Debt[]> {
    const response = await api.post<Debt[]>('/debts/installment', data)
    return response.data
  },

  async createRecurring(data: CreateRecurringDebtDto): Promise<Debt[]> {
    const response = await api.post<Debt[]>('/debts/recurring', data)
    return response.data
  },

  async update(id: string, data: UpdateDebtDto): Promise<Debt> {
    const response = await api.put<Debt>(`/debts/${id}`, data)
    return response.data
  },

  async markAsPaid(id: string): Promise<Debt> {
    const response = await api.post<Debt>(`/debts/${id}/pay`)
    return response.data
  },

  async delete(id: string): Promise<void> {
    await api.delete(`/debts/${id}`)
  },

  async deleteGroup(groupId: string): Promise<void> {
    await api.delete(`/debts/group/${groupId}`)
  },
}
