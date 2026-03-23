import api from './api'
import type { CreditCard, CreditCardWithPurchases, CreateCreditCardDto, UpdateCreditCardDto } from '@/types'

export const creditCardService = {
  async getAll(): Promise<CreditCard[]> {
    const response = await api.get<CreditCard[]>('/creditcards')
    return response.data
  },

  async getById(id: string): Promise<CreditCard> {
    const response = await api.get<CreditCard>(`/creditcards/${id}`)
    return response.data
  },

  async getWithPurchases(id: string): Promise<CreditCardWithPurchases> {
    const response = await api.get<CreditCardWithPurchases>(`/creditcards/${id}/purchases`)
    return response.data
  },

  async create(data: CreateCreditCardDto): Promise<CreditCard> {
    const response = await api.post<CreditCard>('/creditcards', data)
    return response.data
  },

  async update(id: string, data: UpdateCreditCardDto): Promise<CreditCard> {
    const response = await api.put<CreditCard>(`/creditcards/${id}`, data)
    return response.data
  },

  async delete(id: string): Promise<void> {
    await api.delete(`/creditcards/${id}`)
  },
}
