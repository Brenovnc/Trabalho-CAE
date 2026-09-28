import { useEffect, useState } from 'react'
import { ApiRequestError } from '../../services/authApi'
import { studyApi } from '../../services/studyApi'
import type { StudentModule } from '../../types/learning'
import styles from './StudentPages.module.css'

type Props = { onNavigate: (path: string, replace?: boolean) => void; onLogout: () => void; onUnauthorized: () => void }
export function StudentModulesPage({ onNavigate, onLogout, onUnauthorized }: Props) {
  const [modules, setModules] = useState<StudentModule[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  useEffect(() => {
    let active = true
    studyApi.listModules().then(result => { if (active) setModules(result) })
      .catch(cause => {
        if (!active) return
        if (cause instanceof ApiRequestError && cause.status === 401) onUnauthorized()
        else setError(errorMessage(cause))
      })
      .finally(() => { if (active) setLoading(false) })
    return () => { active = false }
  }, [onUnauthorized])

  return <main className={styles.page}>
    <header className={styles.header}><div><p className={styles.eyebrow}>Área do aluno</p><h1>Escolha um módulo</h1></div><button type="button" onClick={onLogout}>Sair</button></header>
    {loading && <p role="status">Carregando módulos…</p>}
    {error && <p role="alert">{error}</p>}
    {!loading && !error && modules.length === 0 && <section className={styles.card}><h2>Nenhum módulo disponível</h2><p>Quando sua turma tiver um módulo publicado, ele aparecerá aqui.</p></section>}
    <div className={styles.moduleGrid}>
      {modules.map(module => <article className={styles.card} key={module.id}>
        <p className={styles.subject}>{module.subject}</p><h2>{module.title}</h2>
        {module.description && <p>{module.description}</p>}
        <button type="button" onClick={() => onNavigate(module.activeSessionId ? `/student/sessions/${module.activeSessionId}` : `/student/modules/${module.id}`)}>
          {module.activeSessionId ? 'Retomar sessão' : 'Abrir módulo'}
        </button>
      </article>)}
    </div>
  </main>
}
export function errorMessage(cause: unknown) { return cause instanceof Error ? cause.message : 'Não foi possível carregar os módulos.' }

