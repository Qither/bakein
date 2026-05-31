import { Image, View, Text } from '@tarojs/components'
import type { CourseStep } from '../../services/api'
import checkIcon from '../../assets/icons/check.svg'

type StepListProps = {
  steps: CourseStep[]
  doneIds: string[]
  onToggle: (id: string) => void
}

export function StepList({ steps, doneIds, onToggle }: StepListProps) {
  const firstOpen = steps.find((step) => !doneIds.includes(step.id))?.id

  return (
    <View className='step-list'>
      {steps.map((step) => {
        const done = doneIds.includes(step.id)
        const active = step.id === firstOpen
        return (
          <View key={step.id} className={active ? 'step-item step-item--active' : 'step-item'} onClick={() => onToggle(step.id)}>
            <View className={done ? 'step-item__check step-item__check--done' : 'step-item__check'}>
              {done ? <Image className='step-item__check-icon' src={checkIcon} mode='aspectFit' /> : null}
            </View>
            <View>
              <Text className='step-item__title'>
                {step.title} · {step.time}
              </Text>
              <View className='step-item__desc'>{step.description}</View>
            </View>
          </View>
        )
      })}
    </View>
  )
}
