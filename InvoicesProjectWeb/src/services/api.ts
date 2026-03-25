import axios from 'axios'
import { useToast } from '@/composables/useToast'

const api = axios.create({
  baseURL: import.meta.env.VITE_API_URL || 'https://localhost:7144/api',
  headers: {
    'Content-Type': 'application/json',
  },
})

// Interceptor para adicionar token JWT
api.interceptors.request.use((config) => {
  const token = localStorage.getItem('token')
  if (token) {
    config.headers.Authorization = `Bearer ${token}`
  }
  return config
})

// Interceptor para tratar erros
api.interceptors.response.use(
  (response) => response,
  (error) => {
    const { error: showError } = useToast()
    const status = error.response?.status

    if (!error.response) {
      showError('Servidor indisponível. Verifique sua conexão.')
      return Promise.reject(error)
    }

    switch (status) {
      case 401:
        localStorage.removeItem('token')
        localStorage.removeItem('user')
        window.location.href = '/login'
        break
      case 403:
        showError('Acesso negado. Você não tem permissão para esta ação.')
        break
      case 404:
        showError('Recurso não encontrado.')
        break
      case 500:
        showError('Erro interno do servidor. Tente novamente mais tarde.')
        break
      default:
        if (status && status >= 400) {
          const msg = error.response?.data?.message || 'Ocorreu um erro inesperado.'
          showError(msg)
        }
        break
    }

    return Promise.reject(error)
  }
)

export default api
