<script setup lang="ts">
const { needsLogin, submitting, errorMsg, reset, submit } = useAuthGate()

// refresh_token (recomendado, renueva solo ~1h) o id_token (parche, caduca ~1h).
const mode = ref<'refresh' | 'bearer'>('refresh')
const token = ref('')
const showHelp = ref(false)

const canSubmit = computed(() => token.value.trim().length > 20 && !submitting.value)

const onSubmit = async () => {
  const value = token.value.trim()
  if (!value) return
  const ok = await submit(
    mode.value === 'refresh' ? { refreshToken: value } : { bearerToken: value }
  )
  if (ok) token.value = ''
}
</script>

<template>
  <Teleport to="body">
    <div
      v-if="needsLogin"
      class="fixed inset-0 z-50 flex items-center justify-center bg-ink-950/80 p-4 backdrop-blur"
    >
      <div class="panel w-full max-w-lg p-6">
        <div class="mb-4 flex items-start justify-between gap-4">
          <div>
            <h2 class="text-lg font-extrabold text-white">Sesión de LaLiga caducada</h2>
            <p class="mt-1 text-sm text-muted">
              El acceso automático dejó de ser válido. Pega un token nuevo para continuar.
            </p>
          </div>
          <button
            class="rounded-lg px-2 py-1 text-muted hover:text-white"
            title="Cerrar (seguir con datos en caché)"
            @click="reset"
          >✕</button>
        </div>

        <!-- Tipo de token -->
        <div class="mb-3 flex gap-2">
          <button
            type="button"
            class="flex-1 rounded-lg border px-3 py-2 text-sm font-semibold transition"
            :class="mode === 'refresh'
              ? 'border-brand bg-brand/10 text-white'
              : 'border-white/10 text-muted hover:text-white'"
            @click="mode = 'refresh'"
          >
            refresh_token
            <span class="block text-[11px] font-normal opacity-70">recomendado · se renueva solo</span>
          </button>
          <button
            type="button"
            class="flex-1 rounded-lg border px-3 py-2 text-sm font-semibold transition"
            :class="mode === 'bearer'
              ? 'border-brand bg-brand/10 text-white'
              : 'border-white/10 text-muted hover:text-white'"
            @click="mode = 'bearer'"
          >
            id_token
            <span class="block text-[11px] font-normal opacity-70">parche rápido · caduca ~1h</span>
          </button>
        </div>

        <textarea
          v-model="token"
          rows="4"
          spellcheck="false"
          placeholder="Pega aquí el token…"
          class="w-full resize-none rounded-lg border border-white/10 bg-ink-950 px-3 py-2 font-mono text-xs text-white outline-none focus:border-brand"
          @keydown.ctrl.enter="onSubmit"
        />

        <p v-if="errorMsg" class="mt-2 text-sm text-down">{{ errorMsg }}</p>

        <div class="mt-4 flex items-center justify-between gap-3">
          <button
            type="button"
            class="text-xs text-muted underline underline-offset-2 hover:text-white"
            @click="showHelp = !showHelp"
          >
            ¿Cómo consigo el token?
          </button>
          <button class="btn-brand" :disabled="!canSubmit" @click="onSubmit">
            <span
              v-if="submitting"
              class="h-4 w-4 animate-spin rounded-full border-2 border-white/40 border-t-white"
            />
            {{ submitting ? 'Validando…' : 'Iniciar sesión' }}
          </button>
        </div>

        <div
          v-if="showHelp"
          class="mt-4 space-y-1 rounded-lg border border-white/10 bg-ink-950/60 p-3 text-xs text-muted"
        >
          <p>Como tu cuenta entra con Google, no se puede automatizar el login. Captura el token de una sesión real:</p>
          <ol class="list-decimal space-y-1 pl-4">
            <li>Abre la web/app de LaLiga Fantasy ya logueado con tu cuenta de Google.</li>
            <li>DevTools (F12) → pestaña <b>Network</b> → filtra por <code>token</code> o <code>llt-services</code>.</li>
            <li>Copia el <code>refresh_token</code> (recomendado) o el <code>id_token</code> de la respuesta.</li>
            <li>Pégalo arriba. Se guarda cifrado; la contraseña de Google nunca se usa ni se guarda.</li>
          </ol>
        </div>
      </div>
    </div>
  </Teleport>
</template>
