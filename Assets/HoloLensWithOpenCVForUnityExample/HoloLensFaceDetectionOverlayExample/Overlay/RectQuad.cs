using System;
using UnityEngine;

namespace HoloLensWithOpenCVForUnityExample
{
    [RequireComponent(typeof(MeshRenderer), typeof(MeshFilter), typeof(MeshCollider))]
    public class RectQuad : MonoBehaviour
    {

        private MeshFilter meshFilter;

        private MeshRenderer meshRenderer;

        private MeshCollider meshCollider;

        public MeshFilter MeshFilter
        {
            get { return meshFilter; }
        }
        public MeshRenderer MeshRenderer
        {
            get { return meshRenderer; }
        }
        public MeshCollider MeshCollider
        {
            get { return meshCollider; }
        }

        public int Id
        {
            get { return id; }
            set { id = value; }
        }
        private int id = 0;

        public Material Material
        {
            get { return material; }
        }
        private Material material;


        void Awake()
        {
            meshFilter = this.GetComponent<MeshFilter>();
            meshRenderer = this.GetComponent<MeshRenderer>();
            meshCollider = this.GetComponent<MeshCollider>();

            if (meshRenderer.material == null)
                throw new Exception("Material does not exist.");

            material = meshRenderer.material;
            meshRenderer.sortingOrder = 32767;
        }

        void OnDestroy(){
            if(meshFilter != null && meshFilter.mesh != null){
                DestroyImmediate(meshFilter.mesh);
            }
            if(meshRenderer != null && meshRenderer.materials != null){
                foreach(var m in meshRenderer.materials){
                    DestroyImmediate(m);
                }
            }
        }
    }
}