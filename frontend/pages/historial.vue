<script setup lang="ts">
import type { Holding } from '~/components/HoldingsTable.vue'

interface Sale {
  id: number
  playerId: number
  name: string
  team: string | null
  position: string
  purchasePrice: number
  salePrice: number
  profitLoss: number
  dailyDelta: number | null
  weeklyDelta: number | null
  purchaseDate: string
  saleDate: string
  salePriceIsManual: boolean
}

const api = useApi()
const { eur, signed, deltaClass, date } = useFormat()

const tab = ref<'holdings' | 'sales'>('holdings')

const { data: holdings, pending: pHold, error: eHold } = await useAsyncData('history-holdings', () =>
  api.get<Holding[]>('/api/history/holdings')
)
const { data: sales, pending: pSales, error: eSales } = await useAsyncData('history-sales', () =>
  api.get<Sale[]>('/api/history/sales')
)

const posShort = (p: string) =>
  ({ Portero: 'POR', Defensa: 'DEF', Centrocampista: 'MED', Delantero: 'DEL' } as Record<string, string>)[p] || p

// Ordenación de la tabla de vendidos
const sSortKey = ref<string>('saleDate')
const sSortDir = ref<'asc' | 'desc'>('desc')
const setSalesSort = (key: string) => {
  if (sSortKey.value === key) {
    sSortDir.value = sSortDir.value === 'asc' ? 'desc' : 'asc'
  } else {
    sSortKey.value = key
    sSortDir.value = key === 'name' ? 'asc' : 'desc'
  }
}
const sortedSales = computed(() => {
  const arr = [...(sales.value || [])]
  const k = sSortKey.value
  const mul = sSortDir.value === 'asc' ? 1 : -1
  return arr.sort((a: any, b: any) => {
    if (k === 'name') return String(a.name).localeCompare(String(b.name)) * mul
    if (k === 'saleDate') return (new Date(a.saleDate).getTime() - new Date(b.saleDate).getTime()) * mul
    const av = a[k] ?? Number.NEGATIVE_INFINITY
    const bv = b[k] ?? Number.NEGATIVE_INFINITY
    return (av - bv) * mul
  })
})
</script>

<template>
  <section class="space-y-4">
    <div>
      <h1 class="text-2xl font-extrabold tracking-tight">Historial</h1>
      <p class="text-sm text-muted">Jugadores que tienes y operaciones ya cerradas.</p>
    </div>

    <!-- Subpestañas -->
    <div class="inline-flex rounded-xl bg-ink-800 p-1">
      <button
        class="rounded-lg px-4 py-1.5 text-sm font-semibold transition"
        :class="tab === 'holdings' ? 'bg-brand text-white' : 'text-muted hover:text-white'"
        @click="tab = 'holdings'"
      >
        Sin vender
      </button>
      <button
        class="rounded-lg px-4 py-1.5 text-sm font-semibold transition"
        :class="tab === 'sales' ? 'bg-brand text-white' : 'text-muted hover:text-white'"
        @click="tab = 'sales'"
      >
        Vendidos
      </button>
    </div>

    <!-- Sin vender -->
    <template v-if="tab === 'holdings'">
      <div v-if="pHold" class="h-64 animate-pulse rounded-card bg-white/5" />
      <AppError v-else-if="eHold" />
      <AppEmpty v-else-if="!holdings || holdings.length === 0" title="Sin jugadores activos" />
      <HoldingsTable v-else :rows="holdings" />
    </template>

    <!-- Vendidos (stats congeladas) -->
    <template v-else>
      <div v-if="pSales" class="h-64 animate-pulse rounded-card bg-white/5" />
      <AppError v-else-if="eSales" />
      <AppEmpty
        v-else-if="!sales || sales.length === 0"
        icon="💸"
        title="Aún no hay ventas"
        hint="Cuando vendas un jugador (desaparezca de tu plantilla en la API), aparecerá aquí con sus stats congeladas."
      />
      <div v-else class="card overflow-hidden">
        <div class="overflow-x-auto">
          <table class="w-full min-w-[760px] text-sm">
            <thead>
              <tr class="border-b border-ink-900/10">
                <SortTh col-key="name" :active="sSortKey" :dir="sSortDir" @sort="setSalesSort">Jugador</SortTh>
                <SortTh col-key="purchasePrice" align="right" :active="sSortKey" :dir="sSortDir" @sort="setSalesSort">Compra</SortTh>
                <SortTh col-key="salePrice" align="right" :active="sSortKey" :dir="sSortDir" @sort="setSalesSort">Venta</SortTh>
                <SortTh col-key="profitLoss" align="right" :active="sSortKey" :dir="sSortDir" @sort="setSalesSort">G/P</SortTh>
                <SortTh col-key="saleDate" align="right" :active="sSortKey" :dir="sSortDir" @sort="setSalesSort">Fecha venta</SortTh>
              </tr>
            </thead>
            <tbody class="divide-y divide-ink-900/5">
              <tr v-for="s in sortedSales" :key="s.id" class="hover:bg-ink-900/[0.02]">
                <td class="px-4 py-3">
                  <div class="font-semibold text-ink-900">{{ s.name }}</div>
                  <div class="text-xs text-ink-600">{{ posShort(s.position) }} · {{ s.team || '—' }}</div>
                </td>
                <td class="px-4 py-3 text-right tabular-nums text-ink-900">{{ eur(s.purchasePrice) }}</td>
                <td class="px-4 py-3 text-right tabular-nums text-ink-900">
                  {{ eur(s.salePrice) }}<span v-if="s.salePriceIsManual" class="text-[10px] text-ink-600"> *</span>
                </td>
                <td class="px-4 py-3 text-right font-semibold tabular-nums" :class="deltaClass(s.profitLoss)">
                  {{ signed(s.profitLoss) }}
                </td>
                <td class="px-4 py-3 text-right text-ink-600">{{ date(s.saleDate) }}</td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </template>
  </section>
</template>
