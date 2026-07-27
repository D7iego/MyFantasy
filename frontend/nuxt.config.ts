// https://nuxt.com/docs/api/configuration/nuxt-config
export default defineNuxtConfig({
  compatibilityDate: '2025-01-01',
  devtools: { enabled: false },
  modules: ['@nuxtjs/tailwindcss'],
  css: ['~/assets/css/main.css'],
  ssr: true,
  app: {
    head: {
      title: 'MyFantasy',
      meta: [
        { name: 'viewport', content: 'width=device-width, initial-scale=1' },
        { name: 'description', content: 'Seguimiento de precios y trading de LaLiga Fantasy' }
      ],
      link: [
        {
          rel: 'preconnect',
          href: 'https://fonts.googleapis.com'
        },
        {
          rel: 'stylesheet',
          href: 'https://fonts.googleapis.com/css2?family=Sora:wght@400;500;600;700;800&display=swap'
        }
      ]
    }
  },
  runtimeConfig: {
    public: {
      // URL del backend C#. Sobrescribible con NUXT_PUBLIC_API_BASE.
      apiBase: 'http://localhost:5298'
    }
  }
})
