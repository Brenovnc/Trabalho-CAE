import { useEffect, useRef, useState } from 'react'
import { ApiRequestError } from '../../services/authApi'
import { studyApi } from '../../services/studyApi'
import type { StudentModule } from '../../types/learning'
import styles from './StudentPages.module.css'
import { errorMessage } from './StudentModulesPage'

type Props = { moduleId: string; startFreePracticeRequested?: boolean; onNavigate: (path: string, replace?: boolean) => void; onLogout: () => void; onUnauthorized: () => void }
export function StudentModulePage({ moduleId, startFreePracticeRequested = false, onNavigate, onLogout, onUnauthorized }: Props) {
  const [module, setModule] = useState<StudentModule | null>(null)
  const [loading, setLoading] = useState(true)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [freeChoiceOpen, setFreeChoiceOpen] = useState(false)
  const [selectedConcept, setSelectedConcept] = useState('')
  const [confirmReset, setConfirmReset] = useState(false)
  const startLock = useRef(false)
  const resetLock = useRef(false)
  const resetCancelRef = useRef<HTMLButtonElement>(null)

  async function loadModule() {
    const result = await studyApi.getModule(moduleId)
    setModule(result)
  }
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

  useEffect(() => { if (confirmReset) resetCancelRef.current?.focus() }, [confirmReset])
  useEffect(() => { if (startFreePracticeRequested) setFreeChoiceOpen(true) }, [startFreePracticeRequested])

  async function begin(mode: 'normal' | 'free', conceptId: string | null = null) {
    if (startLock.current) return
    startLock.current = true
    setBusy(true); setError('')
    try {
      const session = mode === 'normal' ? await studyApi.startSession(moduleId) : await studyApi.startFreePractice(moduleId, conceptId)
      onNavigate(`/student/sessions/${session.sessionId}`)
    } catch (cause) {
      if (cause instanceof ApiRequestError && cause.status === 401) onUnauthorized()
      else if (cause instanceof ApiRequestError && cause.status === 404) setError('Este módulo não está mais disponível.')
      else setError(errorMessage(cause))
    } finally { startLock.current = false; setBusy(false) }
  }

  async function resetProgress() {
    if (resetLock.current) return
    resetLock.current = true
    setBusy(true); setError('')
    try {
      await studyApi.resetModuleProgress(moduleId)
      setConfirmReset(false)
      await loadModule()
    } catch (cause) {
      if (cause instanceof ApiRequestError && cause.status === 401) onUnauthorized()
      else setError(errorMessage(cause))
    } finally { resetLock.current = false; setBusy(false) }
  }

  const dateText = module?.nextReviewAtUtc ? formatReviewDate(module.nextReviewAtUtc) : null
  const activeSessionPath = module?.activeSessionId ? `/student/sessions/${module.activeSessionId}` : null
  const hasNormalSession = module?.activeSessionMode === 'NORMAL'
  const hasFreeSession = module?.activeSessionMode === 'FREE_PRACTICE'

  return <main className={styles.page}>
    <header className={styles.header}><button type="button" onClick={() => onNavigate('/student')}>← Módulos</button><button type="button" onClick={onLogout}>Sair</button></header>
    {loading && <p role="status">Carregando módulo…</p>}
    {error && <p role="alert">{error}</p>}
    {module && <>
      <section className={styles.card}>
        <p className={styles.subject}>{module.subject}</p><h1>{module.title}</h1>
        {module.description && <p>{module.description}</p>}
        <section className={styles.progressSummary} aria-labelledby="official-progress-title">
          <h2 id="official-progress-title">Seu progresso oficial</h2>
          <div className={styles.progressTrack} role="progressbar" aria-label="Progresso do módulo" aria-valuemin={0} aria-valuemax={100} aria-valuenow={module.progressPercent}>
            <span style={{ width: `${module.progressPercent}%` }} />
          </div>
          <p className={styles.progressPercent}>{module.progressPercent}% dominado</p>
          <p>{module.masteredConcepts} de {module.activeConcepts} conceitos dominados</p>
          <ul className={styles.progressCounts}>
            <li>{module.learningConcepts} em aprendizagem</li>
            <li>{module.notStartedConcepts} ainda não iniciados</li>
          </ul>
          <div className={styles.reviewSummary}>
            <p>{module.pendingReviews} {module.pendingReviews === 1 ? 'revisão pendente' : 'revisões pendentes'}</p>
            <p>Próxima revisão: {dateText ?? 'nenhuma agendada'}</p>
          </div>
        </section>
        <div className={styles.moduleActions}>
          {activeSessionPath ? <button type="button" disabled={busy} onClick={() => onNavigate(activeSessionPath)}>
            {hasFreeSession ? 'Continuar prática livre' : 'Continuar sessão'}
          </button> : <button type="button" disabled={busy} onClick={() => void begin('normal')}>
            {busy ? 'Abrindo sessão…' : 'Iniciar estudo'}
          </button>}
        </div>
      </section>

      <section className={styles.card} aria-labelledby="free-practice-title">
        <h2 id="free-practice-title">Estudo livre</h2>
        <p>Pratique qualquer conteúdo sem alterar seu progresso oficial.</p>
        {hasNormalSession && <p role="status">Conclua ou abandone sua sessão atual para começar uma prática livre.</p>}
        {hasFreeSession ? <p role="status">A prática livre está em andamento; continue pela ação principal desta página.</p> : <>
          <div className={styles.freePracticeActions}>
            <button type="button" disabled={busy || hasNormalSession || module.concepts.length === 0} onClick={() => void begin('free')}>
              {busy ? 'Abrindo prática…' : 'Praticar módulo inteiro'}
            </button>
            <button type="button" className={styles.secondaryButton} disabled={busy || hasNormalSession || module.concepts.length === 0} onClick={() => setFreeChoiceOpen(open => !open)}>
              Escolher conceito
            </button>
          </div>
          {freeChoiceOpen && <div className={styles.freeChoice}>
            <label htmlFor="free-concept">Conceito para praticar</label>
            <select id="free-concept" value={selectedConcept} onChange={event => setSelectedConcept(event.target.value)}>
              <option value="">Selecione um conceito</option>
              {module.concepts.map(concept => <option key={concept.id} value={concept.id}>{concept.name}</option>)}
            </select>
            <button type="button" disabled={busy || !selectedConcept} onClick={() => void begin('free', selectedConcept)}>Praticar conceito</button>
          </div>}
        </>}
      </section>

      <section className={styles.dangerZone} aria-labelledby="reset-title">
        <h2 id="reset-title">Reiniciar módulo</h2>
        <p>O progresso atual deste módulo voltará ao início. O histórico continuará registrado.</p>
        <button type="button" className={styles.dangerButton} disabled={busy} onClick={() => setConfirmReset(true)}>Reiniciar módulo</button>
      </section>
    </>}
    {confirmReset && <div className={styles.dialogBackdrop}>
      <section role="alertdialog" aria-modal="true" aria-labelledby="reset-confirm-title" aria-describedby="reset-confirm-description" className={styles.confirmDialog}
        onKeyDown={event => {
          if (event.key === 'Escape') { event.preventDefault(); setConfirmReset(false); return }
          if (event.key !== 'Tab') return
          const buttons = event.currentTarget.querySelectorAll<HTMLButtonElement>('button:not(:disabled)')
          const first = buttons[0]
          const last = buttons[buttons.length - 1]
          if (event.shiftKey && document.activeElement === first) { event.preventDefault(); last.focus() }
          else if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first.focus() }
        }}>
        <h2 id="reset-confirm-title">Reiniciar módulo?</h2>
        <p id="reset-confirm-description">Seu progresso atual neste módulo voltará ao início. Suas sessões e atividades anteriores continuarão registradas no histórico. Esta ação não pode ser desfeita.</p>
        <div className={styles.dialogActions}>
          <button ref={resetCancelRef} type="button" className={styles.secondaryButton} disabled={busy} onClick={() => setConfirmReset(false)}>Cancelar</button>
          <button type="button" className={styles.dangerButton} disabled={busy} onClick={() => void resetProgress()}>{busy ? 'Reiniciando…' : 'Reiniciar módulo'}</button>
        </div>
      </section>
    </div>}
  </main>
}

export function formatReviewDate(value: string, now = new Date()) {
  const date = new Date(value)
  const today = new Date(now.getFullYear(), now.getMonth(), now.getDate())
  const reviewDay = new Date(date.getFullYear(), date.getMonth(), date.getDate())
  const days = Math.round((reviewDay.getTime() - today.getTime()) / 86_400_000)
  if (days === 0) return `Hoje às ${new Intl.DateTimeFormat('pt-BR', { hour: '2-digit', minute: '2-digit' }).format(date)}`
  if (days === 1) return 'Amanhã'
  return new Intl.DateTimeFormat('pt-BR', { day: '2-digit', month: '2-digit', year: 'numeric' }).format(date)
}
