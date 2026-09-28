import type { AuthenticatedUser } from '../../types/auth'
import styles from './TeacherAccountPage.module.css'

export function TeacherAccountPage({ user, onLogout }: { user: AuthenticatedUser; onLogout: () => void }) {
  return <main className={styles.page}><p className={styles.eyebrow}>Perfil</p><h1>Conta</h1><section className={styles.card} aria-labelledby='account-title'>
    <div className={styles.avatar} aria-hidden='true'>{(user.name?.trim()[0] || 'P').toLocaleUpperCase('pt-BR')}</div><div className={styles.identity}><h2 id='account-title'>{user.name || 'Professor'}</h2><p>Conta de professor</p></div>
    <dl><dt>Nome</dt><dd>{user.name || '—'}</dd><dt>E-mail</dt><dd>{user.email || '—'}</dd><dt>Perfil</dt><dd>Professor</dd></dl>
    <button type='button' onClick={onLogout}>Sair da conta</button>
  </section></main>
}
