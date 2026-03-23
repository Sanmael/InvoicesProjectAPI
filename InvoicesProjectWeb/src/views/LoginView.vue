<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'

const router = useRouter()
const authStore = useAuthStore()

const email = ref('')
const password = ref('')
const errorMessage = ref('')

async function handleLogin() {
  errorMessage.value = ''
  try {
    await authStore.login({ email: email.value, password: password.value })
    router.push('/dashboard')
  } catch {
    errorMessage.value = authStore.error || 'Erro ao fazer login'
  }
}
</script>

<template>
  <div class="login-page">
    <div class="login-card">
      <div class="login-header">
        <h1>💰 Kash</h1>
        <p>Entre na sua conta</p>
      </div>

      <form @submit.prevent="handleLogin" class="login-form">
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
          />
        </div>

        <div v-if="errorMessage" class="error-message">
          {{ errorMessage }}
        </div>

        <button type="submit" class="login-btn" :disabled="authStore.loading">
          {{ authStore.loading ? 'Entrando...' : 'Entrar' }}
        </button>
      </form>

      <div class="login-footer">
        <p>Não tem conta? <RouterLink to="/register">Cadastre-se</RouterLink></p>
      </div>
    </div>
  </div>
</template>

<style scoped>
.login-page {
  min-height: 100vh;
  display: flex;
  align-items: center;
  justify-content: center;
  background: var(--sidebar-bg);
  padding: 1rem;
}

.login-card {
  background: var(--card-bg);
  border-radius: 16px;
  padding: 2.5rem;
  width: 100%;
  max-width: 400px;
  box-shadow: var(--shadow-lg);
}

.login-header {
  text-align: center;
  margin-bottom: 2rem;
}

.login-header h1 {
  font-size: 2rem;
  margin: 0 0 0.5rem;
  color: var(--text-primary);
}

.login-header p {
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

.login-btn {
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

.login-btn:hover:not(:disabled) {
  background: var(--color-primary-hover);
  transform: translateY(-2px);
  box-shadow: 0 4px 12px rgba(79, 70, 229, 0.4);
}

.login-btn:disabled {
  opacity: 0.7;
  cursor: not-allowed;
}

.login-footer {
  text-align: center;
  margin-top: 1.5rem;
  color: var(--text-secondary);
}

.login-footer a {
  color: var(--color-primary);
  font-weight: 600;
  text-decoration: none;
}

.login-footer a:hover {
  text-decoration: underline;
}

/* Responsivo */
@media (max-width: 480px) {
  .login-card {
    padding: 1.5rem;
  }
  
  .login-header h1 {
    font-size: 1.75rem;
  }
}
</style>
