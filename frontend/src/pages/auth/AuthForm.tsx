import { useId, useRef, useState, type FormEvent, type KeyboardEvent } from 'react'
import type { AuthMode } from '../../types/auth'
import styles from './AuthForm.module.css'

type Props = {
  onSubmit: (mode: AuthMode, fields: Record<string, string>) => Promise<void>
  disabled?: boolean
  initialMode?: AuthMode
}

type Field = {
  name: string
  label: string
  type?: string
  autoComplete?: string
  maxLength?: number
  minLength?: number
}

const forms: Record<AuthMode, { title: string; fields: Field[] }> = {
  'teacher-register': {
    title: 'Criar conta de professor',
    fields: [
      { name: 'name', label: 'Nome', autoComplete: 'name', maxLength: 120 },
      { name: 'email', label: 'E-mail', type: 'email', autoComplete: 'email', maxLength: 320 },
      { name: 'password', label: 'Senha', type: 'password', autoComplete: 'new-password', minLength: 8, maxLength: 128 },
      { name: 'confirmPassword', label: 'Confirmar senha', type: 'password', autoComplete: 'new-password', minLength: 8, maxLength: 128 },
    ],
  },
  'teacher-login': {
    title: 'Entrar como professor',
    fields: [
      { name: 'email', label: 'E-mail', type: 'email', autoComplete: 'username', maxLength: 320 },
      { name: 'password', label: 'Senha', type: 'password', autoComplete: 'current-password', maxLength: 128 },
    ],
  },
  'student-activate': {
    title: 'Ativar acesso de aluno',
    fields: [
      { name: 'classroomCode', label: 'Código da turma', maxLength: 32 },
      { name: 'enrollmentNumber', label: 'Matrícula', maxLength: 64 },
      { name: 'temporaryCode', label: 'Código temporário', autoComplete: 'one-time-code', maxLength: 128 },
      { name: 'password', label: 'Nova senha', type: 'password', autoComplete: 'new-password', minLength: 8, maxLength: 128 },
      { name: 'confirmPassword', label: 'Confirmar senha', type: 'password', autoComplete: 'new-password', minLength: 8, maxLength: 128 },
    ],
  },
  'student-login': {
    title: 'Entrar como aluno',
    fields: [
      { name: 'classroomCode', label: 'Código da turma', maxLength: 32 },
      { name: 'enrollmentNumber', label: 'Matrícula', maxLength: 64 },
      { name: 'password', label: 'Senha', type: 'password', autoComplete: 'current-password', maxLength: 128 },
    ],
  },
}

const modes: { id: AuthMode; label: string }[] = [
  { id: 'teacher-login', label: 'Professor: entrar' },
  { id: 'teacher-register', label: 'Professor: cadastrar' },
  { id: 'student-login', label: 'Aluno: entrar' },
  { id: 'student-activate', label: 'Aluno: primeiro acesso' },
]

export function AuthForm({ onSubmit, disabled = false, initialMode = 'teacher-login' }: Props) {
  const id = useId().replaceAll(':', '')
  const tabRefs = useRef(new Map<AuthMode, HTMLButtonElement>())
  const [mode, setMode] = useState<AuthMode>(initialMode)
  const [error, setError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)
  const form = forms[mode]
  const tabId = (tabMode: AuthMode) => `${id}-tab-${tabMode}`
  const panelId = `${id}-panel`

  function selectMode(nextMode: AuthMode) {
    setMode(nextMode)
    setError(null)
  }

  function handleTabKeyDown(event: KeyboardEvent<HTMLButtonElement>, currentIndex: number) {
    let nextIndex: number | null = null
    if (event.key === 'ArrowRight') nextIndex = (currentIndex + 1) % modes.length
    else if (event.key === 'ArrowLeft') nextIndex = (currentIndex - 1 + modes.length) % modes.length
    else if (event.key === 'Home') nextIndex = 0
    else if (event.key === 'End') nextIndex = modes.length - 1
    if (nextIndex === null) return
    event.preventDefault()
    const nextMode = modes[nextIndex].id
    selectMode(nextMode)
    tabRefs.current.get(nextMode)?.focus()
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setError(null)
    setSubmitting(true)
    const fields = Object.fromEntries(
      Array.from(new FormData(event.currentTarget).entries(), ([key, value]) => [key, String(value)]),
    )

    try {
      await onSubmit(mode, fields)
      event.currentTarget.reset()
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'Não foi possível concluir a operação.')
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <section className={styles.panel} aria-labelledby="auth-title">
      <div className={styles.modeList} role="tablist" aria-label="Tipo de acesso">
        {modes.map((item, index) => (
          <button
            key={item.id}
            ref={element => { if (element) tabRefs.current.set(item.id, element); else tabRefs.current.delete(item.id) }}
            id={tabId(item.id)}
            type="button"
            role="tab"
            aria-selected={mode === item.id}
            aria-controls={panelId}
            tabIndex={mode === item.id ? 0 : -1}
            onClick={() => selectMode(item.id)}
            onKeyDown={event => handleTabKeyDown(event, index)}
          >
            {item.label}
          </button>
        ))}
      </div>

      <div className={styles.tabPanel} id={panelId} role="tabpanel" aria-labelledby={tabId(mode)} tabIndex={0}>
      <h2 id="auth-title">{form.title}</h2>
      <form onSubmit={handleSubmit}>
        {form.fields.map(field => (
          <label className={styles.field} key={field.name}>
            <span>{field.label}</span>
            <input
              name={field.name}
              type={field.type ?? 'text'}
              autoComplete={field.autoComplete}
              required
              maxLength={field.maxLength}
              minLength={field.minLength}
              disabled={disabled || submitting}
            />
          </label>
        ))}
        {error && <p className={styles.error} role="alert">{error}</p>}
        <button className={styles.submit} type="submit" disabled={disabled || submitting}>
          {submitting ? 'Enviando…' : 'Continuar'}
        </button>
      </form>
      </div>
    </section>
  )
}
