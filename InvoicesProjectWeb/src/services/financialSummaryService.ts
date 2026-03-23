import api from './api'
import type { FinancialSummary } from '@/types'

export const financialSummaryService = {
  async getCurrentMonth(): Promise<FinancialSummary> {
    const response = await api.get<FinancialSummary>('/financialsummary/current')
    return response.data
  },

  async getByMonth(year: number, month: number): Promise<FinancialSummary> {
    const response = await api.get<FinancialSummary>(`/financialsummary/${year}/${month}`)
    return response.data
  },
}
