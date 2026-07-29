<script setup lang="ts">
import type { MarketRow } from '~/components/MarketTable.vue'

interface ForSaleRow {
  playerId: number | null
  externalId: string
  name: string
  team: string | null
  position: string
  currentValue: number | null
  salePrice: number
  numberOfOffers: number
  bestOffer: number | null
  offerDiff: number | null
  offerPct: number | null
  imageUrl: string | null
}
interface MarketResp { market: MarketRow[]; forSale: ForSaleRow[] }

const api = useApi()
const { eur, signed } = useFormat()
const { open } = usePlayerModal()

const { data, pending, error } = await useAsyncData('market', () => api.get<MarketResp>('/api/market'))

const posShort = (p: string) =>
  ({ Portero: 'POR', Defensa: 'DEF', Centrocampista: 'MED', Delantero: 'DEL' } as Record<string, string>)[p] || p
const posColor = (p: string) =>
  ({
    Portero: 'bg-amber-100 text-amber-700',
    Defensa: 'bg-sky-100 text-sky-700',
    Centrocampista: 'bg-emerald-100 text-emerald-700',
    Delantero: 'bg-rose-100 text-rose-700'
  } as Record<string, string>)[p] || 'bg-gray-100 text-gray-600'

const pctTone = (v: number | null) => (v == null ? 'text-ink-600' : v > 0 ? 'text-emerald-600' : v < 0 ? 'text-rose-600' : 'text-ink-600')
const fmtPct = (v: number | null) => (v == null ? '—' : `${v > 0 ? '+' : ''}${v}%`)
</script>

<template>
  <section class="space-y-6">
    <div>
      <h1 class="text-2xl font-extrabold tracking-tight">Mercado</h1>
      <p class="text-sm text-muted">Tus ventas activas y el mercado de la liga.</p>
    </div>

    <div v-if="pending" class="h-64 animate-pulse rounded-card bg-white/5" />
    <AppError v-else-if="error" />

    <template v-else-if="data">
      <!-- En venta (mis jugadores listados) -->
      <div v-if="data.forSale.length" class="space-y-2">
        <p class="section-label">En venta · {{ data.forSale.length }}</p>
        <div class="card overflow-hidden">
          <div class="overflow-x-auto">
            <table class="w-full min-w-[720px] text-sm">
              <thead>
                <tr class="border-b border-ink-900/10 text-left text-[11px] uppercase tracking-wide text-ink-600">
                  <th class="px-4 py-3 font-semibold">Jugador</th>
                  <th class="px-4 py-3 text-right font-semibold">Valor</th>
                  <th class="px-4 py-3 text-right font-semibold">Listado</th>
                  <th class="px-4 py-3 text-right font-semibold">Mejor oferta</th>
                  <th class="px-4 py-3 text-right font-semibold">Dif. oferta</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-ink-900/5">
                <tr v-for="r in data.forSale" :key="r.externalId" class="hover:bg-ink-900/[0.02]">
                  <td class="px-4 py-3">
                    <div class="flex items-center gap-3" :class="r.playerId ? 'cursor-pointer' : ''" @click="r.playerId && open(r.playerId)">
                      <span class="pill shrink-0" :class="posColor(r.position)">{{ posShort(r.position) }}</span>
                      <div class="min-w-0">
                        <div class="truncate font-semibold text-ink-900" :class="r.playerId ? 'hover:underline' : ''">{{ r.name }}</div>
                        <div class="truncate text-xs text-ink-600">{{ r.team || '—' }} · {{ r.numberOfOffers }} oferta(s)</div>
                      </div>
                      <img v-if="r.imageUrl" :src="r.imageUrl" :alt="r.name" class="ml-auto h-11 w-11 shrink-0 rounded-full bg-ink-900/5 object-cover object-top ring-1 ring-ink-900/10" @error="(e) => ((e.target as HTMLImageElement).style.display='none')" />
                    </div>
                  </td>
                  <td class="px-4 py-3 text-right tabular-nums text-ink-900">{{ eur(r.currentValue) }}</td>
                  <td class="px-4 py-3 text-right tabular-nums text-ink-900">{{ eur(r.salePrice) }}</td>
                  <td class="px-4 py-3 text-right font-semibold tabular-nums text-ink-900">{{ r.bestOffer ? eur(r.bestOffer) : '—' }}</td>
                  <td class="px-4 py-3 text-right">
                    <div class="font-bold tabular-nums" :class="pctTone(r.offerPct)">{{ fmtPct(r.offerPct) }}</div>
                    <div v-if="r.offerDiff != null" class="text-[11px] tabular-nums" :class="pctTone(r.offerPct)">{{ signed(r.offerDiff) }}</div>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
          <p class="border-t border-ink-900/10 px-4 py-2 text-[11px] text-ink-600">
            La diferencia y el % son de la mejor oferta actual respecto al precio al que lo listaste.
          </p>
        </div>
      </div>

      <!-- Mercado completo (sin mis jugadores) -->
      <div class="space-y-2">
        <p class="section-label">Mercado · {{ data.market.length }}</p>
        <AppEmpty
          v-if="data.market.length === 0"
          icon="🛒"
          title="Mercado vacío"
          hint="No hay jugadores en venta ahora mismo. Sincroniza y reintenta."
        />
        <MarketTable v-else :rows="data.market" />
      </div>
    </template>
  </section>
</template>
