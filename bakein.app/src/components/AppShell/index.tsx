import type { ReactNode } from 'react'
import { View } from '@tarojs/components'
import { FloatingCart } from '../FloatingCart'

type AppShellProps = {
  title?: string
  subtitle?: string
  withAction?: boolean
  children: ReactNode
}

export function AppShell({ title, subtitle, withAction, children }: AppShellProps) {
  return (
    <View className={withAction ? 'page-shell page-shell--with-action' : 'page-shell'}>
      {title ? <View className='page-title'>{title}</View> : null}
      {subtitle ? <View className='page-subtitle'>{subtitle}</View> : null}
      {children}
      <FloatingCart />
    </View>
  )
}
