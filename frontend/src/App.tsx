import { useCallback, useEffect, useState } from 'react'
import { AuthForm } from './pages/auth/AuthForm'
import { ModulesPage } from './pages/modules/ModulesPage'
import { ClassroomsPage } from './pages/classrooms/ClassroomsPage'
import { StudentModulePage } from './pages/student/StudentModulePage'
import { StudentCrosswordPage } from './pages/student/StudentCrosswordPage'
import { StudentModulesPage } from './pages/student/StudentModulesPage'
import { StudySessionPage } from './pages/student/StudySessionPage'
import { TeacherLayout, type TeacherSection } from './layouts/TeacherLayout'
import { TeacherDashboardPage } from './pages/dashboard/TeacherDashboardPage'
import { TeacherAccountPage } from './pages/dashboard/TeacherAccountPage'
import { TeacherHomePage } from './pages/home/TeacherHomePage'
import { StudentProgressPage } from './pages/classrooms/StudentProgressPage'
import { ApiRequestError, getCurrentUser, logout, prepareCsrfToken, submitAuth } from './services/authApi'
import { teacherProfileApi, type TeacherProfile } from './services/teacherProfileApi'
import type { AuthenticatedUser, AuthMode } from './types/auth'
import { PereLogo } from './components/PereLogo'
import styles from './App.module.css'

function App() {
  const [user, setUser] = useState<AuthenticatedUser | null>(null)
  const [profile, setProfile] = useState<TeacherProfile | null>(null)
  const [ready, setReady] = useState(false)
  const [notice, setNotice] = useState('')
  const [pathname, setPathname] = useState(window.location.pathname + window.location.search)
  const navigate = useCallback((path: string, replace = false) => { if (replace) window.history.replaceState(null, '', path); else window.history.pushState(null, '', path); setPathname(path) }, [])
  useEffect(() => { const pop = () => setPathname(window.location.pathname + window.location.search); window.addEventListener('popstate', pop); return () => window.removeEventListener('popstate', pop) }, [])
  useEffect(() => {
    let active = true
    async function load() { try { await prepareCsrfToken(); const current = await getCurrentUser(); if (active) setUser(current) }
      catch (error) { if (active && !(error instanceof ApiRequestError && error.status === 401)) setNotice(error instanceof Error ? error.message : 'Não foi possível conectar à API.') }
      finally { if (active) setReady(true) } }
    void load(); return () => { active = false }
  }, [])
  useEffect(() => { if (user?.role !== 'TEACHER') { setProfile(null); return } let active = true; teacherProfileApi.get().then(value => { if (active) setProfile(value) }).catch(() => {}); return () => { active = false } }, [user?.id, user?.role])
  async function handleSubmit(mode: AuthMode, fields: Record<string, string>) { const authenticated = await submitAuth(mode, fields); setUser(authenticated); navigate(authenticated.role === 'STUDENT' ? '/student' : '/teacher/home'); setNotice('Autenticação concluída.') }
  async function handleLogout() { try { await logout(); setUser(null); setProfile(null); navigate('/'); setNotice('Sessão encerrada.') } catch (error) { setNotice(error instanceof Error ? error.message : 'Não foi possível encerrar a sessão.') } }
  const handleUnauthorized = useCallback(() => { setUser(null); setProfile(null); setNotice('Sua sessão expirou. Entre novamente.') }, [])

  if (ready && user?.role === 'STUDENT') {
    const crossword = /^\/student\/modules\/([^/]+)\/crossword(?:\?.*)?$/.exec(pathname)
    const moduleMatch = /^\/student\/modules\/([^/?]+)(?:\?.*)?$/.exec(pathname)
    const session = /^\/student\/sessions\/([^/]+)$/.exec(pathname)
    if (crossword) return <StudentCrosswordPage moduleId={crossword[1]} allWords={new URLSearchParams(pathname.split('?')[1] ?? '').get('allWords') === 'true'} onNavigate={navigate} onUnauthorized={handleUnauthorized} />
    if (moduleMatch) return <StudentModulePage moduleId={moduleMatch[1]} startFreePracticeRequested={pathname.includes('practice=free')} onNavigate={navigate} onLogout={() => void handleLogout()} onUnauthorized={handleUnauthorized} />
    if (session) return <StudySessionPage sessionId={session[1]} onNavigate={navigate} onLogout={() => void handleLogout()} onUnauthorized={handleUnauthorized} />
    if (pathname !== '/student') navigate('/student', true)
    return <StudentModulesPage onNavigate={navigate} onLogout={() => void handleLogout()} onUnauthorized={handleUnauthorized} />
  }
  if (ready && user?.role === 'TEACHER') {
    const detail = /^\/teacher\/classrooms\/([^/]+)\/students\/([^/]+)\/progress$/.exec(pathname)
    const oldProgress = /^\/teacher\/classrooms\/([^/]+)\/progress$/.exec(pathname)
    if (oldProgress) { navigate(`/teacher/dashboard?classroom=${encodeURIComponent(oldProgress[1])}`, true); return null }
    const path = pathname.split('?')[0]
    const section: TeacherSection = detail || path === '/teacher/dashboard' ? 'dashboard' : path.startsWith('/teacher/modules') ? 'modules' : path.startsWith('/teacher/classrooms') ? 'classrooms' : path === '/teacher/account' ? 'account' : 'home'
    if (!(detail || ['/teacher/home', '/teacher/dashboard', '/teacher/modules', '/teacher/classrooms', '/teacher/account'].includes(path))) navigate('/teacher/home', true)
    let page
    if (detail) page = <StudentProgressPage classroomId={detail[1]} studentId={detail[2]} onBack={() => navigate(`/teacher/dashboard?classroom=${encodeURIComponent(detail[1])}`)} />
    else if (section === 'modules') page = <ModulesPage />
    else if (section === 'classrooms') page = <ClassroomsPage onProgress={classroomId => navigate(`/teacher/dashboard?classroom=${encodeURIComponent(classroomId)}`)} />
    else if (section === 'account') page = <TeacherAccountPage user={user} profile={profile} onProfileChange={setProfile} onLogout={() => void handleLogout()} />
    else if (section === 'dashboard') page = <TeacherDashboardPage onNavigate={navigate} onStudent={(classroomId, studentId) => navigate(`/teacher/classrooms/${classroomId}/students/${studentId}/progress`)} />
    else page = <TeacherHomePage user={user} onNavigate={navigate} />
    return <TeacherLayout active={section} user={user} profile={profile} onNavigate={navigate} onLogout={() => void handleLogout()}>{page}</TeacherLayout>
  }
  return <main className={styles.page}><section className={styles.card}><PereLogo size={48} /><p className={styles.eyebrow}>PERE</p><h1>Plataforma Educacional de Repetição Espaçada</h1><p className={styles.description}>Entre para acessar seus módulos e atividades.</p>
    {notice && <p className={styles.notice} role="status">{notice}</p>}{!ready ? <p role="status">Conectando à API…</p> : <AuthForm initialMode={pathname.startsWith('/student') ? 'student-login' : 'teacher-login'} onSubmit={handleSubmit} />}</section></main>
}
export default App
