import { useCallback, useEffect, useState } from 'react'
import { Image, Text, View } from '@tarojs/components'
import Taro, { useDidShow } from '@tarojs/taro'
import shoppingCartIcon from '../../assets/icons/shopping-cart.svg'
import { api, CART_UPDATED_EVENT, type Cart as CartData } from '../../services/api'

function getCartItemCount(cart: CartData) {
  return cart.items.reduce((total, item) => total + item.quantity, 0)
}

export function FloatingCart() {
  const [count, setCount] = useState(0)

  const refreshCount = useCallback(() => {
    api
      .getCart()
      .then((cart) => setCount(getCartItemCount(cart)))
      .catch(() => setCount(0))
  }, [])

  useDidShow(refreshCount)

  useEffect(() => {
    refreshCount()

    const handleCartUpdated = (cart?: CartData) => {
      if (cart) {
        setCount(getCartItemCount(cart))
        return
      }

      refreshCount()
    }

    Taro.eventCenter.on(CART_UPDATED_EVENT, handleCartUpdated)
    return () => {
      Taro.eventCenter.off(CART_UPDATED_EVENT, handleCartUpdated)
    }
  }, [refreshCount])

  const badgeText = count > 99 ? '99+' : String(count)

  return (
    <View
      className={count > 0 ? 'floating-cart floating-cart--active' : 'floating-cart'}
      hoverClass='floating-cart--pressed'
      onClick={() => Taro.navigateTo({ url: '/pages/cart/index' })}>
      <Image className='floating-cart__icon' src={shoppingCartIcon} mode='aspectFit' />
      <Text className={count > 0 ? 'floating-cart__badge floating-cart__badge--active' : 'floating-cart__badge'}>
        {badgeText}
      </Text>
    </View>
  )
}
