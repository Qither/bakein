import { Image, View, Text } from '@tarojs/components'
import breadIcon from '../../assets/icons/bread.svg'
import cakeIcon from '../../assets/icons/cake.svg'
import cookiesIcon from '../../assets/icons/cookie.svg'
import dessertIcon from '../../assets/icons/ice-cream.svg'
import pipingIcon from '../../assets/icons/paint-brush.svg'

type CategoryGridProps = {
  categories: string[]
  onSelect: (category: string) => void
}

const categoryIcons: Record<string, string> = {
  面包: breadIcon,
  蛋糕: cakeIcon,
  饼干: cookiesIcon,
  甜点: dessertIcon,
  裱花: pipingIcon,
}

export function CategoryGrid({ categories, onSelect }: CategoryGridProps) {
  return (
    <View className='category-grid'>
      {categories.map((category) => {
        const icon = categoryIcons[category]

        return (
          <View key={category} className='category-grid__item' onClick={() => onSelect(category)}>
            <View className='category-grid__icon'>
              {icon ? <Image className='category-grid__image' src={icon} mode='aspectFit' /> : category.slice(0, 1)}
            </View>
            <Text>{category}</Text>
          </View>
        )
      })}
    </View>
  )
}
