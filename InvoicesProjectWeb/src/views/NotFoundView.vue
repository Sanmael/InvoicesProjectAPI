<script setup lang="ts">
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'

const router = useRouter()
const authStore = useAuthStore()

function goHome() {
  if (authStore.isAuthenticated) {
    router.push('/dashboard')
  } else {
    router.push('/login')
  }
}
</script>

<template>
  <div class="not-found">
    <div class="not-found-card">
      <span class="not-found-code">404</span>
      <h1>Página não encontrada</h1>
      <p>A página que você está procurando não existe ou foi movida.</p>
      <button class="btn-primary" @click="goHome">Voltar ao início</button>
    </div>
  </div>
</template>

<style scoped>
.not-found {
  min-height: 100vh;
  display: flex;
  align-items: center;
  justify-content: center;
  background: var(--bg-secondary);
  padding: 2rem;
}

.not-found-card {
  text-align: center;
  background: var(--card-bg);
  border-radius: var(--border-radius-lg, 12px);
  padding: 3rem 2.5rem;
  box-shadow: var(--shadow-md);
  max-width: 440px;
}

.not-found-code {
  display: block;
  font-size: 5rem;
  font-weight: 800;
  color: var(--color-primary);
  line-height: 1;
  margin-bottom: 0.5rem;
}

.not-found-card h1 {
  margin: 0 0 0.5rem;
  color: var(--text-primary);
  font-size: 1.5rem;
}

.not-found-card p {
  color: var(--text-secondary);
  margin: 0 0 2rem;
}

.btn-primary {
  background: var(--color-primary);
  color: #fff;
  border: none;
  border-radius: var(--border-radius, 8px);
  padding: 0.75rem 2rem;
  font-size: 1rem;
  cursor: pointer;
  transition: background 0.2s;
}

.btn-primary:hover {
  background: var(--color-primary-hover);
}
</style>
