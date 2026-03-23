import { defineStore } from 'pinia'
import { ref } from 'vue'
import type { Receivable, CreateReceivableDto, CreateRecurringReceivableDto, UpdateReceivableDto } from '@/types'
import { receivableService } from '@/services/receivableService'

export const useReceivableStore = defineStore('receivable', () => {
  const receivables = ref<Receivable[]>([])
  const loading = ref(false)
  const error = ref<string | null>(null)

  async function fetchAll(): Promise<void> {
    loading.value = true
    error.value = null
    try {
      receivables.value = await receivableService.getAll()
    } catch (e: any) {
      error.value = e.response?.data?.message || 'Erro ao carregar recebíveis'
    } finally {
      loading.value = false
    }
  }

  async function fetchPending(): Promise<void> {
    loading.value = true
    error.value = null
    try {
      receivables.value = await receivableService.getPending()
    } catch (e: any) {
      error.value = e.response?.data?.message || 'Erro ao carregar recebíveis pendentes'
    } finally {
      loading.value = false
    }
  }

  async function create(data: CreateReceivableDto): Promise<Receivable> {
    loading.value = true
    error.value = null
    try {
      const receivable = await receivableService.create(data)
      receivables.value.push(receivable)
      return receivable
    } catch (e: any) {
      error.value = e.response?.data?.message || 'Erro ao criar recebível'
      throw e
    } finally {
      loading.value = false
    }
  }

  async function createRecurring(data: CreateRecurringReceivableDto): Promise<void> {
    loading.value = true
    error.value = null
    try {
      const newReceivables = await receivableService.createRecurring(data)
      receivables.value.push(...newReceivables)
    } catch (e: any) {
      error.value = e.response?.data?.message || 'Erro ao criar recebível recorrente'
      throw e
    } finally {
      loading.value = false
    }
  }

  async function update(id: string, data: UpdateReceivableDto): Promise<void> {
    loading.value = true
    error.value = null
    try {
      const updated = await receivableService.update(id, data)
      const index = receivables.value.findIndex((r) => r.id === id)
      if (index !== -1) {
        receivables.value[index] = updated
      }
    } catch (e: any) {
      error.value = e.response?.data?.message || 'Erro ao atualizar recebível'
      throw e
    } finally {
      loading.value = false
    }
  }

  async function markAsReceived(id: string): Promise<void> {
    loading.value = true
    error.value = null
    try {
      const updated = await receivableService.markAsReceived(id)
      const index = receivables.value.findIndex((r) => r.id === id)
      if (index !== -1) {
        receivables.value[index] = updated
      }
    } catch (e: any) {
      error.value = e.response?.data?.message || 'Erro ao marcar como recebido'
      throw e
    } finally {
      loading.value = false
    }
  }

  async function remove(id: string): Promise<void> {
    loading.value = true
    error.value = null
    try {
      await receivableService.delete(id)
      receivables.value = receivables.value.filter((r) => r.id !== id)
    } catch (e: any) {
      error.value = e.response?.data?.message || 'Erro ao remover recebível'
      throw e
    } finally {
      loading.value = false
    }
  }

  async function removeGroup(groupId: string): Promise<void> {
    loading.value = true
    error.value = null
    try {
      await receivableService.deleteGroup(groupId)
      receivables.value = receivables.value.filter((r) => r.recurrenceGroupId !== groupId || r.isReceived)
    } catch (e: any) {
      error.value = e.response?.data?.message || 'Erro ao remover recorrência'
      throw e
    } finally {
      loading.value = false
    }
  }

  return {
    receivables,
    loading,
    error,
    fetchAll,
    fetchPending,
    create,
    createRecurring,
    update,
    markAsReceived,
    remove,
    removeGroup,
  }
})
