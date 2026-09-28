import type { StudyActivity } from '../types/learning'
import { ExposureGame } from './exposure/ExposureGame'
import { TrueFalseGame } from './true-false/TrueFalseGame'
import { FillBlankGame } from './fill-blank/FillBlankGame'
import { GuessConceptGame } from './guess-concept/GuessConceptGame'
import { OrderingGame } from './ordering/OrderingGame'

export type GameProps = { activity: StudyActivity; busy: boolean; onReveal: () => Promise<void>; onSubmit: (answer: unknown, hintsUsed?: number) => void }
export function ActivityGame(props: GameProps) {
  switch (props.activity.type) {
    case 'EXPOSURE': return <ExposureGame activity={props.activity} disabled={props.busy} onReveal={props.onReveal} onSubmit={answer => props.onSubmit(answer)} />
    case 'TRUE_FALSE': return <TrueFalseGame activity={props.activity} disabled={props.busy} onSubmit={props.onSubmit} />
    case 'FILL_BLANK': return <FillBlankGame activity={props.activity} disabled={props.busy} onSubmit={props.onSubmit} />
    case 'GUESS_CONCEPT': return <GuessConceptGame activity={props.activity} disabled={props.busy} onReveal={props.onReveal} onSubmit={props.onSubmit} />
    case 'ORDERING': return <OrderingGame activity={props.activity} disabled={props.busy} onSubmit={props.onSubmit} />
  }
}

