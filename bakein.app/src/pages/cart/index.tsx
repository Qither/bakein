import { useMemo, useState } from 'react'
import { View, Text } from '@tarojs/components'
import Taro from '@tarojs/taro'
import { AppShell, BottomActionBar } from '../../components'
import { cartItems } from '../../data/mock'

function Cart() {
  const [selectedIds, setSelectedIds] = useState(cartItems.map((item) => item.id))
  const [quantities, setQuantities] = useState<Record<string, number>>(
    cartItems.reduce((result, item) => ({ ...result, [item.id]: item.qty }), {}),
  )

  const total = useMemo(
    () =>
      cartItems.reduce((sum, item) => {
        if (!selectedIds.includes(item.id)) return sum
        return sum + item.price * (quantities[item.id] || 1)
      }, 0),
    [quantities, selectedIds],
  )

  const toggle = (id: string) => {
    setSelectedIds((current) => (current.includes(id) ? current.filter((item) => item !== id) : [...current, id]))
  }

  const changeQty = (id: string, delta: number) => {
    setQuantities((current) => ({ ...current, [id]: Math.max(1, (current[id] || 1) + delta) }))
  }

  return (
    <AppShell title='购物车 / 结算' subtitle='课程和材料包可以一起检查后结算' withAction>
      {cartItems.map((item) => {
        const selected = selectedIds.includes(item.id)
        const qty = quantities[item.id] || 1
        return (
          <View key={item.id} className='course-card course-card--list'>
            <View className={selected ? 'step-item__check step-item__check--done' : 'step-item__check'} onClick={() => toggle(item.id)} />
            <View className='course-card__body'>
              <Text className='course-card__title'>{item.name}</Text>
              <View className='page-subtitle'>
                {item.type} · ¥{item.price}
              </View>
              <View className='course-card__footer'>
                <View className='chip-row'>
                  <Text className='chip' onClick={() => changeQty(item.id, -1)}>
                    -
                  </Text>
                  <Text className='chip chip--accent'>{qty}</Text>
                  <Text className='chip' onClick={() => changeQty(item.id, 1)}>
                    +
                  </Text>
                </View>
                <Text className='price-tag'>¥{item.price * qty}</Text>
              </View>
            </View>
          </View>
        )
      })}

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
        value={`¥${total}`}
        primaryText='去结算'
        onPrimary={() => Taro.showToast({ title: '结算入口待接入', icon: 'none' })}
      />
    </AppShell>
  )
}

export default Cart
