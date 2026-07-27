// Formateadores compartidos: dinero en euros y deltas con signo/color.
export const useFormat = () => {
  const eur = (value?: number | null, opts: { compact?: boolean } = {}) => {
    if (value === null || value === undefined) return '—'
    if (opts.compact) {
      const abs = Math.abs(value)
      if (abs >= 1_000_000) return `${(value / 1_000_000).toFixed(1)}M €`
      if (abs >= 1_000) return `${Math.round(value / 1_000)}K €`
    }
    return new Intl.NumberFormat('es-ES').format(value) + ' €'
  }

  const signed = (value?: number | null, opts: { compact?: boolean } = {}) => {
    if (value === null || value === undefined) return '—'
    const sign = value > 0 ? '+' : value < 0 ? '−' : ''
    return sign + eur(Math.abs(value), opts)
  }

  const deltaClass = (value?: number | null) => {
    if (value === null || value === undefined || value === 0) return 'text-muted'
    return value > 0 ? 'text-up' : 'text-down'
  }

  const date = (value?: string | null) => {
    if (!value) return '—'
    const d = new Date(value)
    if (Number.isNaN(d.getTime())) return String(value)
    return d.toLocaleDateString('es-ES', { day: '2-digit', month: 'short', year: 'numeric' })
  }

  return { eur, signed, deltaClass, date }
}
