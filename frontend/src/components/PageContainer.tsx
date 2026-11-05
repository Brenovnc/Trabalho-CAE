import type { ReactNode } from 'react'
import styles from './PageContainer.module.css'

type Props = { children: ReactNode; className?: string }

export function PageContainer({ children, className }: Props) {
  return <main className={`${styles.container}${className ? ` ${className}` : ''}`}>{children}</main>
}
