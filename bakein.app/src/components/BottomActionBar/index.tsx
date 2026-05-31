import { View, Text } from '@tarojs/components'

type BottomActionBarProps = {
  label: string
  value: string
  primaryText: string
  secondaryText?: string
  tertiaryText?: string
  onPrimary?: () => void
  onSecondary?: () => void
  onTertiary?: () => void
}

export function BottomActionBar({
  label,
  value,
  primaryText,
  secondaryText,
  tertiaryText,
  onPrimary,
  onSecondary,
  onTertiary,
}: BottomActionBarProps) {
  return (
    <View className='bottom-action'>
      <View className='bottom-action__meta'>
        <View className='bottom-action__label'>{label}</View>
        <View className='bottom-action__value'>{value}</View>
      </View>
      {tertiaryText ? (
        <Text className='action-button action-button--ghost' onClick={onTertiary}>
          {tertiaryText}
        </Text>
      ) : null}
      {secondaryText ? (
        <Text className='action-button action-button--ghost' onClick={onSecondary}>
          {secondaryText}
        </Text>
      ) : null}
      <Text className='action-button' onClick={onPrimary}>
        {primaryText}
      </Text>
    </View>
  )
}
