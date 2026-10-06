using UnityEngine;
using UnityEngine.EventSystems;

namespace BattleCities.UI
{
    public sealed class ShopCardFocus : MonoBehaviour,ISelectHandler,IDeselectHandler,IPointerEnterHandler,IPointerExitHandler,IPointerClickHandler
    {
        ShopScreen shop;int product;bool cardPointer;
        public void Configure(ShopScreen owner,int index,bool handleCardPointer=true){shop=owner;product=index;cardPointer=handleCardPointer;}
        public void OnSelect(BaseEventData data){if(shop)shop.FocusProduct(product,true);}
        public void OnDeselect(BaseEventData data){if(shop)shop.ClearProductFocus(product);}
        public void OnPointerEnter(PointerEventData data){if(shop&&cardPointer)shop.HoverProduct(product,true);}
        public void OnPointerExit(PointerEventData data){if(shop&&cardPointer)shop.HoverProduct(product,false);}
        public void OnPointerClick(PointerEventData data){if(shop&&cardPointer&&data.button==PointerEventData.InputButton.Left)shop.SelectProduct(product,true);}
    }
}
