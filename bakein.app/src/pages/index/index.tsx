import { useEffect, useState } from 'react'
import { View, Text, ScrollView } from '@tarojs/components'
import Taro from '@tarojs/taro'
import { AppShell, CategoryGrid, CourseCard, SearchBar, SectionHeader } from '../../components'
import { api, type HomeFeed } from '../../services/api'

function Index() {
  const [home, setHome] = useState<HomeFeed>()
  const [error, setError] = useState('')

  useEffect(() => {
    let active = true
    api
      .getHomeFeed()
      .then((feed) => {
        if (active) {
          setHome(feed)
          setError('')
        }
      })
      .catch(() => {
        if (active) setError('后端连接失败')
      })

    return () => {
      active = false
    }
  }, [])

  const openCourse = (id: string) => {
    Taro.navigateTo({ url: `/pages/course-detail/index?id=${id}` })
  }

  const openCategory = (category: string) => {
    Taro.setStorageSync('pendingCourseCategory', category)
    Taro.switchTab({ url: '/pages/courses/index' })
  }

  const categories = home?.categories.map((category) => category.name) || []

  return (
    <AppShell title='烘焙课堂' subtitle='小白也能跟着完成的移动烘焙课'>
      <SearchBar text='搜索想学的烘焙课' onClick={() => Taro.switchTab({ url: '/pages/courses/index' })} />

      <View className='hero-card' style={{ marginTop: 14 }}>
        <View className='image-placeholder image-placeholder--warm'>
          <Text>本周新课</Text>
        </View>
        <View className='chip-row' style={{ marginTop: 12 }}>
          <Text className='chip chip--accent'>今日推荐</Text>
          <Text className='chip'>新手路线</Text>
          <Text className='chip'>材料包可配</Text>
        </View>
      </View>

      {error ? <View className='panel page-subtitle'>{error}</View> : null}
      {!home && !error ? <View className='panel page-subtitle'>加载中...</View> : null}

      {categories.length ? <CategoryGrid categories={categories} onSelect={openCategory} /> : null}

      <SectionHeader title='新手必学' action='全部 ›' onAction={() => openCategory('面包')} />
      <ScrollView scrollX className='course-rail'>
        <View className='course-rail__inner'>
          {(home?.beginnerCourses || []).map((course) => (
            <CourseCard key={course.id} course={course} variant='rail' onClick={() => openCourse(course.id)} />
          ))}
        </View>
      </ScrollView>

      <SectionHeader title='本周热门' action='更多 ›' onAction={() => Taro.switchTab({ url: '/pages/courses/index' })} />
      {(home?.popularCourses || []).map((course) => (
        <CourseCard key={course.id} course={course} onClick={() => openCourse(course.id)} />
      ))}
    </AppShell>
  )
}

export default Index
