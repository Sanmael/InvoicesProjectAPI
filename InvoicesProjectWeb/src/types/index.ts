// User types
export interface User {
  id: string
  name: string
  email: string
  isActive: boolean
  isAdmin: boolean
  createdAt: string
}

export interface CreateUserDto {
  name: string
  email: string
  password: string
}

export interface LoginDto {
  email: string
  password: string
}

export interface LoginResponse {
  userId: string
  name: string
  email: string
  isAdmin: boolean
  token: string
}

export interface UpdateUserDto {
  name?: string
  email?: string
  currentPassword?: string
  newPassword?: string
}

// Debt types
export interface Debt {
  id: string
  description: string
  amount: number
  dueDate: string
  isPaid: boolean
  paidAt: string | null
  notes: string | null
  isInstallment: boolean
  totalInstallments: number | null
  installmentNumber: number | null
  installmentGroupId: string | null
  createdAt: string
}

export interface CreateDebtDto {
  description: string
  amount: number
  dueDate: string
  notes?: string
}

export interface CreateInstallmentDebtDto {
  description: string
  totalAmount: number
  firstDueDate: string
  installments: number
  notes?: string
}

export interface CreateRecurringDebtDto {
  description: string
  amount: number
  recurringDay: number
  months: number
  startDate?: string
  notes?: string
}

export interface UpdateDebtDto {
  description?: string
  amount?: number
  dueDate?: string
  isPaid?: boolean
  notes?: string
}

// Receivable types
export interface Receivable {
  id: string
  description: string
  amount: number
  expectedDate: string
  isReceived: boolean
  receivedAt: string | null
  notes: string | null
  isRecurring: boolean
  recurringDay: number | null
  recurrenceGroupId: string | null
  createdAt: string
}

export interface CreateReceivableDto {
  description: string
  amount: number
  expectedDate: string
  notes?: string
}

export interface CreateRecurringReceivableDto {
  description: string
  amount: number
  recurringDay: number
  notes?: string
  months?: number
}

export interface UpdateReceivableDto {
  description?: string
  amount?: number
  expectedDate?: string
  isReceived?: boolean
  notes?: string
}

// Credit Card types
export interface CreditCard {
  id: string
  name: string
  lastFourDigits: string
  creditLimit: number | null
  closingDay: number
  dueDay: number
  isActive: boolean
  totalPending: number
  availableLimit: number | null
  createdAt: string
}

export interface CreditCardWithPurchases extends CreditCard {
  totalPending: number
  purchases: CardPurchase[]
}

export interface CreateCreditCardDto {
  name: string
  lastFourDigits: string
  creditLimit: number | null
  closingDay: number
  dueDay: number
}

export interface UpdateCreditCardDto {
  name?: string
  creditLimit?: number
  closingDay?: number
  dueDay?: number
  isActive?: boolean
}

// Card Purchase types
export interface CardPurchase {
  id: string
  creditCardId: string
  description: string
  amount: number
  purchaseDate: string
  installments: number
  currentInstallment: number
  isPaid: boolean
  notes: string | null
  createdAt: string
}

export interface CreateCardPurchaseDto {
  creditCardId: string
  description: string
  amount: number
  purchaseDate: string
  installments: number
  notes?: string
}

export interface UpdateCardPurchaseDto {
  description?: string
  amount?: number
  purchaseDate?: string
  installments?: number
  isPaid?: boolean
  notes?: string
}

// Financial Summary
export interface FinancialSummary {
  year: number
  month: number
  totalDebts: number
  totalReceivables: number
  totalCardPurchases: number
  totalToPay: number
  balance: number
}

export interface NotificationPreference {
  id: string
  userId: string
  notifyLowBalance: boolean
  lowBalanceThreshold: number
  notifyInvoiceClosed: boolean
  daysBeforeInvoiceCloseNotification: number
  notifyCardLimitNearMax: boolean
  cardLimitWarningPercentage: number
  notifyUpcomingDebts: boolean
  daysBeforeDebtDueNotification: number
  notifyUpcomingReceivables: boolean
  daysBeforeReceivableNotification: number
  emailNotificationsEnabled: boolean
}

export interface NotificationPreferenceCreateDto {
  notifyLowBalance: boolean
  lowBalanceThreshold: number
  notifyInvoiceClosed: boolean
  daysBeforeInvoiceCloseNotification: number
  notifyCardLimitNearMax: boolean
  cardLimitWarningPercentage: number
  notifyUpcomingDebts: boolean
  daysBeforeDebtDueNotification: number
  notifyUpcomingReceivables: boolean
  daysBeforeReceivableNotification: number
  emailNotificationsEnabled: boolean
}

export interface NotificationSummary {
  totalSent: number
  totalFailed: number
  totalPending: number
  lastSentAt: string | null
}

export interface EmailNotification {
  id: string
  userId: string
  type: number
  typeName: string
  subject: string
  recipientEmail: string
  sentAt: string
  wasSent: boolean
  errorMessage: string | null
}

export interface EmailSettings {
  id: string
  smtpHost: string
  smtpPort: number
  senderEmail: string
  senderName: string
  useSsl: boolean
  isConfigured: boolean
}

export interface EmailSettingsCreateDto {
  smtpHost: string
  smtpPort: number
  senderEmail: string
  senderName: string
  password: string
  useSsl: boolean
}

export interface EmailPasswordUpdateDto {
  password: string
}

export interface EmailTestDto {
  testEmail: string
  subject?: string
  body?: string
}

export interface EmailTestResult {
  success: boolean
  message: string
}
