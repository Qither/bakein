import { useEffect, useState } from 'react'
import { View, Text } from '@tarojs/components'
import Taro from '@tarojs/taro'
import { AppShell, BottomActionBar, CourseCard, SectionHeader } from '../../components'
import { api, type Course, type MembershipPlan } from '../../services/api'

function Membership() {
  const [plans, setPlans] = useState<MembershipPlan[]>([])
  const [freeCourses, setFreeCourses] = useState<Course[]>([])

  useEffect(() => {
    let active = true
    Promise.all([api.getMembershipPlans(), api.getCourses({ memberFree: true })]).then(([membershipPlans, memberCourses]) => {
      if (!active) return
      setPlans(membershipPlans)
      setFreeCourses(memberCourses)
    })

    return () => {
      active = false
    }
  }, [])

  const recommendedPlan = plans.find((plan) => plan.id === 'season') || plans[0]

  const addMembership = async () => {
    if (!recommendedPlan) return
    await api.addCartItem({ itemType: 'membership_plan', skuId: recommendedPlan.id, quantity: 1 })
    Taro.navigateTo({ url: '/pages/cart/index' })
  }

  return (
    <AppShell title='会员订阅' subtitle='课程、材料包券和答疑权益集中在这里' withAction>
      <View className='hero-card'>
        <Text className='page-title'>解锁新手课程</Text>
        <View className='page-subtitle'>每周上新，适合用会员建立稳定练习节奏。</View>
        <View className='chip-row' style={{ marginTop: 12 }}>
          <Text className='chip chip--accent'>会员免费课</Text>
          <Text className='chip'>直播答疑</Text>
          <Text className='chip'>材料包券</Text>
        </View>
      </View>

      <SectionHeader title='订阅方案' />
      {plans.map((plan) => (
        <View key={plan.id} className='panel'>
          <View className='course-card__footer'>
            <Text className='course-card__title'>{plan.name}</Text>
            <Text className='price-tag'>{plan.price}</Text>
          </View>
          <View className='page-subtitle'>{plan.description}</View>
        </View>
      ))}

      <SectionHeader title='会员免费课程' action='去分类 ›' onAction={() => Taro.switchTab({ url: '/pages/courses/index' })} />
      {freeCourses.map((course) => (
        <CourseCard key={course.id} course={course} onClick={() => Taro.navigateTo({ url: `/pages/course-detail/index?id=${course.id}` })} />
      ))}

      <BottomActionBar
        label='推荐方案'
        value={recommendedPlan?.price || '¥0'}
        primaryText='加入购物车'
        onPrimary={addMembership}
      />
    </AppShell>
  )
}

export default Membership
