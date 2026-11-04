import { useEffect, useState } from 'react'

const STORAGE_KEY = 'pere.theme'
type Theme = 'light' | 'dark'

export function ThemeToggle() {
  const [theme, setTheme] = useState<Theme>(() => {
    try {
      const stored = window.localStorage.getItem(STORAGE_KEY)
      if (stored === 'light' || stored === 'dark') return stored
    } catch {}
    return window.matchMedia('(prefers-color-scheme: light)').matches ? 'light' : 'dark'
  })

  useEffect(() => {
    document.documentElement.dataset.theme = theme
    try { window.localStorage.setItem(STORAGE_KEY, theme) } catch {}
  }, [theme])

  return <button type="button" className="themeToggle" onClick={() => setTheme(value => value === 'dark' ? 'light' : 'dark')} aria-label={`Alternar para o tema ${theme === 'dark' ? 'claro' : 'escuro'}`} title={`Tema ${theme === 'dark' ? 'escuro' : 'claro'}`}>
    <span aria-hidden="true">{theme === 'dark' ? '☼' : '☾'}</span>
    <span>{theme === 'dark' ? 'Modo claro' : 'Modo escuro'}</span>
  </button>
}
