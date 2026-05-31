import { View, Text } from '@tarojs/components'
import Taro from '@tarojs/taro'
import { AppShell, SectionHeader } from '../../components'
import { communityPosts } from '../../data/mock'

function Community() {
  return (
    <AppShell title='作品打卡' subtitle='看别人怎么完成同一节课，也把自己的成品留下来'>
      <View className='hero-card'>
        <Text className='page-title'>今天完成一道烘焙了吗？</Text>
        <View className='page-subtitle'>发布作品后可获得老师点评和同学反馈。</View>
        <View className='action-button' style={{ marginTop: 12, width: 118 }} onClick={() => Taro.showToast({ title: '发布入口待接入', icon: 'none' })}>
          发布打卡
        </View>
      </View>

      <SectionHeader title='精选作品' />
      {communityPosts.map((post) => (
        <View key={post.id} className='community-card panel'>
          <View className='image-placeholder'>
            <Text>作品照片</Text>
          </View>
          <View className='course-card__footer' style={{ marginTop: 10 }}>
            <Text className='course-card__title'>{post.author}</Text>
            <Text className='chip chip--accent'>{post.course}</Text>
          </View>
          <View className='page-subtitle'>{post.text}</View>
          <View className='page-subtitle'>
            ♥ {post.likes} · 评论 {post.comments}
          </View>
        </View>
      ))}
    </AppShell>
  )
}

export default Community
