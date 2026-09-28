import { useState } from 'react'
import type { FillBlankActivity } from '../../types/learning'
import { assignOption, fillBlankAnswer, type SlotAssignment } from './utils'
import styles from './FillBlankGame.module.css'

type Props = { activity: FillBlankActivity; disabled: boolean; onSubmit: (answer: unknown) => void }

export function FillBlankGame({ activity, disabled, onSubmit }: Props) {
  const { slotNumbers, options, text } = activity.payload
  const [selectedOption, setSelectedOption] = useState<number | null>(null)
  const [assignment, setAssignment] = useState<SlotAssignment>({})
  const usedOptions = new Set(Object.values(assignment))
  const complete = slotNumbers.every(slot => assignment[slot] !== undefined)
  const parts = text.split(/(\{\{\d+\}\})/g)

  function chooseSlot(slotNumber: number) {
    if (selectedOption === null) return
    setAssignment(current => assignOption(current, slotNumber, selectedOption))
    setSelectedOption(null)
  }

  return (
    <section aria-labelledby="fill-blank-title">
      <h2 id="fill-blank-title">Complete as lacunas</h2>
      <p className={styles.sentence}>
        {parts.map((part, index) => {
          const match = /^\{\{(\d+)\}\}$/.exec(part)
          if (!match) return <span key={`part-${index}`}>{part}</span>
          const slot = Number(match[1])
          const optionIndex = assignment[slot]
          return <button className={styles.slot} type="button" key={`slot-${slot}`} aria-label={`Lacuna ${slot}${optionIndex === undefined ? ', vazia' : `, ${options[optionIndex]}`}`} disabled={disabled || selectedOption === null} onClick={() => chooseSlot(slot)}>
            {optionIndex === undefined ? 'Escolher palavra' : options[optionIndex]}
          </button>
        })}
      </p>
      <p>Escolha uma opção e depois a lacuna onde quer colocá-la. Para remover, use o botão ao lado da lacuna.</p>
      <ul className={styles.options} aria-label="Opções de resposta">
        {options.map((option, index) => <li key={`${index}-${option}`}>
          <button type="button" aria-pressed={selectedOption === index} disabled={disabled || usedOptions.has(index)} onClick={() => setSelectedOption(index)}>{option}</button>
        </li>)}
      </ul>
      <div className={styles.removeList}>
        {slotNumbers.map(slot => assignment[slot] !== undefined && <button type="button" key={`remove-${slot}`} disabled={disabled} onClick={() => setAssignment(current => Object.fromEntries(Object.entries(current).filter(([key]) => Number(key) !== slot)) as SlotAssignment)}>Remover palavra da lacuna {slot}</button>)}
      </div>
      <button type="button" disabled={disabled || !complete} onClick={() => onSubmit(fillBlankAnswer(slotNumbers, options, assignment))}>Enviar resposta</button>
    </section>
  )
}

