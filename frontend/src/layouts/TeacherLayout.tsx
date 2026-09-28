import type { ReactNode } from 'react'
import type { AuthenticatedUser } from '../types/auth'
import styles from './TeacherLayout.module.css'

export type TeacherSection = 'dashboard' | 'modules' | 'classrooms' | 'account'
type Props = { active: TeacherSection; user: AuthenticatedUser; onNavigate: (path: string) => void; children: ReactNode }
const links: { section: TeacherSection; label: string; path: string }[] = [
  { section: 'dashboard', label: 'Dashboard', path: '/teacher/dashboard' },
  { section: 'modules', label: 'Módulos', path: '/teacher/modules' },
  { section: 'classrooms', label: 'Turmas', path: '/teacher/classrooms' },
  { section: 'account', label: 'Conta', path: '/teacher/account' },
]

export function TeacherLayout({ active, user, onNavigate, children }: Props) {
  return <div className={styles.shell}>
    <header className={styles.header}>
      <button className={styles.brand} type="button" onClick={() => onNavigate('/teacher/dashboard')} aria-label="Plataforma CAE, dashboard">
        <span className={styles.brandMark} aria-hidden="true">C</span><span><strong>CAE</strong><small>Área do professor</small></span>
      </button>
      <nav className={styles.nav} aria-label="Navegação principal">
        {links.map(link => <button key={link.section} type="button" aria-current={active === link.section ? 'page' : undefined}
          className={active === link.section ? styles.active : ''} onClick={() => onNavigate(link.path)}>{link.label}</button>)}
      </nav>
      <div className={styles.user}><span className={styles.avatar} aria-hidden="true">{(user.name?.trim()[0] || 'P').toLocaleUpperCase('pt-BR')}</span><span>{user.name || 'Professor'}</span></div>
    </header>
    <div className={styles.content}>{children}</div>
  </div>
}
