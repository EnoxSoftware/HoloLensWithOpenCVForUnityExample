using System;
using UnityEngine;
using System.Collections.Generic;

namespace HoloLensWithOpenCVForUnityExample
{
    public class RectOverlay : MonoBehaviour
    {

        public int Interval = 1;
        public int PoolSize = 50;
        
        [SerializeField]
        private GameObject baseObject;
        public GameObject BaseObject
        {
            get {
                return baseObject;
            }
            set {
                baseObject = value;
                setBaseObject(baseObject);
            }
        }
        
        public float Width
        {
            get {
                return targetWidth;
            }
        }
        
        public float Height
        {
            get {
                return targetHeight;
            }
        }

        protected Transform overlayTransform;
        protected Transform targetTransform;
        protected float targetWidth = 0;
        protected float targetHeight = 0;
        protected ObjectPool objectPool;

        void Awake()
        {
            init("RectOverlay");
        }

        void OnDestroy()
        {
            overlayTransform = null;
            targetTransform = null;
            targetWidth = 0;
            targetHeight = 0;
            if(objectPool != null)
            {
                Destroy(objectPool.gameObject);
                objectPool = null;
            }
        }

        protected GameObject getPoolObject(Transform parent)
        {
            if(objectPool == null) return null;
            
            GameObject newObj = objectPool.GetInstance(parent);
            if(newObj != null){
                newObj.transform.SetParent (parent, false);
                return newObj;
            }else{
                return null;
            }
        }
        
        protected virtual void init(String name)
        {
            GameObject overlay = new GameObject(name);

            overlayTransform = overlay.transform;
            overlayTransform.parent = gameObject.transform.parent;
            
            if(baseObject != null)
                setBaseObject (baseObject);
        }

        protected virtual void setBaseObject (GameObject obj)
        {
            if (obj.GetComponent<RectQuad>() == null)
            {
                Debug.LogWarning("Object is not RectQuad.");
                return;
            }
            
            if(objectPool != null){
                Destroy(objectPool);
            }
            
            objectPool = overlayTransform.gameObject.AddComponent<ObjectPool>();
            objectPool.prefab = obj;
            objectPool.maxCount = PoolSize;
            objectPool.prepareCount = (int)PoolSize / 2;
            objectPool.Interval = Interval;
        }

        public virtual void UpdateOverlayTransform(Transform targetTransform)
        {
            if (targetTransform == null)
            {
                this.targetTransform = null;
                return;
            }

            this.targetTransform = targetTransform;
            targetWidth = targetTransform.localScale.x;
            targetHeight = targetTransform.localScale.y;
            overlayTransform.localPosition = targetTransform.localPosition;
            overlayTransform.localRotation = targetTransform.localRotation;
            overlayTransform.localScale = targetTransform.localScale;
        }

        public void DrawRects(UnityEngine.Rect[] rects)
        {
            if (rects == null)
                throw new ArgumentNullException("rects");

            if (targetTransform == null) return;

            ResetRects();

            foreach (UnityEngine.Rect rect in rects)
            {
                GameObject poolObject = getPoolObject(overlayTransform);
                if (poolObject == null) return;

                Debug.Log (rect);
                var rectTransform = poolObject.transform;
                rectTransform.localPosition = new Vector3(rect.x + rect.width / 2 -0.5f, 0.5f - rect.y - rect.height / 2, 0);
                rectTransform.localScale = new Vector3(rect.width, rect.height, 1);
            }
        }

        public void ResetRects()
        {
            foreach (Transform child in overlayTransform)
            {
                child.gameObject.SetActive(false);
            }
        }
    }
}