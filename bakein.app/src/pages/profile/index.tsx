import { useEffect, useState } from 'react'
import { View, Text } from '@tarojs/components'
import Taro from '@tarojs/taro'
import { AppShell } from '../../components'
import { api, type Profile as ProfileData } from '../../services/api'

const membershipLabel: Record<string, string> = {
  none: '未开通',
  trialing: '会员体验中',
  active: '会员有效',
  expired: '已过期',
}

function Profile() {
  const [profile, setProfile] = useState<ProfileData>()

  useEffect(() => {
    api.getProfile().then(setProfile)
  }, [])

  return (
    <AppShell title='我的烘焙' subtitle='学习进度、订单和会员权益集中管理'>
      {!profile ? <View className='panel page-subtitle'>加载中...</View> : null}

      {profile ? (
        <>
          <View className='profile-card panel'>
            <View className='course-card__footer'>
              <View>
                <Text className='page-title'>{profile.account.displayName}</Text>
                <View className='page-subtitle'>
                  已学习 {profile.learningDays} 天 · 连续打卡 {profile.streakDays} 天
                </View>
              </View>
              <Text className='chip chip--accent'>{membershipLabel[profile.membershipStatus] || profile.membershipStatus}</Text>
            </View>
          </View>

          <View className='stats-grid'>
            <View className='stat-card'>
              <View className='stat-card__value'>{profile.purchasedCourses}</View>
              <View className='stat-card__label'>已购课程</View>
            </View>
            <View className='stat-card'>
              <View className='stat-card__value'>{profile.completedSteps}</View>
              <View className='stat-card__label'>完成步骤</View>
            </View>
            <View className='stat-card'>
              <View className='stat-card__value'>{profile.checkInCount}</View>
              <View className='stat-card__label'>作品打卡</View>
            </View>
          </View>
        </>
      ) : null}

      <View className='panel'>
        <View className='menu-row' onClick={() => Taro.navigateTo({ url: '/pages/membership/index' })}>
          <Text>会员中心</Text>
          <Text>›</Text>
        </View>
        <View className='menu-row' onClick={() => Taro.navigateTo({ url: '/pages/cart/index' })}>
          <Text>购物车 / 订单</Text>
          <Text>›</Text>
        </View>
        <View className='menu-row'>
          <Text>学习记录</Text>
          <Text>›</Text>
        </View>
        <View className='menu-row'>
          <Text>材料包地址</Text>
          <Text>›</Text>
        </View>
      </View>
    </AppShell>
  )
}

export default Profile
