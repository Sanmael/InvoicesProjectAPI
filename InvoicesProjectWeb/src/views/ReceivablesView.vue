<script setup lang="ts">
import { computed, ref, onMounted } from 'vue'
import { useReceivableStore } from '@/stores/receivable'
import type { CreateReceivableDto, CreateRecurringReceivableDto, UpdateReceivableDto, Receivable } from '@/types'

const receivableStore = useReceivableStore()

const showModal = ref(false)
const showEditModal = ref(false)
const mode = ref<'simple' | 'recurring'>('simple')
const editingReceivable = ref<Receivable | null>(null)
const expandedRecurringGroupIds = ref<Set<string>>(new Set())
const selectedMonth = ref('')

const simpleForm = ref<CreateReceivableDto>({
  description: '',
  amount: 0,
  expectedDate: '',
  notes: '',
})

const recurringForm = ref<CreateRecurringReceivableDto>({
  description: '',
  amount: 0,
  recurringDay: 5,
  notes: '',
  months: 12,
})

const editForm = ref<UpdateReceivableDto>({
  description: '',
  amount: 0,
  expectedDate: '',
  notes: '',
})

onMounted(() => {
  receivableStore.fetchAll()
})

const formatCurrency = (value: number) => {
  return new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(value)
}

const toInputDate = (dateStr: string) => dateStr ? dateStr.split('T')[0] : ''
const formatDate = (dateStr: string) => new Date(dateStr).toLocaleDateString('pt-BR')

type ReceivableListItem =
  | { kind: 'single'; item: Receivable }
  | { kind: 'group'; groupId: string; items: Receivable[]; representative: Receivable }

const groupedReceivables = computed<ReceivableListItem[]>(() => {
  const list = receivableStore.receivables
  const seenGroupIds = new Set<string>()
  const result: ReceivableListItem[] = []

  for (const receivable of list) {
    if (receivable.isRecurring && receivable.recurrenceGroupId) {
      const groupId = receivable.recurrenceGroupId
      if (seenGroupIds.has(groupId)) {
        continue
      }

      const items = list
        .filter((r) => r.isRecurring && r.recurrenceGroupId === groupId)
        .sort((a, b) => new Date(a.expectedDate).getTime() - new Date(b.expectedDate).getTime())

      if (items.length === 0) {
        continue
      }

      const representative = (items.find((r) => !r.isReceived) ?? items[0]) as Receivable

      result.push({ kind: 'group', groupId, items, representative })
      seenGroupIds.add(groupId)
      continue
    }

    result.push({ kind: 'single', item: receivable })
  }

  return result
})

const filteredGroupedReceivables = computed<ReceivableListItem[]>(() => {
  if (!selectedMonth.value) return groupedReceivables.value
  const [year, month] = selectedMonth.value.split('-').map(Number)
  if (!year || !month) return groupedReceivables.value
  return groupedReceivables.value.filter((entry) => {
    if (entry.kind === 'single') {
      const d = new Date(entry.item.expectedDate)
      return d.getFullYear() === year && d.getMonth() + 1 === month
    }
    return entry.items.some((r) => {
      const date = new Date(r.expectedDate)
      return date.getFullYear() === year && date.getMonth() + 1 === month
    })
  })
})

const isGroupExpanded = (groupId: string) => expandedRecurringGroupIds.value.has(groupId)

function toggleGroup(groupId: string) {
  const updated = new Set(expandedRecurringGroupIds.value)
  if (updated.has(groupId)) {
    updated.delete(groupId)
  } else {
    updated.add(groupId)
  }
  expandedRecurringGroupIds.value = updated
}

function openModal() {
  mode.value = 'simple'
  simpleForm.value = { description: '', amount: 0, expectedDate: '', notes: '' }
  recurringForm.value = { description: '', amount: 0, recurringDay: 5, notes: '', months: 12 }
  showModal.value = true
}

function openEditModal(receivable: Receivable) {
  editingReceivable.value = receivable
  editForm.value = {
    description: receivable.description,
    amount: receivable.amount,
    expectedDate: toInputDate(receivable.expectedDate),
    notes: receivable.notes ?? '',
  }
  showEditModal.value = true
}

async function handleSubmit() {
  if (mode.value === 'recurring') {
    await receivableStore.createRecurring(recurringForm.value)
  } else {
    await receivableStore.create(simpleForm.value)
  }
  showModal.value = false
}

async function handleEditSubmit() {
  if (!editingReceivable.value) return
  await receivableStore.update(editingReceivable.value.id, editForm.value)
  showEditModal.value = false
  editingReceivable.value = null
}

async function handleMarkAsReceived(id: string) {
  await receivableStore.markAsReceived(id)
}

async function handleDelete(id: string) {
  if (confirm('Tem certeza que deseja excluir este recebível?')) {
    await receivableStore.remove(id)
  }
}

async function handleDeleteGroup(groupId: string) {
  if (confirm('Cancelar todos os recebimentos futuros desta recorrência?')) {
    await receivableStore.removeGroup(groupId)
  }
}
</script>

<template>
  <div class="receivables-page">
    <header class="page-header">
      <div>
        <h1>📈 Recebíveis</h1>
        <p>Gerencie seus valores a receber</p>
      </div>
      <div class="header-actions">
        <div class="month-filter">
          <label for="receivable-month">Filtrar mês</label>
          <div class="month-filter-input">
            <input id="receivable-month" v-model="selectedMonth" type="month" />
            <button v-if="selectedMonth" class="btn-clear-filter" @click="selectedMonth = ''" title="Limpar filtro">✕</button>
          </div>
        </div>
        <button @click="openModal" class="btn-primary">+ Novo Recebível</button>
      </div>
    </header>

    <div v-if="receivableStore.loading" class="loading">Carregando...</div>

    <div v-else-if="receivableStore.receivables.length === 0" class="empty-state">
      <p>Nenhum recebível cadastrado</p>
      <button @click="openModal" class="btn-primary">Criar primeiro Recebível</button>
    </div>

    <div v-else-if="filteredGroupedReceivables.length === 0" class="empty-state">
      <p>Nenhum recebível encontrado para este mês</p>
    </div>

    <div v-else class="receivables-list">
      <div
        v-for="entry in filteredGroupedReceivables"
        :key="entry.kind === 'group' ? `group-${entry.groupId}` : entry.item.id"
        class="receivable-wrapper"
      >
        <div
          class="receivable-card"
          :class="{ received: entry.kind === 'single' ? entry.item.isReceived : entry.representative.isReceived }"
        >
          <div class="receivable-info">
            <div class="receivable-title-row">
              <h3>{{ entry.kind === 'single' ? entry.item.description : entry.representative.description }}</h3>
              <span v-if="entry.kind === 'group'" class="badge badge-recurring">
                🔄 Recorrente ({{ entry.items.length }})
              </span>
              <span v-else-if="entry.item.isRecurring" class="badge badge-recurring">
                🔄 Recorrente
              </span>
            </div>
            <p class="receivable-date" v-if="entry.kind === 'single'">
              Previsão: {{ formatDate(entry.item.expectedDate) }}
            </p>
            <p class="receivable-date" v-else>
              Próxima/mais recente: {{ formatDate(entry.representative.expectedDate) }}
            </p>
            <p v-if="entry.kind === 'single' && entry.item.notes" class="receivable-notes">{{ entry.item.notes }}</p>
            <p v-else-if="entry.kind === 'group'" class="receivable-notes">
              {{ isGroupExpanded(entry.groupId) ? 'Ocorrências expandidas.' : `Mais ${entry.items.length - 1} ocorrência(s) ocultas.` }}
            </p>
          </div>
          <div class="receivable-actions">
            <span class="receivable-amount">
              {{ formatCurrency(entry.kind === 'single' ? entry.item.amount : entry.representative.amount) }}
            </span>
            <div class="action-buttons" v-if="entry.kind === 'single'">
              <button
                v-if="!entry.item.isReceived"
                @click="handleMarkAsReceived(entry.item.id)"
                class="btn-action btn-success"
                title="Marcar como recebido"
              >
                <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3">
                  <polyline points="20 6 9 17 4 12"></polyline>
                </svg>
              </button>
              <span v-else class="received-badge">Recebido</span>
              <button @click="openEditModal(entry.item)" class="btn-action btn-edit" title="Editar">
                <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                  <path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7"></path>
                  <path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z"></path>
                </svg>
              </button>
              <button
                v-if="entry.item.isRecurring && entry.item.recurrenceGroupId && !entry.item.isReceived"
                @click="handleDeleteGroup(entry.item.recurrenceGroupId)"
                class="btn-action btn-warning"
                title="Cancelar recorrências futuras"
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
                v-if="entry.items.some((r) => !r.isReceived)"
                @click="handleDeleteGroup(entry.groupId)"
                class="btn-action btn-warning"
                title="Cancelar recorrências futuras"
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
            v-for="receivable in entry.items"
            :key="receivable.id"
            class="receivable-card receivable-card-child"
            :class="{ received: receivable.isReceived }"
          >
            <div class="receivable-info">
              <div class="receivable-title-row">
                <h3>{{ receivable.description }}</h3>
                <span class="badge badge-recurring">🔄 Recorrente</span>
              </div>
              <p class="receivable-date">Previsão: {{ formatDate(receivable.expectedDate) }}</p>
              <p v-if="receivable.notes" class="receivable-notes">{{ receivable.notes }}</p>
            </div>
            <div class="receivable-actions">
              <span class="receivable-amount">{{ formatCurrency(receivable.amount) }}</span>
              <div class="action-buttons">
                <button
                  v-if="!receivable.isReceived"
                  @click="handleMarkAsReceived(receivable.id)"
                  class="btn-action btn-success"
                  title="Marcar como recebido"
                >
                  <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3">
                    <polyline points="20 6 9 17 4 12"></polyline>
                  </svg>
                </button>
                <span v-else class="received-badge">Recebido</span>
                <button @click="openEditModal(receivable)" class="btn-action btn-edit" title="Editar">
                  <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                    <path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7"></path>
                    <path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z"></path>
                  </svg>
                </button>
                <button @click="handleDelete(receivable.id)" class="btn-action btn-danger" title="Excluir">
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

    <!-- Modal: Novo Recebível -->
    <div v-if="showModal" class="modal-overlay">
      <div class="modal">
        <div class="modal-header">
          <h2>Novo Recebível</h2>
          <button type="button" class="close-btn" @click="showModal = false">✕</button>
        </div>

        <div class="mode-selector">
          <button :class="['mode-btn', { active: mode === 'simple' }]" type="button" @click="mode = 'simple'">Recebimento único</button>
          <button :class="['mode-btn', { active: mode === 'recurring' }]" type="button" @click="mode = 'recurring'">Recorrente (salário)</button>
        </div>

        <!-- Formulário simples -->
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
            <label>Data Prevista</label>
            <input v-model="simpleForm.expectedDate" type="date" required />
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

        <!-- Formulário recorrente -->
        <form v-else @submit.prevent="handleSubmit">
          <div class="form-group">
            <label>Descrição (ex: Salário, Freelance)</label>
            <input v-model="recurringForm.description" required />
          </div>
          <div class="form-group">
            <label>Valor mensal</label>
            <input v-model.number="recurringForm.amount" type="number" step="0.01" min="0" required />
          </div>
          <div class="form-group">
            <label>Dia do mês para receber (1-28)</label>
            <input v-model.number="recurringForm.recurringDay" type="number" min="1" max="28" required />
          </div>
          <div class="form-group">
            <label>Gerar para quantos meses (1-60)</label>
            <input v-model.number="recurringForm.months" type="number" min="1" max="60" required />
          </div>
          <div class="recurring-preview">
            Serão criadas <strong>{{ recurringForm.months }}</strong> entradas de
            <strong>{{ formatCurrency(recurringForm.amount) }}</strong> todo dia <strong>{{ recurringForm.recurringDay }}</strong>
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

    <!-- Modal: Editar Recebível -->
    <div v-if="showEditModal" class="modal-overlay">
      <div class="modal">
        <div class="modal-header">
          <h2>Editar Recebível</h2>
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
            <label>Data Prevista</label>
            <input v-model="editForm.expectedDate" type="date" required />
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
.receivables-page { max-width: 900px; margin: 0 auto; }

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

.receivables-list { display: flex; flex-direction: column; gap: 1rem; }

.receivable-wrapper {
  display: flex;
  flex-direction: column;
  gap: 0.65rem;
}

.receivable-card {
  background: var(--card-bg);
  border-radius: var(--border-radius-lg);
  padding: 1.25rem;
  display: flex;
  justify-content: space-between;
  align-items: center;
  box-shadow: var(--shadow-sm);
  border-left: 4px solid var(--color-success);
  transition: background-color 0.3s ease;
}
.receivable-card.received { opacity: 0.75; }

.receivable-title-row { display: flex; align-items: center; gap: 0.5rem; flex-wrap: wrap; }
.receivable-title-row h3 { margin: 0; color: var(--text-primary); }

.badge { font-size: 0.75rem; font-weight: 600; padding: 0.2rem 0.6rem; border-radius: 999px; }
.badge-recurring { background: var(--color-info-bg); color: var(--color-info); }

.receivable-date { color: var(--text-secondary); font-size: 0.9rem; margin: 0.25rem 0 0; }
.receivable-notes { color: var(--text-muted); font-size: 0.85rem; margin: 0.5rem 0 0; }

.receivable-actions { display: flex; align-items: center; gap: 1rem; flex-wrap: wrap; }
.receivable-amount { font-size: 1.25rem; font-weight: 700; color: var(--color-success); }

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

.received-badge {
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

.receivable-card-child {
  border-left-color: rgba(45, 212, 191, 0.45);
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
  .receivable-card {
    flex-direction: column;
    align-items: flex-start;
    gap: 1rem;
  }
  
  .receivable-actions {
    width: 100%;
    justify-content: space-between;
  }
  
  .modal {
    padding: 1.5rem;
    margin: 0.5rem;
  }
}
</style>
