import { useState, type FormEvent } from 'react'
import type { GuessConceptActivity } from '../../types/learning'
import styles from './GuessConceptGame.module.css'

type Props = { activity: GuessConceptActivity; disabled: boolean; onReveal: () => Promise<void>; onSubmit: (answer: unknown, hintsUsed: number) => void }
export const guessHintCount = (visibleClues: number) => Math.max(0, visibleClues - 1)

export function GuessConceptGame({ activity, disabled, onReveal, onSubmit }: Props) {
  const [answer, setAnswer] = useState('')
  const { clues, clueCount } = activity.payload
  const hintsUsed = guessHintCount(clues.length)
  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!answer.trim() || disabled) return
    onSubmit({ text: answer }, hintsUsed)
  }

  return (
    <section aria-labelledby="guess-title">
      <h2 id="guess-title">Qual é o conceito?</h2>
      <ol className={styles.clues} aria-label="Pistas reveladas">
        {clues.map((clue, index) => <li key={`${index}-${clue}`}><strong>Pista {index + 1}:</strong> {clue}</li>)}
      </ol>
      {clues.length < clueCount && <button type="button" disabled={disabled} onClick={() => void onReveal()}>Revelar próxima pista</button>}
      <form onSubmit={submit}>
        <label className={styles.answer}>Sua resposta
          <input aria-label="Sua resposta" autoComplete="off" value={answer} onChange={event => setAnswer(event.target.value)} disabled={disabled} />
        </label>
        <button type="submit" disabled={disabled || !answer.trim()}>Enviar resposta</button>
      </form>
    </section>
  )
}

