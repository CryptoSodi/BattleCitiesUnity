using UnityEngine;
using UnityEngine.EventSystems;

namespace BattleCities.UI
{
    public sealed class ShopCardFocus : MonoBehaviour,ISelectHandler,IPointerEnterHandler
    {
        ShopScreen shop;int product;
        public void Configure(ShopScreen owner,int index){shop=owner;product=index;}
        public void OnSelect(BaseEventData data){if(shop)shop.SelectProduct(product,true);}
        public void OnPointerEnter(PointerEventData data){if(shop)shop.SelectProduct(product,false);}
    }
}
