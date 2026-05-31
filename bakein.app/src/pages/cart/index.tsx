import { useEffect, useState } from 'react'
import { View, Text } from '@tarojs/components'
import Taro from '@tarojs/taro'
import { AppShell, BottomActionBar } from '../../components'
import { api, type Cart as CartData } from '../../services/api'

function Cart() {
  const [cart, setCart] = useState<CartData>()
  const [submitting, setSubmitting] = useState(false)

  const loadCart = () => {
    api.getCart().then(setCart)
  }

  useEffect(() => {
    loadCart()
  }, [])

  const toggle = async (id: string, selected: boolean) => {
    setCart(await api.updateCartItem(id, { selected: !selected }))
  }

  const changeQty = async (id: string, quantity: number, delta: number) => {
    setCart(await api.updateCartItem(id, { quantity: Math.max(1, quantity + delta) }))
  }

  const checkout = async () => {
    if (!cart?.items.some((item) => item.selected) || submitting) return
    setSubmitting(true)
    try {
      const order = await api.checkout()
      Taro.showToast({ title: `订单 ${order.orderNo}`, icon: 'success' })
      loadCart()
    } catch {
      Taro.showToast({ title: '结算失败', icon: 'none' })
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <AppShell title='购物车 / 结算' subtitle='课程和材料包可一起结算' withAction>
      {!cart ? <View className='panel page-subtitle'>加载中...</View> : null}
      {cart && cart.items.length === 0 ? <View className='panel page-subtitle'>购物车为空</View> : null}

      {(cart?.items || []).map((item) => (
        <View key={item.id} className='course-card course-card--list'>
          <View className={item.selected ? 'step-item__check step-item__check--done' : 'step-item__check'} onClick={() => toggle(item.id, item.selected)} />
          <View className='course-card__body'>
            <Text className='course-card__title'>{item.name}</Text>
            <View className='page-subtitle'>
              {item.itemType} · {item.unitPrice}
            </View>
            <View className='course-card__footer'>
              <View className='chip-row'>
                <Text className='chip' onClick={() => changeQty(item.id, item.quantity, -1)}>
                  -
                </Text>
                <Text className='chip chip--accent'>{item.quantity}</Text>
                <Text className='chip' onClick={() => changeQty(item.id, item.quantity, 1)}>
                  +
                </Text>
              </View>
              <Text className='price-tag'>{item.lineTotal}</Text>
            </View>
          </View>
        </View>
      ))}

      <View className='panel'>
        <View className='menu-row'>
          <Text>材料包配送</Text>
          <Text>到家</Text>
        </View>
        <View className='menu-row'>
          <Text>优惠券</Text>
          <Text>会员券可用</Text>
        </View>
      </View>

      <BottomActionBar
        label='已选合计'
        value={cart?.total || '¥0'}
        primaryText={submitting ? '结算中' : '去结算'}
        onPrimary={checkout}
      />
    </AppShell>
  )
}

export default Cart
