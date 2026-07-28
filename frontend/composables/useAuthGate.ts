import { ref } from 'vue'

// Estado global de la sesión con LaLiga. Se guarda a nivel de módulo (singleton)
// y NO con useState a propósito: el interceptor 401 de $fetch corre fuera del
// contexto de un componente, donde useState/useNuxtApp no están disponibles.
// Solo se muta en cliente (interceptor o comprobación al arrancar), así que en
// SSR permanece en false y no hay desajuste de hidratación.
const needsLogin = ref(false)
const submitting = ref(false)
const errorMsg = ref<string | null>(null)

/**
 * Puerta de re-login (Opción B): cuando el backend responde 401 { needsLogin:true }
 * porque el refresh_token de LaLiga caducó, se abre un modal para pegar un token
 * recién capturado. El backend lo valida, lo guarda cifrado y la app sigue.
 */
export const useAuthGate = () => {
  const trigger = () => { needsLogin.value = true }
  const reset = () => { needsLogin.value = false; errorMsg.value = null }

  const submit = async (payload: { refreshToken?: string; bearerToken?: string }) => {
    submitting.value = true
    errorMsg.value = null
    try {
      const base = useRuntimeConfig().public.apiBase
      // $fetch directo (sin el interceptor de useApi) para tratar aquí su propio 401.
      await $fetch('/api/auth/login', { baseURL: base, method: 'POST', body: payload })
      needsLogin.value = false
      await refreshNuxtData() // repinta las vistas con la sesión restaurada
      return true
    } catch (e: any) {
      errorMsg.value = e?.data?.error || e?.message || 'No se pudo validar el token.'
      return false
    } finally {
      submitting.value = false
    }
  }

  return { needsLogin, submitting, errorMsg, trigger, reset, submit }
}
