import { View, Text } from '@tarojs/components'
import type { CourseStep } from '../../data/mock'

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
            <View className={done ? 'step-item__check step-item__check--done' : 'step-item__check'} />
            <View>
              <Text className='step-item__title'>
                {step.title} · {step.time}
              </Text>
              <View className='step-item__desc'>{step.desc}</View>
            </View>
          </View>
        )
      })}
    </View>
  )
}
