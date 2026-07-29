// Cliente ligero contra el backend C# (http://localhost:5298 por defecto).
export const useApi = () => {
  const base = useRuntimeConfig().public.apiBase
  const { trigger } = useAuthGate()

  const request = <T>(path: string, opts: any = {}) =>
    $fetch<T>(path, {
      baseURL: base,
      ...opts,
      // Interceptor global: un 401 { needsLogin:true } (refresh caducado) abre el
      // modal de re-login sin romper la llamada que lo provocó.
      onResponseError({ response }: any) {
        if (response?.status === 401 && response?._data?.needsLogin) {
          trigger()
        }
      }
    })

  return {
    base,
    get: <T>(path: string) => request<T>(path, { method: 'GET' }),
    post: <T>(path: string, body?: any) => request<T>(path, { method: 'POST', body }),
    put: <T>(path: string, body?: any) => request<T>(path, { method: 'PUT', body }),
    delete: <T>(path: string) => request<T>(path, { method: 'DELETE' })
  }
}
