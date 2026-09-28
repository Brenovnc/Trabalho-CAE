import { useCallback, useEffect, useRef, useState } from 'react'
import { ApiRequestError } from '../../services/authApi'
import { studyApi } from '../../services/studyApi'
import type { AnswerResponse, StudentModule, StudyActivity, StudySession } from '../../types/learning'
import { ActivityGame } from '../../minigames/ActivityGame'
import { SessionTimer } from '../../components/SessionTimer'
import styles from './StudySessionPage.module.css'

type Props = { sessionId: string; onNavigate: (path: string) => void; onLogout: () => void; onUnauthorized: () => void }
type Feedback = { response: AnswerResponse; activity: StudyActivity }

export function StudySessionPage({ sessionId, onNavigate, onLogout, onUnauthorized }: Props) {
  const [session, setSession] = useState<StudySession | null>(null)
  const [finalModule, setFinalModule] = useState<StudentModule | null>(null)
  const [feedback, setFeedback] = useState<Feedback | null>(null)
  const [busy, setBusy] = useState(false)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const operationLock = useRef(false)

  const refresh = useCallback(async () => {
    const result = await studyApi.getSession(sessionId)
    setSession(result)
    return result
  }, [sessionId])

  useEffect(() => {
    let active = true
    studyApi.getSession(sessionId).then(result => { if (active) setSession(result) })
      .catch(cause => {
        if (!active) return
        if (cause instanceof ApiRequestError && cause.status === 401) onUnauthorized()
        else setError(readableError(cause))
      })
      .finally(() => { if (active) setLoading(false) })
    return () => { active = false }
  }, [sessionId, onUnauthorized])

  useEffect(() => {
    if (session?.status !== 'COMPLETED' || session.mode === 'FREE_PRACTICE') return
    let active = true
    studyApi.getModule(session.moduleId).then(result => { if (active) setFinalModule(result) })
      .catch(() => { if (active) setNotice('A sessão foi salva; o resumo atualizado estará disponível ao voltar ao módulo.') })
    return () => { active = false }
  }, [session?.status, session?.mode, session?.moduleId])

  async function reveal() {
    if (!session?.activity || operationLock.current) return
    operationLock.current = true
    setBusy(true); setError('')
    try {
      await studyApi.reveal(sessionId, session.activity.presentationId)
      await refresh()
    } catch (cause) {
      if (cause instanceof ApiRequestError && cause.status === 401) onUnauthorized()
      else if (cause instanceof ApiRequestError && cause.status === 409) {
        await recoverConflict('A atividade foi atualizada. Confira o estado atual e tente novamente se necessário.')
      } else setError(readableError(cause))
    } finally { operationLock.current = false; setBusy(false) }
  }

  async function submit(answer: unknown, hintsUsed = 0) {
    const activity = session?.activity
    if (!activity || operationLock.current || feedback) return
    operationLock.current = true
    setBusy(true); setError(''); setNotice('')
    try {
      const response = await studyApi.submit(sessionId, activity.presentationId, activity.type, { answer, attemptsUsed: 1, hintsUsed })
      setSession(current => current ? { ...current, completedActivities: response.completedActivities, totalActivities: response.totalActivities } : current)
      setFeedback({ response, activity })
    } catch (cause) {
      if (cause instanceof ApiRequestError && cause.status === 401) onUnauthorized()
      else if (cause instanceof ApiRequestError && cause.status === 409) {
        await recoverConflict('Esta resposta já foi processada ou a atividade mudou. A sessão foi atualizada.')
      } else setError(readableError(cause))
    } finally { operationLock.current = false; setBusy(false) }
  }

  async function recoverConflict(message: string) {
    try {
      const current = await refresh()
      setFeedback(null)
      setNotice(current.status === 'COMPLETED' ? 'A sessão já foi concluída.' : message)
    } catch (cause) { setError(readableError(cause)) }
  }

  function continueAfterFeedback() {
    if (!feedback) return
    const { response } = feedback
    setFeedback(null)
    setNotice('')
    if (response.sessionStatus === 'COMPLETED') {
      setSession(current => current ? { ...current, status: 'COMPLETED', completedActivities: response.completedActivities, totalActivities: response.totalActivities, activity: null } : current)
      return
    }
    if (response.nextActivity) {
      setSession(current => current ? { ...current, activity: response.nextActivity, status: 'ACTIVE' } : current)
      return
    }
    void refresh().catch(cause => setError(readableError(cause)))
  }

  async function abandon() {
    if (!session || operationLock.current || !window.confirm('Deseja abandonar esta sessão? Suas respostas já enviadas serão mantidas.')) return
    operationLock.current = true
    setBusy(true); setError('')
    try {
      await studyApi.abandon(sessionId)
      onNavigate('/student')
    } catch (cause) {
      if (cause instanceof ApiRequestError && cause.status === 401) onUnauthorized()
      else if (cause instanceof ApiRequestError && cause.status === 409) await recoverConflict('A sessão já mudou de estado. O estado atual foi carregado.')
      else setError(readableError(cause))
    } finally { operationLock.current = false; setBusy(false) }
  }

  if (loading) return <main className={styles.page}><p role="status">Carregando sessão…</p></main>
  if (error && !session) return <main className={styles.page}><p role="alert">{error}</p><button type="button" onClick={() => onNavigate('/student')}>Voltar aos módulos</button></main>
  if (!session) return null
  if (session.status === 'COMPLETED') return <main className={styles.page}>
    <section className={styles.card} aria-labelledby="complete-title"><p className={styles.eyebrow}>Sessão finalizada</p><h1 id="complete-title">Sessão concluída</h1>
      <p>{session.completedActivities} {session.completedActivities === 1 ? 'atividade realizada' : 'atividades realizadas'}.</p>
      {session.mode === 'FREE_PRACTICE' ? <p>A prática livre não alterou seu progresso oficial.</p> : finalModule && <section aria-label="Resumo do progresso atualizado">
        <p>Progresso: {finalModule.progressPercent}%</p>
        <p>{finalModule.masteredConcepts} conceitos dominados · {finalModule.learningConcepts} em aprendizagem</p>
        <p>Próxima revisão: {finalModule.nextReviewAtUtc ? reviewLabel(finalModule.nextReviewAtUtc) : 'nenhuma agendada'}</p>
      </section>}
      <p>Quer continuar praticando?</p>
      <button type="button" onClick={() => onNavigate(`/student/modules/${session.moduleId}`)}>Voltar ao módulo</button>
      <button type="button" onClick={() => onNavigate(`/student/modules/${session.moduleId}?practice=free`)}>Estudar livremente</button>
    </section>
  </main>
  if (session.status === 'ABANDONED') return <main className={styles.page}><section className={styles.card}><h1>Sessão abandonada</h1><p>Suas respostas anteriores continuam salvas.</p><button type="button" onClick={() => onNavigate('/student')}>Voltar aos módulos</button></section></main>

  const activity = session.activity
  return <main className={styles.page}>
    <header className={styles.header}><button type="button" onClick={() => onNavigate('/student')}>← Módulos</button><button type="button" onClick={onLogout}>Sair</button><button type="button" disabled={busy} onClick={() => void abandon()}>Abandonar sessão</button></header>
    <section className={styles.card} aria-labelledby="session-title">
      <div className={styles.sessionTop}><div><p className={styles.eyebrow}>Sessão de estudo</p><h1 id="session-title">{activity?.conceptName ?? 'Módulo de estudo'}</h1></div>{activity && <SessionTimer startedAtUtc={activity.startedAtUtc} />}</div>
      <p className={styles.progress} aria-live="polite">{session.completedActivities + (activity ? 1 : 0)} de {Math.max(session.totalActivities, session.completedActivities + (activity ? 1 : 0))} atividades</p>
      {notice && <p className={styles.notice} role="status">{notice}</p>}
      {error && <p className={styles.error} role="alert">{error}</p>}
      {feedback ? <section className={`${styles.feedback} ${feedback.response.wasCorrect ? styles.correct : styles.incorrect}`} aria-live="polite" aria-labelledby="feedback-title">
        <h2 id="feedback-title">{feedback.response.wasCorrect ? 'Correto' : 'Incorreto'}</h2>
        <p>{feedback.response.feedback}</p>
        {!feedback.response.wasCorrect && feedback.response.correctAnswer != null && <CorrectAnswer activity={feedback.activity} answer={feedback.response.correctAnswer} />}
        <button type="button" disabled={busy} onClick={continueAfterFeedback}>{feedback.response.sessionStatus === 'COMPLETED' ? 'Ver resumo' : 'Continuar'}</button>
      </section> : activity ? <ActivityGame key={activity.presentationId} activity={activity} busy={busy} onReveal={reveal} onSubmit={(answer, hints) => void submit(answer, hints)} /> : <section><h2>Nenhuma atividade elegível</h2><p>Esta sessão terminou sem atividades disponíveis.</p><button type="button" onClick={() => onNavigate('/student')}>Voltar aos módulos</button></section>}
    </section>
  </main>
}

function reviewLabel(value: string) {
  const date = new Date(value)
  const now = new Date()
  if (date.toDateString() === now.toDateString()) return `hoje às ${new Intl.DateTimeFormat('pt-BR', { hour: '2-digit', minute: '2-digit' }).format(date)}`
  const tomorrow = new Date(now.getFullYear(), now.getMonth(), now.getDate() + 1)
  if (date.toDateString() === tomorrow.toDateString()) return 'amanhã'
  return new Intl.DateTimeFormat('pt-BR', { day: '2-digit', month: '2-digit', year: 'numeric' }).format(date)
}

function CorrectAnswer({ activity, answer }: { activity: StudyActivity; answer: unknown }) {
  if (!answer || typeof answer !== 'object') return null
  const value = answer as Record<string, unknown>
  let text = ''
  if (activity.type === 'TRUE_FALSE' && typeof value.choice === 'boolean') text = value.choice ? 'Verdadeiro' : 'Falso'
  else if (activity.type === 'GUESS_CONCEPT' && typeof value.text === 'string') text = value.text
  else if (activity.type === 'FILL_BLANK' && Array.isArray(value.answers)) {
    text = value.answers.map(item => typeof item === 'object' && item !== null && 'text' in item ? String(item.text) : '').filter(Boolean).join(' · ')
  } else if (activity.type === 'ORDERING' && Array.isArray(value.itemIds)) {
    const byId = new Map(activity.payload.items.map(item => [item.id, item.text]))
    text = value.itemIds.map(id => byId.get(String(id))).filter(Boolean).join(' → ')
  }
  const explanation = typeof value.explanation === 'string' ? value.explanation : ''
  return text || explanation ? <div><h3>Resposta esperada</h3>{text && <p>{text}</p>}{explanation && <p>{explanation}</p>}</div> : null
}

export function readableError(cause: unknown) {
  if (cause instanceof ApiRequestError) {
    if (cause.status === 403) return 'Esta área está disponível apenas para alunos autorizados.'
    if (cause.status === 404) return 'A sessão ou o módulo não foi encontrado.'
    if (cause.status === 409) return 'A sessão mudou. Atualize para continuar.'
  }
  return cause instanceof Error ? cause.message : 'Não foi possível comunicar com o servidor.'
}



