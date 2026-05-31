import { View, Text } from '@tarojs/components'
import Taro from '@tarojs/taro'
import { AppShell } from '../../components'

function Profile() {
  return (
    <AppShell title='我的烘焙' subtitle='学习进度、订单和会员权益集中管理'>
      <View className='profile-card panel'>
        <View className='course-card__footer'>
          <View>
            <Text className='page-title'>烘焙新手</Text>
            <View className='page-subtitle'>已学习 5 天 · 连续打卡 2 天</View>
          </View>
          <Text className='chip chip--accent'>会员体验中</Text>
        </View>
      </View>

      <View className='stats-grid'>
        <View className='stat-card'>
          <View className='stat-card__value'>6</View>
          <View className='stat-card__label'>已购课程</View>
        </View>
        <View className='stat-card'>
          <View className='stat-card__value'>12</View>
          <View className='stat-card__label'>完成步骤</View>
        </View>
        <View className='stat-card'>
          <View className='stat-card__value'>3</View>
          <View className='stat-card__label'>作品打卡</View>
        </View>
      </View>

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
