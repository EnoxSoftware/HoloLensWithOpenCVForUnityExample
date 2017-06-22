using System;
using UnityEngine;

namespace HoloLensWithOpenCVForUnityExample
{
    [RequireComponent(typeof(MeshRenderer), typeof(MeshFilter), typeof(MeshCollider))]
    public class RectQuad : MonoBehaviour
    {
        public MeshFilter meshFilter
        {
            get { return _meshFilter; }
        }
        private MeshFilter _meshFilter;

        public MeshRenderer meshRenderer
        {
            get { return _meshRenderer; }
        }
        private MeshRenderer _meshRenderer;

        public MeshCollider meshCollider
        {
            get { return _meshCollider; }
        }
        private MeshCollider _meshCollider;

        public int id
        {
            get { return _id; }
            set { _id = value; }
        }
        private int _id = 0;

        public Material material
        {
            get { return _material; }
        }
        private Material _material;

        void Awake()
        {
            _meshFilter = this.GetComponent<MeshFilter>();
            _meshRenderer = this.GetComponent<MeshRenderer>();
            _meshCollider = this.GetComponent<MeshCollider>();

            if (_meshRenderer.material == null)
                throw new Exception("Material does not exist.");

            _material = _meshRenderer.material;
            _meshRenderer.sortingOrder = 32767;
        }

        void OnDestroy(){
            if(_meshFilter != null && _meshFilter.mesh != null){
                DestroyImmediate(_meshFilter.mesh);
            }
            if(_meshRenderer != null && _meshRenderer.materials != null){
                foreach(var m in _meshRenderer.materials){
                    DestroyImmediate(m);
                }
            }
        }
    }
}