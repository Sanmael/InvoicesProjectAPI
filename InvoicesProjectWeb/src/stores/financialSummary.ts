import { defineStore } from 'pinia'
import { ref } from 'vue'
import type { FinancialSummary } from '@/types'
import { financialSummaryService } from '@/services/financialSummaryService'

export const useFinancialSummaryStore = defineStore('financialSummary', () => {
  const summary = ref<FinancialSummary | null>(null)
  const loading = ref(false)
  const error = ref<string | null>(null)

  async function fetchCurrentMonth(): Promise<void> {
    loading.value = true
    error.value = null
    try {
      summary.value = await financialSummaryService.getCurrentMonth()
    } catch (e: any) {
      error.value = e.response?.data?.message || 'Erro ao carregar resumo'
    } finally {
      loading.value = false
    }
  }

  async function fetchByMonth(year: number, month: number): Promise<void> {
    loading.value = true
    error.value = null
    try {
      summary.value = await financialSummaryService.getByMonth(year, month)
    } catch (e: any) {
      error.value = e.response?.data?.message || 'Erro ao carregar resumo'
    } finally {
      loading.value = false
    }
  }

  return {
    summary,
    loading,
    error,
    fetchCurrentMonth,
    fetchByMonth,
  }
})
