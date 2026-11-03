using System;
using UnityEngine;

namespace Zoo.Presentation
{
    public sealed class CameraArena
    {
        readonly Camera camera;
        public CameraArena(Camera camera)
        { this.camera = camera ? camera : throw new ArgumentNullException(nameof(camera)); }
        public void SetHalfHeight(float value) => camera.orthographicSize = value;
        public Rect Bounds
        {
            get
            {
                var half = camera.orthographicSize;
                var width = half * camera.aspect;
                var center = camera.transform.position;
                return new Rect(center.x - width, center.z - half, width * 2, half * 2);
            }
        }
    }
}
