import api from './api'
import type { LoginDto, LoginResponse, CreateUserDto, User, UpdateUserDto } from '@/types'

export const authService = {
  async login(credentials: LoginDto): Promise<LoginResponse> {
    const response = await api.post<LoginResponse>('/auth/login', credentials)
    return response.data
  },

  async register(data: CreateUserDto): Promise<User> {
    const response = await api.post<User>('/auth/register', data)
    return response.data
  },

  async getProfile(userId: string): Promise<User> {
    const response = await api.get<User>(`/auth/profile/${userId}`)
    return response.data
  },

  async updateProfile(data: UpdateUserDto): Promise<User> {
    const response = await api.put<User>('/auth/profile', data)
    return response.data
  },

  logout(): void {
    localStorage.removeItem('token')
    localStorage.removeItem('user')
  },
}
