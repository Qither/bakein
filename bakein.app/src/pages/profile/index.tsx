import { useEffect, useState } from 'react'
import { Button, Image, Input, Text, View } from '@tarojs/components'
import Taro from '@tarojs/taro'
import { AppShell } from '../../components'
import { api, ApiRequestError, AuthRequiredError, type Profile as ProfileData } from '../../services/api'

type InputValueEvent = {
  detail: {
    value: string
  }
}

type ChooseAvatarEvent = {
  detail: {
    avatarUrl?: string
  }
}

const membershipLabel: Record<string, string> = {
  none: '未开通',
  trialing: '会员体验中',
  active: '会员有效',
  expired: '已过期',
}

function Profile() {
  const [profile, setProfile] = useState<ProfileData>()
  const [loading, setLoading] = useState(api.isAuthenticated())
  const [registering, setRegistering] = useState(false)
  const [nickname, setNickname] = useState('')
  const [avatarUrl, setAvatarUrl] = useState('')
  const [error, setError] = useState('')

  async function loadProfile() {
    setLoading(true)
    try {
      const data = await api.getProfile()
      setProfile(data)
      setNickname(data.wechatIdentity?.displayName || data.account.displayName)
      setAvatarUrl(data.wechatIdentity?.avatarUrl || '')
      setError('')
    } catch (loadError) {
      if (loadError instanceof AuthRequiredError) {
        setProfile(undefined)
        return
      }

      setError('资料加载失败，请稍后重试')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    if (!api.isAuthenticated()) {
      setLoading(false)
      return
    }

    loadProfile()
  }, [])

  async function handleRegister() {
    const nextNickname = nickname.trim()
    if (!nextNickname) {
      setError('请选择或填写微信昵称')
      return
    }

    setRegistering(true)
    setError('')
    try {
      await api.registerWithWechatProfile({
        nickName: nextNickname,
        avatarUrl: avatarUrl || null,
      })
      await loadProfile()
      Taro.showToast({ title: '注册成功', icon: 'success' })
    } catch (registerError) {
      if (registerError instanceof ApiRequestError && registerError.code === 'wechat_not_configured') {
        setError('后端还未配置微信 AppId / AppSecret')
      } else if (registerError instanceof ApiRequestError && registerError.code === 'wechat_code_invalid') {
        setError('微信登录凭证已失效，请重新点击注册')
      } else {
        setError('微信注册失败，请重试')
      }
    } finally {
      setRegistering(false)
    }
  }

  const identity = profile?.wechatIdentity
  const displayName = identity?.displayName || profile?.account.displayName || '微信用户'
  const displayAvatar = identity?.avatarUrl || avatarUrl

  return (
    <AppShell title='我的烘焙' subtitle='微信身份、学习进度、订单和会员权益'>
      {loading ? <View className='panel page-subtitle'>加载中...</View> : null}

      {error ? <View className='panel profile-error'>{error}</View> : null}

      {!profile && !loading ? (
        <View className='panel wechat-register'>
          <View className='wechat-register__header'>
            <Text className='section-header__title'>微信快速注册</Text>
            <Text className='chip chip--accent'>真实后端账号</Text>
          </View>

          <View className='wechat-register__identity'>
            <Button className='wechat-avatar-button' openType='chooseAvatar' onChooseAvatar={(event: ChooseAvatarEvent) => {
              if (event.detail.avatarUrl) {
                setAvatarUrl(event.detail.avatarUrl)
              }
            }}
            >
              {avatarUrl ? (
                <Image className='wechat-avatar' src={avatarUrl} mode='aspectFill' />
              ) : (
                <Text className='wechat-avatar__placeholder'>头像</Text>
              )}
            </Button>

            <View className='wechat-register__fields'>
              <Text className='profile-field-label'>微信昵称</Text>
              <Input
                className='profile-nickname-input'
                type='nickname'
                value={nickname}
                placeholder='选择或填写昵称'
                onInput={(event: InputValueEvent) => setNickname(event.detail.value)}
                onBlur={(event: InputValueEvent) => setNickname(event.detail.value)}
              />
            </View>
          </View>

          <Button className='action-button profile-register-button' loading={registering} onClick={handleRegister}>
            使用微信身份注册
          </Button>
        </View>
      ) : null}

      {profile ? (
        <>
          <View className='profile-card panel'>
            <View className='profile-identity-row'>
              {displayAvatar ? (
                <Image className='profile-avatar' src={displayAvatar} mode='aspectFill' />
              ) : (
                <View className='profile-avatar profile-avatar--placeholder'>{displayName.slice(0, 1)}</View>
              )}
              <View className='profile-identity-copy'>
                <Text className='page-title'>{displayName}</Text>
                <View className='page-subtitle'>微信身份 {identity ? '已注册' : '未绑定'} · 已学 {profile.learningDays} 天 · 连续打卡 {profile.streakDays} 天</View>
              </View>
              <Text className='chip chip--accent'>{membershipLabel[profile.membershipStatus] || profile.membershipStatus}</Text>
            </View>
          </View>

          <View className='panel profile-wechat-card'>
            <Text className='section-header__title'>注册所选微信身份</Text>
            <View className='profile-wechat-card__body'>
              {identity?.avatarUrl ? <Image className='profile-wechat-card__avatar' src={identity.avatarUrl} mode='aspectFill' /> : null}
              <View>
                <Text className='profile-wechat-card__name'>{identity?.displayName || profile.account.displayName}</Text>
                <View className='page-subtitle'>{identity ? '来自微信头像昵称填写能力' : '当前账号还未写入微信身份'}</View>
              </View>
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
