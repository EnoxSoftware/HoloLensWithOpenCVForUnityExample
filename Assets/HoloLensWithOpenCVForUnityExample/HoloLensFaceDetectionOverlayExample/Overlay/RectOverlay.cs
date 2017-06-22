using System;
using UnityEngine;
using System.Collections.Generic;

namespace HoloLensWithOpenCVForUnityExample
{
    public class RectOverlay : MonoBehaviour
    {
        public int interval = 1;
        public int poolSize = 50;
        
        [SerializeField]
        private GameObject _baseObject;
        public GameObject baseObject
        {
            get {
                return _baseObject;
            }
            set {
                _baseObject = value;
                SetBaseObject(_baseObject);
            }
        }
        
        public float width
        {
            get {
                return targetWidth;
            }
        }
        
        public float height
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
            Initialize("RectOverlay");
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

        protected GameObject GetPoolObject(Transform parent)
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
        
        protected virtual void Initialize(String name)
        {
            GameObject overlay = new GameObject(name);

            overlayTransform = overlay.transform;
            overlayTransform.parent = gameObject.transform.parent;
            
            if(_baseObject != null)
                SetBaseObject (_baseObject);
        }

        protected virtual void SetBaseObject (GameObject obj)
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
            objectPool.maxCount = poolSize;
            objectPool.prepareCount = (int)poolSize / 2;
            objectPool.Interval = interval;
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
                GameObject poolObject = GetPoolObject(overlayTransform);
                if (poolObject == null) return;

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