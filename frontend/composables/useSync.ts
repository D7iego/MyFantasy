// Estado global de sincronización + acción. Tras sincronizar, refresca todos
// los useAsyncData de la app para que las tablas se repinten con datos frescos.
interface SyncResult {
  success: boolean
  leagueName: string | null
  playersSnapshotted: number
  newHoldings: number
  newSales: number
  activeHoldings: number
  warning?: string | null
  error?: string | null
}

export const useSync = () => {
  const api = useApi()
  const state = useState('sync-state', () => ({
    loading: false,
    lastResult: null as SyncResult | null,
    error: null as string | null,
    lastRunAt: null as string | null
  }))

  const run = async () => {
    state.value.loading = true
    state.value.error = null
    try {
      const result = await api.post<SyncResult>('/api/sync')
      state.value.lastResult = result
      state.value.lastRunAt = new Date().toISOString()
      await refreshNuxtData()
    } catch (e: any) {
      state.value.error =
        e?.data?.error || e?.data?.warning || e?.message || 'No se pudo conectar con el backend'
    } finally {
      state.value.loading = false
    }
    return state.value
  }

  return { state, run }
}
