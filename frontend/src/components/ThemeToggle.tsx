import { useTheme } from '../hooks/useTheme'
import styles from './ThemeToggle.module.css'

export function ThemeToggle() {
  const { theme, setTheme } = useTheme()
  const nextTheme = theme === 'dark' ? 'light' : 'dark'

  return <button
    className={styles.toggle}
    data-theme-toggle=""
    type="button"
    aria-label={theme === 'dark' ? 'Ativar modo claro' : 'Ativar modo escuro'}
    aria-pressed={theme === 'light'}
    onClick={() => setTheme(nextTheme)}
  >
    <svg aria-hidden="true" viewBox="0 0 24 24" focusable="false">
      {theme === 'dark'
        ? <><circle cx="12" cy="12" r="3.5" /><path d="M12 3v2m0 14v2M5.6 5.6 7 7m10 10 1.4 1.4M3 12h2m14 0h2M5.6 18.4 7 17m10-10 1.4-1.4" /></>
        : <><path d="M20.5 15.5A8.5 8.5 0 0 1 8.5 3.5 8.5 8.5 0 1 0 20.5 15.5Z" /></>}
    </svg>
  </button>
}
