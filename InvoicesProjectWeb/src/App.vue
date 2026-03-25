<script setup lang="ts">
import { RouterView } from 'vue-router'
import { useThemeStore } from '@/stores/theme'
import ToastContainer from '@/components/ToastContainer.vue'

// Inicializa o tema automaticamente ao carregar o app
const themeStore = useThemeStore()
</script>

<template>
  <RouterView />
  <ToastContainer />
</template>

<style scoped></style>
