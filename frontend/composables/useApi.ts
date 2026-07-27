// Cliente ligero contra el backend C# (http://localhost:5298 por defecto).
export const useApi = () => {
  const base = useRuntimeConfig().public.apiBase

  const request = <T>(path: string, opts: any = {}) =>
    $fetch<T>(path, { baseURL: base, ...opts })

  return {
    base,
    get: <T>(path: string) => request<T>(path, { method: 'GET' }),
    post: <T>(path: string, body?: any) => request<T>(path, { method: 'POST', body }),
    put: <T>(path: string, body?: any) => request<T>(path, { method: 'PUT', body })
  }
}
