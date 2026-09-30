using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BattleCities.Multiplayer
{
    public sealed partial class BattleLobbyUI
    {
        GameObject codeKeypad;
        Button codeButton;
        Text codePreview;
        readonly List<Selectable[]> codeKeyRows = new List<Selectable[]>();

        void BuildRoomCodeKeypad()
        {
            codeButton=Button(panel.transform,"ENTER ROOM CODE",Vector2.zero,Vector2.zero,OpenRoomCodeKeypad);
            Place((RectTransform)codeButton.transform,.06f,.94f,.56f,.64f);
            codeButton.gameObject.SetActive(false);
            codeKeypad=new GameObject("Room code keypad",typeof(RectTransform),typeof(Image));
            codeKeypad.transform.SetParent(panel.transform,false);
            Place((RectTransform)codeKeypad.transform,.02f,.98f,.02f,.98f);
            codeKeypad.GetComponent<Image>().color=new Color(Dark.r,Dark.g,Dark.b,1f);
            var title=Label(codeKeypad.transform,"ENTER ROOM CODE",30,TextAnchor.MiddleCenter);
            Place(title.rectTransform,.04f,.96f,.88f,.98f);
            codePreview=Label(codeKeypad.transform,"",28,TextAnchor.MiddleCenter);
            Place(codePreview.rectTransform,.04f,.96f,.77f,.87f);
            const string characters="ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-";
            for(int row=0;row<7;row++)
            {
                var buttons=new List<Selectable>();
                for(int col=0;col<6;col++)
                {
                    int index=row*6+col;if(index>=characters.Length)break;
                    string character=characters[index].ToString();
                    var key=Button(codeKeypad.transform,character,Vector2.zero,Vector2.zero,()=>
                    {if(code.text.Length<code.characterLimit)code.text+=character;UpdateCodePreview();});
                    float left=.06f+col*.148f,top=.75f-row*.079f;
                    Place((RectTransform)key.transform,left,left+.138f,top-.069f,top);
                    buttons.Add(key);
                }
                codeKeyRows.Add(buttons.ToArray());
            }
            var delete=Button(codeKeypad.transform,"DELETE",Vector2.zero,Vector2.zero,()=>
            {if(code.text.Length>0)code.text=code.text.Substring(0,code.text.Length-1);UpdateCodePreview();});
            var clear=Button(codeKeypad.transform,"CLEAR",Vector2.zero,Vector2.zero,()=>{code.text="";UpdateCodePreview();});
            var done=Button(codeKeypad.transform,"DONE",Vector2.zero,Vector2.zero,CloseRoomCodeKeypad);
            Place((RectTransform)delete.transform,.06f,.34f,.10f,.18f);
            Place((RectTransform)clear.transform,.36f,.64f,.10f,.18f);
            Place((RectTransform)done.transform,.66f,.94f,.10f,.18f);
            codeKeyRows.Add(new Selectable[]{delete,clear,done});
            var hint=Label(codeKeypad.transform,"D-PAD / STICK: MOVE    A: SELECT    B: DONE",18,TextAnchor.MiddleCenter);
            Place(hint.rectTransform,.03f,.97f,.015f,.08f);
            codeKeypad.SetActive(false);
        }

        void UpdateCodePreview()=>codePreview.text=string.IsNullOrEmpty(code.text)?"e.g. ASIA-A1B2C3":code.text;
        void OpenRoomCodeKeypad()
        {
            if(Session.Busy||Session.Online)return;
            codeKeypad.SetActive(true);codeKeypad.transform.SetAsLastSibling();UpdateCodePreview();
            Psg1UiNavigation.Rows(codeKeyRows.ToArray());
            EventSystem.current.SetSelectedGameObject(codeKeyRows[0][0].gameObject);
        }
        void CloseRoomCodeKeypad()
        {
            codeKeypad.SetActive(false);
            if(EventSystem.current)EventSystem.current.SetSelectedGameObject(codeButton.gameObject);
        }
        void ConfigureLobbyNavigation()
        {
            if(codeKeypad.activeSelf){Psg1UiNavigation.KeepFocus(codeKeypad.transform,codeKeyRows[0][0]);return;}
            Psg1UiNavigation.Rows(new Selectable[]{mode,region},new Selectable[]{previousMap,nextMap},
                new Selectable[]{codeButton},new Selectable[]{create,join},new Selectable[]{start,rematch,leave},new Selectable[]{close});
            Psg1UiNavigation.KeepFocus(panel.transform,Session.QuickMatching?leave:close);
        }
    }
}
