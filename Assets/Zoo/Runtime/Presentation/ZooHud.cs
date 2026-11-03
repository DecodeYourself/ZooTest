using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Zoo.Simulation;

namespace Zoo.Presentation
{
    public sealed class ZooHud : MonoBehaviour
    {
        sealed class Label { public AnimalActor Actor; public Text Text; public float Until; }
        readonly List<Label> labels = new();
        Text counts;
        Text status;
        Canvas canvas;
        Font font;
        Camera worldCamera;

        public void Initialize(Camera camera, Action reload, Action updateVisuals)
        {
            worldCamera = camera;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            gameObject.AddComponent<GraphicRaycaster>();
            MakeText("Title", "ZOO WORLD", new Vector2(24, -22), new Vector2(350, 38), new Vector2(0, 1), 28);
            MakeText("Subtitle", "A small world, driven by data", new Vector2(25, -64), new Vector2(370, 30), new Vector2(0, 1), 17);
            counts = MakeText("Deaths", "", new Vector2(-24, -24), new Vector2(300, 95), Vector2.one, 21);
            counts.alignment = TextAnchor.UpperRight;
            status = MakeText("Content status", "", new Vector2(24, 24), new Vector2(740, 62), Vector2.zero, 16);
            Button("Reload JSON", new Vector2(-24, 24), reload);
            Button("Update content", new Vector2(-210, 24), updateVisuals);
        }

        Text MakeText(string name, string value, Vector2 position, Vector2 size, Vector2 anchor, int fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var text = go.AddComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.color = new Color(0.91f, 0.96f, 0.94f);
            text.text = value;
            text.raycastTarget = false;
            return text;
        }

        void Button(string title, Vector2 position, Action action)
        {
            var go = new GameObject(title, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(transform, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1, 0);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(170, 40);
            go.GetComponent<Image>().color = new Color(0.12f, 0.28f, 0.25f, 0.95f);
            go.GetComponent<Button>().onClick.AddListener(() => action());
            var text = MakeText(title + " label", title, Vector2.zero, Vector2.zero, Vector2.one * 0.5f, 17);
            text.transform.SetParent(go.transform, false);
            var tr = text.rectTransform;
            tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
            tr.offsetMin = tr.offsetMax = Vector2.zero;
            text.alignment = TextAnchor.MiddleCenter;
        }

        public void Refresh(int prey, int predators, int alive, string contentStatus)
        {
            counts.text = "DEATHS\nPrey  " + prey + "    Predators  " + predators + "\nAlive  " + alive;
            status.text = contentStatus;
        }

        public void ShowTasty(AnimalActor animal)
        {
            var text = MakeText("Tasty", "Tasty!", Vector2.zero, new Vector2(120, 32), Vector2.one * 0.5f, 22);
            text.color = new Color(1, 0.84f, 0.4f);
            text.alignment = TextAnchor.MiddleCenter;
            labels.Add(new Label { Actor = animal, Text = text, Until = Time.time + 1.1f });
        }

        void LateUpdate()
        {
            for (int i = labels.Count - 1; i >= 0; i--)
            {
                var item = labels[i];
                if (!item.Actor || !item.Actor.IsAlive || Time.time >= item.Until)
                { Destroy(item.Text.gameObject); labels.RemoveAt(i); continue; }
                var screen = worldCamera.WorldToScreenPoint(item.Actor.transform.position);
                screen.y -= 38;
                RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)canvas.transform, screen, null, out var point);
                item.Text.rectTransform.anchoredPosition = point;
            }
        }
    }
}
