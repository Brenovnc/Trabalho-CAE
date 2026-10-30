import { useEffect, useRef, useState, type ReactNode } from 'react'
import type { AuthenticatedUser } from '../types/auth'
import { PereLogo } from '../components/PereLogo'
import { apiUrl } from '../services/authApi'
import type { TeacherProfile } from '../services/teacherProfileApi'
import styles from './TeacherLayout.module.css'

export type TeacherSection = 'home' | 'dashboard' | 'modules' | 'classrooms' | 'account'
type Props = { active: TeacherSection; user: AuthenticatedUser; profile: TeacherProfile | null; onNavigate: (path: string) => void; onLogout: () => void; children: ReactNode }
const links: { section: TeacherSection; label: string; path: string }[] = [
  { section: 'home', label: 'Página inicial', path: '/teacher/home' },
  { section: 'dashboard', label: 'Dashboard', path: '/teacher/dashboard' },
  { section: 'modules', label: 'Módulos', path: '/teacher/modules' },
  { section: 'classrooms', label: 'Turmas', path: '/teacher/classrooms' },
]

export function TeacherLayout({ active, user, profile, onNavigate, onLogout, children }: Props) {
  const [menuOpen, setMenuOpen] = useState(false)
  const menuRef = useRef<HTMLDivElement>(null)
  const triggerRef = useRef<HTMLButtonElement>(null)
  useEffect(() => {
    if (!menuOpen) return
    const outside = (event: PointerEvent) => { if (!menuRef.current?.contains(event.target as Node)) setMenuOpen(false) }
    const key = (event: KeyboardEvent) => { if (event.key === 'Escape') { setMenuOpen(false); triggerRef.current?.focus() } }
    document.addEventListener('pointerdown', outside); document.addEventListener('keydown', key)
    return () => { document.removeEventListener('pointerdown', outside); document.removeEventListener('keydown', key) }
  }, [menuOpen])
  const photo = profile?.hasPhoto ? apiUrl(`/api/teacher/profile/photo?v=${profile.photoVersion ?? ''}`) : null
  return <div className={styles.shell}><header className={styles.header}>
    <button className={styles.brand} type="button" onClick={() => onNavigate('/teacher/home')} aria-label="PERE, página inicial"><PereLogo /><span><strong>PERE</strong><small>Área do professor</small></span></button>
    <nav className={styles.nav} aria-label="Navegação principal">{links.map(link => <button key={link.section} type="button" aria-current={active === link.section ? 'page' : undefined} className={active === link.section ? styles.active : ''} onClick={() => onNavigate(link.path)}>{link.label}</button>)}</nav>
    <div className={styles.userMenu} ref={menuRef}><button ref={triggerRef} className={styles.user} type="button" aria-haspopup="menu" aria-expanded={menuOpen} onClick={() => setMenuOpen(value => !value)}>
      {photo ? <img className={styles.avatar} src={photo} alt="" /> : <span className={styles.avatar} aria-hidden="true">{(profile?.name || user.name || 'P').trim()[0]?.toLocaleUpperCase('pt-BR')}</span>}
      <span>{profile?.name || user.name || 'Professor'}</span><span aria-hidden="true">⌄</span>
    </button>{menuOpen && <div className={styles.menu} role="menu"><button type="button" role="menuitem" onClick={() => { setMenuOpen(false); onNavigate('/teacher/account') }}>Meu perfil</button><button type="button" role="menuitem" onClick={() => { setMenuOpen(false); onLogout() }}>Sair</button></div>}</div>
  </header><div className={styles.content}>{children}</div></div>
}
