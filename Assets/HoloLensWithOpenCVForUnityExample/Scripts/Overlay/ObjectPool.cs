using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HoloLensWithOpenCVForUnityExample
{
    public class ObjectPool : MonoBehaviour
    {
        public GameObject prefab;
        public int maxCount = 100;
        public int prepareCount = 0;

        [SerializeField]
        private int interval = 1;

        private List<GameObject> pooledObjectList = new List<GameObject>();
        private IEnumerator removeObjectCheckCoroutine;

        void OnEnable()
        {
            if (interval > 0){
                removeObjectCheckCoroutine = RemoveObjectCheck();
                StartCoroutine(removeObjectCheckCoroutine);
            }
        }

        void OnDisable()
        {
            if (removeObjectCheckCoroutine != null){
                StopCoroutine(removeObjectCheckCoroutine);
                removeObjectCheckCoroutine = null;
            }
        }

        void OnDestroy()
        {
            DestroyAllObjects ();
        }

        public int Interval
        {
            get
            {
                return interval;
            }
            set
            {
                if (interval != value)
                {
                    interval = value;

                    if (removeObjectCheckCoroutine != null){
                        StopCoroutine(removeObjectCheckCoroutine);
                        removeObjectCheckCoroutine = null;
                    }
                    if (interval > 0){
                        removeObjectCheckCoroutine = RemoveObjectCheck();
                        StartCoroutine(removeObjectCheckCoroutine);
                    }
                }
            }
        }

        public GameObject GetInstance()
        {
            return GetInstance(transform);
        }

        public GameObject GetInstance(Transform parent)
        {
            if (prefab == null){
                Debug.LogWarning("prefab object is not set.");
                return null;
            }

            pooledObjectList.RemoveAll((obj) => obj == null);

            foreach (GameObject obj in pooledObjectList)
            {
                if (obj.activeSelf == false)
                {
                    obj.SetActive(true);
                    return obj;
                }
            }

            if (pooledObjectList.Count < maxCount)
            {
                GameObject obj = (GameObject)GameObject.Instantiate(prefab);
                obj.SetActive(true);
                obj.transform.SetParent (parent, false);
                pooledObjectList.Add(obj);
                return obj;
            }

            return null;
        }

        IEnumerator RemoveObjectCheck()
        {
            while (true)
            {
                RemoveObject(prepareCount);
                yield return new WaitForSeconds(interval);
            }
        }

        public void RemoveObject(int max)
        {
            if (pooledObjectList.Count > max)
            {
                
                int needRemoveCount = pooledObjectList.Count - max;
                foreach (GameObject obj in pooledObjectList.ToArray())
                {
                    if (needRemoveCount == 0)
                    {
                        break;
                    }
                    if (obj.activeSelf == false)
                    {
                        pooledObjectList.Remove(obj);
                        Destroy(obj);
                        needRemoveCount--;
                    }
                }
            }
        }

        public void DestroyAllObjects ()
        {
            foreach (var obj in pooledObjectList)
            {
                Destroy(obj);
            }
            pooledObjectList.Clear();
        }
    }
}