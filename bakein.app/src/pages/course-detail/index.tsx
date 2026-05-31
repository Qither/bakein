import { useEffect, useState } from 'react'
import { View, Text } from '@tarojs/components'
import Taro, { useRouter } from '@tarojs/taro'
import { AppShell, BottomActionBar, PriceTag, SectionHeader } from '../../components'
import { api, type CourseDetail as CourseDetailData } from '../../services/api'

const tabs = ['简介', '目录', '评价']

function CourseDetail() {
  const router = useRouter()
  const courseId = String(router.params.id || '')
  const [detail, setDetail] = useState<CourseDetailData>()
  const [tab, setTab] = useState('目录')

  useEffect(() => {
    let active = true
    api.getCourseDetail(courseId || 'soft-bread').then((data) => {
      if (active) setDetail(data)
    })

    return () => {
      active = false
    }
  }, [courseId])

  const addCourse = async () => {
    if (!detail) return
    await api.addCartItem({ itemType: 'course', skuId: detail.course.id, quantity: 1 })
    Taro.showToast({ title: '已加入购物车', icon: 'success' })
  }

  const addMaterialKit = async () => {
    if (!detail?.materialKit) return
    await api.addCartItem({ itemType: 'material_kit', skuId: detail.materialKit.id, quantity: 1 })
    Taro.showToast({ title: '已加入购物车', icon: 'success' })
  }

  if (!detail) {
    return (
      <AppShell title='课程详情'>
        <View className='panel page-subtitle'>加载中...</View>
      </AppShell>
    )
  }

  const { course, steps, reviews, materialKit } = detail

  return (
    <AppShell withAction>
      <View className='image-placeholder image-placeholder--warm' style={{ minHeight: 168 }}>
        <Text>课程预览 · 03:20 试看</Text>
      </View>

      <View className='panel'>
        <Text className='page-title'>{course.title}</Text>
        <View className='page-subtitle'>
          {course.teacher} · ★ {course.ratingLabel} · {course.students} 在学
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
            {steps.map((step, index) => (
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
            {reviews.map((review) => (
              <View key={review.id} className='menu-row'>
                <Text>{review.content}</Text>
                <Text>{review.rating}</Text>
              </View>
            ))}
          </View>
        ) : null}
      </View>

      {materialKit ? (
        <>
          <SectionHeader title='材料包' action='加入购物车 ›' onAction={addMaterialKit} />
          <View className='course-card course-card--list' onClick={addMaterialKit}>
            <View className='image-placeholder course-card__cover'>
              <Text>材料包</Text>
            </View>
            <View className='course-card__body'>
              <Text className='course-card__title'>{materialKit.name}</Text>
              <View className='course-card__meta'>{materialKit.description}</View>
              <View className='course-card__footer'>
                <Text className='chip chip--accent'>可选购</Text>
                <PriceTag value={materialKit.price} />
              </View>
            </View>
          </View>
        </>
      ) : null}

      <BottomActionBar
        label='单课购买'
        value={course.price}
        tertiaryText={materialKit ? '材料包' : undefined}
        secondaryText='加入课程'
        primaryText='开始学习'
        onTertiary={addMaterialKit}
        onSecondary={addCourse}
        onPrimary={() => Taro.navigateTo({ url: `/pages/player/index?id=${course.id}` })}
      />
    </AppShell>
  )
}

export default CourseDetail
