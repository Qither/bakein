export default defineAppConfig({
  pages: [
    'pages/index/index',
    'pages/courses/index',
    'pages/course-detail/index',
    'pages/player/index',
    'pages/membership/index',
    'pages/community/index',
    'pages/cart/index',
    'pages/profile/index',
  ],
  window: {
    backgroundTextStyle: 'light',
    navigationBarBackgroundColor: '#f3efe6',
    navigationBarTitleText: '烘焙课堂',
    navigationBarTextStyle: 'black',
    backgroundColor: '#f3efe6',
  },
  tabBar: {
    color: '#a8a094',
    selectedColor: '#a8703f',
    backgroundColor: '#ffffff',
    borderStyle: 'black',
    list: [
      {
        pagePath: 'pages/index/index',
        text: '首页',
      },
      {
        pagePath: 'pages/courses/index',
        text: '分类',
      },
      {
        pagePath: 'pages/community/index',
        text: '社区',
      },
      {
        pagePath: 'pages/profile/index',
        text: '我的',
      },
    ],
  },
})
