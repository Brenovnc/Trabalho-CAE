import { useEffect, useState } from 'react'
import { AuthForm } from './pages/auth/AuthForm'
import { ModulesPage } from './pages/modules/ModulesPage'
import { ClassroomsPage } from './pages/classrooms/ClassroomsPage'
import { ApiRequestError, getCurrentUser, logout, prepareCsrfToken, submitAuth } from './services/authApi'
import type { AuthenticatedUser, AuthMode } from './types/auth'
import styles from './App.module.css'

function App() {
  const [user, setUser] = useState<AuthenticatedUser | null>(null)
  const [ready, setReady] = useState(false)
  const [teacherArea, setTeacherArea] = useState<'modules' | 'classrooms'>('modules')
  const [notice, setNotice] = useState('')

  useEffect(() => {
    let active = true
    async function loadIdentity() {
      try {
        await prepareCsrfToken()
        const current = await getCurrentUser()
        if (active) setUser(current)
      } catch (error) {
        if (active && !(error instanceof ApiRequestError && error.status === 401)) {
          setNotice(error instanceof Error ? error.message : 'Não foi possível conectar à API.')
        }
      } finally {
        if (active) setReady(true)
      }
    }
    void loadIdentity()
    return () => { active = false }
  }, [])

  async function handleSubmit(mode: AuthMode, fields: Record<string, string>) {
    const authenticatedUser = await submitAuth(mode, fields)
    setUser(authenticatedUser)
    setNotice('Autenticação concluída.')
  }

  async function handleLogout() {
    try {
      await logout()
      setUser(null)
      setNotice('Sessão encerrada.')
    } catch (error) {
      setNotice(error instanceof Error ? error.message : 'Não foi possível encerrar a sessão.')
    }
  }

  if (ready && user?.role === 'TEACHER') return teacherArea === 'modules'
    ? <ModulesPage onLogout={() => void handleLogout()} onClassrooms={() => setTeacherArea('classrooms')} />
    : <ClassroomsPage onLogout={() => void handleLogout()} onModules={() => setTeacherArea('modules')} />

  return (
    <main className={styles.page}>
      <section className={styles.card}>
        <p className={styles.eyebrow}>Etapa 4 · Autenticação</p>
        <h1>Plataforma Educacional</h1>
        <p className={styles.description}>
          Interface temporária para validar os acessos de professores e alunos.
        </p>
        {notice && <p className={styles.notice} role="status">{notice}</p>}

        {!ready ? (
          <p role="status">Conectando à API…</p>
        ) : user ? (
          <section className={styles.identity} aria-labelledby="identity-title">
            <h2 id="identity-title">Identidade autenticada</h2>
            <dl>
              <dt>Perfil</dt><dd>{user.role}</dd>
              <dt>Nome</dt><dd>{user.name ?? '—'}</dd>
              {user.email && <><dt>E-mail</dt><dd>{user.email}</dd></>}
              {user.classroomId && <><dt>Turma</dt><dd>{user.classroomId}</dd></>}
              {user.enrollmentNumber && <><dt>Matrícula</dt><dd>{user.enrollmentNumber}</dd></>}
              <dt>ID</dt><dd>{user.id}</dd>
            </dl>
            <button className={styles.logout} type="button" onClick={() => void handleLogout()}>
              Sair
            </button>
          </section>
        ) : (
          <AuthForm onSubmit={handleSubmit} />
        )}
      </section>
    </main>
  )
}

export default App
