<script setup lang="ts">
export interface Holding {
  holdingId: number
  playerId: number
  externalId: string
  name: string
  team: string | null
  position: string
  currentValue: number | null
  dailyDelta: number | null
  weeklyDelta: number | null
  purchasePrice: number
  profitLoss: number | null
  purchasePriceIsManual: boolean
  purchaseDate: string
  status: string
  imageUrl: string | null
}

const props = defineProps<{ rows: Holding[]; editable?: boolean }>()
const emit = defineEmits<{ (e: 'saved'): void }>()

const api = useApi()
const { eur, deltaClass, signed } = useFormat()

const posColor = (p: string) =>
  ({
    Portero: 'bg-amber-100 text-amber-700',
    Defensa: 'bg-sky-100 text-sky-700',
    Centrocampista: 'bg-emerald-100 text-emerald-700',
    Delantero: 'bg-rose-100 text-rose-700'
  } as Record<string, string>)[p] || 'bg-gray-100 text-gray-600'

const posShort = (p: string) =>
  ({ Portero: 'POR', Defensa: 'DEF', Centrocampista: 'MED', Delantero: 'DEL' } as Record<string, string>)[p] || p

// Ordenación por columna (clic en la cabecera)
const sortKey = ref<string>('currentValue')
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

// Edición en línea del precio de compra
const editingId = ref<number | null>(null)
const editValue = ref<number>(0)
const saving = ref(false)

const startEdit = (h: Holding) => {
  editingId.value = h.holdingId
  editValue.value = h.purchasePrice
}
const cancelEdit = () => {
  editingId.value = null
}
const saveEdit = async (h: Holding) => {
  saving.value = true
  try {
    await api.put(`/api/players/holdings/${h.holdingId}/purchase-price`, {
      purchasePrice: Math.max(0, Math.round(editValue.value))
    })
    editingId.value = null
    emit('saved')
  } finally {
    saving.value = false
  }
}
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
            <SortTh col-key="purchasePrice" align="right" :active="sortKey" :dir="sortDir" @sort="setSort">Compra</SortTh>
            <SortTh col-key="profitLoss" align="right" :active="sortKey" :dir="sortDir" @sort="setSort">G/P</SortTh>
          </tr>
        </thead>
        <tbody class="divide-y divide-ink-900/5">
          <tr v-for="h in sortedRows" :key="h.holdingId" class="hover:bg-ink-900/[0.02]">
            <!-- Jugador -->
            <td class="px-4 py-3">
              <div class="flex items-center gap-3">
                <span class="pill shrink-0" :class="posColor(h.position)">{{ posShort(h.position) }}</span>
                <div class="min-w-0">
                  <div class="truncate font-semibold text-ink-900">{{ h.name }}</div>
                  <div class="truncate text-xs text-ink-600">{{ h.team || '—' }}</div>
                </div>
                <!-- Foto del jugador, a la derecha en el espacio libre -->
                <div class="ml-auto shrink-0">
                  <img
                    v-if="h.imageUrl"
                    :src="h.imageUrl"
                    :alt="h.name"
                    class="h-11 w-11 rounded-full bg-gradient-to-b from-ink-900/5 to-ink-900/10 object-cover object-top ring-1 ring-ink-900/10"
                    @error="(e) => ((e.target as HTMLImageElement).style.display = 'none')"
                  />
                  <div
                    v-else
                    class="grid h-11 w-11 place-items-center rounded-full bg-ink-900/5 text-xs font-bold text-ink-600 ring-1 ring-ink-900/10"
                  >
                    {{ h.name.charAt(0) }}
                  </div>
                </div>
              </div>
            </td>

            <!-- Valor actual -->
            <td class="px-4 py-3 text-right font-semibold tabular-nums text-ink-900">
              {{ eur(h.currentValue) }}
            </td>

            <!-- Deltas -->
            <td class="px-4 py-3 text-right"><DeltaBadge :value="h.dailyDelta" compact /></td>
            <td class="px-4 py-3 text-right"><DeltaBadge :value="h.weeklyDelta" compact /></td>

            <!-- Compra (editable) -->
            <td class="px-4 py-3 text-right tabular-nums">
              <div v-if="editingId === h.holdingId" class="flex items-center justify-end gap-1">
                <input
                  v-model.number="editValue"
                  type="number"
                  class="w-32 rounded-lg border border-ink-900/20 px-2 py-1 text-right text-ink-900 outline-none focus:border-brand"
                  @keyup.enter="saveEdit(h)"
                  @keyup.esc="cancelEdit"
                />
                <button class="rounded-md bg-brand px-2 py-1 text-xs font-semibold text-white disabled:opacity-50" :disabled="saving" @click="saveEdit(h)">✓</button>
                <button class="rounded-md bg-ink-900/10 px-2 py-1 text-xs font-semibold text-ink-900" @click="cancelEdit">✕</button>
              </div>
              <button
                v-else-if="editable"
                class="group inline-flex items-center gap-1 text-ink-900"
                title="Editar precio de compra"
                @click="startEdit(h)"
              >
                <span class="tabular-nums">{{ eur(h.purchasePrice) }}</span>
                <span v-if="h.purchasePriceIsManual" class="text-[10px] text-ink-600" title="Estimado / manual">*</span>
                <span class="text-ink-600 opacity-0 transition group-hover:opacity-100">✎</span>
              </button>
              <span v-else class="text-ink-900">{{ eur(h.purchasePrice) }}</span>
            </td>

            <!-- G/P -->
            <td class="px-4 py-3 text-right font-semibold tabular-nums" :class="deltaClass(h.profitLoss)">
              {{ signed(h.profitLoss) }}
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <p v-if="editable" class="border-t border-ink-900/10 px-4 py-2 text-[11px] text-ink-600">
      * precio de compra estimado (valor de mercado). Haz clic para poner el importe real que pagaste.
    </p>
  </div>
</template>
