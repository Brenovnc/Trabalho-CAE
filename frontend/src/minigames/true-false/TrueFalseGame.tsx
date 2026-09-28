import type { TrueFalseActivity } from '../../types/learning'
import styles from './TrueFalseGame.module.css'

type Props = { activity: TrueFalseActivity; disabled: boolean; onSubmit: (answer: unknown) => void }
export const trueFalseAnswer = (choice: boolean) => ({ choice })

export function TrueFalseGame({ activity, disabled, onSubmit }: Props) {
  return (
    <section aria-labelledby="true-false-title">
      <h2 id="true-false-title">Verdadeiro ou falso?</h2>
      <p>{activity.payload.statement}</p>
      <div className={styles.group} role="group" aria-label="Escolha verdadeiro ou falso">
        <button type="button" disabled={disabled} onClick={() => onSubmit(trueFalseAnswer(true))}>Verdadeiro</button>{' '}
        <button type="button" disabled={disabled} onClick={() => onSubmit(trueFalseAnswer(false))}>Falso</button>
      </div>
    </section>
  )
}

