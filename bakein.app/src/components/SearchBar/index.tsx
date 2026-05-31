import { View, Text } from '@tarojs/components'

type SearchBarProps = {
  text: string
  onClick?: () => void
}

export function SearchBar({ text, onClick }: SearchBarProps) {
  return (
    <View className='search-bar' onClick={onClick}>
      <View className='search-bar__icon' />
      <Text>{text}</Text>
    </View>
  )
}
