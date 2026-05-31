import { View, Text, ScrollView } from '@tarojs/components'
import Taro from '@tarojs/taro'
import { AppShell, CategoryGrid, CourseCard, SearchBar, SectionHeader } from '../../components'
import { beginnerCourseIds, categories, getCoursesByIds, popularCourseIds } from '../../data/mock'

function Index() {
  const beginnerCourses = getCoursesByIds(beginnerCourseIds)
  const popularCourses = getCoursesByIds(popularCourseIds)

  const openCourse = (id: string) => {
    Taro.navigateTo({ url: `/pages/course-detail/index?id=${id}` })
  }

  const openCategory = (category: string) => {
    Taro.setStorageSync('pendingCourseCategory', category)
    Taro.switchTab({ url: '/pages/courses/index' })
  }

  return (
    <AppShell title='烘焙课堂' subtitle='小白也能跟着完成的移动烘焙课'>
      <SearchBar text='搜索想学的烘焙课' onClick={() => Taro.switchTab({ url: '/pages/courses/index' })} />

      <View className='hero-card' style={{ marginTop: 14 }}>
        <View className='image-placeholder image-placeholder--warm'>
          <Text>首页 Banner · 新课 / 活动</Text>
        </View>
        <View className='chip-row' style={{ marginTop: 12 }}>
          <Text className='chip chip--accent'>今日推荐</Text>
          <Text className='chip'>新手路线</Text>
          <Text className='chip'>材料包可配</Text>
        </View>
      </View>

      <CategoryGrid categories={categories} onSelect={openCategory} />

      <SectionHeader title='新手必学' action='全部 ›' onAction={() => openCategory('面包')} />
      <ScrollView scrollX className='course-rail'>
        <View className='course-rail__inner'>
          {beginnerCourses.map((course) => (
            <CourseCard key={course.id} course={course} variant='rail' onClick={() => openCourse(course.id)} />
          ))}
        </View>
      </ScrollView>

      <SectionHeader title='本周热门' action='更多 ›' onAction={() => Taro.switchTab({ url: '/pages/courses/index' })} />
      {popularCourses.map((course) => (
        <CourseCard key={course.id} course={course} onClick={() => openCourse(course.id)} />
      ))}
    </AppShell>
  )
}

export default Index
