import { createRouter, createWebHistory } from 'vue-router'
import { useAuthStore } from '@/stores/auth'

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    {
      path: '/login',
      name: 'login',
      component: () => import('@/views/LoginView.vue'),
      meta: { requiresAuth: false },
    },
    {
      path: '/register',
      name: 'register',
      component: () => import('@/views/RegisterView.vue'),
      meta: { requiresAuth: false },
    },
    {
      path: '/',
      component: () => import('@/layouts/MainLayout.vue'),
      meta: { requiresAuth: true },
      children: [
        {
          path: '',
          redirect: '/dashboard',
        },
        {
          path: 'dashboard',
          name: 'dashboard',
          component: () => import('@/views/DashboardView.vue'),
        },
        {
          path: 'debts',
          name: 'debts',
          component: () => import('@/views/DebtsView.vue'),
        },
        {
          path: 'receivables',
          name: 'receivables',
          component: () => import('@/views/ReceivablesView.vue'),
        },
        {
          path: 'credit-cards',
          name: 'credit-cards',
          component: () => import('@/views/CreditCardsView.vue'),
        },
        {
          path: 'profile',
          name: 'profile',
          component: () => import('@/views/ProfileView.vue'),
        },
        {
          path: 'admin/email-settings',
          name: 'admin-email-settings',
          component: () => import('@/views/AdminEmailSettingsView.vue'),
          meta: { requiresAdmin: true },
        },
      ],
    },
    {
      path: '/:pathMatch(.*)*',
      name: 'not-found',
      component: () => import('@/views/NotFoundView.vue'),
    },
  ],
})

// Navigation guard para autenticação
router.beforeEach(async (to, _from, next) => {
  const authStore = useAuthStore()

  await authStore.initializeAuth()

  if (to.meta.requiresAuth && !authStore.isAuthenticated) {
    next('/login')
  } else if (to.meta.requiresAdmin && !authStore.user?.isAdmin) {
    next('/dashboard')
  } else if (!to.meta.requiresAuth && authStore.isAuthenticated && (to.name === 'login' || to.name === 'register')) {
    next('/dashboard')
  } else {
    next()
  }
})

export default router
