import { defineStore } from 'pinia'
import { ref } from 'vue'
import type { Debt, CreateDebtDto, CreateInstallmentDebtDto, CreateRecurringDebtDto, UpdateDebtDto } from '@/types'
import { debtService } from '@/services/debtService'

export const useDebtStore = defineStore('debt', () => {
  const debts = ref<Debt[]>([])
  const loading = ref(false)
  const error = ref<string | null>(null)

  async function fetchAll(): Promise<void> {
    loading.value = true
    error.value = null
    try {
      debts.value = await debtService.getAll()
    } catch (e: any) {
      error.value = e.response?.data?.message || 'Erro ao carregar débitos'
    } finally {
      loading.value = false
    }
  }

  async function fetchPending(): Promise<void> {
    loading.value = true
    error.value = null
    try {
      debts.value = await debtService.getPending()
    } catch (e: any) {
      error.value = e.response?.data?.message || 'Erro ao carregar débitos pendentes'
    } finally {
      loading.value = false
    }
  }

  async function create(data: CreateDebtDto): Promise<Debt> {
    loading.value = true
    error.value = null
    try {
      const debt = await debtService.create(data)
      debts.value.push(debt)
      return debt
    } catch (e: any) {
      error.value = e.response?.data?.message || 'Erro ao criar débito'
      throw e
    } finally {
      loading.value = false
    }
  }

  async function createInstallment(data: CreateInstallmentDebtDto): Promise<void> {
    loading.value = true
    error.value = null
    try {
      const newDebts = await debtService.createInstallment(data)
      debts.value.push(...newDebts)
    } catch (e: any) {
      error.value = e.response?.data?.message || 'Erro ao criar dívida parcelada'
      throw e
    } finally {
      loading.value = false
    }
  }

  async function createRecurring(data: CreateRecurringDebtDto): Promise<void> {
    loading.value = true
    error.value = null
    try {
      const newDebts = await debtService.createRecurring(data)
      debts.value.push(...newDebts)
    } catch (e: any) {
      error.value = e.response?.data?.message || 'Erro ao criar débito recorrente'
      throw e
    } finally {
      loading.value = false
    }
  }

  async function update(id: string, data: UpdateDebtDto): Promise<void> {
    loading.value = true
    error.value = null
    try {
      const updated = await debtService.update(id, data)
      const index = debts.value.findIndex((d) => d.id === id)
      if (index !== -1) {
        debts.value[index] = updated
      }
    } catch (e: any) {
      error.value = e.response?.data?.message || 'Erro ao atualizar débito'
      throw e
    } finally {
      loading.value = false
    }
  }

  async function markAsPaid(id: string): Promise<void> {
    loading.value = true
    error.value = null
    try {
      const updated = await debtService.markAsPaid(id)
      const index = debts.value.findIndex((d) => d.id === id)
      if (index !== -1) {
        debts.value[index] = updated
      }
    } catch (e: any) {
      error.value = e.response?.data?.message || 'Erro ao marcar como pago'
      throw e
    } finally {
      loading.value = false
    }
  }

  async function remove(id: string): Promise<void> {
    loading.value = true
    error.value = null
    try {
      await debtService.delete(id)
      debts.value = debts.value.filter((d) => d.id !== id)
    } catch (e: any) {
      error.value = e.response?.data?.message || 'Erro ao remover débito'
      throw e
    } finally {
      loading.value = false
    }
  }

  async function removeGroup(groupId: string): Promise<void> {
    loading.value = true
    error.value = null
    try {
      await debtService.deleteGroup(groupId)
      debts.value = debts.value.filter((d) => d.installmentGroupId !== groupId || d.isPaid)
    } catch (e: any) {
      error.value = e.response?.data?.message || 'Erro ao remover parcelas'
      throw e
    } finally {
      loading.value = false
    }
  }

  return {
    debts,
    loading,
    error,
    fetchAll,
    fetchPending,
    create,
    createInstallment,
    createRecurring,
    update,
    markAsPaid,
    remove,
    removeGroup,
  }
})
