<script setup lang="ts">
import type { Holding } from '~/components/HoldingsTable.vue'

interface SavedLineup { id: number; name: string; formation: string; data: string; updatedAt: string }
interface Official {
  formation: string
  goalkeepers: string[]
  defenders: string[]
  midfielders: string[]
  strikers: string[]
}

const api = useApi()
const { eur } = useFormat()
const { open } = usePlayerModal()

const { data: squad } = await useAsyncData('lineup-squad', () => api.get<Holding[]>('/api/players'))
const { data: saved, refresh: refreshSaved } = await useAsyncData('lineups', () => api.get<SavedLineup[]>('/api/lineups'))

const FORMATIONS: Record<string, [number, number, number]> = {
  '4-3-3': [4, 3, 3], '4-4-2': [4, 4, 2], '4-5-1': [4, 5, 1], '4-2-4': [4, 2, 4],
  '3-4-3': [3, 4, 3], '3-5-2': [3, 5, 2], '5-3-2': [5, 3, 2], '5-4-1': [5, 4, 1]
}
const POS_OF_LINE: Record<string, string> = { GK: 'Portero', DEF: 'Defensa', MID: 'Centrocampista', FWD: 'Delantero' }

const formation = ref('4-3-3')
const name = ref('Mi alineación')
const currentId = ref<number | null>(null)
// asignaciones: slotId -> externalId
const assign = ref<Record<string, string>>({})

const playerByExt = computed(() => {
  const m: Record<string, Holding> = {}
  for (const p of squad.value || []) m[p.externalId] = p
  return m
})

// Líneas del campo, de arriba (delanteros) a abajo (portero)
const lines = computed(() => {
  const [d, m, f] = FORMATIONS[formation.value]
  return [
    { key: 'FWD', count: f },
    { key: 'MID', count: m },
    { key: 'DEF', count: d },
    { key: 'GK', count: 1 }
  ]
})
const validSlotIds = computed(() => new Set(lines.value.flatMap((l) => Array.from({ length: l.count }, (_, i) => `${l.key}-${i}`))))

// Al cambiar de formación, descarta asignaciones que ya no existen.
watch(formation, () => {
  const next: Record<string, string> = {}
  for (const [slot, ext] of Object.entries(assign.value)) if (validSlotIds.value.has(slot)) next[slot] = ext
  assign.value = next
})

const assignedExt = computed(() => new Set(Object.values(assign.value)))
const bench = computed(() => (squad.value || []).filter((p) => !assignedExt.value.has(p.externalId)))

const posShort = (p: string) =>
  ({ Portero: 'POR', Defensa: 'DEF', Centrocampista: 'MED', Delantero: 'DEL' } as Record<string, string>)[p] || p

// Picker: al pulsar un hueco vacío
const picking = ref<{ slot: string; line: string } | null>(null)
const eligible = computed(() => {
  if (!picking.value) return []
  const pos = POS_OF_LINE[picking.value.line]
  return bench.value.filter((p) => p.position === pos)
})
const assignPlayer = (ext: string) => {
  if (!picking.value) return
  assign.value = { ...assign.value, [picking.value.slot]: ext }
  picking.value = null
}
const clearSlot = (slot: string) => {
  const next = { ...assign.value }
  delete next[slot]
  assign.value = next
}

// Guardar / cargar / borrar
const saving = ref(false)
const save = async () => {
  saving.value = true
  const body = { name: name.value, formation: formation.value, data: JSON.stringify(assign.value) }
  try {
    if (currentId.value) await api.put(`/api/lineups/${currentId.value}`, body)
    else {
      const created = await api.post<SavedLineup>('/api/lineups', body)
      currentId.value = created.id
    }
    await refreshSaved()
  } finally {
    saving.value = false
  }
}
const load = (l: SavedLineup) => {
  formation.value = l.formation
  name.value = l.name
  currentId.value = l.id
  try { assign.value = JSON.parse(l.data || '{}') } catch { assign.value = {} }
}
const remove = async (l: SavedLineup) => {
  await api.delete(`/api/lineups/${l.id}`)
  if (currentId.value === l.id) newLineup()
  await refreshSaved()
}
const newLineup = () => {
  currentId.value = null
  name.value = 'Mi alineación'
  assign.value = {}
}

const loadingOfficial = ref(false)
const loadOfficial = async () => {
  loadingOfficial.value = true
  try {
    const o = await api.get<Official>('/api/lineups/official')
    if (FORMATIONS[o.formation]) formation.value = o.formation
    const next: Record<string, string> = {}
    const fill = (key: string, ids: string[]) => ids.forEach((ext, i) => { if (validSlotIds.value.has(`${key}-${i}`)) next[`${key}-${i}`] = ext })
    // esperar a que la formación recalcule los huecos
    await nextTick()
    fill('GK', o.goalkeepers); fill('DEF', o.defenders); fill('MID', o.midfielders); fill('FWD', o.strikers)
    assign.value = next
    currentId.value = null
    name.value = 'Once oficial'
  } catch {
    // degradar en silencio
  } finally {
    loadingOfficial.value = false
  }
}

const placedCount = computed(() => Object.keys(assign.value).length)
</script>

<template>
  <section class="space-y-4">
    <div class="flex flex-wrap items-end justify-between gap-3">
      <div>
        <h1 class="text-2xl font-extrabold tracking-tight">Alineación</h1>
        <p class="text-sm text-muted">Planifica tu once con tu plantilla. {{ placedCount }}/11 colocados.</p>
      </div>
      <div class="flex flex-wrap items-center gap-2">
        <select v-model="formation" class="rounded-xl border border-white/10 bg-ink-800 px-3 py-2 text-sm text-white outline-none focus:border-brand">
          <option v-for="(_, f) in FORMATIONS" :key="f" :value="f">{{ f }}</option>
        </select>
        <button class="btn-ghost" :disabled="loadingOfficial" @click="loadOfficial">
          {{ loadingOfficial ? 'Cargando…' : 'Cargar once oficial' }}
        </button>
        <button class="btn-brand" :disabled="saving" @click="save">{{ saving ? 'Guardando…' : (currentId ? 'Guardar' : 'Guardar nueva') }}</button>
      </div>
    </div>

    <div class="flex flex-wrap items-center gap-2">
      <input v-model="name" class="rounded-xl border border-white/10 bg-ink-800 px-3 py-2 text-sm text-white outline-none focus:border-brand" placeholder="Nombre de la alineación" />
      <button class="btn-ghost" @click="newLineup">Nueva</button>
    </div>

    <!-- Campo -->
    <div class="rounded-card border border-emerald-900/40 bg-gradient-to-b from-emerald-800/40 to-emerald-950/40 p-4">
      <div class="mx-auto flex max-w-2xl flex-col gap-6 py-2">
        <div v-for="line in lines" :key="line.key" class="flex justify-center gap-3">
          <div v-for="i in line.count" :key="`${line.key}-${i - 1}`" class="w-20 sm:w-24">
            <template v-if="assign[`${line.key}-${i - 1}`] && playerByExt[assign[`${line.key}-${i - 1}`]]">
              <div class="flex flex-col items-center gap-1">
                <div class="group relative">
                  <button class="block" @click="open(playerByExt[assign[`${line.key}-${i - 1}`]].playerId)">
                    <img
                      v-if="playerByExt[assign[`${line.key}-${i - 1}`]].imageUrl"
                      :src="playerByExt[assign[`${line.key}-${i - 1}`]].imageUrl!"
                      class="h-14 w-14 rounded-full bg-black/20 object-cover object-top ring-2 ring-white/70"
                    />
                    <div v-else class="grid h-14 w-14 place-items-center rounded-full bg-black/30 text-sm font-bold text-white ring-2 ring-white/70">
                      {{ playerByExt[assign[`${line.key}-${i - 1}`]].name.charAt(0) }}
                    </div>
                  </button>
                  <!-- X roja al pasar el ratón para quitar el jugador -->
                  <button
                    class="absolute -right-1 -top-1 hidden h-5 w-5 place-items-center rounded-full bg-down text-[10px] font-bold text-white ring-2 ring-ink-900/40 transition hover:brightness-110 group-hover:grid"
                    title="Quitar jugador"
                    @click.stop="clearSlot(`${line.key}-${i - 1}`)"
                  >✕</button>
                </div>
                <div class="max-w-full truncate text-center text-[11px] font-semibold text-white">
                  {{ playerByExt[assign[`${line.key}-${i - 1}`]].name }}
                </div>
              </div>
            </template>
            <button
              v-else
              class="flex w-full flex-col items-center gap-1"
              @click="picking = { slot: `${line.key}-${i - 1}`, line: line.key }"
            >
              <span class="grid h-14 w-14 place-items-center rounded-full border-2 border-dashed border-white/40 text-white/70">+</span>
              <span class="text-[10px] uppercase tracking-wide text-white/50">{{ posShort(POS_OF_LINE[line.key]) }}</span>
            </button>
          </div>
        </div>
      </div>
    </div>

    <!-- Banquillo -->
    <div>
      <p class="section-label mb-2">Banquillo · {{ bench.length }}</p>
      <div v-if="bench.length === 0" class="text-sm text-muted">Todos colocados.</div>
      <div v-else class="grid grid-cols-2 gap-2 sm:grid-cols-3 lg:grid-cols-4">
        <div v-for="p in bench" :key="p.externalId" class="card flex items-center gap-2 p-2">
          <span class="pill shrink-0" :class="{
            'bg-amber-100 text-amber-700': p.position==='Portero',
            'bg-sky-100 text-sky-700': p.position==='Defensa',
            'bg-emerald-100 text-emerald-700': p.position==='Centrocampista',
            'bg-rose-100 text-rose-700': p.position==='Delantero'
          }">{{ posShort(p.position) }}</span>
          <div class="min-w-0 flex-1">
            <div class="truncate text-sm font-semibold text-ink-900">{{ p.name }}</div>
            <div class="truncate text-[11px] text-ink-600">{{ eur(p.currentValue, { compact: true }) }}</div>
          </div>
        </div>
      </div>
    </div>

    <!-- Alineaciones guardadas -->
    <div v-if="saved && saved.length">
      <p class="section-label mb-2">Guardadas</p>
      <div class="flex flex-wrap gap-2">
        <div v-for="l in saved" :key="l.id" class="flex items-center gap-2 rounded-xl bg-ink-800 px-3 py-2">
          <button class="text-sm font-semibold hover:text-brand" @click="load(l)">{{ l.name }} · {{ l.formation }}</button>
          <button class="text-xs text-muted hover:text-down" title="Borrar" @click="remove(l)">✕</button>
        </div>
      </div>
    </div>

    <!-- Picker de jugador para un hueco -->
    <Teleport to="body">
      <div v-if="picking" class="fixed inset-0 z-50 grid place-items-center bg-black/60 p-4" @click.self="picking = null">
        <div class="w-full max-w-sm rounded-2xl border border-white/10 bg-ink-800 p-4">
          <div class="mb-2 flex items-center justify-between">
            <h3 class="font-bold">Elegir {{ POS_OF_LINE[picking.line].toLowerCase() }}</h3>
            <button class="text-muted hover:text-white" @click="picking = null">✕</button>
          </div>
          <div v-if="eligible.length === 0" class="py-6 text-center text-sm text-muted">No hay jugadores libres de esa posición.</div>
          <div v-else class="max-h-72 space-y-1 overflow-y-auto">
            <button
              v-for="p in eligible"
              :key="p.externalId"
              class="flex w-full items-center gap-3 rounded-lg px-2 py-2 text-left hover:bg-white/5"
              @click="assignPlayer(p.externalId)"
            >
              <img v-if="p.imageUrl" :src="p.imageUrl" class="h-9 w-9 rounded-full bg-black/20 object-cover object-top" />
              <div class="min-w-0 flex-1">
                <div class="truncate text-sm font-semibold">{{ p.name }}</div>
                <div class="truncate text-xs text-muted">{{ p.team || '—' }} · {{ eur(p.currentValue, { compact: true }) }}</div>
              </div>
            </button>
          </div>
        </div>
      </div>
    </Teleport>
  </section>
</template>
