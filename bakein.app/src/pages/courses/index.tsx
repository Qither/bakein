import { useEffect, useState } from 'react'
import { View, Text } from '@tarojs/components'
import Taro, { useDidShow, useRouter } from '@tarojs/taro'
import { AppShell, CourseCard, SearchBar } from '../../components'
import { api, type Category, type Course } from '../../services/api'

function Courses() {
  const router = useRouter()
  const [categories, setCategories] = useState<Category[]>([])
  const [category, setCategory] = useState(String(router.params.category || ''))
  const [memberOnly, setMemberOnly] = useState(false)
  const [courses, setCourses] = useState<Course[]>([])
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    let active = true
    api.getCategories().then((items) => {
      if (!active) return
      setCategories(items)
      setCategory((current) => current || items[0]?.name || '')
    })

    return () => {
      active = false
    }
  }, [])

  useDidShow(() => {
    const pendingCategory = Taro.getStorageSync<string>('pendingCourseCategory')
    if (pendingCategory) {
      setCategory(pendingCategory)
      Taro.removeStorageSync('pendingCourseCategory')
    }
  })

  useEffect(() => {
    if (!category) return

    let active = true
    setLoading(true)
    api
      .getCourses({ category, memberFree: memberOnly || undefined })
      .then((items) => {
        if (active) setCourses(items)
      })
      .finally(() => {
        if (active) setLoading(false)
      })

    return () => {
      active = false
    }
  }, [category, memberOnly])

  return (
    <AppShell title='课程分类' subtitle='按品类和会员权益筛选课程'>
      <SearchBar text={category || '全部课程'} />

      <View className='segment'>
        {categories.map((item) => (
          <Text
            key={item.id}
            className={item.name === category ? 'segment__item segment__item--active' : 'segment__item'}
            onClick={() => setCategory(item.name)}>
            {item.name}
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

      {loading ? <View className='panel page-subtitle'>加载中...</View> : null}
      {!loading && courses.length === 0 ? <View className='panel page-subtitle'>暂无课程</View> : null}

      {courses.map((course) => (
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
