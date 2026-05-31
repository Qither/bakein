export type Course = {
  id: string
  title: string
  category: string
  cover: string
  duration: string
  level: string
  price: string
  memberFree: boolean
  teacher: string
  rating: string
  students: string
  tags: string[]
  intro: string
}

export type CourseStep = {
  id: string
  title: string
  desc: string
  time: string
}

export type MembershipPlan = {
  id: string
  name: string
  price: string
  desc: string
}

export type CommunityPost = {
  id: string
  author: string
  course: string
  text: string
  likes: number
  comments: number
}

export type CartItem = {
  id: string
  name: string
  type: string
  price: number
  qty: number
}

export const categories = ['面包', '蛋糕', '饼干', '甜点', '裱花']

export const courses: Course[] = [
  {
    id: 'soft-bread',
    title: '零失败软欧包',
    category: '面包',
    cover: '软欧包封面',
    duration: '40min',
    level: '新手',
    price: '¥39',
    memberFree: false,
    teacher: '王老师',
    rating: '4.9',
    students: '1.2k',
    tags: ['新手友好', '家用烤箱', '免揉技巧'],
    intro: '从称量、醒发到割包，用最少工具做出稳定蓬松的软欧包。',
  },
  {
    id: 'toast',
    title: '基础吐司课',
    category: '面包',
    cover: '吐司封面',
    duration: '35min',
    level: '新手',
    price: '¥29',
    memberFree: false,
    teacher: '林老师',
    rating: '4.8',
    students: '860',
    tags: ['揉面判断', '发酵观察', '早餐'],
    intro: '掌握吐司面团状态、一次发酵和二次发酵的关键判断。',
  },
  {
    id: 'chiffon',
    title: '戚风蛋糕不塌陷',
    category: '蛋糕',
    cover: '戚风封面',
    duration: '45min',
    level: '新手',
    price: '会员免费',
    memberFree: true,
    teacher: '陈老师',
    rating: '4.9',
    students: '2.1k',
    tags: ['蛋白打发', '翻拌', '脱模'],
    intro: '用可观察的状态点解决戚风塌腰、开裂和回缩问题。',
  },
  {
    id: 'cookies',
    title: '黄油曲奇入门',
    category: '饼干',
    cover: '曲奇封面',
    duration: '20min',
    level: '新手',
    price: '¥19',
    memberFree: false,
    teacher: '周老师',
    rating: '4.7',
    students: '720',
    tags: ['黄油软化', '裱花袋', '下午茶'],
    intro: '适合第一次使用裱花袋的酥松曲奇课程。',
  },
  {
    id: 'tart',
    title: '葡式蛋挞',
    category: '甜点',
    cover: '蛋挞封面',
    duration: '25min',
    level: '新手',
    price: '会员免费',
    memberFree: true,
    teacher: '宋老师',
    rating: '4.8',
    students: '940',
    tags: ['快手', '材料少', '亲子'],
    intro: '用成品挞皮快速完成稳定焦斑和嫩滑内馅。',
  },
  {
    id: 'piping',
    title: '奶油裱花基础',
    category: '裱花',
    cover: '裱花封面',
    duration: '60min',
    level: '进阶',
    price: '¥45',
    memberFree: false,
    teacher: '许老师',
    rating: '4.8',
    students: '680',
    tags: ['花嘴认识', '手法练习', '生日蛋糕'],
    intro: '从直线、贝壳边到玫瑰花，建立稳定手感。',
  },
]

export const beginnerCourseIds = ['soft-bread', 'chiffon', 'cookies']
export const popularCourseIds = ['soft-bread', 'chiffon', 'toast', 'tart']

export const courseSteps: CourseStep[] = [
  {
    id: 'prep',
    title: '准备材料并称量',
    desc: '面粉、酵母、水和盐先分区摆放，避免漏加。',
    time: '03:00',
  },
  {
    id: 'mix',
    title: '混合到无干粉',
    desc: '用刮刀压拌，盆底没有干粉后静置 10 分钟。',
    time: '06:00',
  },
  {
    id: 'fold',
    title: '折叠建立筋度',
    desc: '从四边向中心折叠，动作轻但要拉出张力。',
    time: '09:00',
  },
  {
    id: 'bake',
    title: '预热并入炉烘烤',
    desc: '观察上色后加盖锡纸，出炉敲底部有空响。',
    time: '18:00',
  },
]

export const membershipPlans: MembershipPlan[] = [
  { id: 'monthly', name: '月卡', price: '¥29', desc: '适合先体验，每周更新新手课。' },
  { id: 'season', name: '季卡', price: '¥79', desc: '包含会员课程与一次材料包券。' },
  { id: 'yearly', name: '年卡', price: '¥199', desc: '全年课程、直播答疑和作品点评。' },
]

export const communityPosts: CommunityPost[] = [
  {
    id: 'p1',
    author: '小鹿',
    course: '零失败软欧包',
    text: '第一次割包成功，外壳很脆，里面很软。',
    likes: 128,
    comments: 18,
  },
  {
    id: 'p2',
    author: '阿南',
    course: '戚风蛋糕不塌陷',
    text: '按步骤冷却倒扣，这次终于没有塌腰。',
    likes: 96,
    comments: 12,
  },
  {
    id: 'p3',
    author: 'Momo',
    course: '黄油曲奇入门',
    text: '花纹保持得不错，下次试试少糖版本。',
    likes: 73,
    comments: 9,
  },
]

export const cartItems: CartItem[] = [
  { id: 'course-soft-bread', name: '零失败软欧包课程', type: '课程', price: 39, qty: 1 },
  { id: 'kit-soft-bread', name: '软欧包材料包', type: '材料包', price: 46, qty: 1 },
]

export function getCourse(id?: string) {
  return courses.find((course) => course.id === id) || courses[0]
}

export function getCoursesByIds(ids: string[]) {
  return ids.map((id) => getCourse(id))
}
