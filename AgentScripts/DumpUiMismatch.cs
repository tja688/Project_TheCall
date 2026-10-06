using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

internal static class DumpUiMismatch
{
    public static string Main()
    {
        var sb = new StringBuilder();
        sb.AppendLine("playing=" + EditorApplication.isPlaying);
        var selected = Selection.activeGameObject;
        sb.AppendLine("selected=" + (selected != null ? PathOf(selected.transform) : "<null>"));

        var cam = Camera.main;
        if (cam != null)
        {
            sb.AppendLine("camera=" + cam.name
                + " ortho=" + cam.orthographic
                + " size=" + cam.orthographicSize
                + " fov=" + cam.fieldOfView
                + " pos=" + cam.transform.position
                + " rot=" + cam.transform.eulerAngles
                + " pixelRect=" + cam.pixelRect
                + " aspect=" + cam.aspect
                + " near=" + cam.nearClipPlane
                + " far=" + cam.farClipPlane
                + " culling=" + cam.cullingMask);
            foreach (var c in cam.GetComponents<Component>())
                sb.AppendLine("  camComp " + c.GetType().FullName);
        }

        var canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include);
        foreach (var canvas in canvases)
        {
            var scaler = canvas.GetComponent<CanvasScaler>();
            sb.AppendLine("CANVAS " + PathOf(canvas.transform)
                + " mode=" + canvas.renderMode
                + " scaleFactor=" + canvas.scaleFactor
                + " planeDistance=" + canvas.planeDistance
                + " worldCam=" + (canvas.worldCamera != null ? canvas.worldCamera.name : "<null>")
                + " pixelRect=" + canvas.pixelRect
                + " overrideSorting=" + canvas.overrideSorting
                + " sortingOrder=" + canvas.sortingOrder
                + " rootScale=" + canvas.transform.localScale
                + " rootPos=" + canvas.transform.position);
            if (scaler != null)
            {
                sb.AppendLine("  scaler mode=" + scaler.uiScaleMode
                    + " ref=" + scaler.referenceResolution
                    + " match=" + scaler.matchWidthOrHeight
                    + " scaleFactor=" + scaler.scaleFactor
                    + " refPixelsPerUnit=" + scaler.referencePixelsPerUnit
                    + " dynamic=" + scaler.dynamicPixelsPerUnit
                    + " physicalUnit=" + scaler.physicalUnit
                    + " fallbackDpi=" + scaler.fallbackScreenDPI
                    + " defaultSpriteDpi=" + scaler.defaultSpriteDPI);
            }
        }

        var focus = selected != null ? selected.transform : null;
        if (focus == null)
        {
            var research = GameObject.Find("Research Area");
            if (research != null)
                focus = research.transform;
        }

        if (focus != null)
        {
            sb.AppendLine("--- FOCUS TREE ---");
            DumpTree(focus, sb, 0, 6);
            var research = GameObject.Find("Research Area");
            if (research != null)
            {
                sb.AppendLine("--- RESEARCH AREA ---");
                DumpTree(research.transform, sb, 0, 5);
            }
        }

        return sb.ToString();
    }

    static void DumpTree(Transform t, StringBuilder sb, int depth, int maxDepth)
    {
        if (depth > maxDepth) return;
        var indent = new string(' ', depth * 2);
        var rt = t as RectTransform;
        sb.Append(indent).Append(t.name)
            .Append(" active=").Append(t.gameObject.activeInHierarchy)
            .Append(" layer=").Append(LayerMask.LayerToName(t.gameObject.layer))
            .Append(" pos=").Append(t.position)
            .Append(" localPos=").Append(t.localPosition)
            .Append(" localScale=").Append(t.localScale)
            .Append(" lossy=").Append(t.lossyScale);
        if (rt != null)
        {
            sb.Append(" anchorMin=").Append(rt.anchorMin)
                .Append(" anchorMax=").Append(rt.anchorMax)
                .Append(" pivot=").Append(rt.pivot)
                .Append(" anchored=").Append(rt.anchoredPosition)
                .Append(" sizeDelta=").Append(rt.sizeDelta)
                .Append(" rect=").Append(rt.rect)
                .Append(" offsetMin=").Append(rt.offsetMin)
                .Append(" offsetMax=").Append(rt.offsetMax);
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            sb.Append(" worldCorners=")
                .Append(corners[0]).Append("|").Append(corners[1]).Append("|")
                .Append(corners[2]).Append("|").Append(corners[3]);
        }
        sb.AppendLine();
        foreach (var c in t.GetComponents<Component>())
        {
            if (c == null)
            {
                sb.Append(indent).AppendLine("  MISSING SCRIPT");
                continue;
            }
            if (c is Transform || c is RectTransform) continue;
            sb.Append(indent).Append("  ").Append(c.GetType().Name);
            if (c is UnityEngine.UI.Image img)
            {
                sb.Append(" sprite=").Append(img.sprite != null ? img.sprite.name : "<null>")
                    .Append(" type=").Append(img.type)
                    .Append(" preserve=").Append(img.preserveAspect)
                    .Append(" color=").Append(img.color)
                    .Append(" enabled=").Append(img.enabled)
                    .Append(" pixelsPerUnitMul=").Append(img.pixelsPerUnitMultiplier);
                if (img.sprite != null)
                    sb.Append(" spriteRect=").Append(img.sprite.rect)
                        .Append(" ppu=").Append(img.sprite.pixelsPerUnit);
            }
            else if (c is RawImage raw)
            {
                sb.Append(" tex=").Append(raw.texture != null ? raw.texture.name : "<null>")
                    .Append(" uv=").Append(raw.uvRect)
                    .Append(" enabled=").Append(raw.enabled);
            }
            else if (c is SpriteRenderer sr)
            {
                sb.Append(" sprite=").Append(sr.sprite != null ? sr.sprite.name : "<null>")
                    .Append(" drawMode=").Append(sr.drawMode)
                    .Append(" size=").Append(sr.size)
                    .Append(" enabled=").Append(sr.enabled)
                    .Append(" sorting=").Append(sr.sortingLayerName).Append(":").Append(sr.sortingOrder)
                    .Append(" color=").Append(sr.color);
                if (sr.sprite != null)
                    sb.Append(" spriteRect=").Append(sr.sprite.rect)
                        .Append(" ppu=").Append(sr.sprite.pixelsPerUnit)
                        .Append(" bounds=").Append(sr.bounds);
            }
            else if (c is CanvasRenderer cr)
            {
                sb.Append(" cull=").Append(cr.cull)
                    .Append(" absDepth=").Append(cr.absoluteDepth)
                    .Append(" hasPop=").Append(cr.hasPopInstruction);
            }
            else if (c is AspectRatioFitter arf)
            {
                sb.Append(" mode=").Append(arf.aspectMode).Append(" ratio=").Append(arf.aspectRatio);
            }
            else if (c is ContentSizeFitter csf)
            {
                sb.Append(" h=").Append(csf.horizontalFit).Append(" v=").Append(csf.verticalFit);
            }
            else if (c is LayoutElement le)
            {
                sb.Append(" ignore=").Append(le.ignoreLayout)
                    .Append(" min=").Append(le.minWidth).Append("x").Append(le.minHeight)
                    .Append(" pref=").Append(le.preferredWidth).Append("x").Append(le.preferredHeight)
                    .Append(" flex=").Append(le.flexibleWidth).Append("x").Append(le.flexibleHeight);
            }
            else if (c is HorizontalOrVerticalLayoutGroup lg)
            {
                sb.Append(" childControlW=").Append(lg.childControlWidth)
                    .Append(" childControlH=").Append(lg.childControlHeight)
                    .Append(" childForceW=").Append(lg.childForceExpandWidth)
                    .Append(" childForceH=").Append(lg.childForceExpandHeight)
                    .Append(" spacing=").Append(lg.spacing)
                    .Append(" padding=").Append(lg.padding.left).Append(",").Append(lg.padding.top)
                    .Append(",").Append(lg.padding.right).Append(",").Append(lg.padding.bottom);
            }
            else if (c is GridLayoutGroup grid)
            {
                sb.Append(" cell=").Append(grid.cellSize)
                    .Append(" spacing=").Append(grid.spacing)
                    .Append(" constraint=").Append(grid.constraint)
                    .Append(" count=").Append(grid.constraintCount);
            }
            else if (c is Mask || c is RectMask2D)
            {
                sb.Append(" MASK");
            }
            sb.AppendLine();
        }
        for (int i = 0; i < t.childCount; i++)
            DumpTree(t.GetChild(i), sb, depth + 1, maxDepth);
    }

    static string PathOf(Transform t)
    {
        var s = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            s = t.name + "/" + s;
        }
        return s;
    }
}
