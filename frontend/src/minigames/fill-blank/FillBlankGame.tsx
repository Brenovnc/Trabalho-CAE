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
    if (disabled) return
    if (selectedOption !== null) {
      setAssignment(current => assignOption(current, slotNumber, selectedOption))
      setSelectedOption(null)
    } else if (assignment[slotNumber] !== undefined) {
      setAssignment(current => Object.fromEntries(Object.entries(current).filter(([key]) => Number(key) !== slotNumber)) as SlotAssignment)
    }
  }

  function dropOption(event: DragEndEvent) {
    if (disabled) return
    const activeId = String(event.active.id)
    if (activeId.startsWith('assigned:')) {
      const source = Number(activeId.slice(9))
      if (!event.over || !String(event.over.id).startsWith('drop:')) {
        setAssignment(current => Object.fromEntries(Object.entries(current).filter(([slot]) => Number(slot) !== source)) as SlotAssignment)
        return
      }
    }
    if (!event.over) return
    const overId = String(event.over.id)
    if (!overId.startsWith('drop:')) return
    const target = Number(overId.slice(5))
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
      <p className={styles.sentence} data-option-selected={selectedOption !== null || undefined}>
        {parts.map((part, index) => {
          const match = /^\{\{(\d+)\}\}$/.exec(part)
          if (!match) return <span key={'part-' + index}>{part}</span>
          const slot = Number(match[1])
          const optionIndex = assignment[slot]
          return <FillSlot key={'slot-' + slot} slot={slot} value={optionIndex === undefined ? undefined : options[optionIndex]}
            optionIndex={optionIndex} disabled={disabled} selected={selectedOption !== null} onChoose={() => chooseSlot(slot)} />
        })}
      </p>
      <p>Clique numa opção para destacá-la e depois na lacuna, ou arraste-a até a lacuna. Para remover uma palavra, clique nela ou arraste-a para fora da lacuna.</p>
      <ul className={styles.options} aria-label="Opções de resposta">
        {options.map((option, index) => <li key={index + '-' + option}>
          <DraggableOption id={'option:' + index} label={option} used={usedOptions.has(index)} disabled={disabled || usedOptions.has(index)}
            pressed={selectedOption === index} onClick={() => setSelectedOption(current => current === index ? null : index)} />
        </li>)}
      </ul>
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
    <button ref={drag.setNodeRef} type="button" className={styles.slot + (selected ? ' ' + styles.slotSelectable : '')}
      aria-label={value === undefined ? 'Lacuna ' + slot + ', vazia' : 'Lacuna ' + slot + ', preenchida com ' + value + '. Clique para remover'}
      disabled={disabled}
      {...drag.attributes} {...drag.listeners}
      style={{ transform: CSS.Translate.toString(drag.transform), opacity: drag.isDragging ? .5 : 1 }}
      onClick={onChoose}>{value ?? 'Escolher palavra'}</button>
  </span>
}

function DraggableOption({ id, label, disabled, used, pressed, onClick }: {
  id: string; label: string; disabled: boolean; used: boolean; pressed: boolean; onClick: () => void
}) {
  const drag = useDraggable({ id, disabled })
  return <button ref={drag.setNodeRef} type="button" disabled={disabled}
    {...drag.attributes} {...drag.listeners} aria-pressed={pressed} aria-label={label + (used ? ', já usada' : '')}
    style={{ transform: CSS.Translate.toString(drag.transform), opacity: drag.isDragging ? .5 : 1 }}
    onClick={onClick}>{label}</button>
}
