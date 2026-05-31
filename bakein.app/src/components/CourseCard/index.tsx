import { View, Text } from '@tarojs/components'
import type { Course } from '../../services/api'
import { PriceTag } from '../PriceTag'

type CourseCardProps = {
  course: Course
  variant?: 'rail' | 'list'
  onClick?: () => void
}

export function CourseCard({ course, variant = 'list', onClick }: CourseCardProps) {
  const className = variant === 'rail' ? 'course-card course-card--rail' : 'course-card course-card--list'

  return (
    <View className={className} onClick={onClick}>
      <View className='image-placeholder course-card__cover'>
        <Text>{course.cover}</Text>
      </View>
      <View className='course-card__body'>
        <Text className='course-card__title'>{course.title}</Text>
        <View className='course-card__meta'>
          {course.category} · {course.duration} · {course.level}
        </View>
        <View className='course-card__footer'>
          <Text className={course.memberFree ? 'chip chip--accent' : 'chip'}>{course.memberFree ? '会员免费' : course.teacher}</Text>
          <PriceTag value={course.price} />
        </View>
      </View>
    </View>
  )
}
