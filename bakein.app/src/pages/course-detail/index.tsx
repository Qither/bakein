import { useState } from 'react'
import { View, Text } from '@tarojs/components'
import Taro, { useRouter } from '@tarojs/taro'
import { AppShell, BottomActionBar, PriceTag, SectionHeader } from '../../components'
import { courseSteps, getCourse } from '../../data/mock'

const tabs = ['简介', '目录', '评价']

function CourseDetail() {
  const router = useRouter()
  const course = getCourse(String(router.params.id || ''))
  const [tab, setTab] = useState('目录')

  return (
    <AppShell withAction>
      <View className='image-placeholder image-placeholder--warm' style={{ minHeight: 168 }}>
        <Text>课程预览视频 · 03:20 试看</Text>
      </View>

      <View className='panel'>
        <Text className='page-title'>{course.title}</Text>
        <View className='page-subtitle'>
          {course.teacher} · ★ {course.rating} · {course.students} 在学
        </View>
        <View className='chip-row' style={{ marginTop: 12 }}>
          {course.tags.map((tag, index) => (
            <Text key={tag} className={index === 0 ? 'chip chip--accent' : 'chip'}>
              {tag}
            </Text>
          ))}
        </View>
      </View>

      <View className='segment'>
        {tabs.map((item) => (
          <Text key={item} className={item === tab ? 'segment__item segment__item--active' : 'segment__item'} onClick={() => setTab(item)}>
            {item}
          </Text>
        ))}
      </View>

      <View className='panel'>
        {tab === '简介' ? <View className='page-subtitle'>{course.intro}</View> : null}
        {tab === '目录' ? (
          <View>
            {courseSteps.map((step, index) => (
              <View key={step.id} className='menu-row'>
                <Text>
                  {index + 1}. {step.title}
                </Text>
                <Text>{step.time}</Text>
              </View>
            ))}
          </View>
        ) : null}
        {tab === '评价' ? (
          <View>
            <View className='menu-row'>
              <Text>小白也能听懂，步骤很清楚</Text>
              <Text>4.9</Text>
            </View>
            <View className='menu-row'>
              <Text>材料替代提示很实用</Text>
              <Text>4.8</Text>
            </View>
          </View>
        ) : null}
      </View>

      <SectionHeader title='材料包' action='加入购物车 ›' onAction={() => Taro.navigateTo({ url: '/pages/cart/index' })} />
      <View className='course-card course-card--list' onClick={() => Taro.navigateTo({ url: '/pages/cart/index' })}>
        <View className='image-placeholder course-card__cover'>
          <Text>材料包</Text>
        </View>
        <View className='course-card__body'>
          <Text className='course-card__title'>{course.title} 配套材料包</Text>
          <View className='course-card__meta'>已按新手用量分装，减少采购成本。</View>
          <View className='course-card__footer'>
            <Text className='chip chip--accent'>可选购</Text>
            <PriceTag value='¥46' />
          </View>
        </View>
      </View>

      <BottomActionBar
        label='单课购买'
        value={course.price}
        tertiaryText='材料包'
        secondaryText='会员学习'
        primaryText='开始学习'
        onTertiary={() => Taro.navigateTo({ url: '/pages/cart/index' })}
        onSecondary={() => Taro.navigateTo({ url: '/pages/membership/index' })}
        onPrimary={() => Taro.navigateTo({ url: `/pages/player/index?id=${course.id}` })}
      />
    </AppShell>
  )
}

export default CourseDetail
