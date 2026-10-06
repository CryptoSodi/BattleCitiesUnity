using UnityEngine;
using UnityEngine.UI;

namespace BattleCities.UI
{
    /// <summary>Clear card focus while preserving the existing gold activation artwork.</summary>
    public sealed class CardSelectionHighlight : MonoBehaviour
    {
        Image frame;
        Outline edge;
        bool activated,focused;
        Color restingTint=Color.white;

        public static CardSelectionHighlight Ensure(Image frame,Color? normalTint=null)
        {
            var highlight=frame.GetComponent<CardSelectionHighlight>();
            if(!highlight)highlight=frame.gameObject.AddComponent<CardSelectionHighlight>();
            highlight.frame=frame;
            highlight.edge=frame.GetComponent<Outline>();
            if(!highlight.edge)highlight.edge=frame.gameObject.AddComponent<Outline>();
            highlight.edge.useGraphicAlpha=true;
            if(normalTint.HasValue)highlight.restingTint=normalTint.Value;
            return highlight;
        }
        public void SetActivated(bool value){activated=value;Refresh();}
        public void SetFocused(bool value){focused=value;Refresh();}
        public void SetState(bool active,bool focus){activated=active;focused=focus;Refresh();}
        void Refresh()
        {
            if(!frame||!edge)return;
            frame.color=activated?Color.white:focused?new Color(.76f,.90f,1f):restingTint;
            edge.enabled=focused&&!activated;
            edge.effectColor=new Color32(37,203,255,255);
            edge.effectDistance=Vector2.one*3f;
        }
    }
}
