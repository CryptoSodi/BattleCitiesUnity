using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace BattleCities.UI
{
    // Runs after local layout/state refreshes. Cached bindings preserve actions and dynamic account data.
    [DefaultExecutionOrder(30000), DisallowMultipleComponent]
    public sealed class RemoteScreenView : MonoBehaviour
    {
        sealed class Target { public ScreenPatch Patch; public RectTransform Rect; public TMP_Text Text; public UnityEngine.UI.Text LegacyText; public UnityEngine.UI.Image Image; }
        sealed class Flow { public ScreenFlow Definition; public RectTransform Parent; public RectTransform[] Children; public float TotalWeight; public UnityEngine.UI.Selectable[][] Selectables; public UnityEngine.UI.Selectable Before,After; public readonly List<UnityEngine.UI.Selectable> Active=new List<UnityEngine.UI.Selectable>(); }
        string screenId, boundId, boundPlatform;
        Func<string> resolveId;
        int boundRevision = -1, warnedRevision = -1;
        float retryAt;
        RectTransform root;
        readonly List<Target> targets = new List<Target>();
        readonly List<Flow> flows = new List<Flow>();
        public bool BindingValid { get; private set; } = true;
        public static RemoteScreenView Bind(string id, RectTransform target, Func<string> resolver = null)
        {
            if (!target) return null;
            var view = target.GetComponent<RemoteScreenView>(); if (!view) view = target.gameObject.AddComponent<RemoteScreenView>();
            if(!Application.isPlaying)view.hideFlags=HideFlags.DontSaveInEditor;
            if(view.root!=target||view.screenId!=id||view.resolveId!=resolver){view.root=target;view.screenId=id;view.resolveId=resolver;view.boundRevision=-1;}
            return view;
        }
        void LateUpdate() { if (root && root.gameObject.activeInHierarchy) ApplyNow(Platform()); }
        static string Platform() => RuntimePlatformInfo.IsPsg1 ? "psg1" : RuntimePlatformInfo.IsAndroid ? "seeker" : "web";
        public void ApplyNow(string platform)
        {
            string id = resolveId == null ? screenId : resolveId();
            if (boundRevision != ScreenUpdates.Revision || boundId != id || boundPlatform != platform
                || (BindingValid&&targets.Any(x=>!x.Rect)) || (!BindingValid&&Time.unscaledTime>=retryAt)) Compile(id, platform);
            if (!BindingValid) return;
            foreach (var target in targets)
            {
                if (!target.Rect) continue;
                if (target.Patch.rect != null) SetRect(target.Rect, target.Patch.rect);
                if(target.Patch.text!=null) { if(target.Text&&target.Text.text!=target.Patch.text)target.Text.text=target.Patch.text;
                    if(target.LegacyText&&target.LegacyText.text!=target.Patch.text)target.LegacyText.text=target.Patch.text; }
                if (target.Text && target.Patch.fontSize.HasValue && target.Text.fontSizeMax != target.Patch.fontSize.Value)
                { target.Text.fontSizeMax = target.Patch.fontSize.Value; target.Text.fontSize = target.Patch.fontSize.Value; }
                if(target.LegacyText&&target.Patch.fontSize.HasValue){target.LegacyText.fontSize=target.LegacyText.resizeTextMaxSize=Mathf.RoundToInt(target.Patch.fontSize.Value);}
                if (target.Patch.image != null)
                {
                    var sprite = ScreenUpdates.Image(target.Patch.image);
                    if (sprite && target.Image.sprite != sprite) { target.Image.sprite = sprite; target.Image.type = UnityEngine.UI.Image.Type.Simple; target.Image.preserveAspect = true; }
                }
            }
            foreach (var flow in flows)
            {
                float available = flow.Parent.rect.height - flow.Definition.gap * (flow.Children.Length - 1);
                if (available <= 0) continue;
                float unit = available / flow.TotalWeight, y = 0;
                for (int i = 0; i < flow.Children.Length; i++)
                {
                    var child = flow.Children[i]; if (!child) continue;
                    float height = unit * flow.Definition.children[i].weight;
                    // Preserve the host's top-left placement convention and controller row bindings.
                    if (child.anchorMin != new Vector2(0, 1)) child.anchorMin = new Vector2(0, 1);
                    if (child.anchorMax != new Vector2(0, 1)) child.anchorMax = new Vector2(0, 1);
                    if (child.pivot != new Vector2(0, 1)) child.pivot = new Vector2(0, 1);
                    var position = new Vector2(0, -y); var size = new Vector2(flow.Parent.rect.width, height);
                    if (child.anchoredPosition != position) child.anchoredPosition = position;
                    if (child.sizeDelta != size) child.sizeDelta = size;
                    y += height + flow.Definition.gap;
                }
                flow.Active.Clear();
                foreach(var group in flow.Selectables)foreach(var selectable in group)
                    if(selectable&&selectable.IsActive()&&selectable.IsInteractable())flow.Active.Add(selectable);
                for(int i=0;i<flow.Active.Count;i++)
                {
                    var selectable=flow.Active[i];var navigation=selectable.navigation;navigation.mode=UnityEngine.UI.Navigation.Mode.Explicit;
                    navigation.selectOnUp=i>0?flow.Active[i-1]:flow.Before;
                    navigation.selectOnDown=i+1<flow.Active.Count?flow.Active[i+1]:flow.After;selectable.navigation=navigation;
                }
                if(flow.Active.Count>0)
                {
                    if(flow.Before){var navigation=flow.Before.navigation;navigation.selectOnDown=flow.Active[0];flow.Before.navigation=navigation;}
                    if(flow.After){var navigation=flow.After.navigation;navigation.selectOnUp=flow.Active[flow.Active.Count-1];flow.After.navigation=navigation;}
                }
            }
        }
        void Compile(string id, string platform)
        {
            targets.Clear(); flows.Clear(); BindingValid = true;
            boundId = id; boundPlatform = platform; boundRevision = ScreenUpdates.Revision;retryAt=Time.unscaledTime+1;
            var definition = ScreenUpdates.Definition(id, platform); if (definition == null) return;
            // Validate the complete binding before changing a single object. Unknown layouts fall back locally.
            var patches=new List<ScreenPatch>(definition.patches);
            foreach(var item in definition.art)if(item.Value!=null&&item.Key.Contains("/"))patches.Add(new ScreenPatch{path=item.Key,image=item.Value});
            foreach (var patch in patches)
            {
                var transform = root.Find(patch.path) as RectTransform;
                if (!transform || (patch.rect != null && transform.parent.GetComponent<UnityEngine.UI.LayoutGroup>())) { Invalid(id); return; }
                var target = new Target { Patch = patch, Rect = transform, Text = transform.GetComponent<TMP_Text>(), LegacyText=transform.GetComponent<UnityEngine.UI.Text>(), Image = transform.GetComponent<UnityEngine.UI.Image>() };
                if (((patch.text != null || patch.fontSize.HasValue) && !target.Text && !target.LegacyText) || (patch.image != null && !target.Image)) { Invalid(id); return; }
                targets.Add(target);
            }
            foreach (var value in definition.flows)
            {
                var parent = root.Find(value.parent) as RectTransform;
                if (!parent || parent.GetComponent<UnityEngine.UI.LayoutGroup>()) { Invalid(id); return; }
                var children = value.children.Select(x => parent.Find(x.path) as RectTransform).ToArray();
                if (children.Any(x => !x)) { Invalid(id); return; }
                var selectables=children.Select(x=>x.GetComponentsInChildren<UnityEngine.UI.Selectable>(true)).ToArray();
                var all=new HashSet<UnityEngine.UI.Selectable>(selectables.SelectMany(x=>x));
                var before=all.Select(x=>x.navigation.selectOnUp).FirstOrDefault(x=>x&&!all.Contains(x));
                var after=all.Select(x=>x.navigation.selectOnDown).FirstOrDefault(x=>x&&!all.Contains(x));
                flows.Add(new Flow { Parent = parent, Definition = value, Children = children, TotalWeight=value.children.Sum(x=>x.weight),Selectables=selectables,Before=before,After=after });
            }
        }
        void Invalid(string id) { targets.Clear(); flows.Clear(); BindingValid = false; if(warnedRevision!=boundRevision){warnedRevision=boundRevision;Debug.LogWarning("Screen definition does not match " + id + "; using local layout.");} }
        static void SetRect(RectTransform rect, float[] bounds)
        {
            var minimum = new Vector2(bounds[0], 1 - bounds[1] - bounds[3]); var maximum = new Vector2(bounds[0] + bounds[2], 1 - bounds[1]);
            if (rect.pivot != new Vector2(.5f, .5f)) rect.pivot = new Vector2(.5f, .5f);
            if (rect.anchorMin != minimum) rect.anchorMin = minimum; if (rect.anchorMax != maximum) rect.anchorMax = maximum;
            if (rect.offsetMin != Vector2.zero) rect.offsetMin = Vector2.zero; if (rect.offsetMax != Vector2.zero) rect.offsetMax = Vector2.zero;
        }
    }
}
