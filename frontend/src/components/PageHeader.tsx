import type { ReactNode } from 'react'
import styles from './PageHeader.module.css'

type Props = {
  eyebrow: string
  title: ReactNode
  description?: string
  actions?: ReactNode
}

export function PageHeader({ eyebrow, title, description, actions }: Props) {
  return <header className={styles.header}>
    <div className={styles.copy}>
      <p className={styles.eyebrow}>{eyebrow}</p>
      <h1 className={styles.title}>{title}</h1>
      {description && <p className={styles.description}>{description}</p>}
    </div>
    {actions && <div className={styles.actions}>{actions}</div>}
  </header>
}
