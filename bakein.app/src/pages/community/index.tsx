import { useEffect, useState } from 'react'
import { View, Text } from '@tarojs/components'
import Taro from '@tarojs/taro'
import { AppShell, SectionHeader } from '../../components'
import { api, type CommunityPost } from '../../services/api'

function Community() {
  const [posts, setPosts] = useState<CommunityPost[]>([])

  const loadPosts = () => {
    api.getCommunityPosts().then(setPosts)
  }

  useEffect(() => {
    loadPosts()
  }, [])

  const publishCheckIn = async () => {
    await api.createCommunityPost({
      courseId: 'soft-bread',
      text: '今天完成了一次跟做练习。',
      imageText: '作品照片',
    })
    Taro.showToast({ title: '已发布', icon: 'success' })
    loadPosts()
  }

  return (
    <AppShell title='作品打卡' subtitle='看看同学怎么完成同一节课'>
      <View className='hero-card'>
        <Text className='page-title'>今天完成一道烘焙了吗？</Text>
        <View className='page-subtitle'>发布作品后可获得老师点评和同学反馈。</View>
        <View className='action-button' style={{ marginTop: 12, width: 118 }} onClick={publishCheckIn}>
          发布打卡
        </View>
      </View>

      <SectionHeader title='精选作品' />
      {posts.map((post) => (
        <View key={post.id} className='community-card panel'>
          <View className='image-placeholder'>
            <Text>{post.imageText}</Text>
          </View>
          <View className='course-card__footer' style={{ marginTop: 10 }}>
            <Text className='course-card__title'>{post.author}</Text>
            {post.courseTitle ? <Text className='chip chip--accent'>{post.courseTitle}</Text> : null}
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
