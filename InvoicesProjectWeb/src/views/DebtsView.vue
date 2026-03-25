<script setup lang="ts">
import { computed, ref, onMounted } from 'vue'
import { useDebtStore } from '@/stores/debt'
import type { CreateDebtDto, CreateInstallmentDebtDto, CreateRecurringDebtDto, UpdateDebtDto, Debt } from '@/types'

const debtStore = useDebtStore()

const showModal = ref(false)
const showEditModal = ref(false)
const mode = ref<'simple' | 'installment' | 'recurring'>('simple')
const editingDebt = ref<Debt | null>(null)
const expandedInstallmentGroupIds = ref<Set<string>>(new Set())
const selectedMonth = ref('')
const defaultStartMonth = new Date().toISOString().slice(0, 7)

const simpleForm = ref<CreateDebtDto>({
  description: '',
  amount: 0,
  dueDate: '',
  notes: '',
})

const installmentForm = ref<CreateInstallmentDebtDto>({
  description: '',
  totalAmount: 0,
  firstDueDate: '',
  installments: 2,
  notes: '',
})

const recurringForm = ref<CreateRecurringDebtDto & { startMonth: string }>({
  description: '',
  amount: 0,
  recurringDay: 5,
  months: 12,
  startMonth: defaultStartMonth,
  notes: '',
})

const editForm = ref<UpdateDebtDto>({
  description: '',
  amount: 0,
  dueDate: '',
  notes: '',
})

onMounted(() => {
  debtStore.fetchAll()
})

const formatCurrency = (value: number) => {
  return new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(value)
}

const toInputDate = (dateStr: string) => dateStr ? dateStr.split('T')[0] : ''
const formatDate = (dateStr: string) => new Date(dateStr).toLocaleDateString('pt-BR')

type DebtListItem =
  | { kind: 'single'; item: Debt }
  | { kind: 'group'; groupId: string; groupType: 'installment' | 'recurring'; items: Debt[]; representative: Debt }

const groupedDebts = computed<DebtListItem[]>(() => {
  const list = debtStore.debts
  const seenGroupIds = new Set<string>()
  const result: DebtListItem[] = []

  for (const debt of list) {
    if (debt.installmentGroupId) {
      const groupId = debt.installmentGroupId
      if (seenGroupIds.has(groupId)) {
        continue
      }

      const items = list
        .filter((d) => d.installmentGroupId === groupId)
        .sort((a, b) => new Date(a.dueDate).getTime() - new Date(b.dueDate).getTime())

      if (items.length === 0) {
        continue
      }

      const representative = (items.find((d) => !d.isPaid) ?? items[0]) as Debt
      const groupType: 'installment' | 'recurring' = items.some((d) => d.isInstallment)
        ? 'installment'
        : 'recurring'

      result.push({ kind: 'group', groupId, groupType, items, representative })
      seenGroupIds.add(groupId)
      continue
    }

    result.push({ kind: 'single', item: debt })
  }

  return result
})

const filteredGroupedDebts = computed<DebtListItem[]>(() => {
  if (!selectedMonth.value) return groupedDebts.value
  const [year, month] = selectedMonth.value.split('-').map(Number)
  if (!year || !month) return groupedDebts.value
  return groupedDebts.value.filter((entry) => {
    if (entry.kind === 'single') {
      const d = new Date(entry.item.dueDate)
      return d.getFullYear() === year && d.getMonth() + 1 === month
    }
    return entry.items.some((d) => {
      const date = new Date(d.dueDate)
      return date.getFullYear() === year && date.getMonth() + 1 === month
    })
  })
})

const isGroupExpanded = (groupId: string) => expandedInstallmentGroupIds.value.has(groupId)

function toggleGroup(groupId: string) {
  const updated = new Set(expandedInstallmentGroupIds.value)
  if (updated.has(groupId)) {
    updated.delete(groupId)
  } else {
    updated.add(groupId)
  }
  expandedInstallmentGroupIds.value = updated
}

function openModal() {
  mode.value = 'simple'
  simpleForm.value = { description: '', amount: 0, dueDate: '', notes: '' }
  installmentForm.value = { description: '', totalAmount: 0, firstDueDate: '', installments: 2, notes: '' }
  recurringForm.value = { description: '', amount: 0, recurringDay: 5, months: 12, startMonth: defaultStartMonth, notes: '' }
  showModal.value = true
}

function openEditModal(debt: Debt) {
  editingDebt.value = debt
  editForm.value = {
    description: debt.description,
    amount: debt.amount,
    dueDate: toInputDate(debt.dueDate),
    notes: debt.notes ?? '',
  }
  showEditModal.value = true
}

async function handleSubmit() {
  if (mode.value === 'installment') {
    await debtStore.createInstallment(installmentForm.value)
  } else if (mode.value === 'recurring') {
    await debtStore.createRecurring({
      description: recurringForm.value.description,
      amount: recurringForm.value.amount,
      recurringDay: recurringForm.value.recurringDay,
      months: recurringForm.value.months,
      startDate: recurringForm.value.startMonth ? `${recurringForm.value.startMonth}-01` : undefined,
      notes: recurringForm.value.notes,
    })
  } else {
    await debtStore.create(simpleForm.value)
  }
  showModal.value = false
}

async function handleEditSubmit() {
  if (!editingDebt.value) return
  await debtStore.update(editingDebt.value.id, editForm.value)
  showEditModal.value = false
  editingDebt.value = null
}

async function handleMarkAsPaid(id: string) {
  await debtStore.markAsPaid(id)
}

async function handleDelete(id: string) {
  if (confirm('Tem certeza que deseja excluir este débito?')) {
    await debtStore.remove(id)
  }
}

async function handleDeleteGroup(groupId: string) {
  if (confirm('Cancelar todos os débitos não pagos deste lote?')) {
    await debtStore.removeGroup(groupId)
  }
}
</script>

<template>
  <div class="debts-page">
    <header class="page-header">
      <div>
        <h1>📉 Débitos</h1>
        <p>Gerencie suas contas a pagar</p>
      </div>
      <div class="header-actions">
        <div class="month-filter">
          <label for="debt-month">Filtrar mês</label>
          <div class="month-filter-input">
            <input id="debt-month" v-model="selectedMonth" type="month" />
            <button v-if="selectedMonth" class="btn-clear-filter" @click="selectedMonth = ''" title="Limpar filtro">✕</button>
          </div>
        </div>
        <button @click="openModal" class="btn-primary">+ Novo Débito</button>
      </div>
    </header>

    <div v-if="debtStore.loading" class="loading">Carregando...</div>

    <div v-else-if="debtStore.error" class="error-alert">
      {{ debtStore.error }}
    </div>

    <div v-else-if="debtStore.debts.length === 0" class="empty-state">
      <p>Nenhum débito cadastrado</p>
      <button @click="openModal" class="btn-primary">Criar primeiro débito</button>
    </div>

    <div v-else-if="filteredGroupedDebts.length === 0" class="empty-state">
      <p>Nenhum débito encontrado para este mês</p>
    </div>

    <div v-else class="debts-list">
      <div
        v-for="entry in filteredGroupedDebts"
        :key="entry.kind === 'group' ? `group-${entry.groupId}` : entry.item.id"
        class="debt-wrapper"
      >
        <div
          class="debt-card"
          :class="{ paid: entry.kind === 'single' ? entry.item.isPaid : entry.representative.isPaid }"
        >
          <div class="debt-info">
            <div class="debt-title-row">
              <h3>{{ entry.kind === 'single' ? entry.item.description : entry.representative.description }}</h3>
              <span v-if="entry.kind === 'group' && entry.groupType === 'installment'" class="badge badge-installment">
                Parcelado ({{ entry.items.length }}x)
              </span>
              <span v-else-if="entry.kind === 'group' && entry.groupType === 'recurring'" class="badge badge-recurring">
                Recorrente ({{ entry.items.length }}x)
              </span>
              <span v-else-if="entry.kind === 'single' && entry.item.isInstallment" class="badge badge-installment">
                Parcela {{ entry.item.installmentNumber }}/{{ entry.item.totalInstallments }}
              </span>
              <span v-else-if="entry.kind === 'single' && entry.item.installmentGroupId" class="badge badge-recurring">
                Recorrente
              </span>
            </div>
            <p class="debt-date" v-if="entry.kind === 'single'">
              Vencimento: {{ formatDate(entry.item.dueDate) }}
            </p>
            <p class="debt-date" v-else>
              Próxima/mais recente: {{ formatDate(entry.representative.dueDate) }}
            </p>
            <p v-if="entry.kind === 'single' && entry.item.notes" class="debt-notes">{{ entry.item.notes }}</p>
            <p v-else-if="entry.kind === 'group'" class="debt-notes">
              {{
                isGroupExpanded(entry.groupId)
                  ? entry.groupType === 'installment' ? 'Parcelas expandidas.' : 'Recorrências expandidas.'
                  : entry.groupType === 'installment'
                    ? `Mais ${entry.items.length - 1} parcela(s) ocultas.`
                    : `Mais ${entry.items.length - 1} recorrência(s) ocultas.`
              }}
            </p>
          </div>
          <div class="debt-actions">
            <span class="debt-amount">
              {{ formatCurrency(entry.kind === 'single' ? entry.item.amount : entry.representative.amount) }}
            </span>
            <div class="action-buttons" v-if="entry.kind === 'single'">
              <button
                v-if="!entry.item.isPaid"
                @click="handleMarkAsPaid(entry.item.id)"
                class="btn-action btn-success"
                title="Marcar como pago"
              >
                <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3">
                  <polyline points="20 6 9 17 4 12"></polyline>
                </svg>
              </button>
              <span v-else class="paid-badge">Pago</span>
              <button
                @click="openEditModal(entry.item)"
                class="btn-action btn-edit"
                title="Editar"
              >
                <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                  <path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7"></path>
                  <path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z"></path>
                </svg>
              </button>
              <button
                v-if="entry.item.isInstallment && entry.item.installmentGroupId && !entry.item.isPaid"
                @click="handleDeleteGroup(entry.item.installmentGroupId)"
                class="btn-action btn-warning"
                title="Cancelar lote"
              >
                <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                  <line x1="18" y1="6" x2="6" y2="18"></line>
                  <line x1="6" y1="6" x2="18" y2="18"></line>
                </svg>
              </button>
              <button @click="handleDelete(entry.item.id)" class="btn-action btn-danger" title="Excluir">
                <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                  <polyline points="3 6 5 6 21 6"></polyline>
                  <path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"></path>
                </svg>
              </button>
            </div>
            <div class="action-buttons" v-else>
              <button class="btn-action btn-outline" @click="toggleGroup(entry.groupId)">
                {{ isGroupExpanded(entry.groupId) ? 'Ocultar' : 'Abrir' }}
              </button>
              <button
                v-if="entry.items.some((d) => !d.isPaid)"
                @click="handleDeleteGroup(entry.groupId)"
                class="btn-action btn-warning"
                title="Cancelar lote"
              >
                <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                  <line x1="18" y1="6" x2="6" y2="18"></line>
                  <line x1="6" y1="6" x2="18" y2="18"></line>
                </svg>
              </button>
            </div>
          </div>
        </div>

        <div
          v-if="entry.kind === 'group' && isGroupExpanded(entry.groupId)"
          class="group-items"
        >
          <div
            v-for="debt in entry.items"
            :key="debt.id"
            class="debt-card debt-card-child"
            :class="{ paid: debt.isPaid }"
          >
            <div class="debt-info">
              <div class="debt-title-row">
                <h3>{{ debt.description }}</h3>
                <span v-if="entry.groupType === 'installment'" class="badge badge-installment">
                  Parcela {{ debt.installmentNumber }}/{{ debt.totalInstallments }}
                </span>
                <span v-else class="badge badge-recurring">
                  Recorrente
                </span>
              </div>
              <p class="debt-date">Vencimento: {{ formatDate(debt.dueDate) }}</p>
              <p v-if="debt.notes" class="debt-notes">{{ debt.notes }}</p>
            </div>
            <div class="debt-actions">
              <span class="debt-amount">{{ formatCurrency(debt.amount) }}</span>
              <div class="action-buttons">
                <button
                  v-if="!debt.isPaid"
                  @click="handleMarkAsPaid(debt.id)"
                  class="btn-action btn-success"
                  title="Marcar como pago"
                >
                  <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3">
                    <polyline points="20 6 9 17 4 12"></polyline>
                  </svg>
                </button>
                <span v-else class="paid-badge">Pago</span>
                <button
                  @click="openEditModal(debt)"
                  class="btn-action btn-edit"
                  title="Editar"
                >
                  <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                    <path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7"></path>
                    <path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z"></path>
                  </svg>
                </button>
                <button @click="handleDelete(debt.id)" class="btn-action btn-danger" title="Excluir">
                  <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                    <polyline points="3 6 5 6 21 6"></polyline>
                    <path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"></path>
                  </svg>
                </button>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- Modal: Novo Débito -->
    <div v-if="showModal" class="modal-overlay">
      <div class="modal">
        <div class="modal-header">
          <h2>Novo Débito</h2>
          <button type="button" class="close-btn" @click="showModal = false">✕</button>
        </div>

        <div class="mode-selector">
          <button :class="['mode-btn', { active: mode === 'simple' }]" type="button" @click="mode = 'simple'">Débito simples</button>
          <button :class="['mode-btn', { active: mode === 'installment' }]" type="button" @click="mode = 'installment'">Parcelado</button>
          <button :class="['mode-btn', { active: mode === 'recurring' }]" type="button" @click="mode = 'recurring'">Recorrente</button>
        </div>

        <form v-if="mode === 'simple'" @submit.prevent="handleSubmit">
          <div class="form-group">
            <label>Descrição</label>
            <input v-model="simpleForm.description" required />
          </div>
          <div class="form-group">
            <label>Valor</label>
            <input v-model.number="simpleForm.amount" type="number" step="0.01" min="0" required />
          </div>
          <div class="form-group">
            <label>Data de Vencimento</label>
            <input v-model="simpleForm.dueDate" type="date" required />
          </div>
          <div class="form-group">
            <label>Observações</label>
            <textarea v-model="simpleForm.notes" rows="2"></textarea>
          </div>
          <div class="modal-actions">
            <button type="button" @click="showModal = false" class="btn-secondary">Cancelar</button>
            <button type="submit" class="btn-primary">Salvar</button>
          </div>
        </form>

        <form v-else-if="mode === 'installment'" @submit.prevent="handleSubmit">
          <div class="form-group">
            <label>Descrição (ex: Dívida com João)</label>
            <input v-model="installmentForm.description" required />
          </div>
          <div class="form-group">
            <label>Valor total da dívida</label>
            <input v-model.number="installmentForm.totalAmount" type="number" step="0.01" min="0" required />
          </div>
          <div class="form-group">
            <label>Vencimento da 1ª parcela</label>
            <input v-model="installmentForm.firstDueDate" type="date" required />
          </div>
          <div class="form-group">
            <label>Número de parcelas (2-60)</label>
            <input v-model.number="installmentForm.installments" type="number" min="2" max="60" required />
          </div>
          <div v-if="installmentForm.totalAmount > 0 && installmentForm.installments > 0" class="installment-preview">
            Valor por parcela: <strong>{{ formatCurrency(installmentForm.totalAmount / installmentForm.installments) }}</strong>
          </div>
          <div class="form-group">
            <label>Observações</label>
            <textarea v-model="installmentForm.notes" rows="2"></textarea>
          </div>
          <div class="modal-actions">
            <button type="button" @click="showModal = false" class="btn-secondary">Cancelar</button>
            <button type="submit" class="btn-primary">Gerar parcelas</button>
          </div>
        </form>

        <form v-else @submit.prevent="handleSubmit">
          <div class="form-group">
            <label>Descrição (ex: Ajuda familiar)</label>
            <input v-model="recurringForm.description" required />
          </div>
          <div class="form-group">
            <label>Valor mensal</label>
            <input v-model.number="recurringForm.amount" type="number" step="0.01" min="0" required />
          </div>
          <div class="form-group">
            <label>Dia fixo do mês (1-28)</label>
            <input v-model.number="recurringForm.recurringDay" type="number" min="1" max="28" required />
          </div>
          <div class="form-group">
            <label>Mês de início</label>
            <input v-model="recurringForm.startMonth" type="month" required />
          </div>
          <div class="form-group">
            <label>Quantidade de meses (1-60)</label>
            <input v-model.number="recurringForm.months" type="number" min="1" max="60" required />
          </div>
          <div class="recurring-preview">
            Serão gerados <strong>{{ recurringForm.months }}</strong> débitos mensais de
            <strong>{{ formatCurrency(recurringForm.amount) }}</strong>, todo dia
            <strong>{{ recurringForm.recurringDay }}</strong>.
          </div>
          <div class="form-group">
            <label>Observações</label>
            <textarea v-model="recurringForm.notes" rows="2"></textarea>
          </div>
          <div class="modal-actions">
            <button type="button" @click="showModal = false" class="btn-secondary">Cancelar</button>
            <button type="submit" class="btn-primary">Gerar recorrência</button>
          </div>
        </form>
      </div>
    </div>

    <!-- Modal: Editar Débito -->
    <div v-if="showEditModal" class="modal-overlay">
      <div class="modal">
        <div class="modal-header">
          <h2>Editar Débito</h2>
          <button type="button" class="close-btn" @click="showEditModal = false">✕</button>
        </div>
        <form @submit.prevent="handleEditSubmit">
          <div class="form-group">
            <label>Descrição</label>
            <input v-model="editForm.description" required />
          </div>
          <div class="form-group">
            <label>Valor</label>
            <input v-model.number="editForm.amount" type="number" step="0.01" min="0" required />
          </div>
          <div class="form-group">
            <label>Data de Vencimento</label>
            <input v-model="editForm.dueDate" type="date" required />
          </div>
          <div class="form-group">
            <label>Observações</label>
            <textarea v-model="editForm.notes" rows="2"></textarea>
          </div>
          <div class="modal-actions">
            <button type="button" @click="showEditModal = false" class="btn-secondary">Cancelar</button>
            <button type="submit" class="btn-primary">Salvar</button>
          </div>
        </form>
      </div>
    </div>
  </div>
</template>

<style scoped>
.debts-page { max-width: 900px; margin: 0 auto; }

.page-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 2rem;
  flex-wrap: wrap;
  gap: 1rem;
}
.page-header h1 { margin: 0; color: var(--text-primary); }
.page-header p { margin: 0.25rem 0 0; color: var(--text-secondary); }

.header-actions { display: flex; align-items: center; gap: 1rem; flex-wrap: wrap; }

.month-filter { display: flex; flex-direction: column; gap: 0.25rem; }
.month-filter label { font-size: 0.8rem; color: var(--text-secondary); }
.month-filter-input { display: flex; align-items: center; gap: 0.35rem; }
.month-filter-input input {
  padding: 0.5rem 0.65rem;
  border: 1px solid var(--input-border);
  border-radius: var(--border-radius);
  background: var(--input-bg);
  color: var(--text-primary);
}
.btn-clear-filter {
  background: none;
  border: none;
  color: var(--text-muted);
  cursor: pointer;
  font-size: 1.1rem;
  padding: 0.25rem;
  line-height: 1;
}
.btn-clear-filter:hover { color: var(--color-danger); }

.btn-primary {
  background: var(--color-primary);
  color: white;
  border: none;
  padding: 0.75rem 1.5rem;
  border-radius: var(--border-radius);
  font-weight: 600;
  cursor: pointer;
  transition: all 0.2s ease;
}
.btn-primary:hover { 
  background: var(--color-primary-hover);
  transform: translateY(-2px); 
}

.loading, .empty-state { 
  text-align: center; 
  padding: 3rem; 
  color: var(--text-secondary); 
}

.debts-list { display: flex; flex-direction: column; gap: 1rem; }

.debt-wrapper {
  display: flex;
  flex-direction: column;
  gap: 0.65rem;
}

.debt-card {
  background: var(--card-bg);
  border-radius: var(--border-radius-lg);
  padding: 1.25rem;
  display: flex;
  justify-content: space-between;
  align-items: center;
  box-shadow: var(--shadow-sm);
  border-left: 4px solid var(--color-danger);
  transition: background-color 0.3s ease;
}
.debt-card.paid { 
  border-left-color: var(--color-success); 
  opacity: 0.75; 
}

.debt-title-row { display: flex; align-items: center; gap: 0.5rem; flex-wrap: wrap; }
.debt-title-row h3 { margin: 0; color: var(--text-primary); }

.badge { font-size: 0.75rem; font-weight: 600; padding: 0.2rem 0.6rem; border-radius: 999px; }
.badge-installment { background: var(--color-warning-bg); color: var(--color-warning); }
.badge-recurring { background: var(--color-info-bg); color: var(--color-info); }

.debt-date { color: var(--text-secondary); font-size: 0.9rem; margin: 0.25rem 0 0; }
.debt-notes { color: var(--text-muted); font-size: 0.85rem; margin: 0.5rem 0 0; }

.debt-actions { display: flex; align-items: center; gap: 1rem; flex-wrap: wrap; }
.debt-amount { font-size: 1.25rem; font-weight: 700; color: var(--color-danger); }
.debt-card.paid .debt-amount { color: var(--color-success); }

.action-buttons { display: flex; gap: 0.5rem; }

.btn-action {
  width: 40px;
  height: 40px;
  border-radius: var(--border-radius);
  border: none;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: all 0.2s ease;
}

.btn-outline {
  width: auto;
  padding: 0 0.9rem;
  background: transparent;
  border: 1px solid var(--input-border);
  color: var(--text-primary);
  font-weight: 600;
}

.btn-action:hover {
  transform: scale(1.1);
}

.btn-success { 
  background: var(--color-success); 
  color: white;
}
.btn-success:hover { 
  background: var(--color-success-hover); 
}

.btn-edit { 
  background: var(--color-info); 
  color: white; 
}
.btn-edit:hover { 
  background: #0369a1; 
}

.btn-warning { 
  background: var(--color-warning); 
  color: white;
}
.btn-warning:hover { 
  background: #b45309; 
}

.btn-danger { 
  background: var(--color-danger); 
  color: white;
}
.btn-danger:hover { 
  background: var(--color-danger-hover); 
}

.paid-badge {
  background: var(--color-success-bg);
  color: var(--color-success);
  padding: 0.25rem 0.75rem;
  border-radius: 999px;
  font-size: 0.85rem;
  font-weight: 500;
}

.group-items {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  padding-left: 1rem;
  border-left: 2px dashed var(--input-border);
}

.debt-card-child {
  border-left-color: rgba(245, 158, 11, 0.5);
}

.modal-overlay {
  position: fixed;
  inset: 0;
  background: rgba(0,0,0,0.5);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1000;
  padding: 1rem;
}

.modal {
  background: var(--card-bg);
  border-radius: 16px;
  padding: 2rem;
  width: 100%;
  max-width: 450px;
  max-height: 90vh;
  overflow-y: auto;
}

.modal-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 1.25rem;
}
.modal-header h2 { margin: 0; color: var(--text-primary); }
.close-btn { 
  background: none; 
  border: none; 
  font-size: 1.5rem; 
  cursor: pointer; 
  color: var(--text-muted); 
  padding: 0.25rem;
  width: 36px;
  height: 36px;
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: var(--border-radius);
}
.close-btn:hover { 
  color: var(--text-primary); 
  background: var(--border-color);
}

.mode-selector {
  display: flex;
  gap: 0.5rem;
  margin-bottom: 1.5rem;
  background: var(--bg-secondary);
  border-radius: var(--border-radius);
  padding: 4px;
}
.mode-btn {
  flex: 1;
  padding: 0.5rem;
  border: none;
  border-radius: 6px;
  font-weight: 500;
  cursor: pointer;
  background: transparent;
  color: var(--text-secondary);
  transition: all 0.2s;
}
.mode-btn.active { 
  background: var(--card-bg); 
  color: var(--text-primary); 
  box-shadow: var(--shadow-sm); 
}

.installment-preview {
  background: var(--color-warning-bg);
  border-radius: var(--border-radius);
  padding: 0.75rem;
  margin-bottom: 1rem;
  font-size: 0.9rem;
  color: var(--color-warning);
}

.recurring-preview {
  background: var(--color-info-bg);
  border-radius: var(--border-radius);
  padding: 0.75rem;
  margin-bottom: 1rem;
  font-size: 0.9rem;
  color: var(--color-info);
}

.form-group { margin-bottom: 1rem; }
.form-group label { 
  display: block; 
  margin-bottom: 0.5rem; 
  font-weight: 500; 
  color: var(--text-primary); 
}
.form-group input,
.form-group textarea {
  width: 100%;
  padding: 0.75rem;
  border: 2px solid var(--input-border);
  border-radius: var(--border-radius);
  font-size: 1rem;
  box-sizing: border-box;
  background: var(--input-bg);
  color: var(--text-primary);
  transition: border-color 0.2s ease;
}
.form-group input:focus,
.form-group textarea:focus { 
  outline: none; 
  border-color: var(--color-primary); 
}

.modal-actions { display: flex; gap: 1rem; margin-top: 1.5rem; }
.btn-secondary {
  flex: 1;
  padding: 0.75rem;
  background: var(--bg-secondary);
  color: var(--text-primary);
  border: 1px solid var(--border-color);
  border-radius: var(--border-radius);
  font-weight: 500;
  cursor: pointer;
  transition: background 0.2s ease;
}
.btn-secondary:hover {
  background: var(--border-color);
}
.modal-actions .btn-primary { flex: 1; }

/* Responsivo */
@media (max-width: 640px) {
  .debt-card {
    flex-direction: column;
    align-items: flex-start;
    gap: 1rem;
  }
  
  .debt-actions {
    width: 100%;
    justify-content: space-between;
  }
  
  .modal {
    padding: 1.5rem;
    margin: 0.5rem;
  }
}
</style>
