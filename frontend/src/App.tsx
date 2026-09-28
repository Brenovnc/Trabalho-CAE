import { useCallback, useEffect, useState } from 'react'
import { AuthForm } from './pages/auth/AuthForm'
import { ModulesPage } from './pages/modules/ModulesPage'
import { ClassroomsPage } from './pages/classrooms/ClassroomsPage'
import { StudentModulePage } from './pages/student/StudentModulePage'
import { StudentModulesPage } from './pages/student/StudentModulesPage'
import { StudySessionPage } from './pages/student/StudySessionPage'
import { ApiRequestError, getCurrentUser, logout, prepareCsrfToken, submitAuth } from './services/authApi'
import type { AuthenticatedUser, AuthMode } from './types/auth'
import styles from './App.module.css'

function App() {
  const [user, setUser] = useState<AuthenticatedUser | null>(null)
  const [ready, setReady] = useState(false)
  const [teacherArea, setTeacherArea] = useState<'modules' | 'classrooms'>('modules')
  const [notice, setNotice] = useState('')
  const [pathname, setPathname] = useState(window.location.pathname)

  const navigate = useCallback((path: string, replace = false) => {
    if (replace) window.history.replaceState(null, '', path)
    else window.history.pushState(null, '', path)
    setPathname(path)
  }, [])

  useEffect(() => {
    const handlePopState = () => setPathname(window.location.pathname)
    window.addEventListener('popstate', handlePopState)
    return () => window.removeEventListener('popstate', handlePopState)
  }, [])

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
    navigate(authenticatedUser.role === 'STUDENT' ? '/student' : '/')
    setNotice('Autenticação concluída.')
  }

  async function handleLogout() {
    try {
      await logout()
      setUser(null)
      navigate('/')
      setNotice('Sessão encerrada.')
    } catch (error) {
      setNotice(error instanceof Error ? error.message : 'Não foi possível encerrar a sessão.')
    }
  }

  const handleUnauthorized = useCallback(() => {
    setUser(null)
    setNotice('Sua sessão expirou. Entre novamente.')
  }, [navigate])

  if (ready && user?.role === 'STUDENT') {
    const moduleMatch = /^\/student\/modules\/([^/]+)$/.exec(pathname)
    const sessionMatch = /^\/student\/sessions\/([^/]+)$/.exec(pathname)
    if (moduleMatch) return <StudentModulePage moduleId={moduleMatch[1]} onNavigate={navigate} onLogout={() => void handleLogout()} onUnauthorized={handleUnauthorized} />
    if (sessionMatch) return <StudySessionPage sessionId={sessionMatch[1]} onNavigate={navigate} onLogout={() => void handleLogout()} onUnauthorized={handleUnauthorized} />
    if (pathname !== '/student') navigate('/student', true)
    return <StudentModulesPage onNavigate={navigate} onLogout={() => void handleLogout()} onUnauthorized={handleUnauthorized} />
  }

  if (ready && user?.role === 'TEACHER') return teacherArea === 'modules'
    ? <ModulesPage onLogout={() => void handleLogout()} onClassrooms={() => setTeacherArea('classrooms')} />
    : <ClassroomsPage onLogout={() => void handleLogout()} onModules={() => setTeacherArea('modules')} />

  return (
    <main className={styles.page}>
      <section className={styles.card}>
        <p className={styles.eyebrow}>Autenticação</p>
        <h1>Plataforma Educacional</h1>
        <p className={styles.description}>Entre para acessar seus módulos e atividades.</p>
        {notice && <p className={styles.notice} role="status">{notice}</p>}
        {!ready ? <p role="status">Conectando à API…</p> : <AuthForm initialMode={pathname.startsWith('/student') ? 'student-login' : 'teacher-login'} onSubmit={handleSubmit} />}
      </section>
    </main>
  )
}

export default App
