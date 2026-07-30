import { ref } from 'vue'

/** Puja sugerida (heurística del backend). Ver BidSuggestionService. */
export interface BidSuggestion {
  avgPointsLast5: number | null
  weeklyPct: number | null
  performanceScore: number | null
  priceTrendScore: number
  combinedScore: number
  suggestedBid: number
  limitedData: boolean
}

// Estado global (nivel de módulo, como useAuthGate): qué jugador tiene el modal
// de detalle abierto y, si se abrió desde Mercado, su puja sugerida (que es un
// dato de mercado, no del endpoint de detalle). Las tablas llaman open(id[, bid]);
// app.vue monta el modal.
const openPlayerId = ref<number | null>(null)
const openBid = ref<BidSuggestion | null>(null)

export const usePlayerModal = () => {
  const open = (playerId?: number | null, bid?: BidSuggestion | null) => {
    if (playerId == null) return
    openPlayerId.value = playerId
    openBid.value = bid ?? null
  }
  const close = () => {
    openPlayerId.value = null
    openBid.value = null
  }
  return { openPlayerId, openBid, open, close }
}
