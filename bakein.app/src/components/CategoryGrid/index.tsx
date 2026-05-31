import { View, Text } from '@tarojs/components'

type CategoryGridProps = {
  categories: string[]
  onSelect: (category: string) => void
}

export function CategoryGrid({ categories, onSelect }: CategoryGridProps) {
  return (
    <View className='category-grid'>
      {categories.map((category) => (
        <View key={category} className='category-grid__item' onClick={() => onSelect(category)}>
          <View className='category-grid__icon'>{category.slice(0, 1)}</View>
          <Text>{category}</Text>
        </View>
      ))}
    </View>
  )
}
