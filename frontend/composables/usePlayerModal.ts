import { ref } from 'vue'

// Estado global (nivel de módulo, como useAuthGate): qué jugador tiene el modal
// de detalle abierto. Las tablas llaman open(id); app.vue monta el modal.
const openPlayerId = ref<number | null>(null)

export const usePlayerModal = () => {
  const open = (playerId?: number | null) => {
    if (playerId == null) return
    openPlayerId.value = playerId
  }
  const close = () => {
    openPlayerId.value = null
  }
  return { openPlayerId, open, close }
}
