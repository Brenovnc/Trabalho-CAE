import type { ExposureActivity } from '../../types/learning'
import styles from './ExposureGame.module.css'

type Props = { activity: ExposureActivity; disabled: boolean; onReveal: () => Promise<void>; onSubmit: (answer: unknown) => void }

export function ExposureGame({ activity, disabled, onReveal, onSubmit }: Props) {
  const { payload } = activity
  const revealedCount = payload.revealedCount ?? payload.revealedKeywords.length
  const complete = revealedCount >= payload.keywordCount
  let nextHiddenRendered = false

  return (
    <section aria-labelledby="exposure-title">
      <h2 id="exposure-title">Leia e explore</h2>
      <p className={styles.definition}>
        {payload.segments.map((segment, index) => {
          if (segment.keywordIndex === null) return <span key={`text-${index}`}>{segment.text}</span>
          const keyword = payload.revealedKeywords[segment.keywordIndex]
          if (keyword !== undefined) return <mark className={styles.revealed} key={`keyword-${index}`}>{keyword}</mark>
          if (segment.keywordIndex === revealedCount && !nextHiddenRendered) {
            nextHiddenRendered = true
            return <button className={styles.hidden} key={`keyword-${index}`} type="button" disabled={disabled} aria-label={`Revelar palavra-chave ${segment.keywordIndex + 1}`} onClick={() => void onReveal()}>????</button>
          }
          return <span className={styles.hiddenText} key={`keyword-${index}`} aria-label={`Palavra-chave ${segment.keywordIndex + 1} oculta`}>????</span>
        })}
      </p>
      <p aria-live="polite">{revealedCount} de {payload.keywordCount} palavras-chave reveladas</p>
      <div className={styles.actions}>
        {!complete && <button type="button" disabled={disabled} onClick={() => void onReveal()}>Revelar próxima palavra</button>}
        <button type="button" disabled={disabled || !complete} onClick={() => onSubmit({ completed: true })}>Concluir exposição</button>
      </div>
    </section>
  )
}

