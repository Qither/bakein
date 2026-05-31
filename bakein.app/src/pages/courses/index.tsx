import { useMemo, useState } from 'react'
import { View, Text } from '@tarojs/components'
import Taro, { useDidShow, useRouter } from '@tarojs/taro'
import { AppShell, CourseCard, SearchBar } from '../../components'
import { categories, courses } from '../../data/mock'

function Courses() {
  const router = useRouter()
  const initialCategory = decodeURIComponent(String(router.params.category || categories[0]))
  const [category, setCategory] = useState(categories.includes(initialCategory) ? initialCategory : categories[0])
  const [memberOnly, setMemberOnly] = useState(false)

  useDidShow(() => {
    const pendingCategory = Taro.getStorageSync<string>('pendingCourseCategory')
    if (pendingCategory && categories.includes(pendingCategory)) {
      setCategory(pendingCategory)
      Taro.removeStorageSync('pendingCourseCategory')
    }
  })

  const visibleCourses = useMemo(
    () => courses.filter((course) => course.category === category && (!memberOnly || course.memberFree)),
    [category, memberOnly],
  )

  return (
    <AppShell title='课程分类' subtitle='先选品类，再按难度和会员权益快速缩小范围'>
      <SearchBar text={category} />

      <View className='segment'>
        {categories.map((item) => (
          <Text
            key={item}
            className={item === category ? 'segment__item segment__item--active' : 'segment__item'}
            onClick={() => setCategory(item)}>
            {item}
          </Text>
        ))}
      </View>

      <View className='chip-row' style={{ marginTop: 14, marginBottom: 12 }}>
        <Text className='chip chip--accent'>综合</Text>
        <Text className='chip'>最新</Text>
        <Text className='chip'>难度 ▾</Text>
        <Text className={memberOnly ? 'chip chip--accent' : 'chip'} onClick={() => setMemberOnly(!memberOnly)}>
          会员免费
        </Text>
      </View>

      {visibleCourses.map((course) => (
        <CourseCard
          key={course.id}
          course={course}
          onClick={() => Taro.navigateTo({ url: `/pages/course-detail/index?id=${course.id}` })}
        />
      ))}
    </AppShell>
  )
}

export default Courses
