using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using VContainer;
using Object = UnityEngine.Object;

namespace ToyCarArena.Gameplay
{
    /// <summary>
    /// Injects scene objects and spawned prefabs by calling their <c>Construct</c> methods with services from the
    /// container. VContainer would find those methods by their <c>[Inject]</c> attribute, but that attribute derives
    /// from <c>PreserveAttribute</c>, and Unity's linker removes it from player builds (WebGL, Unity 6000.6, at every
    /// stripping level), so the container would inject nothing there and every scene object would run without its
    /// services. The methods keep <c>[Inject]</c> for readers; this finds them by name instead. <c>Assets/link.xml</c>
    /// keeps them from being stripped. In the Editor, where the attribute is still there, an <c>[Inject]</c> method
    /// named otherwise is an error, so the convention is checked. VContainer's source generator would avoid reflection,
    /// but it takes no private inject methods, which every <c>Construct</c> here is.
    /// </summary>
    public static class SceneInjector
    {
        private const string MethodName = "Construct";
        private const BindingFlags Flags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        private static readonly Dictionary<Type, MethodInfo[]> Methods = new();
        private static readonly List<MonoBehaviour> Buffer = new();

        private static Transform holder;

        /// <summary>
        /// Calls <c>Construct</c> on every MonoBehaviour of <paramref name="root"/> and its children, inactive ones too.
        /// </summary>
        public static void InjectConstructs(this IObjectResolver resolver, GameObject root)
        {
            Buffer.Clear();
            root.GetComponentsInChildren(true, Buffer);
            // Copied first: a Construct may spawn and inject more objects.
            var behaviours = Buffer.ToArray();
            Buffer.Clear();
            foreach (var behaviour in behaviours) {
                // Null when its script is missing from the build.
                if (behaviour) {
                    Inject(resolver, behaviour);
                }
            }
        }

        public static T InstantiateInjected<T>(this IObjectResolver resolver, T prefab, Transform parent)
            where T : Component
        {
            return resolver.InstantiateInjected(prefab, prefab.transform.localPosition, prefab.transform.localRotation,
                parent, false);
        }

        public static T InstantiateInjected<T>(this IObjectResolver resolver, T prefab, Vector3 position,
            Quaternion rotation, Transform parent)
            where T : Component
        {
            return resolver.InstantiateInjected(prefab, position, rotation, parent, true);
        }

        private static T InstantiateInjected<T>(this IObjectResolver resolver, T prefab, Vector3 position,
            Quaternion rotation, Transform parent, bool isWorldSpace)
            where T : Component
        {
            // Made under an inactive parent, so nothing awakes before it has its services.
            var instance = Object.Instantiate(prefab, GetHolder());
            resolver.InjectConstructs(instance.gameObject);

            var transform = instance.transform;
            transform.SetParent(parent, false);
            if (isWorldSpace) {
                transform.SetPositionAndRotation(position, rotation);
            }
            else {
                transform.SetLocalPositionAndRotation(position, rotation);
            }

            return instance;
        }

        private static void Inject(IObjectResolver resolver, object instance)
        {
            foreach (var method in GetMethods(instance.GetType())) {
                var parameters = method.GetParameters();
                var arguments = new object[parameters.Length];
                for (var i = 0; i < parameters.Length; i++) {
                    arguments[i] = resolver.Resolve(parameters[i].ParameterType);
                }

                method.Invoke(instance, arguments);
            }
        }

        private static MethodInfo[] GetMethods(Type type)
        {
            if (Methods.TryGetValue(type, out var methods)) {
                return methods;
            }

            // Base classes first, as VContainer does.
            var found = new List<MethodInfo>();
            for (var current = type; current != null && current != typeof(MonoBehaviour); current = current.BaseType) {
                var method = current.GetMethod(MethodName, Flags);
                if (method != null) {
                    found.Insert(0, method);
                }
            }

#if UNITY_EDITOR
            CheckInjectNames(type);
#endif
            methods = found.ToArray();
            Methods.Add(type, methods);
            return methods;
        }

#if UNITY_EDITOR
        private static void CheckInjectNames(Type type)
        {
            for (var current = type; current != null && current != typeof(MonoBehaviour); current = current.BaseType) {
                foreach (var method in current.GetMethods(Flags)) {
                    if (method.Name != MethodName && method.IsDefined(typeof(InjectAttribute), false)) {
                        Debug.LogError($"[{nameof(SceneInjector)}] {current.Name}.{method.Name} has [Inject] but " +
                            $"player builds only inject methods named {MethodName}; rename it.");
                    }
                }
            }
        }
#endif

        private static Transform GetHolder()
        {
            if (!holder) {
                var holderObject = new GameObject("Injection Holder");
                holderObject.SetActive(false);
                Object.DontDestroyOnLoad(holderObject);
                holder = holderObject.transform;
            }

            return holder;
        }
    }
}
