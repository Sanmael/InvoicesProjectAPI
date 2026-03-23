<script setup lang="ts">
import { onMounted, computed, ref, watch } from 'vue'
import { useFinancialSummaryStore } from '@/stores/financialSummary'
import { useDebtStore } from '@/stores/debt'
import { useReceivableStore } from '@/stores/receivable'
import type { Debt, Receivable } from '@/types'

const summaryStore = useFinancialSummaryStore()
const debtStore = useDebtStore()
const receivableStore = useReceivableStore()
const selectedMonth = ref(new Date().toISOString().slice(0, 7))

type DashboardDebtItem =
  | { kind: 'single'; debt: Debt }
  | { kind: 'group'; groupId: string; debts: Debt[]; representative: Debt }

type DashboardReceivableItem =
  | { kind: 'single'; receivable: Receivable }
  | { kind: 'group'; groupId: string; receivables: Receivable[]; representative: Receivable }

onMounted(async () => {
  const parts = selectedMonth.value.split('-')
  const parsedYear = Number(parts[0])
  const parsedMonth = Number(parts[1])
  const now = new Date()
  const year = Number.isFinite(parsedYear) ? parsedYear : now.getFullYear()
  const month = Number.isFinite(parsedMonth) ? parsedMonth : now.getMonth() + 1
  await Promise.all([
    summaryStore.fetchByMonth(year, month),
    debtStore.fetchPending(),
    receivableStore.fetchPending(),
  ])
})

watch(selectedMonth, async (value) => {
  showAllDebts.value = false
  showAllReceivables.value = false
  const [year, month] = value.split('-').map(Number)
  if (!year || !month) return
  await summaryStore.fetchByMonth(year, month)
})

const formatCurrency = (value: number) => {
  return new Intl.NumberFormat('pt-BR', {
    style: 'currency',
    currency: 'BRL',
  }).format(value)
}

const balanceClass = computed(() => {
  if (!summaryStore.summary) return ''
  return summaryStore.summary.balance >= 0 ? 'positive' : 'negative'
})

const monthName = computed(() => {
  if (!summaryStore.summary) return ''
  const date = new Date(summaryStore.summary.year, summaryStore.summary.month - 1)
  return date.toLocaleDateString('pt-BR', { month: 'long', year: 'numeric' })
})

const groupedPendingDebts = computed<DashboardDebtItem[]>(() => {
  const seen = new Set<string>()
  const result: DashboardDebtItem[] = []

  for (const debt of debtStore.debts) {
    if (!debt.isInstallment && debt.installmentGroupId) {
      const groupId = debt.installmentGroupId
      if (seen.has(groupId)) continue

      const debts = debtStore.debts
        .filter((d) => !d.isInstallment && d.installmentGroupId === groupId)
        .sort((a, b) => new Date(a.dueDate).getTime() - new Date(b.dueDate).getTime())

      if (debts.length === 0) continue

      result.push({ kind: 'group', groupId, debts, representative: debts[0] as Debt })
      seen.add(groupId)
      continue
    }

    result.push({ kind: 'single', debt })
  }

  return result
})

const groupedPendingReceivables = computed<DashboardReceivableItem[]>(() => {
  const seen = new Set<string>()
  const result: DashboardReceivableItem[] = []

  for (const receivable of receivableStore.receivables) {
    if (receivable.isRecurring && receivable.recurrenceGroupId) {
      const groupId = receivable.recurrenceGroupId
      if (seen.has(groupId)) continue

      const receivables = receivableStore.receivables
        .filter((r) => r.isRecurring && r.recurrenceGroupId === groupId)
        .sort((a, b) => new Date(a.expectedDate).getTime() - new Date(b.expectedDate).getTime())

      if (receivables.length === 0) continue

      result.push({ kind: 'group', groupId, receivables, representative: receivables[0] as Receivable })
      seen.add(groupId)
      continue
    }

    result.push({ kind: 'single', receivable })
  }

  return result
})

const showAllDebts = ref(false)
const showAllReceivables = ref(false)

const filteredPendingDebts = computed<DashboardDebtItem[]>(() => {
  const [year, month] = selectedMonth.value.split('-').map(Number)
  if (!year || !month) return groupedPendingDebts.value
  return groupedPendingDebts.value.filter((item) => {
    if (item.kind === 'single') {
      const d = new Date(item.debt.dueDate)
      return d.getFullYear() === year && d.getMonth() + 1 === month
    }
    return item.debts.some((d) => {
      const date = new Date(d.dueDate)
      return date.getFullYear() === year && date.getMonth() + 1 === month
    })
  })
})

const filteredPendingReceivables = computed<DashboardReceivableItem[]>(() => {
  const [year, month] = selectedMonth.value.split('-').map(Number)
  if (!year || !month) return groupedPendingReceivables.value
  return groupedPendingReceivables.value.filter((item) => {
    if (item.kind === 'single') {
      const d = new Date(item.receivable.expectedDate)
      return d.getFullYear() === year && d.getMonth() + 1 === month
    }
    return item.receivables.some((r) => {
      const date = new Date(r.expectedDate)
      return date.getFullYear() === year && date.getMonth() + 1 === month
    })
  })
})

const displayedDebts = computed(() => {
  if (showAllDebts.value) return filteredPendingDebts.value
  return filteredPendingDebts.value.slice(0, 5)
})

const displayedReceivables = computed(() => {
  if (showAllReceivables.value) return filteredPendingReceivables.value
  return filteredPendingReceivables.value.slice(0, 5)
})
</script>

<template>
  <div class="dashboard">
    <header class="dashboard-header">
      <div>
        <h1>Dashboard</h1>
        <p class="current-month">{{ monthName }}</p>
      </div>
      <div class="month-picker">
        <label for="month-select">Filtrar por mês</label>
        <input id="month-select" v-model="selectedMonth" type="month" />
      </div>
    </header>

    <!-- Loading state -->
    <div v-if="summaryStore.loading" class="loading">
      Carregando...
    </div>

    <!-- Summary Cards -->
    <div v-else-if="summaryStore.summary" class="summary-grid">
      <div class="summary-card balance" :class="balanceClass">
        <div class="card-icon">💰</div>
        <div class="card-content">
          <h3>Saldo do Mês</h3>
          <p class="amount">{{ formatCurrency(summaryStore.summary.balance) }}</p>
        </div>
      </div>

      <div class="summary-card receivables">
        <div class="card-icon">📈</div>
        <div class="card-content">
          <h3>A Receber</h3>
          <p class="amount">{{ formatCurrency(summaryStore.summary.totalReceivables) }}</p>
        </div>
      </div>

      <div class="summary-card debts">
        <div class="card-icon">📉</div>
        <div class="card-content">
          <h3>Débitos</h3>
          <p class="amount">{{ formatCurrency(summaryStore.summary.totalDebts) }}</p>
        </div>
      </div>

      <div class="summary-card cards">
        <div class="card-icon">💳</div>
        <div class="card-content">
          <h3>Cartões</h3>
          <p class="amount">{{ formatCurrency(summaryStore.summary.totalCardPurchases) }}</p>
        </div>
      </div>
    </div>

    <!-- Recent Items -->
    <div class="recent-section">
      <div class="recent-card">
        <h2>📉 Débitos Pendentes</h2>
        <div v-if="debtStore.loading" class="loading-small">Carregando...</div>
        <ul v-else-if="filteredPendingDebts.length > 0" class="item-list">
          <li
            v-for="item in displayedDebts"
            :key="item.kind === 'group' ? `group-${item.groupId}` : item.debt.id"
            class="item"
          >
            <div class="item-info">
              <span class="item-description">
                {{ item.kind === 'group' ? item.representative.description : item.debt.description }}
                <span v-if="item.kind === 'group'" class="item-badge">Recorrente ({{ item.debts.length }})</span>
              </span>
              <span class="item-date">
                {{ new Date(item.kind === 'group' ? item.representative.dueDate : item.debt.dueDate).toLocaleDateString('pt-BR') }}
              </span>
            </div>
            <span class="item-amount negative">
              {{ formatCurrency(item.kind === 'group' ? item.representative.amount : item.debt.amount) }}
            </span>
          </li>
        </ul>
        <p v-else class="empty-message">Nenhum débito pendente neste mês 🎉</p>
        <button v-if="filteredPendingDebts.length > 5" class="view-all-btn" @click="showAllDebts = !showAllDebts">
          {{ showAllDebts ? 'Ver menos ↑' : `Ver todos (${filteredPendingDebts.length}) ↓` }}
        </button>
      </div>

      <div class="recent-card">
        <h2>📈 Recebíveis Pendentes</h2>
        <div v-if="receivableStore.loading" class="loading-small">Carregando...</div>
        <ul v-else-if="filteredPendingReceivables.length > 0" class="item-list">
          <li
            v-for="item in displayedReceivables"
            :key="item.kind === 'group' ? `group-${item.groupId}` : item.receivable.id"
            class="item"
          >
            <div class="item-info">
              <span class="item-description">
                {{ item.kind === 'group' ? item.representative.description : item.receivable.description }}
                <span v-if="item.kind === 'group'" class="item-badge">Recorrente ({{ item.receivables.length }})</span>
              </span>
              <span class="item-date">
                {{ new Date(item.kind === 'group' ? item.representative.expectedDate : item.receivable.expectedDate).toLocaleDateString('pt-BR') }}
              </span>
            </div>
            <span class="item-amount positive">
              {{ formatCurrency(item.kind === 'group' ? item.representative.amount : item.receivable.amount) }}
            </span>
          </li>
        </ul>
        <p v-else class="empty-message">Nenhum recebível pendente neste mês</p>
        <button v-if="filteredPendingReceivables.length > 5" class="view-all-btn" @click="showAllReceivables = !showAllReceivables">
          {{ showAllReceivables ? 'Ver menos ↑' : `Ver todos (${filteredPendingReceivables.length}) ↓` }}
        </button>
      </div>
    </div>
  </div>
</template>

<style scoped>
.dashboard {
  max-width: 1200px;
  margin: 0 auto;
}

.dashboard-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-end;
  gap: 1rem;
  margin-bottom: 2rem;
}

.month-picker {
  display: flex;
  flex-direction: column;
  gap: 0.35rem;
}

.month-picker label {
  font-size: 0.85rem;
  color: var(--text-secondary);
}

.month-picker input {
  padding: 0.55rem 0.7rem;
  border: 1px solid var(--input-border);
  border-radius: var(--border-radius);
  background: var(--input-bg);
  color: var(--text-primary);
}

.dashboard-header h1 {
  margin: 0 0 0.25rem;
  color: var(--text-primary);
}

.current-month {
  color: var(--text-secondary);
  text-transform: capitalize;
  margin: 0;
}

.loading {
  text-align: center;
  padding: 3rem;
  color: var(--text-secondary);
}

.summary-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(240px, 1fr));
  gap: 1.5rem;
  margin-bottom: 2rem;
}

.summary-card {
  background: var(--card-bg);
  border-radius: 16px;
  padding: 1.5rem;
  display: flex;
  align-items: center;
  gap: 1rem;
  box-shadow: var(--shadow-sm);
  transition: all 0.3s ease;
}

.summary-card.balance.positive {
  background: linear-gradient(135deg, var(--color-success-bg) 0%, var(--color-success-bg) 100%);
}

.summary-card.balance.negative {
  background: linear-gradient(135deg, var(--color-danger-bg) 0%, var(--color-danger-bg) 100%);
}

.card-icon {
  font-size: 2.5rem;
}

.card-content h3 {
  margin: 0 0 0.25rem;
  font-size: 0.9rem;
  color: var(--text-secondary);
  font-weight: 500;
}

.card-content .amount {
  margin: 0;
  font-size: 1.5rem;
  font-weight: 700;
  color: var(--text-primary);
}

.recent-section {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(300px, 1fr));
  gap: 1.5rem;
}

.recent-card {
  background: var(--card-bg);
  border-radius: 16px;
  padding: 1.5rem;
  box-shadow: var(--shadow-sm);
  transition: background-color 0.3s ease;
}

.recent-card h2 {
  margin: 0 0 1rem;
  font-size: 1.1rem;
  color: var(--text-primary);
}

.item-list {
  list-style: none;
  padding: 0;
  margin: 0;
}

.item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 0.75rem 0;
  border-bottom: 1px solid var(--border-color);
}

.item:last-child {
  border-bottom: none;
}

.item-info {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.item-description {
  font-weight: 500;
  color: var(--text-primary);
}

.item-badge {
  margin-left: 0.5rem;
  font-size: 0.72rem;
  background: var(--color-info-bg);
  color: var(--color-info);
  border-radius: 999px;
  padding: 0.15rem 0.45rem;
  font-weight: 600;
}

.item-date {
  font-size: 0.85rem;
  color: var(--text-muted);
}

.item-amount {
  font-weight: 600;
}

.item-amount.positive {
  color: var(--color-success);
}

.item-amount.negative {
  color: var(--color-danger);
}

.empty-message {
  color: var(--text-muted);
  text-align: center;
  padding: 1rem 0;
}

.view-all-btn {
  display: block;
  width: 100%;
  text-align: center;
  margin-top: 1rem;
  padding: 0.5rem;
  background: none;
  border: 1px solid var(--input-border);
  border-radius: var(--border-radius);
  color: var(--color-primary);
  font-weight: 500;
  cursor: pointer;
  transition: all 0.2s ease;
}

.view-all-btn:hover {
  background: var(--color-primary);
  color: white;
  border-color: var(--color-primary);
}

.loading-small {
  color: var(--text-muted);
  padding: 1rem 0;
}

/* Responsivo */
@media (max-width: 640px) {
  .dashboard-header {
    flex-direction: column;
    align-items: flex-start;
  }

  .summary-grid {
    grid-template-columns: 1fr;
  }
  
  .recent-section {
    grid-template-columns: 1fr;
  }
  
  .summary-card {
    padding: 1.25rem;
  }
  
  .card-content .amount {
    font-size: 1.25rem;
  }
}
</style>
