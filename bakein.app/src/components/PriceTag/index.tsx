import { Text } from '@tarojs/components'

type PriceTagProps = {
  value: string
}

export function PriceTag({ value }: PriceTagProps) {
  return <Text className='price-tag'>{value}</Text>
}
