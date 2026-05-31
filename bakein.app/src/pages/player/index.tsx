import { useEffect, useState } from 'react'
import { View, Text } from '@tarojs/components'
import Taro, { useRouter } from '@tarojs/taro'
import { AppShell, BottomActionBar, SectionHeader, StepList } from '../../components'
import { api, type CourseDetail } from '../../services/api'

function Player() {
  const router = useRouter()
  const courseId = String(router.params.id || 'soft-bread')
  const [detail, setDetail] = useState<CourseDetail>()
  const [doneIds, setDoneIds] = useState<string[]>([])

  useEffect(() => {
    let active = true
    Promise.all([api.getCourseDetail(courseId), api.getProgress(courseId)]).then(([courseDetail, progress]) => {
      if (!active) return
      setDetail(courseDetail)
      setDoneIds(progress[0]?.completedStepIds || [])
    })

    return () => {
      active = false
    }
  }, [courseId])

  const toggleStep = async (id: string) => {
    if (!detail) return

    const completed = !doneIds.includes(id)
    setDoneIds((current) => (completed ? [...current, id] : current.filter((item) => item !== id)))

    try {
      const progress = await api.updateProgress({ courseId: detail.course.id, stepId: id, completed })
      setDoneIds(progress.completedStepIds)
    } catch {
      setDoneIds((current) => (completed ? current.filter((item) => item !== id) : [...current, id]))
      Taro.showToast({ title: '进度同步失败', icon: 'none' })
    }
  }

  if (!detail) {
    return (
      <AppShell title='课程学习'>
        <View className='panel page-subtitle'>加载中...</View>
      </AppShell>
    )
  }

  const progress = `${doneIds.length}/${detail.steps.length}`

  return (
    <AppShell title={detail.course.title} subtitle={`跟做进度 ${progress}`} withAction>
      <View className='image-placeholder image-placeholder--warm' style={{ minHeight: 176 }}>
        <Text>视频播放区 · 可随时回看</Text>
      </View>

      <View className='panel'>
        <View className='chip-row'>
          <Text className='chip chip--accent'>当前步骤</Text>
          <Text className='chip'>厨房模式</Text>
          <Text className='chip'>答疑入口</Text>
        </View>
        <View className='page-subtitle' style={{ marginTop: 10 }}>
          勾选已完成步骤后，进度会同步到后端。
        </View>
      </View>

      <SectionHeader title='分步骤跟做' action={`${progress} 完成`} />
      <StepList steps={detail.steps} doneIds={doneIds} onToggle={toggleStep} />

      <BottomActionBar
        label='学习进度'
        value={progress}
        secondaryText='提问'
        primaryText='完成打卡'
        onSecondary={() => Taro.showToast({ title: '已记录问题入口', icon: 'none' })}
        onPrimary={() => Taro.switchTab({ url: '/pages/community/index' })}
      />
    </AppShell>
  )
}

export default Player
