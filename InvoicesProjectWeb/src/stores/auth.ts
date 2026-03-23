import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import type { User, LoginDto, CreateUserDto, LoginResponse, UpdateUserDto } from '@/types'
import { authService } from '@/services/authService'

export const useAuthStore = defineStore('auth', () => {
  const user = ref<User | null>(null)
  const token = ref<string | null>(localStorage.getItem('token'))
  const loading = ref(false)
  const error = ref<string | null>(null)
  const initialized = ref(false)

  const isAuthenticated = computed(() => !!token.value)

  // Carregar usuário do localStorage ao iniciar
  const storedUser = localStorage.getItem('user')
  if (storedUser) {
    user.value = JSON.parse(storedUser)
  }

  async function initializeAuth(): Promise<void> {
    if (initialized.value) {
      return
    }

    initialized.value = true

    if (!token.value || !user.value?.id) {
      return
    }

    try {
      const profile = await authService.getProfile(user.value.id)
      user.value = profile
      localStorage.setItem('user', JSON.stringify(profile))
    } catch (e: any) {
      if (e.response?.status === 401 || e.response?.status === 404) {
        logout()
      }
    }
  }

  async function login(credentials: LoginDto): Promise<void> {
    loading.value = true
    error.value = null
    try {
      const response: LoginResponse = await authService.login(credentials)
      token.value = response.token
      user.value = {
        id: response.userId,
        name: response.name,
        email: response.email,
        isActive: true,
        isAdmin: response.isAdmin,
        createdAt: new Date().toISOString(),
      }
      localStorage.setItem('token', response.token)
      localStorage.setItem('user', JSON.stringify(user.value))
      initialized.value = true
    } catch (e: any) {
      error.value = e.response?.data?.message || 'Erro ao fazer login'
      throw e
    } finally {
      loading.value = false
    }
  }

  async function register(data: CreateUserDto): Promise<void> {
    loading.value = true
    error.value = null
    try {
      await authService.register(data)
    } catch (e: any) {
      error.value = e.response?.data?.message || 'Erro ao registrar'
      throw e
    } finally {
      loading.value = false
    }
  }

  function logout(): void {
    authService.logout()
    user.value = null
    token.value = null
    initialized.value = false
  }

  async function updateProfile(data: UpdateUserDto): Promise<void> {
    loading.value = true
    error.value = null
    try {
      const updated = await authService.updateProfile(data)
      user.value = { ...user.value!, name: updated.name, email: updated.email, isAdmin: updated.isAdmin }
      localStorage.setItem('user', JSON.stringify(user.value))
    } catch (e: any) {
      error.value = e.response?.data?.message || 'Erro ao atualizar perfil'
      throw e
    } finally {
      loading.value = false
    }
  }

  return {
    user,
    token,
    loading,
    error,
    isAuthenticated,
    initialized,
    initializeAuth,
    login,
    register,
    logout,
    updateProfile,
  }
})
