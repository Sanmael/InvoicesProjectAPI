<script setup lang="ts">
import { ref } from 'vue'
import { RouterLink, RouterView, useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import { useThemeStore } from '@/stores/theme'

const authStore = useAuthStore()
const themeStore = useThemeStore()
const router = useRouter()
const sidebarOpen = ref(true)
const mobileMenuOpen = ref(false)

function logout() {
  authStore.logout()
  router.push('/login')
}

function closeMobileMenu() {
  mobileMenuOpen.value = false
}
</script>

<template>
  <div class="app-layout">
    <!-- Mobile Header -->
    <header class="mobile-header">
      <button @click="mobileMenuOpen = !mobileMenuOpen" class="menu-btn">
        <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
          <line x1="3" y1="6" x2="21" y2="6"></line>
          <line x1="3" y1="12" x2="21" y2="12"></line>
          <line x1="3" y1="18" x2="21" y2="18"></line>
        </svg>
      </button>
      <h1>💰 Kash</h1>
      <button @click="themeStore.toggleTheme()" class="theme-toggle-mobile">
        <svg v-if="themeStore.theme === 'light'" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
          <path d="M21 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 21 12.79z"></path>
        </svg>
        <svg v-else width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
          <circle cx="12" cy="12" r="5"></circle>
          <line x1="12" y1="1" x2="12" y2="3"></line>
          <line x1="12" y1="21" x2="12" y2="23"></line>
          <line x1="4.22" y1="4.22" x2="5.64" y2="5.64"></line>
          <line x1="18.36" y1="18.36" x2="19.78" y2="19.78"></line>
          <line x1="1" y1="12" x2="3" y2="12"></line>
          <line x1="21" y1="12" x2="23" y2="12"></line>
          <line x1="4.22" y1="19.78" x2="5.64" y2="18.36"></line>
          <line x1="18.36" y1="5.64" x2="19.78" y2="4.22"></line>
        </svg>
      </button>
    </header>

    <!-- Mobile Overlay -->
    <div v-if="mobileMenuOpen" class="mobile-overlay" @click="closeMobileMenu"></div>

    <!-- Sidebar -->
    <aside class="sidebar" :class="{ collapsed: !sidebarOpen, 'mobile-open': mobileMenuOpen }">
      <div class="sidebar-header">
        <h1 v-if="sidebarOpen">💰 Kash</h1>
        <button @click="sidebarOpen = !sidebarOpen" class="toggle-btn desktop-only">
          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <polyline v-if="sidebarOpen" points="15 18 9 12 15 6"></polyline>
            <polyline v-else points="9 18 15 12 9 6"></polyline>
          </svg>
        </button>
        <button @click="closeMobileMenu" class="close-btn mobile-only">
          <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <line x1="18" y1="6" x2="6" y2="18"></line>
            <line x1="6" y1="6" x2="18" y2="18"></line>
          </svg>
        </button>
      </div>

      <nav class="sidebar-nav">
        <RouterLink to="/dashboard" class="nav-item" @click="closeMobileMenu">
          <svg class="nav-icon" width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <rect x="3" y="3" width="7" height="9"></rect>
            <rect x="14" y="3" width="7" height="5"></rect>
            <rect x="14" y="12" width="7" height="9"></rect>
            <rect x="3" y="16" width="7" height="5"></rect>
          </svg>
          <span v-if="sidebarOpen" class="label">Dashboard</span>
        </RouterLink>
        <RouterLink to="/debts" class="nav-item" @click="closeMobileMenu">
          <svg class="nav-icon" width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <line x1="12" y1="1" x2="12" y2="23"></line>
            <path d="M17 5H9.5a3.5 3.5 0 0 0 0 7h5a3.5 3.5 0 0 1 0 7H6"></path>
          </svg>
          <span v-if="sidebarOpen" class="label">Débitos</span>
        </RouterLink>
        <RouterLink to="/receivables" class="nav-item" @click="closeMobileMenu">
          <svg class="nav-icon" width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <polyline points="23 6 13.5 15.5 8.5 10.5 1 18"></polyline>
            <polyline points="17 6 23 6 23 12"></polyline>
          </svg>
          <span v-if="sidebarOpen" class="label">Recebíveis</span>
        </RouterLink>
        <RouterLink to="/credit-cards" class="nav-item" @click="closeMobileMenu">
          <svg class="nav-icon" width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <rect x="1" y="4" width="22" height="16" rx="2" ry="2"></rect>
            <line x1="1" y1="10" x2="23" y2="10"></line>
          </svg>
          <span v-if="sidebarOpen" class="label">Cartões</span>
        </RouterLink>
        <RouterLink to="/profile" class="nav-item" @click="closeMobileMenu">
          <svg class="nav-icon" width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"></path>
            <circle cx="12" cy="7" r="4"></circle>
          </svg>
          <span v-if="sidebarOpen" class="label">Meu Perfil</span>
        </RouterLink>
        <RouterLink v-if="authStore.user?.isAdmin" to="/admin/email-settings" class="nav-item" @click="closeMobileMenu">
          <svg class="nav-icon" width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <path d="M4 4h16v12H4z"></path>
            <path d="M22 6l-10 7L2 6"></path>
            <path d="M7 20h10"></path>
          </svg>
          <span v-if="sidebarOpen" class="label">Admin Email</span>
        </RouterLink>
      </nav>

      <div class="sidebar-footer">
        <!-- Theme Toggle -->
        <button @click="themeStore.toggleTheme()" class="theme-toggle">
          <svg v-if="themeStore.theme === 'light'" class="nav-icon" width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <path d="M21 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 21 12.79z"></path>
          </svg>
          <svg v-else class="nav-icon" width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <circle cx="12" cy="12" r="5"></circle>
            <line x1="12" y1="1" x2="12" y2="3"></line>
            <line x1="12" y1="21" x2="12" y2="23"></line>
            <line x1="4.22" y1="4.22" x2="5.64" y2="5.64"></line>
            <line x1="18.36" y1="18.36" x2="19.78" y2="19.78"></line>
            <line x1="1" y1="12" x2="3" y2="12"></line>
            <line x1="21" y1="12" x2="23" y2="12"></line>
            <line x1="4.22" y1="19.78" x2="5.64" y2="18.36"></line>
            <line x1="18.36" y1="5.64" x2="19.78" y2="4.22"></line>
          </svg>
          <span v-if="sidebarOpen" class="label">{{ themeStore.theme === 'light' ? 'Tema Escuro' : 'Tema Claro' }}</span>
        </button>
        
        <div class="user-info" v-if="sidebarOpen">
          <span>{{ authStore.user?.name }}</span>
        </div>
        <button @click="logout" class="logout-btn">
          <svg class="nav-icon" width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4"></path>
            <polyline points="16 17 21 12 16 7"></polyline>
            <line x1="21" y1="12" x2="9" y2="12"></line>
          </svg>
          <span v-if="sidebarOpen" class="label">Sair</span>
        </button>
      </div>
    </aside>

    <!-- Main Content -->
    <main class="main-content">
      <RouterView />
    </main>
  </div>
</template>

<style scoped>
.app-layout {
  display: flex;
  min-height: 100vh;
}

/* Mobile Header */
.mobile-header {
  display: none;
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  height: 56px;
  background: var(--sidebar-bg);
  color: var(--sidebar-text);
  padding: 0 1rem;
  align-items: center;
  justify-content: space-between;
  z-index: 100;
}

.mobile-header h1 {
  font-size: 1.2rem;
  margin: 0;
}

.menu-btn,
.theme-toggle-mobile {
  background: none;
  border: none;
  color: var(--sidebar-text);
  cursor: pointer;
  padding: 0.5rem;
  border-radius: 8px;
  display: flex;
  align-items: center;
  justify-content: center;
}

.menu-btn:hover,
.theme-toggle-mobile:hover {
  background: var(--sidebar-hover-bg);
}

.mobile-overlay {
  display: none;
  position: fixed;
  inset: 0;
  background: rgba(0, 0, 0, 0.5);
  z-index: 150;
}

.mobile-only {
  display: none;
}

/* Sidebar */
.sidebar {
  width: var(--sidebar-width);
  background: var(--sidebar-bg);
  color: var(--sidebar-text);
  display: flex;
  flex-direction: column;
  transition: width 0.3s ease, transform 0.3s ease;
  position: relative;
  z-index: 200;
}

.sidebar.collapsed {
  width: var(--sidebar-collapsed-width);
}

.sidebar-header {
  padding: 1rem;
  display: flex;
  align-items: center;
  justify-content: space-between;
  border-bottom: 1px solid rgba(255, 255, 255, 0.1);
  min-height: 60px;
}

.sidebar-header h1 {
  font-size: 1.2rem;
  margin: 0;
  white-space: nowrap;
}

.toggle-btn,
.close-btn {
  background: none;
  border: none;
  color: var(--sidebar-text);
  cursor: pointer;
  padding: 0.5rem;
  border-radius: 6px;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: background 0.2s;
}

.toggle-btn:hover,
.close-btn:hover {
  background: var(--sidebar-hover-bg);
}

.sidebar-nav {
  flex: 1;
  padding: 1rem 0;
  overflow-y: auto;
}

.nav-item {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  padding: 0.85rem 1rem;
  color: var(--sidebar-text-muted);
  text-decoration: none;
  transition: all 0.2s ease;
  margin: 0.25rem 0.5rem;
  border-radius: 8px;
}

.nav-item:hover,
.nav-item.router-link-active {
  background: var(--sidebar-active-bg);
  color: var(--sidebar-text);
}

.nav-icon {
  flex-shrink: 0;
  stroke-width: 2;
}

.sidebar.collapsed .nav-item {
  justify-content: center;
  padding: 0.85rem;
}

.sidebar-footer {
  padding: 1rem;
  border-top: 1px solid rgba(255, 255, 255, 0.1);
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.theme-toggle {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  width: 100%;
  padding: 0.75rem;
  background: transparent;
  border: 1px solid rgba(255, 255, 255, 0.2);
  border-radius: 8px;
  color: var(--sidebar-text);
  cursor: pointer;
  transition: all 0.2s ease;
  font-size: 0.9rem;
}

.theme-toggle:hover {
  background: var(--sidebar-hover-bg);
  border-color: rgba(255, 255, 255, 0.3);
}

.sidebar.collapsed .theme-toggle {
  justify-content: center;
  padding: 0.75rem;
}

.user-info {
  padding: 0.5rem 0;
  font-size: 0.9rem;
  color: var(--sidebar-text-muted);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.logout-btn {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  width: 100%;
  padding: 0.75rem;
  background: rgba(239, 68, 68, 0.15);
  border: none;
  border-radius: 8px;
  color: #fca5a5;
  cursor: pointer;
  transition: all 0.2s ease;
  font-size: 0.9rem;
}

.logout-btn:hover {
  background: rgba(239, 68, 68, 0.25);
}

.sidebar.collapsed .logout-btn {
  justify-content: center;
  padding: 0.75rem;
}

.main-content {
  flex: 1;
  padding: 2rem;
  background: var(--bg-secondary);
  overflow-y: auto;
  transition: background-color 0.3s ease;
}

/* Responsivo - Mobile */
@media (max-width: 768px) {
  .mobile-header {
    display: flex;
  }

  .mobile-overlay {
    display: block;
  }

  .mobile-only {
    display: flex;
  }

  .desktop-only {
    display: none;
  }

  .sidebar {
    position: fixed;
    left: 0;
    top: 0;
    bottom: 0;
    transform: translateX(-100%);
    width: 280px;
  }

  .sidebar.mobile-open {
    transform: translateX(0);
  }

  .sidebar.collapsed {
    width: 280px;
  }

  .main-content {
    padding: 1rem;
    padding-top: 72px;
  }
}

/* Tablet */
@media (min-width: 769px) and (max-width: 1024px) {
  .sidebar {
    width: var(--sidebar-collapsed-width);
  }

  .sidebar .label {
    display: none;
  }

  .sidebar .nav-item {
    justify-content: center;
  }

  .sidebar .user-info {
    display: none;
  }

  .sidebar .theme-toggle {
    justify-content: center;
  }

  .sidebar .logout-btn {
    justify-content: center;
  }
}
</style>
