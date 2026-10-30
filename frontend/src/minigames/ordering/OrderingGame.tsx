import { useState } from 'react'
import { DndContext, KeyboardSensor, PointerSensor, TouchSensor, closestCenter, useSensor, useSensors, type DragEndEvent } from '@dnd-kit/core'
import { SortableContext, arrayMove, useSortable, verticalListSortingStrategy } from '@dnd-kit/sortable'
import { CSS } from '@dnd-kit/utilities'
import type { OrderingActivity } from '../../types/learning'
import { moveOrderingItem, orderingAnswer } from './utils'
import styles from './OrderingGame.module.css'

type Props = { activity: OrderingActivity; disabled: boolean; onSubmit: (answer: unknown) => void }

export function OrderingGame({ activity, disabled, onSubmit }: Props) {
  const [items, setItems] = useState(activity.payload.items)
  const sensors = useSensors(
    useSensor(PointerSensor, { activationConstraint: { distance: 6 } }),
    useSensor(TouchSensor, { activationConstraint: { delay: 180, tolerance: 6 } }),
    useSensor(KeyboardSensor),
  )
  function reorder(event: DragEndEvent) {
    if (disabled || !event.over) return
    const from = items.findIndex(item => item.id === event.active.id)
    const to = items.findIndex(item => item.id === event.over?.id)
    if (from >= 0 && to >= 0 && from !== to) setItems(current => arrayMove(current, from, to))
  }
  return <DndContext sensors={sensors} collisionDetection={closestCenter} onDragEnd={reorder}>
    <section aria-labelledby="ordering-title">
      <h2 id="ordering-title">{activity.payload.instruction}</h2>
      <SortableContext items={items.map(item => item.id)} strategy={verticalListSortingStrategy}>
        <ol className={styles.items}>
          {items.map((item, index) => <SortableOrderingItem key={item.id} id={item.id} text={item.text} disabled={disabled}
            first={index === 0} last={index === items.length - 1}
            onUp={() => setItems(current => moveOrderingItem(current, index, -1))}
            onDown={() => setItems(current => moveOrderingItem(current, index, 1))} />)}
        </ol>
      </SortableContext>
      <button type="button" disabled={disabled} onClick={() => onSubmit(orderingAnswer(items))}>Enviar sequência</button>
    </section>
  </DndContext>
}

function SortableOrderingItem({ id, text, disabled, first, last, onUp, onDown }: {
  id: string; text: string; disabled: boolean; first: boolean; last: boolean; onUp: () => void; onDown: () => void
}) {
  const { attributes, listeners, setNodeRef, setActivatorNodeRef, transform, transition, isDragging, isOver } = useSortable({ id, disabled })
  return <li ref={setNodeRef} data-over={isOver || undefined}
    className={styles.item + (isDragging ? ' ' + styles.dragging : '')}
    style={{ transform: CSS.Transform.toString(transform), transition, zIndex: isDragging ? 2 : undefined }}>
    <span>{text}</span>
    <div className={styles.controls}>
      <button ref={setActivatorNodeRef} type="button" className={styles.dragHandle} aria-label={'Arrastar ' + text}
        disabled={disabled} {...attributes} {...listeners}>⠿</button>
      <button type="button" aria-label={'Mover ' + text + ' para cima'} disabled={disabled || first} onClick={onUp}>↑</button>
      <button type="button" aria-label={'Mover ' + text + ' para baixo'} disabled={disabled || last} onClick={onDown}>↓</button>
    </div>
  </li>
}
