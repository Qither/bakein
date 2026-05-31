import Taro from '@tarojs/taro'

const AUTH_TOKEN_KEY = 'bakeinAuthToken'
const DEFAULT_API_BASE_URL = 'http://localhost:5164'
const API_BASE_URL = (
  typeof __API_BASE_URL__ === 'string' && __API_BASE_URL__ ? __API_BASE_URL__ : DEFAULT_API_BASE_URL
).replace(/\/$/, '')

type HttpMethod = 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE'

type RequestOptions = {
  method?: HttpMethod
  data?: unknown
  auth?: boolean
}

type ApiErrorPayload = {
  code?: string
  message?: string
}

export type Category = {
  id: string
  name: string
  sortOrder: number
}

export type Course = {
  id: string
  title: string
  category: string
  cover: string
  durationMinutes: number
  duration: string
  level: string
  priceCents: number
  price: string
  memberFree: boolean
  teacher: string
  rating: number
  ratingLabel: string
  studentCount: number
  students: string
  tags: string[]
  intro: string
}

export type CourseStep = {
  id: string
  title: string
  description: string
  durationSeconds: number
  time: string
  sortOrder: number
}

export type CourseReview = {
  id: string
  author: string
  content: string
  rating: number
  createdAt: string
}

export type MaterialKit = {
  id: string
  courseId: string
  name: string
  description: string
  priceCents: number
  price: string
}

export type CourseDetail = {
  course: Course
  steps: CourseStep[]
  reviews: CourseReview[]
  materialKit?: MaterialKit | null
}

export type HomeFeed = {
  categories: Category[]
  beginnerCourses: Course[]
  popularCourses: Course[]
}

export type MembershipPlan = {
  id: string
  name: string
  priceCents: number
  price: string
  billingPeriod: string
  description: string
  sortOrder: number
}

export type CommunityPost = {
  id: string
  author: string
  courseId?: string | null
  courseTitle?: string | null
  text: string
  imageText: string
  likes: number
  comments: number
  createdAt: string
}

export type Account = {
  id: string
  email: string
  displayName: string
  role: string
  createdAt: string
}

export type ExternalIdentity = {
  provider: string
  displayName: string
  avatarUrl?: string | null
  updatedAt: string
}

export type Profile = {
  account: Account
  membershipStatus: string
  learningDays: number
  streakDays: number
  purchasedCourses: number
  completedSteps: number
  checkInCount: number
  wechatIdentity?: ExternalIdentity | null
}

export type CartItem = {
  id: string
  itemType: string
  skuId: string
  name: string
  unitPriceCents: number
  unitPrice: string
  quantity: number
  selected: boolean
  lineTotalCents: number
  lineTotal: string
}

export type Cart = {
  items: CartItem[]
  totalCents: number
  total: string
}

export const CART_UPDATED_EVENT = 'bakein:cart-updated'

function notifyCartUpdated(cart?: Cart) {
  Taro.eventCenter.trigger(CART_UPDATED_EVENT, cart)
}

export type Order = {
  id: string
  orderNo: string
  status: string
  totalCents: number
  total: string
  createdAt: string
}

export type LearningProgress = {
  courseId: string
  completedStepIds: string[]
  completedSteps: number
  totalSteps: number
}

type AuthResponse = {
  token: string
  expiresAt: string
  account: Account
}

export class AuthRequiredError extends Error {
  constructor() {
    super('AUTH_REQUIRED')
    this.name = 'AuthRequiredError'
  }
}

export class ApiRequestError extends Error {
  constructor(
    public readonly statusCode: number,
    public readonly path: string,
    public readonly code?: string,
    message?: string,
  ) {
    super(message || `API ${statusCode}: ${path}`)
    this.name = 'ApiRequestError'
  }
}

function readAuthToken() {
  const token = Taro.getStorageSync<string>(AUTH_TOKEN_KEY)
  return typeof token === 'string' ? token : ''
}

function saveAuthToken(token: string) {
  Taro.setStorageSync(AUTH_TOKEN_KEY, token)
}

function clearAuthToken() {
  Taro.removeStorageSync(AUTH_TOKEN_KEY)
}

function buildUrl(path: string) {
  return `${API_BASE_URL}${path.startsWith('/') ? path : `/${path}`}`
}

function withQuery(path: string, params: Record<string, string | boolean | undefined>) {
  const query = Object.entries(params)
    .filter(([, value]) => value !== undefined && value !== '')
    .map(([key, value]) => `${encodeURIComponent(key)}=${encodeURIComponent(String(value))}`)
    .join('&')

  return query ? `${path}?${query}` : path
}

function readApiErrorPayload(data: unknown): ApiErrorPayload {
  if (typeof data === 'string') {
    try {
      return JSON.parse(data) as ApiErrorPayload
    } catch {
      return { message: data }
    }
  }

  if (data && typeof data === 'object') {
    const payload = data as Record<string, unknown>
    return {
      code: typeof payload.code === 'string' ? payload.code : undefined,
      message: typeof payload.message === 'string' ? payload.message : undefined,
    }
  }

  return {}
}

async function request<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const header: Record<string, string> = {}
  if (options.data !== undefined) {
    header['Content-Type'] = 'application/json'
  }

  if (options.auth) {
    const token = readAuthToken()
    if (!token) {
      throw new AuthRequiredError()
    }

    header.Authorization = `Bearer ${token}`
  }

  const response = await Taro.request<T>({
    url: buildUrl(path),
    method: options.method || 'GET',
    data: options.data,
    header,
  })

  if (response.statusCode === 401 && options.auth) {
    clearAuthToken()
    throw new AuthRequiredError()
  }

  if (response.statusCode < 200 || response.statusCode >= 300) {
    const payload = readApiErrorPayload(response.data)
    throw new ApiRequestError(response.statusCode, path, payload.code, payload.message)
  }

  return response.data
}

async function registerWithWechatProfile(profile: { nickName: string; avatarUrl?: string | null }) {
  const login = await Taro.login()
  if (!login.code) {
    throw new Error('Wechat login did not return a code.')
  }

  const response = await request<AuthResponse>('/api/auth/wechat/register', {
    method: 'POST',
    data: {
      code: login.code,
      profile,
    },
  })

  saveAuthToken(response.token)
  return response
}

export const api = {
  isAuthenticated: () => Boolean(readAuthToken()),
  clearAuth: clearAuthToken,
  registerWithWechatProfile,
  getHomeFeed: () => request<HomeFeed>('/api/catalog/home'),
  getCategories: () => request<Category[]>('/api/catalog/categories'),
  getCourses: (params: { category?: string; memberFree?: boolean; search?: string } = {}) =>
    request<Course[]>(withQuery('/api/courses', params)),
  getCourseDetail: (id: string) => request<CourseDetail>(`/api/courses/${encodeURIComponent(id)}`),
  getMembershipPlans: () => request<MembershipPlan[]>('/api/membership/plans'),
  getCommunityPosts: () => request<CommunityPost[]>('/api/community/posts'),
  createCommunityPost: (data: { courseId?: string; text: string; imageText?: string }) =>
    request<CommunityPost>('/api/community/posts', { method: 'POST', data, auth: true }),
  getProfile: () => request<Profile>('/api/users/me/profile', { auth: true }),
  getCart: () => request<Cart>('/api/users/me/cart', { auth: true }),
  addCartItem: async (data: { itemType: string; skuId: string; quantity?: number; selected?: boolean }) => {
    const cart = await request<Cart>('/api/users/me/cart/items', { method: 'PUT', data, auth: true })
    notifyCartUpdated(cart)
    return cart
  },
  updateCartItem: async (id: string, data: { quantity?: number; selected?: boolean }) => {
    const cart = await request<Cart>(`/api/users/me/cart/items/${encodeURIComponent(id)}`, {
      method: 'PATCH',
      data,
      auth: true,
    })
    notifyCartUpdated(cart)
    return cart
  },
  checkout: async () => {
    const order = await request<Order>('/api/users/me/cart/checkout', { method: 'POST', auth: true })
    notifyCartUpdated()
    return order
  },
  getProgress: (courseId: string) =>
    request<LearningProgress[]>(withQuery('/api/users/me/progress', { courseId }), { auth: true }),
  updateProgress: (data: { courseId: string; stepId: string; completed: boolean }) =>
    request<LearningProgress>('/api/users/me/progress', { method: 'PUT', data, auth: true }),
}
