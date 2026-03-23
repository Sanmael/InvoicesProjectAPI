<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'

const router = useRouter()
const authStore = useAuthStore()

const name = ref('')
const email = ref('')
const password = ref('')
const confirmPassword = ref('')
const errorMessage = ref('')
const successMessage = ref('')

async function handleRegister() {
  errorMessage.value = ''
  successMessage.value = ''

  if (password.value !== confirmPassword.value) {
    errorMessage.value = 'As senhas não coincidem'
    return
  }

  try {
    await authStore.register({
      name: name.value,
      email: email.value,
      password: password.value,
    })
    successMessage.value = 'Conta criada com sucesso! Redirecionando...'
    setTimeout(() => router.push('/login'), 2000)
  } catch {
    errorMessage.value = authStore.error || 'Erro ao registrar'
  }
}
</script>

<template>
  <div class="register-page">
    <div class="register-card">
      <div class="register-header">
        <h1>💰 Kash</h1>
        <p>Crie sua conta</p>
      </div>

      <form @submit.prevent="handleRegister" class="register-form">
        <div class="form-group">
          <label for="name">Nome</label>
          <input
            id="name"
            v-model="name"
            type="text"
            placeholder="Seu nome"
            required
          />
        </div>

        <div class="form-group">
          <label for="email">Email</label>
          <input
            id="email"
            v-model="email"
            type="email"
            placeholder="seu@email.com"
            required
          />
        </div>

        <div class="form-group">
          <label for="password">Senha</label>
          <input
            id="password"
            v-model="password"
            type="password"
            placeholder="••••••••"
            required
            minlength="6"
          />
        </div>

        <div class="form-group">
          <label for="confirmPassword">Confirmar Senha</label>
          <input
            id="confirmPassword"
            v-model="confirmPassword"
            type="password"
            placeholder="••••••••"
            required
          />
        </div>

        <div v-if="errorMessage" class="error-message">
          {{ errorMessage }}
        </div>

        <div v-if="successMessage" class="success-message">
          {{ successMessage }}
        </div>

        <button type="submit" class="register-btn" :disabled="authStore.loading">
          {{ authStore.loading ? 'Criando...' : 'Criar Conta' }}
        </button>
      </form>

      <div class="register-footer">
        <p>Já tem conta? <RouterLink to="/login">Entre</RouterLink></p>
      </div>
    </div>
  </div>
</template>

<style scoped>
.register-page {
  min-height: 100vh;
  display: flex;
  align-items: center;
  justify-content: center;
  background: var(--sidebar-bg);
  padding: 1rem;
}

.register-card {
  background: var(--card-bg);
  border-radius: 16px;
  padding: 2.5rem;
  width: 100%;
  max-width: 400px;
  box-shadow: var(--shadow-lg);
}

.register-header {
  text-align: center;
  margin-bottom: 2rem;
}

.register-header h1 {
  font-size: 2rem;
  margin: 0 0 0.5rem;
  color: var(--text-primary);
}

.register-header p {
  color: var(--text-secondary);
  margin: 0;
}

.form-group {
  margin-bottom: 1.25rem;
}

.form-group label {
  display: block;
  margin-bottom: 0.5rem;
  font-weight: 500;
  color: var(--text-primary);
}

.form-group input {
  width: 100%;
  padding: 0.75rem 1rem;
  border: 2px solid var(--input-border);
  border-radius: var(--border-radius);
  font-size: 1rem;
  transition: border-color 0.2s ease;
  box-sizing: border-box;
  background: var(--input-bg);
  color: var(--text-primary);
}

.form-group input:focus {
  outline: none;
  border-color: var(--color-primary);
}

.error-message {
  background: var(--color-danger-bg);
  color: var(--color-danger);
  padding: 0.75rem;
  border-radius: var(--border-radius);
  margin-bottom: 1rem;
  font-size: 0.9rem;
}

.success-message {
  background: var(--color-success-bg);
  color: var(--color-success);
  padding: 0.75rem;
  border-radius: var(--border-radius);
  margin-bottom: 1rem;
  font-size: 0.9rem;
}

.register-btn {
  width: 100%;
  padding: 0.875rem;
  background: var(--color-primary);
  color: white;
  border: none;
  border-radius: var(--border-radius);
  font-size: 1rem;
  font-weight: 600;
  cursor: pointer;
  transition: all 0.2s ease;
}

.register-btn:hover:not(:disabled) {
  background: var(--color-primary-hover);
  transform: translateY(-2px);
  box-shadow: 0 4px 12px rgba(79, 70, 229, 0.4);
}

.register-btn:disabled {
  opacity: 0.7;
  cursor: not-allowed;
}

.register-footer {
  text-align: center;
  margin-top: 1.5rem;
  color: var(--text-secondary);
}

.register-footer a {
  color: var(--color-primary);
  font-weight: 600;
  text-decoration: none;
}

.register-footer a:hover {
  text-decoration: underline;
}

/* Responsivo */
@media (max-width: 480px) {
  .register-card {
    padding: 1.5rem;
  }
  
  .register-header h1 {
    font-size: 1.75rem;
  }
}
</style>
