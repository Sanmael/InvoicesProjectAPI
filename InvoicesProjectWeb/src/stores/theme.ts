import { defineStore } from 'pinia'
import { ref, watch } from 'vue'

export type Theme = 'light' | 'dark'

export const useThemeStore = defineStore('theme', () => {
  const theme = ref<Theme>(getInitialTheme())

  function getInitialTheme(): Theme {
    const saved = localStorage.getItem('theme')
    if (saved === 'light' || saved === 'dark') {
      return saved
    }
    // Detecta preferência do sistema
    if (window.matchMedia('(prefers-color-scheme: dark)').matches) {
      return 'dark'
    }
    return 'light'
  }

  function setTheme(newTheme: Theme) {
    theme.value = newTheme
    localStorage.setItem('theme', newTheme)
    applyTheme(newTheme)
  }

  function toggleTheme() {
    setTheme(theme.value === 'light' ? 'dark' : 'light')
  }

  function applyTheme(t: Theme) {
    document.documentElement.setAttribute('data-theme', t)
  }

  // Aplica tema inicial
  applyTheme(theme.value)

  // Watch para mudanças
  watch(theme, (newTheme) => {
    applyTheme(newTheme)
  })

  return {
    theme,
    setTheme,
    toggleTheme,
  }
})
