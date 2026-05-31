const baseUrl = (process.env.TARO_APP_API_BASE_URL || process.env.API_BASE_URL || 'http://localhost:5164').replace(/\/$/, '')
const shouldSmokeWechat = process.env.WECHAT_SMOKE === 'true'

function assert(condition, message) {
  if (!condition) {
    throw new Error(message)
  }
}

async function request(path, options = {}) {
  const response = await fetch(`${baseUrl}${path}`, {
    ...options,
    headers: {
      ...(options.body ? { 'Content-Type': 'application/json' } : {}),
      ...(options.headers || {}),
    },
  })

  const text = await response.text()
  const data = text ? JSON.parse(text) : undefined
  if (!response.ok) {
    throw new Error(`${options.method || 'GET'} ${path} failed with ${response.status}: ${text}`)
  }

  return data
}

const health = await request('/health')
assert(health.status === 'ok', 'health endpoint did not return ok')

const readiness = await request('/api/operations/readiness')
assert(readiness.status === 'ready', 'readiness endpoint did not return ready')

const home = await request('/api/catalog/home')
assert(home.categories?.length >= 5, 'home feed should include categories')
assert(home.beginnerCourses?.length > 0, 'home feed should include beginner courses')
assert(home.popularCourses?.length > 0, 'home feed should include popular courses')

const firstCourse = home.beginnerCourses[0]
const detail = await request(`/api/courses/${encodeURIComponent(firstCourse.id)}`)
assert(detail.course?.id === firstCourse.id, 'course detail should match selected course')
assert(detail.steps?.length > 0, 'course detail should include steps')

const plans = await request('/api/membership/plans')
assert(plans.length > 0, 'membership plans should be available')

const posts = await request('/api/community/posts')
assert(posts.length > 0, 'community feed should be available')

const auth = await request('/api/auth/login', {
  method: 'POST',
  body: JSON.stringify({ email: 'demo@bakein.local', password: 'bakein123' }),
})
assert(auth.token, 'demo login should return a bearer token')

const authHeaders = { Authorization: `Bearer ${auth.token}` }
const profile = await request('/api/users/me/profile', { headers: authHeaders })
assert(profile.account?.email === 'demo@bakein.local', 'profile should belong to demo account')

const cart = await request('/api/users/me/cart/items', {
  method: 'PUT',
  headers: authHeaders,
  body: JSON.stringify({ itemType: 'course', skuId: firstCourse.id, quantity: 1, selected: true }),
})
assert(cart.items?.some((item) => item.skuId === firstCourse.id), 'cart should accept course items')

const step = detail.steps[0]
const progress = await request('/api/users/me/progress', {
  method: 'PUT',
  headers: authHeaders,
  body: JSON.stringify({ courseId: firstCourse.id, stepId: step.id, completed: true }),
})
assert(progress.completedStepIds.includes(step.id), 'progress should record completed step')

await request('/api/users/me/progress', {
  method: 'PUT',
  headers: authHeaders,
  body: JSON.stringify({ courseId: firstCourse.id, stepId: step.id, completed: false }),
})

if (shouldSmokeWechat) {
  const wechatName = process.env.WECHAT_SMOKE_NICKNAME || 'Bakein WeChat Smoke'
  const wechatAvatarUrl =
    process.env.WECHAT_SMOKE_AVATAR_URL || 'https://thirdwx.qlogo.cn/mmopen/vi_32/bakein-smoke-avatar/132'
  const wechatCode = process.env.WECHAT_SMOKE_CODE || `bakein-smoke-${Date.now()}`

  const wechatAuth = await request('/api/auth/wechat/register', {
    method: 'POST',
    body: JSON.stringify({
      code: wechatCode,
      profile: {
        nickName: wechatName,
        avatarUrl: wechatAvatarUrl,
      },
    }),
  })
  assert(wechatAuth.token, 'WeChat registration should return a bearer token')

  const wechatProfile = await request('/api/users/me/profile', {
    headers: { Authorization: `Bearer ${wechatAuth.token}` },
  })
  assert(wechatProfile.account?.displayName === wechatName, 'WeChat account should use the selected nickname')
  assert(wechatProfile.wechatIdentity?.displayName === wechatName, 'profile should include the selected WeChat nickname')
  assert(
    wechatProfile.wechatIdentity?.avatarUrl === wechatAvatarUrl,
    'profile should include the selected WeChat avatar URL',
  )
}

console.log(`API smoke passed against ${baseUrl}${shouldSmokeWechat ? ' with WeChat registration' : ''}`)
