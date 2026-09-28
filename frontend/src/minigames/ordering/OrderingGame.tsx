import { useState } from 'react'
import type { OrderingActivity } from '../../types/learning'
import { moveOrderingItem, orderingAnswer } from './utils'
import styles from './OrderingGame.module.css'

type Props = { activity: OrderingActivity; disabled: boolean; onSubmit: (answer: unknown) => void }

export function OrderingGame({ activity, disabled, onSubmit }: Props) {
  const [items, setItems] = useState(activity.payload.items)
  return (
    <section aria-labelledby="ordering-title">
      <h2 id="ordering-title">{activity.payload.instruction}</h2>
      <ol className={styles.items}>
        {items.map((item, index) => <li key={item.id}>
          <span>{item.text}</span>
          <div className={styles.controls}>
            <button type="button" aria-label={`Mover ${item.text} para cima`} disabled={disabled || index === 0} onClick={() => setItems(current => moveOrderingItem(current, index, -1))}>↑</button>
            <button type="button" aria-label={`Mover ${item.text} para baixo`} disabled={disabled || index === items.length - 1} onClick={() => setItems(current => moveOrderingItem(current, index, 1))}>↓</button>
          </div>
        </li>)}
      </ol>
      <button type="button" disabled={disabled} onClick={() => onSubmit(orderingAnswer(items))}>Enviar sequência</button>
    </section>
  )
}
