import { View, Text } from '@tarojs/components'

type SectionHeaderProps = {
  title: string
  action?: string
  onAction?: () => void
}

export function SectionHeader({ title, action, onAction }: SectionHeaderProps) {
  return (
    <View className='section-header'>
      <Text className='section-header__title'>{title}</Text>
      {action ? (
        <Text className='section-header__action' onClick={onAction}>
          {action}
        </Text>
      ) : null}
    </View>
  )
}
