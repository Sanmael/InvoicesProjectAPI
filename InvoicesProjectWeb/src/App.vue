<script setup lang="ts">
import { RouterView } from 'vue-router'
import { useThemeStore } from '@/stores/theme'

// Inicializa o tema automaticamente ao carregar o app
const themeStore = useThemeStore()
</script>

<template>
  <RouterView />
</template>

<style scoped></style>
