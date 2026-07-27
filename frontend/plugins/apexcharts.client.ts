import VueApexCharts from 'vue3-apexcharts'

// ApexCharts solo en cliente (usa window). Registra <apexchart>.
export default defineNuxtPlugin((nuxtApp) => {
  nuxtApp.vueApp.use(VueApexCharts)
})
