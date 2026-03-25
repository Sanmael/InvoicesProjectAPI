import { ref } from 'vue'

export interface Toast {
  id: number
  message: string
  type: 'success' | 'error' | 'warning' | 'info'
}

const toasts = ref<Toast[]>([])
let nextId = 0

export function useToast() {
  function show(message: string, type: Toast['type'] = 'info', duration = 5000) {
    const id = nextId++
    toasts.value.push({ id, message, type })
    if (duration > 0) {
      setTimeout(() => remove(id), duration)
    }
  }

  function remove(id: number) {
    toasts.value = toasts.value.filter((t) => t.id !== id)
  }

  function error(message: string) {
    show(message, 'error')
  }

  function success(message: string) {
    show(message, 'success', 3000)
  }

  function warning(message: string) {
    show(message, 'warning')
  }

  return { toasts, show, remove, error, success, warning }
}
