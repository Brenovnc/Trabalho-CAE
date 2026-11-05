import type { ReactNode } from 'react'
import styles from './EmptyState.module.css'

export function EmptyState({ children, className }: { children: ReactNode; className?: string }) {
  return <div className={[styles.emptyState, className].filter(Boolean).join(' ')}>
    <span className={styles.icon} aria-hidden="true">
      <svg viewBox="0 0 24 24" focusable="false">
        <path d="M4 7.5h16v12H4zM8 7.5V5h8v2.5M8 12h8m-8 3.5h5" />
      </svg>
    </span>
    <div>{children}</div>
  </div>
}
