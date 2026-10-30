import { useState } from 'react'
import { DndContext, KeyboardSensor, PointerSensor, TouchSensor, useDraggable, useDroppable, useSensor, useSensors, type DragEndEvent } from '@dnd-kit/core'
import { CSS } from '@dnd-kit/utilities'
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
  const sensors = useSensors(
    useSensor(PointerSensor, { activationConstraint: { distance: 6 } }),
    useSensor(TouchSensor, { activationConstraint: { delay: 180, tolerance: 6 } }),
    useSensor(KeyboardSensor),
  )

  function chooseSlot(slotNumber: number) {
    if (selectedOption === null || disabled) return
    setAssignment(current => assignOption(current, slotNumber, selectedOption))
    setSelectedOption(null)
  }

  function dropOption(event: DragEndEvent) {
    if (disabled || !event.over) return
    const overId = String(event.over.id)
    if (!overId.startsWith('drop:')) return
    const target = Number(overId.slice(5))
    const activeId = String(event.active.id)
    if (activeId.startsWith('option:')) {
      setAssignment(current => assignOption(current, target, Number(activeId.slice(7))))
      setSelectedOption(null)
    } else if (activeId.startsWith('assigned:')) {
      const source = Number(activeId.slice(9))
      const option = assignment[source]
      if (option !== undefined && source !== target) setAssignment(current => assignOption(current, target, option))
    }
  }

  return <DndContext sensors={sensors} onDragEnd={dropOption}>
    <section aria-labelledby="fill-blank-title">
      <h2 id="fill-blank-title">Complete as lacunas</h2>
      <p className={styles.sentence}>
        {parts.map((part, index) => {
          const match = /^\{\{(\d+)\}\}$/.exec(part)
          if (!match) return <span key={'part-' + index}>{part}</span>
          const slot = Number(match[1])
          const optionIndex = assignment[slot]
          return <FillSlot key={'slot-' + slot} slot={slot} value={optionIndex === undefined ? undefined : options[optionIndex]}
            optionIndex={optionIndex} disabled={disabled} selected={selectedOption !== null} onChoose={() => chooseSlot(slot)} />
        })}
      </p>
      <p>Escolha uma opção e depois a lacuna onde quer colocá-la. Também é possível arrastar opções entre lacunas.</p>
      <ul className={styles.options} aria-label="Opções de resposta">
        {options.map((option, index) => <li key={index + '-' + option}>
          <DraggableOption id={'option:' + index} label={option} disabled={disabled || usedOptions.has(index)}
            pressed={selectedOption === index} onClick={() => setSelectedOption(index)} />
        </li>)}
      </ul>
      <div className={styles.removeList}>
        {slotNumbers.map(slot => assignment[slot] !== undefined && <button type="button" key={'remove-' + slot} disabled={disabled}
          onClick={() => setAssignment(current => Object.fromEntries(Object.entries(current).filter(([key]) => Number(key) !== slot)) as SlotAssignment)}>Remover palavra da lacuna {slot}</button>)}
      </div>
      <button type="button" disabled={disabled || !complete} onClick={() => onSubmit(fillBlankAnswer(slotNumbers, options, assignment))}>Enviar resposta</button>
    </section>
  </DndContext>
}

function FillSlot({ slot, value, optionIndex, disabled, selected, onChoose }: {
  slot: number; value?: string; optionIndex?: number; disabled: boolean; selected: boolean; onChoose: () => void
}) {
  const target = useDroppable({ id: 'drop:' + slot, disabled })
  const drag = useDraggable({ id: 'assigned:' + slot, disabled: disabled || optionIndex === undefined })
  return <span ref={target.setNodeRef} className={styles.slotTarget + (target.isOver ? ' ' + styles.dropTarget : '')}>
    <button ref={drag.setNodeRef} type="button" className={styles.slot}
      aria-label={'Lacuna ' + slot + (value === undefined ? ', vazia' : ', ' + value)}
      {...drag.attributes} {...drag.listeners} aria-disabled={disabled || !selected}
      style={{ transform: CSS.Translate.toString(drag.transform), opacity: drag.isDragging ? .5 : 1 }}
      onClick={onChoose}>{value ?? 'Escolher palavra'}</button>
  </span>
}

function DraggableOption({ id, label, disabled, pressed, onClick }: {
  id: string; label: string; disabled: boolean; pressed: boolean; onClick: () => void
}) {
  const drag = useDraggable({ id, disabled })
  return <button ref={drag.setNodeRef} type="button" disabled={disabled}
    {...drag.attributes} {...drag.listeners} aria-pressed={pressed}
    style={{ transform: CSS.Translate.toString(drag.transform), opacity: drag.isDragging ? .5 : 1 }}
    onClick={onClick}>{label}</button>
}
