<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { adminService } from '@/services/adminService'
import type { EmailSettingsCreateDto } from '@/types'

const loading = ref(false)
const testing = ref(false)
const processing = ref(false)
const successMsg = ref('')
const errorMsg = ref('')
const hasSettings = ref(false)

const form = ref<EmailSettingsCreateDto>({
  smtpHost: 'smtp.gmail.com',
  smtpPort: 587,
  senderEmail: '',
  senderName: 'Sistema de Faturas',
  password: '',
  useSsl: true,
})

const testEmail = ref('')
const passwordOnly = ref('')

async function loadSettings() {
  loading.value = true
  errorMsg.value = ''

  try {
    const settings = await adminService.getEmailSettings()
    if (settings) {
      hasSettings.value = true
      form.value.smtpHost = settings.smtpHost
      form.value.smtpPort = settings.smtpPort
      form.value.senderEmail = settings.senderEmail
      form.value.senderName = settings.senderName
      form.value.useSsl = settings.useSsl
      testEmail.value = settings.senderEmail
    }
  } catch (e: any) {
    errorMsg.value = e.response?.data?.message || 'Erro ao carregar configuração de email'
  } finally {
    loading.value = false
  }
}

async function handleSave() {
  if (!hasSettings.value && !form.value.password.trim()) {
    errorMsg.value = 'Na configuração inicial, informe a senha de app do email remetente.'
    return
  }

  loading.value = true
  successMsg.value = ''
  errorMsg.value = ''

  try {
    await adminService.saveEmailSettings(form.value)
    hasSettings.value = true
    form.value.password = ''
    successMsg.value = 'Configuração SMTP salva com sucesso.'
  } catch (e: any) {
    errorMsg.value = e.response?.data?.message || 'Erro ao salvar configuração SMTP'
  } finally {
    loading.value = false
  }
}

async function handlePasswordUpdate() {
  if (!passwordOnly.value) {
    errorMsg.value = 'Informe uma nova senha de app.'
    return
  }

  loading.value = true
  successMsg.value = ''
  errorMsg.value = ''

  try {
    await adminService.updateEmailPassword({ password: passwordOnly.value })
    passwordOnly.value = ''
    successMsg.value = 'Senha do remetente atualizada com sucesso.'
  } catch (e: any) {
    errorMsg.value = e.response?.data?.message || 'Erro ao atualizar senha do remetente'
  } finally {
    loading.value = false
  }
}

async function handleTest() {
  testing.value = true
  successMsg.value = ''
  errorMsg.value = ''

  try {
    const result = await adminService.testEmailSettings({ testEmail: testEmail.value })
    successMsg.value = result.message
  } catch (e: any) {
    errorMsg.value = e.response?.data?.message || e.response?.data?.Message || 'Erro ao enviar email de teste'
  } finally {
    testing.value = false
  }
}

async function handleProcessAll() {
  processing.value = true
  successMsg.value = ''
  errorMsg.value = ''

  try {
    await adminService.processAllNotifications()
    successMsg.value = 'Processamento manual de notificações executado.'
  } catch (e: any) {
    errorMsg.value = e.response?.data?.message || 'Erro ao processar notificações'
  } finally {
    processing.value = false
  }
}

onMounted(() => {
  loadSettings()
})
</script>

<template>
  <div class="admin-page">
    <header class="page-header">
      <div>
        <h1>Email do Sistema</h1>
        <p>Configure a conta que enviará os avisos automáticos aos usuários.</p>
      </div>
      <button class="btn-secondary" :disabled="processing" @click="handleProcessAll">
        {{ processing ? 'Processando...' : 'Processar avisos agora' }}
      </button>
    </header>

    <div v-if="successMsg" class="alert alert-success">{{ successMsg }}</div>
    <div v-if="errorMsg" class="alert alert-error">{{ errorMsg }}</div>

    <div class="panel-grid">
      <section class="panel">
        <h2>SMTP</h2>
        <p class="helper">Para Gmail, use smtp.gmail.com, porta 587 e uma senha de app.</p>

        <form class="form-stack" @submit.prevent="handleSave">
          <label>
            <span>Servidor SMTP</span>
            <input v-model="form.smtpHost" required />
          </label>

          <label>
            <span>Porta</span>
            <input v-model.number="form.smtpPort" type="number" min="1" max="65535" required />
          </label>

          <label>
            <span>Email remetente</span>
            <input v-model="form.senderEmail" type="email" required />
          </label>

          <label>
            <span>Nome do remetente</span>
            <input v-model="form.senderName" required />
          </label>

          <label>
            <span>Senha de app {{ hasSettings ? '(opcional para manter a atual)' : '(obrigatória)' }}</span>
            <input
              v-model="form.password"
              type="password"
              :required="!hasSettings"
              :placeholder="hasSettings ? 'Preencha apenas para trocar a senha atual' : ''"
            />
          </label>

          <label class="checkbox-row">
            <input v-model="form.useSsl" type="checkbox" />
            <span>Usar TLS/SSL</span>
          </label>

          <button class="btn-primary" type="submit" :disabled="loading">
            {{ loading ? 'Salvando...' : 'Salvar configuração' }}
          </button>
        </form>
      </section>

      <section class="panel">
        <h2>Operações</h2>

        <div class="form-stack">
          <label>
            <span>Email para teste</span>
            <input v-model="testEmail" type="email" required />
          </label>
          <button class="btn-outline" :disabled="testing || !testEmail" @click="handleTest">
            {{ testing ? 'Enviando...' : 'Enviar email de teste' }}
          </button>
        </div>

        <hr />

        <div class="form-stack">
          <label>
            <span>Atualizar somente a senha</span>
            <input v-model="passwordOnly" type="password" placeholder="Nova senha de app" />
          </label>
          <button class="btn-outline" :disabled="loading || !passwordOnly" @click="handlePasswordUpdate">
            Atualizar senha
          </button>
        </div>

        <hr />

        <div class="helper-box">
          <strong>Como funciona</strong>
          <p>As configurações SMTP ficam salvas no banco e a senha é armazenada criptografada. O processamento roda automaticamente no backend em intervalo configurável e também pode ser disparado manualmente aqui.</p>
        </div>
      </section>
    </div>
  </div>
</template>

<style scoped>
.admin-page {
  max-width: 1100px;
  margin: 0 auto;
}

.page-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 1rem;
  margin-bottom: 2rem;
}

.page-header h1 {
  margin: 0;
  color: var(--text-primary);
}

.page-header p,
.helper {
  color: var(--text-secondary);
}

.panel-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(320px, 1fr));
  gap: 1.5rem;
}

.panel {
  background: var(--card-bg);
  padding: 2rem;
  border-radius: 18px;
  box-shadow: var(--shadow-sm);
}

.panel h2 {
  margin-top: 0;
}

.form-stack {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.form-stack label {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  color: var(--text-primary);
}

.form-stack input {
  width: 100%;
  padding: 0.8rem 0.9rem;
  border: 2px solid var(--input-border);
  border-radius: var(--border-radius);
  background: var(--input-bg);
  color: var(--text-primary);
  box-sizing: border-box;
}

.checkbox-row {
  flex-direction: row;
  align-items: center;
}

.checkbox-row input {
  width: auto;
}

.btn-primary,
.btn-secondary,
.btn-outline {
  padding: 0.85rem 1rem;
  border-radius: var(--border-radius);
  font-weight: 600;
  cursor: pointer;
  border: none;
}

.btn-primary {
  background: var(--color-primary);
  color: white;
}

.btn-secondary {
  background: var(--bg-secondary);
  color: var(--text-primary);
}

.btn-outline {
  border: 1px solid var(--input-border);
  background: transparent;
  color: var(--text-primary);
}

.alert {
  padding: 1rem 1.25rem;
  border-radius: var(--border-radius);
  margin-bottom: 1.5rem;
  font-weight: 500;
}

.alert-success {
  background: var(--color-success-bg);
  color: var(--color-success);
}

.alert-error {
  background: var(--color-danger-bg);
  color: var(--color-danger);
}

.helper-box {
  padding: 1rem;
  border-radius: 12px;
  background: var(--bg-secondary);
}

.helper-box p {
  margin-bottom: 0;
  color: var(--text-secondary);
}

hr {
  border: none;
  border-top: 1px solid var(--input-border);
  margin: 1.5rem 0;
}

@media (max-width: 640px) {
  .page-header {
    flex-direction: column;
  }

  .panel {
    padding: 1.5rem;
  }
}
</style>