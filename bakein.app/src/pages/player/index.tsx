import { useState } from 'react'
import { View, Text } from '@tarojs/components'
import Taro, { useRouter } from '@tarojs/taro'
import { AppShell, BottomActionBar, SectionHeader, StepList } from '../../components'
import { courseSteps, getCourse } from '../../data/mock'

function Player() {
  const router = useRouter()
  const course = getCourse(String(router.params.id || ''))
  const [doneIds, setDoneIds] = useState<string[]>([])

  const toggleStep = (id: string) => {
    setDoneIds((current) => (current.includes(id) ? current.filter((item) => item !== id) : [...current, id]))
  }

  const progress = `${doneIds.length}/${courseSteps.length}`

  return (
    <AppShell title={course.title} subtitle={`跟做进度 ${progress}`} withAction>
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
          手忙脚乱时先看高亮步骤，完成后勾选，系统会把下一步推到最前。
        </View>
      </View>

      <SectionHeader title='分步骤跟做' action={`${progress} 完成`} />
      <StepList steps={courseSteps} doneIds={doneIds} onToggle={toggleStep} />

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
