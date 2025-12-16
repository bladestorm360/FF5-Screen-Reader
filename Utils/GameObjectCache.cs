using System;
using System.Collections.Generic;
using UnityEngine;

namespace FFV_ScreenReader.Utils
{
    public static class GameObjectCache
    {
        private static Dictionary<Type, object> singleCache = new Dictionary<Type, object>();

        private static Dictionary<Type, List<object>> multiCache = new Dictionary<Type, List<object>>();

        private static object lockObject = new object();

        public static T Get<T>()
        {
            lock (lockObject)
            {
                Type type = typeof(T);

                if (singleCache.TryGetValue(type, out var cached))
                {
                    return (T)cached;
                }

                return default(T);
            }
        }

        public static List<T> GetAll<T>()
        {
            lock (lockObject)
            {
                Type type = typeof(T);

                if (!multiCache.TryGetValue(type, out var cached))
                {
                    return new List<T>();
                }

                List<object> validObjects = new List<object>();
                foreach (var obj in cached)
                {
                    validObjects.Add(obj);
                }

                multiCache[type] = validObjects;

                List<T> result = new List<T>();
                foreach (var obj in validObjects)
                {
                    result.Add((T)obj);
                }

                return result;
            }
        }

        public static void Register<T>(T obj)
        {
            if (obj == null)
                return;

            lock (lockObject)
            {
                Type type = typeof(T);
                singleCache[type] = obj;
            }
        }

        public static void RegisterMultiple<T>(T obj)
        {
            if (obj == null)
                return;

            lock (lockObject)
            {
                Type type = typeof(T);

                if (!multiCache.TryGetValue(type, out var list))
                {
                    list = new List<object>();
                    multiCache[type] = list;
                }

                if (!list.Contains(obj))
                {
                    list.Add(obj);
                }
            }
        }

        public static void UnregisterMultiple<T>(T obj)
        {
            if (obj == null)
                return;

            lock (lockObject)
            {
                Type type = typeof(T);

                if (multiCache.TryGetValue(type, out var list))
                {
                    list.Remove(obj);
                }
            }
        }

        public static bool Has<T>()
        {
            lock (lockObject)
            {
                Type type = typeof(T);

                if (singleCache.TryGetValue(type, out var cached))
                {
                    return true;
                }

                return false;
            }
        }

        public static T Refresh<T>() where T : UnityEngine.Object
        {
            lock (lockObject)
            {
                Type type = typeof(T);

                singleCache.Remove(type);

                T found = UnityEngine.Object.FindObjectOfType<T>();
                if (found != null)
                {
                    singleCache[type] = found;
                }

                return found;
            }
        }

        public static void Clear<T>()
        {
            lock (lockObject)
            {
                Type type = typeof(T);
                singleCache.Remove(type);
            }
        }

        public static void ClearMultiple<T>()
        {
            lock (lockObject)
            {
                Type type = typeof(T);
                multiCache.Remove(type);
            }
        }

        public static void ClearAll()
        {
            lock (lockObject)
            {
                singleCache.Clear();
                multiCache.Clear();
            }
        }
    }
}
