<script setup lang="ts">
export interface MarketRow {
  playerId: number | null
  externalId: string
  name: string
  team: string | null
  position: string
  currentValue: number | null
  dailyDelta: number | null
  weeklyDelta: number | null
  salePrice: number | null
  imageUrl: string | null
}

const props = defineProps<{ rows: MarketRow[] }>()

const { eur } = useFormat()
const { open } = usePlayerModal()

const posColor = (p: string) =>
  ({
    Portero: 'bg-amber-100 text-amber-700',
    Defensa: 'bg-sky-100 text-sky-700',
    Centrocampista: 'bg-emerald-100 text-emerald-700',
    Delantero: 'bg-rose-100 text-rose-700'
  } as Record<string, string>)[p] || 'bg-gray-100 text-gray-600'

const posShort = (p: string) =>
  ({ Portero: 'POR', Defensa: 'DEF', Centrocampista: 'MED', Delantero: 'DEL' } as Record<string, string>)[p] || p

// Ordenación por columna. Por defecto, los que más suben hoy.
const sortKey = ref<string>('dailyDelta')
const sortDir = ref<'asc' | 'desc'>('desc')
const setSort = (key: string) => {
  if (sortKey.value === key) {
    sortDir.value = sortDir.value === 'asc' ? 'desc' : 'asc'
  } else {
    sortKey.value = key
    sortDir.value = key === 'name' ? 'asc' : 'desc'
  }
}
const sortedRows = computed(() => {
  const arr = [...props.rows]
  const k = sortKey.value
  const mul = sortDir.value === 'asc' ? 1 : -1
  return arr.sort((a: any, b: any) => {
    if (k === 'name') return String(a.name).localeCompare(String(b.name)) * mul
    const av = a[k] ?? Number.NEGATIVE_INFINITY
    const bv = b[k] ?? Number.NEGATIVE_INFINITY
    return (av - bv) * mul
  })
})
</script>

<template>
  <div class="card overflow-hidden">
    <div class="overflow-x-auto">
      <table class="w-full min-w-[720px] text-sm">
        <thead>
          <tr class="border-b border-ink-900/10">
            <SortTh col-key="name" :active="sortKey" :dir="sortDir" @sort="setSort">Jugador</SortTh>
            <SortTh col-key="currentValue" align="right" :active="sortKey" :dir="sortDir" @sort="setSort">Valor actual</SortTh>
            <SortTh col-key="dailyDelta" align="right" :active="sortKey" :dir="sortDir" @sort="setSort">Día</SortTh>
            <SortTh col-key="weeklyDelta" align="right" :active="sortKey" :dir="sortDir" @sort="setSort">Semana</SortTh>
            <SortTh col-key="salePrice" align="right" :active="sortKey" :dir="sortDir" @sort="setSort">En venta</SortTh>
          </tr>
        </thead>
        <tbody class="divide-y divide-ink-900/5">
          <tr v-for="r in sortedRows" :key="r.externalId" class="hover:bg-ink-900/[0.02]">
            <!-- Jugador -->
            <td class="px-4 py-3">
              <div
                class="flex items-center gap-3"
                :class="r.playerId ? 'cursor-pointer' : ''"
                @click="r.playerId && open(r.playerId)"
              >
                <span class="pill shrink-0" :class="posColor(r.position)">{{ posShort(r.position) }}</span>
                <div class="min-w-0">
                  <div class="truncate font-semibold text-ink-900" :class="r.playerId ? 'hover:underline' : ''">{{ r.name }}</div>
                  <div class="truncate text-xs text-ink-600">{{ r.team || '—' }}</div>
                </div>
                <div class="ml-auto shrink-0">
                  <img
                    v-if="r.imageUrl"
                    :src="r.imageUrl"
                    :alt="r.name"
                    class="h-11 w-11 rounded-full bg-gradient-to-b from-ink-900/5 to-ink-900/10 object-cover object-top ring-1 ring-ink-900/10"
                    @error="(e) => ((e.target as HTMLImageElement).style.display = 'none')"
                  />
                  <div
                    v-else
                    class="grid h-11 w-11 place-items-center rounded-full bg-ink-900/5 text-xs font-bold text-ink-600 ring-1 ring-ink-900/10"
                  >
                    {{ r.name.charAt(0) }}
                  </div>
                </div>
              </div>
            </td>

            <!-- Valor actual -->
            <td class="px-4 py-3 text-right font-semibold tabular-nums text-ink-900">
              {{ eur(r.currentValue) }}
            </td>

            <!-- Deltas -->
            <td class="px-4 py-3 text-right"><DeltaBadge :value="r.dailyDelta" compact /></td>
            <td class="px-4 py-3 text-right"><DeltaBadge :value="r.weeklyDelta" compact /></td>

            <!-- Precio de venta en el mercado -->
            <td class="px-4 py-3 text-right tabular-nums text-ink-900">
              {{ r.salePrice ? eur(r.salePrice) : '—' }}
            </td>
          </tr>
        </tbody>
      </table>
    </div>
  </div>
</template>
