import { Image, View, Text } from '@tarojs/components'
import searchIcon from '../../assets/icons/magnifying-glass.svg'

type SearchBarProps = {
  text: string
  onClick?: () => void
}

export function SearchBar({ text, onClick }: SearchBarProps) {
  return (
    <View className='search-bar' onClick={onClick}>
      <Image className='search-bar__icon' src={searchIcon} mode='aspectFit' />
      <Text>{text}</Text>
    </View>
  )
}
