<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useCreditCardStore } from '@/stores/creditCard'
import type { CreateCreditCardDto, CreateCardPurchaseDto, UpdateCardPurchaseDto, CardPurchase } from '@/types'
import { cardPurchaseService } from '@/services/cardPurchaseService'

const cardStore = useCreditCardStore()

const showCardModal = ref(false)
const showPurchaseModal = ref(false)
const showEditPurchaseModal = ref(false)
const selectedCardId = ref('')
const editingPurchase = ref<CardPurchase | null>(null)

const cardFormData = ref<CreateCreditCardDto>({
  name: '',
  lastFourDigits: '',
  creditLimit: null,
  closingDay: 1,
  dueDay: 10,
})

const purchaseFormData = ref<CreateCardPurchaseDto>({
  creditCardId: '',
  description: '',
  amount: 0,
  purchaseDate: '',
  installments: 1,
  notes: '',
})

const editPurchaseForm = ref<UpdateCardPurchaseDto>({
  description: '',
  amount: 0,
  purchaseDate: '',
  installments: 1,
  notes: '',
})

onMounted(() => {
  cardStore.fetchAll()
})

const formatCurrency = (value: number) => {
  return new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(value)
}

const toInputDate = (dateStr: string) => dateStr ? dateStr.split('T')[0] : ''

function openCardModal() {
  cardFormData.value = { name: '', lastFourDigits: '', creditLimit: null, closingDay: 1, dueDay: 10 }
  showCardModal.value = true
}

function openPurchaseModal(cardId: string) {
  selectedCardId.value = cardId
  purchaseFormData.value = {
    creditCardId: cardId,
    description: '',
    amount: 0,
    purchaseDate: new Date().toISOString().split('T')[0] ?? '',
    installments: 1,
    notes: '',
  }
  showPurchaseModal.value = true
}

function openEditPurchaseModal(purchase: CardPurchase) {
  editingPurchase.value = purchase
  editPurchaseForm.value = {
    description: purchase.description,
    amount: purchase.amount,
    purchaseDate: toInputDate(purchase.purchaseDate),
    installments: purchase.installments,
    notes: purchase.notes ?? '',
  }
  showEditPurchaseModal.value = true
}

async function handleCardSubmit() {
  await cardStore.create(cardFormData.value)
  showCardModal.value = false
}

async function handlePurchaseSubmit() {
  await cardPurchaseService.create(purchaseFormData.value)
  showPurchaseModal.value = false
  if (cardStore.currentCard?.id === selectedCardId.value) {
    await cardStore.fetchWithPurchases(selectedCardId.value)
  }
}

async function handleEditPurchaseSubmit() {
  if (!editingPurchase.value) return
  await cardPurchaseService.update(editingPurchase.value.id, editPurchaseForm.value)
  showEditPurchaseModal.value = false
  editingPurchase.value = null
  if (cardStore.currentCard) {
    await cardStore.fetchWithPurchases(cardStore.currentCard.id)
  }
}

async function handleDeletePurchase(purchaseId: string) {
  if (confirm('Tem certeza que deseja excluir esta compra?')) {
    await cardPurchaseService.delete(purchaseId)
    if (cardStore.currentCard) {
      await cardStore.fetchWithPurchases(cardStore.currentCard.id)
    }
  }
}

async function viewCardDetails(cardId: string) {
  await cardStore.fetchWithPurchases(cardId)
}

async function handleDeleteCard(id: string) {
  if (confirm('Tem certeza que deseja excluir este cartão?')) {
    await cardStore.remove(id)
  }
}
</script>

<template>
  <div class="credit-cards-page">
    <header class="page-header">
      <div>
        <h1>💳 Cartões de Crédito</h1>
        <p>Gerencie seus cartões e compras</p>
      </div>
      <button @click="openCardModal" class="btn-primary">+ Novo Cartão</button>
    </header>

    <div v-if="cardStore.loading" class="loading">Carregando...</div>

    <div v-else-if="cardStore.error" class="error-alert">
      {{ cardStore.error }}
    </div>

    <div v-else-if="cardStore.cards.length === 0" class="empty-state">
      <p>Nenhum cartão cadastrado</p>
      <button @click="openCardModal" class="btn-primary">Adicionar primeiro cartão</button>
    </div>

    <div v-else class="cards-grid">
      <div v-for="card in cardStore.cards" :key="card.id" class="credit-card" :class="{ inactive: !card.isActive }">
        <div class="card-header">
          <h3>{{ card.name }}</h3>
          <span class="card-digits">•••• {{ card.lastFourDigits }}</span>
        </div>
        <div v-if="card.creditLimit != null" class="card-limit-bar">
          <div class="limit-bar-header">
            <span>Disponível: <strong>{{ formatCurrency(card.availableLimit!) }}</strong></span>
            <span>{{ formatCurrency(card.creditLimit) }}</span>
          </div>
          <div class="limit-bar-track">
            <div
              class="limit-bar-fill"
              :class="{ warning: card.availableLimit! / card.creditLimit < 0.3, danger: card.availableLimit! / card.creditLimit < 0.1 }"
              :style="{ width: Math.max(0, Math.min(100, (card.availableLimit! / card.creditLimit) * 100)) + '%' }"
            />
          </div>
          <span class="limit-used-text">Usado: {{ formatCurrency(card.totalPending) }}</span>
        </div>
        <div v-else class="card-no-limit">
          <span class="no-limit-badge">Sem limite definido</span>
          <span class="no-limit-spent">Gastos: <strong>{{ formatCurrency(card.totalPending) }}</strong></span>
        </div>
        <div class="card-info">
          <div class="info-item">
            <span class="label">Fechamento</span>
            <span class="value">Dia {{ card.closingDay }}</span>
          </div>
          <div class="info-item">
            <span class="label">Vencimento</span>
            <span class="value">Dia {{ card.dueDay }}</span>
          </div>
        </div>
        <div class="card-actions">
          <button @click="viewCardDetails(card.id)" class="btn-outline">Ver Compras</button>
          <button @click="openPurchaseModal(card.id)" class="btn-secondary">+ Compra</button>
          <button @click="handleDeleteCard(card.id)" class="btn-danger-small" title="Excluir cartão">
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <polyline points="3 6 5 6 21 6"></polyline>
              <path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"></path>
            </svg>
          </button>
        </div>
      </div>
    </div>

    <!-- Painel de detalhes do cartão -->
    <div v-if="cardStore.currentCard" class="details-panel">
      <div class="details-header">
        <h2>{{ cardStore.currentCard.name }} — Compras</h2>
        <button @click="cardStore.currentCard = null" class="close-btn">✕</button>
      </div>
      <div class="details-summary">
        <div class="summary-item">
          <span class="summary-label">Total pendente</span>
          <span class="summary-value danger">{{ formatCurrency(cardStore.currentCard.totalPending) }}</span>
        </div>
        <div v-if="cardStore.currentCard.creditLimit != null" class="summary-item">
          <span class="summary-label">Limite disponível</span>
          <span class="summary-value success">{{ formatCurrency(cardStore.currentCard.creditLimit - cardStore.currentCard.totalPending) }}</span>
        </div>
        <div v-if="cardStore.currentCard.creditLimit != null" class="summary-item">
          <span class="summary-label">Limite total</span>
          <span class="summary-value">{{ formatCurrency(cardStore.currentCard.creditLimit) }}</span>
        </div>
      </div>
      <ul class="purchases-list">
        <li v-for="purchase in cardStore.currentCard.purchases" :key="purchase.id" class="purchase-item">
          <div class="purchase-info">
            <span class="purchase-description">{{ purchase.description }}</span>
            <span class="purchase-date">
              {{ new Date(purchase.purchaseDate).toLocaleDateString('pt-BR') }}
              <span v-if="purchase.installments > 1">
                ({{ purchase.currentInstallment }}/{{ purchase.installments }})
              </span>
            </span>
          </div>
          <div class="purchase-right">
            <span class="purchase-amount" :class="{ paid: purchase.isPaid }">
              {{ formatCurrency(purchase.amount) }}
            </span>
            <div class="purchase-actions">
              <button @click="openEditPurchaseModal(purchase)" class="btn-action-sm btn-edit-sm" title="Editar">
                <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                  <path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7"></path>
                  <path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z"></path>
                </svg>
              </button>
              <button @click="handleDeletePurchase(purchase.id)" class="btn-action-sm btn-danger-sm" title="Excluir">
                <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                  <polyline points="3 6 5 6 21 6"></polyline>
                  <path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"></path>
                </svg>
              </button>
            </div>
          </div>
        </li>
      </ul>
    </div>

    <!-- Modal: Novo Cartão -->
    <div v-if="showCardModal" class="modal-overlay">
      <div class="modal">
        <div class="modal-header">
          <h2>Novo Cartão</h2>
          <button type="button" class="close-btn" @click="showCardModal = false">✕</button>
        </div>
        <form @submit.prevent="handleCardSubmit">
          <div class="form-group">
            <label>Nome do Cartão</label>
            <input v-model="cardFormData.name" placeholder="Ex: Nubank" required />
          </div>
          <div class="form-group">
            <label>Últimos 4 dígitos</label>
            <input v-model="cardFormData.lastFourDigits" maxlength="4" placeholder="1234" required />
          </div>
          <div class="form-group">
            <label>Limite <span class="optional-hint">(deixe vazio para sem limite)</span></label>
            <input v-model.number="cardFormData.creditLimit" type="number" step="0.01" min="0" placeholder="Sem limite" />
          </div>
          <div class="form-row">
            <div class="form-group">
              <label>Dia Fechamento</label>
              <input v-model.number="cardFormData.closingDay" type="number" min="1" max="31" required />
            </div>
            <div class="form-group">
              <label>Dia Vencimento</label>
              <input v-model.number="cardFormData.dueDay" type="number" min="1" max="31" required />
            </div>
          </div>
          <div class="modal-actions">
            <button type="button" @click="showCardModal = false" class="btn-secondary">Cancelar</button>
            <button type="submit" class="btn-primary">Salvar</button>
          </div>
        </form>
      </div>
    </div>

    <!-- Modal: Nova Compra -->
    <div v-if="showPurchaseModal" class="modal-overlay">
      <div class="modal">
        <div class="modal-header">
          <h2>Nova Compra</h2>
          <button type="button" class="close-btn" @click="showPurchaseModal = false">✕</button>
        </div>
        <form @submit.prevent="handlePurchaseSubmit">
          <div class="form-group">
            <label>Descrição</label>
            <input v-model="purchaseFormData.description" required />
          </div>
          <div class="form-group">
            <label>Valor Total</label>
            <input v-model.number="purchaseFormData.amount" type="number" step="0.01" min="0" required />
          </div>
          <div class="form-row">
            <div class="form-group">
              <label>Data</label>
              <input v-model="purchaseFormData.purchaseDate" type="date" required />
            </div>
            <div class="form-group">
              <label>Parcelas</label>
              <input v-model.number="purchaseFormData.installments" type="number" min="1" max="48" required />
            </div>
          </div>
          <div class="form-group">
            <label>Observações</label>
            <textarea v-model="purchaseFormData.notes" rows="2"></textarea>
          </div>
          <div class="modal-actions">
            <button type="button" @click="showPurchaseModal = false" class="btn-secondary">Cancelar</button>
            <button type="submit" class="btn-primary">Salvar</button>
          </div>
        </form>
      </div>
    </div>

    <!-- Modal: Editar Compra -->
    <div v-if="showEditPurchaseModal" class="modal-overlay">
      <div class="modal">
        <div class="modal-header">
          <h2>Editar Compra</h2>
          <button type="button" class="close-btn" @click="showEditPurchaseModal = false">✕</button>
        </div>
        <form @submit.prevent="handleEditPurchaseSubmit">
          <div class="form-group">
            <label>Descrição</label>
            <input v-model="editPurchaseForm.description" required />
          </div>
          <div class="form-group">
            <label>Valor</label>
            <input v-model.number="editPurchaseForm.amount" type="number" step="0.01" min="0" required />
          </div>
          <div class="form-row">
            <div class="form-group">
              <label>Data</label>
              <input v-model="editPurchaseForm.purchaseDate" type="date" required />
            </div>
            <div class="form-group">
              <label>Parcelas</label>
              <input v-model.number="editPurchaseForm.installments" type="number" min="1" max="48" required />
            </div>
          </div>
          <div class="form-group">
            <label>Observações</label>
            <textarea v-model="editPurchaseForm.notes" rows="2"></textarea>
          </div>
          <div class="modal-actions">
            <button type="button" @click="showEditPurchaseModal = false" class="btn-secondary">Cancelar</button>
            <button type="submit" class="btn-primary">Salvar</button>
          </div>
        </form>
      </div>
    </div>
  </div>
</template>

<style scoped>
.credit-cards-page { max-width: 1100px; margin: 0 auto; }

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

.cards-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(300px, 1fr));
  gap: 1.5rem;
}

.credit-card {
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  border-radius: 16px;
  padding: 1.5rem;
  color: white;
  box-shadow: 0 8px 24px rgba(102, 126, 234, 0.4);
  transition: transform 0.2s ease;
}
.credit-card:hover {
  transform: translateY(-4px);
}
.credit-card.inactive { opacity: 0.6; }

.card-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 1.5rem; }
.card-header h3 { margin: 0; font-size: 1.25rem; }
.card-digits { font-family: monospace; font-size: 1.1rem; }

.card-limit-bar { margin-bottom: 1.25rem; }
.limit-bar-header { display: flex; justify-content: space-between; font-size: 0.85rem; margin-bottom: 0.4rem; opacity: 0.95; }
.limit-bar-track { 
  height: 8px; 
  background: rgba(255,255,255,0.25); 
  border-radius: 4px; 
  overflow: hidden; 
}
.limit-bar-fill { 
  height: 100%; 
  background: #4ade80; 
  border-radius: 4px; 
  transition: width 0.4s ease; 
}
.limit-bar-fill.warning { background: #fbbf24; }
.limit-bar-fill.danger { background: #f87171; }
.limit-used-text { font-size: 0.75rem; opacity: 0.7; margin-top: 0.3rem; display: block; }

.card-no-limit { 
  margin-bottom: 1.25rem; 
  display: flex; 
  flex-direction: column; 
  gap: 0.4rem; 
}
.no-limit-badge { 
  font-size: 0.8rem; 
  opacity: 0.8; 
  background: rgba(255,255,255,0.15); 
  padding: 0.25rem 0.6rem; 
  border-radius: 12px; 
  width: fit-content; 
}
.no-limit-spent { font-size: 0.95rem; }

.optional-hint { font-size: 0.75rem; font-weight: 400; color: var(--text-muted); }

.card-info { display: grid; grid-template-columns: repeat(2, 1fr); gap: 1rem; margin-bottom: 1.5rem; }
.info-item .label { display: block; font-size: 0.75rem; opacity: 0.8; text-transform: uppercase; }
.info-item .value { font-weight: 600; }

.card-actions { display: flex; gap: 0.5rem; }
.btn-outline { 
  flex: 1; 
  padding: 0.5rem; 
  background: transparent; 
  border: 2px solid white; 
  color: white; 
  border-radius: var(--border-radius); 
  cursor: pointer; 
  font-size: 0.85rem;
  font-weight: 500;
  transition: all 0.2s ease;
}
.btn-outline:hover {
  background: rgba(255,255,255,0.2);
}
.btn-secondary { 
  flex: 1; 
  padding: 0.5rem; 
  background: rgba(255,255,255,0.25); 
  border: none; 
  color: white; 
  border-radius: var(--border-radius); 
  cursor: pointer; 
  font-size: 0.85rem;
  font-weight: 500;
  transition: all 0.2s ease;
}
.btn-secondary:hover {
  background: rgba(255,255,255,0.35);
}
.btn-danger-small { 
  padding: 0.5rem 0.75rem; 
  background: rgba(255,255,255,0.25); 
  border: none; 
  color: white; 
  border-radius: var(--border-radius); 
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: all 0.2s ease;
}
.btn-danger-small:hover {
  background: rgba(220, 38, 38, 0.8);
}

.details-panel {
  margin-top: 2rem;
  background: var(--card-bg);
  border-radius: 16px;
  padding: 1.5rem;
  box-shadow: var(--shadow-sm);
  transition: background-color 0.3s ease;
}

.details-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 1rem; }
.details-header h2 { margin: 0; color: var(--text-primary); }

.details-summary { 
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 1rem;
  margin-bottom: 1rem;
}
.summary-item {
  background: var(--bg-secondary);
  padding: 1rem;
  border-radius: var(--border-radius);
  text-align: center;
}
.summary-label { display: block; font-size: 0.8rem; color: var(--text-muted); margin-bottom: 0.25rem; text-transform: uppercase; }
.summary-value { font-size: 1.1rem; font-weight: 700; color: var(--text-primary); }
.summary-value.danger { color: var(--color-danger); }
.summary-value.success { color: var(--color-success); }

.purchases-list { list-style: none; padding: 0; margin: 0; }

.purchase-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 0.75rem 0;
  border-bottom: 1px solid var(--border-color);
  flex-wrap: wrap;
  gap: 0.5rem;
}

.purchase-info { display: flex; flex-direction: column; }
.purchase-description { font-weight: 500; color: var(--text-primary); }
.purchase-date { font-size: 0.85rem; color: var(--text-muted); }

.purchase-right { display: flex; align-items: center; gap: 0.75rem; }
.purchase-amount { font-weight: 600; color: var(--color-danger); }
.purchase-amount.paid { color: var(--color-success); text-decoration: line-through; }

.purchase-actions { display: flex; gap: 0.4rem; }

.btn-action-sm {
  width: 34px;
  height: 34px;
  border-radius: var(--border-radius);
  border: none;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: all 0.2s ease;
}
.btn-action-sm:hover {
  transform: scale(1.1);
}
.btn-edit-sm { 
  background: var(--color-info); 
  color: white; 
}
.btn-danger-sm { 
  background: var(--color-danger); 
  color: white; 
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
  margin-bottom: 1.5rem;
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

.form-row { display: flex; gap: 1rem; }
.form-row .form-group { flex: 1; }

.modal-actions { display: flex; gap: 1rem; margin-top: 1.5rem; }
.modal-actions .btn-secondary,
.modal-actions .btn-primary { flex: 1; padding: 0.75rem; }
.modal-actions .btn-secondary {
  background: var(--bg-secondary);
  color: var(--text-primary);
  border: 1px solid var(--border-color);
  border-radius: var(--border-radius);
  font-weight: 500;
  cursor: pointer;
  transition: background 0.2s ease;
}
.modal-actions .btn-secondary:hover {
  background: var(--border-color);
}

/* Responsivo */
@media (max-width: 640px) {
  .cards-grid {
    grid-template-columns: 1fr;
  }
  
  .card-info {
    grid-template-columns: 1fr 1fr;
  }
  
  .details-summary {
    grid-template-columns: 1fr;
  }
  
  .card-actions {
    flex-wrap: wrap;
  }
  
  .purchase-item {
    flex-direction: column;
    align-items: flex-start;
  }
  
  .purchase-right {
    width: 100%;
    justify-content: space-between;
  }
  
  .modal {
    padding: 1.5rem;
    margin: 0.5rem;
  }
  
  .form-row {
    flex-direction: column;
    gap: 0;
  }
}
</style>
