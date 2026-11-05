import { useSyncExternalStore } from 'react'

export type Theme = 'light' | 'dark'
const storageKey = 'pere-theme'
const eventName = 'pere-theme-change'

function resolveTheme(): Theme {
  const current = document.documentElement.dataset.theme
  if (current === 'light' || current === 'dark') return current

  try {
    const saved = window.localStorage.getItem(storageKey)
    if (saved === 'light' || saved === 'dark') return saved
  } catch { /* Use the system preference when storage is unavailable. */ }

  if (typeof window.matchMedia === 'function') {
    return window.matchMedia('(prefers-color-scheme: light)').matches ? 'light' : 'dark'
  }
  return 'dark'
}

function subscribe(listener: () => void) {
  window.addEventListener(eventName, listener)
  window.addEventListener('storage', listener)
  return () => {
    window.removeEventListener(eventName, listener)
    window.removeEventListener('storage', listener)
  }
}

function getSnapshot(): Theme { return resolveTheme() }

export function setTheme(theme: Theme) {
  document.documentElement.dataset.theme = theme
  document.querySelector<HTMLMetaElement>('meta[name="theme-color"]')?.setAttribute('content', theme === 'dark' ? '#111111' : '#F7F5F1')
  try { window.localStorage.setItem(storageKey, theme) } catch { /* Theme still changes for this page view. */ }
  window.dispatchEvent(new Event(eventName))
}

export function useTheme() {
  const theme = useSyncExternalStore(subscribe, getSnapshot, () => 'dark')
  return { theme, setTheme }
}
