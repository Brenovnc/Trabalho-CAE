import { useEffect, useState } from 'react'
import { ApiRequestError } from '../../services/authApi'
import { studyApi } from '../../services/studyApi'
import type { StudentCrosswordEntry } from '../../types/learning'
import { CrosswordGame } from '../../minigames/crossword'
import styles from './StudentPages.module.css'
import { errorMessage } from './StudentModulesPage'

type Props = { moduleId: string; allWords: boolean; onNavigate: (path: string) => void; onUnauthorized: () => void }
export function StudentCrosswordPage({ moduleId, allWords, onNavigate, onUnauthorized }: Props) {
  const [entries, setEntries] = useState<StudentCrosswordEntry[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  useEffect(() => {
    let active = true
    studyApi.getCrossword(moduleId, allWords).then(result => { if (active) setEntries(result) })
      .catch(cause => {
        if (!active) return
        if (cause instanceof ApiRequestError && cause.status === 401) onUnauthorized()
        else setError(errorMessage(cause))
      })
      .finally(() => { if (active) setLoading(false) })
    return () => { active = false }
  }, [moduleId, allWords, onUnauthorized])

  const back = () => onNavigate('/student/modules/' + moduleId)
  const showGame = !loading && !error && entries.length >= 2
  return <main className={styles.page}>
    <header className={styles.header}><button type="button" onClick={back}>Voltar ao módulo</button></header>
    <section className={styles.card} aria-labelledby="crossword-title">
      {!showGame && <h1 id="crossword-title">Cruzadinha</h1>}
      {loading && <p role="status">Carregando cruzadinha…</p>}
      {error && <p role="alert">{error}</p>}
      {!loading && !error && entries.length < 2 && <div className={styles.crosswordEmpty}>
        <p>Ainda não há palavras suficientes para montar a cruzadinha.</p>
        <p>{allWords ? 'O professor ainda não cadastrou palavras suficientes para este módulo.' : 'É preciso dominar mais conceitos que tenham palavras cadastradas para a cruzadinha.'}</p>
        <button type="button" onClick={back}>Voltar ao módulo</button>
      </div>}
      {showGame && <CrosswordGame entries={entries} onNew={() => {}} onBack={back} title={<h1 id="crossword-title">Cruzadinha</h1>} />}
    </section>
  </main>
}
