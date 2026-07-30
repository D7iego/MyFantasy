<script setup lang="ts">
const { state, run } = useSync()

// Al arrancar, comprueba si la sesión de LaLiga sigue viva; si no, abre el
// re-login proactivamente (sin esperar al primer 401 de una vista).
const { trigger } = useAuthGate()
onMounted(async () => {
  try {
    const base = useRuntimeConfig().public.apiBase
    const res = await $fetch<{ authenticated: boolean }>('/api/auth/status', { baseURL: base })
    if (!res.authenticated) trigger()
  } catch {
    // Backend caído: las vistas ya muestran AppError.
  }
})

const tabs = [
  { to: '/', label: 'Ligas', icon: '🏆' },
  { to: '/jugadores', label: 'Jugadores', icon: '👥' },
  { to: '/plantilla', label: 'Alineación', icon: '⚽' },
  { to: '/mercado', label: 'Mercado', icon: '🛒' },
  { to: '/general', label: 'General', icon: '📋' },
  { to: '/rivales', label: 'Rivales', icon: '⚔️' },
  { to: '/historial', label: 'Historial', icon: '🗂️' },
  { to: '/stats', label: 'Stats', icon: '📊' }
]

const syncSummary = computed(() => {
  if (state.value.error) return { text: state.value.error, tone: 'error' as const }
  const r = state.value.lastResult
  if (!r) return null
  if (r.warning) return { text: r.warning, tone: 'warn' as const }
  return {
    text: `${r.leagueName ?? 'Liga'} · ${r.activeHoldings} en plantilla · ${r.playersSnapshotted} precios`,
    tone: 'ok' as const
  }
})
</script>

<template>
  <div class="min-h-full flex flex-col">
    <!-- Cabecera -->
    <header class="sticky top-0 z-30 border-b border-white/5 bg-ink-950/80 backdrop-blur">
      <div class="mx-auto flex max-w-5xl items-center justify-between px-4 py-3">
        <NuxtLink to="/" class="flex items-center gap-2">
          <span class="grid h-8 w-8 place-items-center rounded-lg bg-brand font-extrabold text-white">M</span>
          <span class="text-lg font-extrabold tracking-tight">
            My<span class="text-brand">Fantasy</span>
          </span>
        </NuxtLink>

        <button class="btn-brand" :disabled="state.loading" @click="run">
          <span
            v-if="state.loading"
            class="h-4 w-4 animate-spin rounded-full border-2 border-white/40 border-t-white"
          />
          <span v-else>↻</span>
          {{ state.loading ? 'Sincronizando…' : 'Sincronizar' }}
        </button>
      </div>

      <!-- Pestañas -->
      <nav class="mx-auto max-w-5xl px-2">
        <div class="flex gap-1 overflow-x-auto pb-1">
          <NuxtLink
            v-for="tab in tabs"
            :key="tab.to"
            :to="tab.to"
            class="flex shrink-0 items-center gap-2 border-b-2 border-transparent px-4 py-2.5 text-sm font-semibold text-muted transition hover:text-white"
            exact-active-class="!border-brand !text-white"
          >
            <span>{{ tab.icon }}</span>
            <span>{{ tab.label }}</span>
          </NuxtLink>
        </div>
      </nav>
    </header>

    <!-- Aviso del último sync -->
    <div
      v-if="syncSummary"
      class="mx-auto mt-3 w-full max-w-5xl px-4"
    >
      <div
        class="rounded-xl border px-3 py-2 text-sm"
        :class="{
          'border-up/20 bg-up/10 text-up': syncSummary.tone === 'ok',
          'border-gold/20 bg-gold/10 text-gold': syncSummary.tone === 'warn',
          'border-down/20 bg-down/10 text-down': syncSummary.tone === 'error'
        }"
      >
        {{ syncSummary.text }}
      </div>
    </div>

    <!-- Contenido -->
    <main class="mx-auto w-full max-w-5xl flex-1 px-4 py-5">
      <NuxtPage />
    </main>

    <footer class="mx-auto w-full max-w-5xl px-4 py-6 text-center text-xs text-muted">
      MyFantasy · seguimiento personal de precios y trading
    </footer>

    <LoginModal />
    <PlayerDetailModal />
  </div>
</template>
