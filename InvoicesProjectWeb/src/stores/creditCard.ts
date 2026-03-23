import { defineStore } from 'pinia'
import { ref } from 'vue'
import type { CreditCard, CreditCardWithPurchases, CreateCreditCardDto, UpdateCreditCardDto } from '@/types'
import { creditCardService } from '@/services/creditCardService'

export const useCreditCardStore = defineStore('creditCard', () => {
  const cards = ref<CreditCard[]>([])
  const currentCard = ref<CreditCardWithPurchases | null>(null)
  const loading = ref(false)
  const error = ref<string | null>(null)

  async function fetchAll(): Promise<void> {
    loading.value = true
    error.value = null
    try {
      cards.value = await creditCardService.getAll()
    } catch (e: any) {
      error.value = e.response?.data?.message || 'Erro ao carregar cartões'
    } finally {
      loading.value = false
    }
  }

  async function fetchWithPurchases(id: string): Promise<void> {
    loading.value = true
    error.value = null
    try {
      currentCard.value = await creditCardService.getWithPurchases(id)
    } catch (e: any) {
      error.value = e.response?.data?.message || 'Erro ao carregar cartão'
    } finally {
      loading.value = false
    }
  }

  async function create(data: CreateCreditCardDto): Promise<CreditCard> {
    loading.value = true
    error.value = null
    try {
      const card = await creditCardService.create(data)
      cards.value.push(card)
      return card
    } catch (e: any) {
      error.value = e.response?.data?.message || 'Erro ao criar cartão'
      throw e
    } finally {
      loading.value = false
    }
  }

  async function update(id: string, data: UpdateCreditCardDto): Promise<void> {
    loading.value = true
    error.value = null
    try {
      const updated = await creditCardService.update(id, data)
      const index = cards.value.findIndex((c) => c.id === id)
      if (index !== -1) {
        cards.value[index] = updated
      }
    } catch (e: any) {
      error.value = e.response?.data?.message || 'Erro ao atualizar cartão'
      throw e
    } finally {
      loading.value = false
    }
  }

  async function remove(id: string): Promise<void> {
    loading.value = true
    error.value = null
    try {
      await creditCardService.delete(id)
      cards.value = cards.value.filter((c) => c.id !== id)
    } catch (e: any) {
      error.value = e.response?.data?.message || 'Erro ao remover cartão'
      throw e
    } finally {
      loading.value = false
    }
  }

  return {
    cards,
    currentCard,
    loading,
    error,
    fetchAll,
    fetchWithPurchases,
    create,
    update,
    remove,
  }
})
