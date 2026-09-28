import { useCallback, useEffect, useState } from 'react'
import { AuthForm } from './pages/auth/AuthForm'
import { ModulesPage } from './pages/modules/ModulesPage'
import { ClassroomsPage } from './pages/classrooms/ClassroomsPage'
import { StudentModulePage } from './pages/student/StudentModulePage'
import { StudentModulesPage } from './pages/student/StudentModulesPage'
import { StudySessionPage } from './pages/student/StudySessionPage'
import { TeacherLayout, type TeacherSection } from './layouts/TeacherLayout'
import { TeacherDashboardPage } from './pages/dashboard/TeacherDashboardPage'
import { TeacherAccountPage } from './pages/dashboard/TeacherAccountPage'
import { ClassroomProgressPage } from './pages/classrooms/ClassroomProgressPage'
import { StudentProgressPage } from './pages/classrooms/StudentProgressPage'
import { ApiRequestError, getCurrentUser, logout, prepareCsrfToken, submitAuth } from './services/authApi'
import type { AuthenticatedUser, AuthMode } from './types/auth'
import styles from './App.module.css'

function App() {
  const [user, setUser] = useState<AuthenticatedUser | null>(null)
  const [ready, setReady] = useState(false)
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
    navigate(authenticatedUser.role === 'STUDENT' ? '/student' : '/teacher/dashboard')
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
    const moduleMatch = /^\/student\/modules\/([^/?]+)(?:\?.*)?$/.exec(pathname)
    const sessionMatch = /^\/student\/sessions\/([^/]+)$/.exec(pathname)
    if (moduleMatch) return <StudentModulePage moduleId={moduleMatch[1]} startFreePracticeRequested={pathname.includes('practice=free')} onNavigate={navigate} onLogout={() => void handleLogout()} onUnauthorized={handleUnauthorized} />
    if (sessionMatch) return <StudySessionPage sessionId={sessionMatch[1]} onNavigate={navigate} onLogout={() => void handleLogout()} onUnauthorized={handleUnauthorized} />
    if (pathname !== '/student') navigate('/student', true)
    return <StudentModulesPage onNavigate={navigate} onLogout={() => void handleLogout()} onUnauthorized={handleUnauthorized} />
  }

  if (ready && user?.role === 'TEACHER') {
    const studentProgressMatch = /^\/teacher\/classrooms\/([^/]+)\/students\/([^/]+)\/progress$/.exec(pathname)
    const classroomProgressMatch = /^\/teacher\/classrooms\/([^/]+)\/progress$/.exec(pathname)
    const section: TeacherSection = pathname.startsWith('/teacher/modules') ? 'modules'
      : pathname.startsWith('/teacher/classrooms') ? 'classrooms'
        : pathname.startsWith('/teacher/account') ? 'account' : 'dashboard'
    if (!pathname.startsWith('/teacher/')) navigate('/teacher/dashboard', true)
    let page
    if (studentProgressMatch) page = <StudentProgressPage classroomId={studentProgressMatch[1]} studentId={studentProgressMatch[2]} onBack={() => navigate('/teacher/classrooms/' + studentProgressMatch[1] + '/progress')} />
    else if (classroomProgressMatch) page = <ClassroomProgressPage classroomId={classroomProgressMatch[1]} onBack={() => navigate('/teacher/classrooms')} onStudent={studentId => navigate('/teacher/classrooms/' + classroomProgressMatch[1] + '/students/' + studentId + '/progress')} />
    else if (section === 'modules') page = <ModulesPage />
    else if (section === 'classrooms') page = <ClassroomsPage onProgress={classroomId => navigate('/teacher/classrooms/' + classroomId + '/progress')} />
    else if (section === 'account') page = <TeacherAccountPage user={user} onLogout={() => void handleLogout()} />
    else page = <TeacherDashboardPage onNavigate={navigate} />
    return <TeacherLayout active={section} user={user} onNavigate={navigate}>{page}</TeacherLayout>
  }

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
