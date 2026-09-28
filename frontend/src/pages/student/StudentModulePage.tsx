import { useEffect, useRef, useState } from 'react'
import { ApiRequestError } from '../../services/authApi'
import { studyApi } from '../../services/studyApi'
import type { StudentModule } from '../../types/learning'
import styles from './StudentPages.module.css'
import { errorMessage } from './StudentModulesPage'

type Props = { moduleId: string; onNavigate: (path: string, replace?: boolean) => void; onLogout: () => void; onUnauthorized: () => void }
export function StudentModulePage({ moduleId, onNavigate, onLogout, onUnauthorized }: Props) {
  const [module, setModule] = useState<StudentModule | null>(null)
  const [loading, setLoading] = useState(true)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const startLock = useRef(false)
  useEffect(() => {
    let active = true
    studyApi.getModule(moduleId).then(result => { if (active) setModule(result) })
      .catch(cause => {
        if (!active) return
        if (cause instanceof ApiRequestError && cause.status === 401) onUnauthorized()
        else setError(cause instanceof ApiRequestError && cause.status === 404 ? 'Este módulo não está disponível para sua turma.' : errorMessage(cause))
      })
      .finally(() => { if (active) setLoading(false) })
    return () => { active = false }
  }, [moduleId, onUnauthorized])

  async function begin() {
    if (startLock.current) return
    startLock.current = true
    setBusy(true); setError('')
    try {
      const session = await studyApi.startSession(moduleId)
      onNavigate(`/student/sessions/${session.sessionId}`)
    } catch (cause) {
      if (cause instanceof ApiRequestError && cause.status === 401) onUnauthorized()
      else setError(cause instanceof ApiRequestError && cause.status === 404 ? 'Este módulo não está mais disponível.' : errorMessage(cause))
    } finally { startLock.current = false; setBusy(false) }
  }

  return <main className={styles.page}>
    <header className={styles.header}><button type="button" onClick={() => onNavigate('/student')}>← Módulos</button><button type="button" onClick={onLogout}>Sair</button></header>
    {loading && <p role="status">Carregando módulo…</p>}
    {error && <p role="alert">{error}</p>}
    {module && <section className={styles.card}>
      <p className={styles.subject}>{module.subject}</p><h1>{module.title}</h1>
      {module.description && <p>{module.description}</p>}
      {module.activeSessionId && <p role="status">Há uma sessão em andamento neste módulo.</p>}
      <button type="button" disabled={busy} onClick={() => void begin()}>{busy ? 'Abrindo sessão…' : module.activeSessionId ? 'Retomar sessão' : 'Iniciar sessão'}</button>
    </section>}
  </main>
}

