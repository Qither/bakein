import { View, Text } from '@tarojs/components'
import Taro from '@tarojs/taro'
import { AppShell, BottomActionBar, CourseCard, SectionHeader } from '../../components'
import { courses, membershipPlans } from '../../data/mock'

function Membership() {
  const freeCourses = courses.filter((course) => course.memberFree)

  return (
    <AppShell title='会员订阅' subtitle='课程、材料包券和答疑权益集中在这里' withAction>
      <View className='hero-card'>
        <Text className='page-title'>开通后解锁 26 门新手课</Text>
        <View className='page-subtitle'>每周上新，适合用会员建立稳定练习节奏。</View>
        <View className='chip-row' style={{ marginTop: 12 }}>
          <Text className='chip chip--accent'>会员免费课</Text>
          <Text className='chip'>直播答疑</Text>
          <Text className='chip'>材料包券</Text>
        </View>
      </View>

      <SectionHeader title='订阅方案' />
      {membershipPlans.map((plan) => (
        <View key={plan.id} className='panel'>
          <View className='course-card__footer'>
            <Text className='course-card__title'>{plan.name}</Text>
            <Text className='price-tag'>{plan.price}</Text>
          </View>
          <View className='page-subtitle'>{plan.desc}</View>
        </View>
      ))}

      <SectionHeader title='会员免费课程' action='去分类 ›' onAction={() => Taro.switchTab({ url: '/pages/courses/index' })} />
      {freeCourses.map((course) => (
        <CourseCard key={course.id} course={course} onClick={() => Taro.navigateTo({ url: `/pages/course-detail/index?id=${course.id}` })} />
      ))}

      <BottomActionBar
        label='推荐方案'
        value='¥79 / 季'
        primaryText='开通会员'
        onPrimary={() => Taro.showToast({ title: '订阅入口待接入', icon: 'none' })}
      />
    </AppShell>
  )
}

export default Membership
