import api from './api'
import type { CardPurchase, CreateCardPurchaseDto, UpdateCardPurchaseDto } from '@/types'

export const cardPurchaseService = {
  async getByCard(cardId: string): Promise<CardPurchase[]> {
    const response = await api.get<CardPurchase[]>(`/cardpurchases/card/${cardId}`)
    return response.data
  },

  async getPendingByCard(cardId: string): Promise<CardPurchase[]> {
    const response = await api.get<CardPurchase[]>(`/cardpurchases/card/${cardId}/pending`)
    return response.data
  },

  async getById(id: string): Promise<CardPurchase> {
    const response = await api.get<CardPurchase>(`/cardpurchases/${id}`)
    return response.data
  },

  async create(data: CreateCardPurchaseDto): Promise<CardPurchase> {
    const response = await api.post<CardPurchase>('/cardpurchases', data)
    return response.data
  },

  async update(id: string, data: UpdateCardPurchaseDto): Promise<CardPurchase> {
    const response = await api.put<CardPurchase>(`/cardpurchases/${id}`, data)
    return response.data
  },

  async markAsPaid(id: string): Promise<CardPurchase> {
    const response = await api.post<CardPurchase>(`/cardpurchases/${id}/pay`)
    return response.data
  },

  async delete(id: string): Promise<void> {
    await api.delete(`/cardpurchases/${id}`)
  },
}
