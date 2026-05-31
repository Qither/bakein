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
        iconPath: 'assets/tabbar/home.png',
        selectedIconPath: 'assets/tabbar/home-active.png',
        text: '首页',
      },
      {
        pagePath: 'pages/courses/index',
        iconPath: 'assets/tabbar/courses.png',
        selectedIconPath: 'assets/tabbar/courses-active.png',
        text: '分类',
      },
      {
        pagePath: 'pages/community/index',
        iconPath: 'assets/tabbar/community.png',
        selectedIconPath: 'assets/tabbar/community-active.png',
        text: '社区',
      },
      {
        pagePath: 'pages/profile/index',
        iconPath: 'assets/tabbar/profile.png',
        selectedIconPath: 'assets/tabbar/profile-active.png',
        text: '我的',
      },
    ],
  },
})
