﻿using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Scene = UnityEngine.SceneManagement.Scene;

namespace Jnk.TinyContainer
{
    [DefaultExecutionOrder(ExecutionOrder.EXECUTION_ORDER_CONTAINER)]
    [DisallowMultipleComponent]
    [AddComponentMenu("TinyContainer/TinyContainer")]
    public class TinyContainer : MonoBehaviour
    {
        private static TinyContainer _global;
        private static Dictionary<Scene, TinyContainer> _sceneContainers;
        private static List<GameObject> _temporarySceneGameObjects;

        public static TinyContainer ByLevel(ContainerLevel level, Component obj)
        {
            switch (level)
            {
                case ContainerLevel.Global:
                    return Global;
                case ContainerLevel.Scene:
                    return ForSceneOf(obj);
                case ContainerLevel.Local:
                default:
                    return For(obj);                            
            }
        }
        
        /// <summary>
        /// The global container instance.
        /// </summary>
        public static TinyContainer Global
        {
            get
            {
                if (_global != null)
                    return _global;

                if (FindObjectOfType<TinyContainerGlobal>() is {} global)
                {
                    global.BootstrapOnDemand();
                    return _global;
                }

                var container = new GameObject("TinyContainer [Global]", typeof(TinyContainer));
                container.AddComponent<TinyContainerGlobal>();

                return _global;
            }
        }

        public static bool IsGlobalConfigured => _global != null;

        [SerializeField] private EventFunction enabledEventFunctions = EventFunction.FixedUpdate | EventFunction.Update | EventFunction.LateUpdate;
        [SerializeField] private bool disposeOnDestroy = true;

        private readonly Dictionary<Type, object> _instances = new Dictionary<Type, object>();
        private readonly Dictionary<Type, Func<TinyContainer, object>> _factories = new Dictionary<Type, Func<TinyContainer, object>>();

        private readonly Dictionary<Type, List<object>> _onChangeInstanceCallbacks = new();
        
        public EventFunction EnabledEventFunctions
        {
            get => enabledEventFunctions;
            set => enabledEventFunctions = value;
        }

        public IEnumerable<object> RegisteredInstances => _instances.Values;

        internal void ConfigureAsGlobal(bool dontDestroyOnLoad)
        {
            if (_global != null && _global == this)
            {
                Debug.LogWarning("This TinyContainer has already been configured as the Global instance.", this);
                return;
            }

            if (_global != null && _global != this)
            {
                Debug.LogError("Another TinyContainer has already been configured as the Global instance.", this);
                return;
            }

            _global = this;

            if (dontDestroyOnLoad)
                DontDestroyOnLoad(_global);
        }

        internal void ConfigureForScene()
        {
            Scene scene = gameObject.scene;

            if (_sceneContainers.ContainsKey(scene))
            {
                Debug.LogError($"Another TinyContainer has already been configured for scene '{scene.name}'.", this);
                return;
            }

            _sceneContainers[scene] = this;
        }

        /// <summary>
        /// Returns the <see cref="TinyContainer"/> configured for the scene of the Component. Falls back to the global instance.
        /// </summary>
        public static TinyContainer ForSceneOf(Component component)
        {
            Scene scene = component.gameObject.scene;

            if (_sceneContainers.TryGetValue(scene, out TinyContainer container) && container != component)
                return container;

            _temporarySceneGameObjects.Clear();
            scene.GetRootGameObjects(_temporarySceneGameObjects);

            foreach (GameObject go in _temporarySceneGameObjects)
            {
                if (go.TryGetComponent(out TinyContainerScene sceneContainer) == false)
                    continue;

                if (sceneContainer.Container == component)
                    continue;

                sceneContainer.BootstrapOnDemand();
                return sceneContainer.Container;
            }

            return Global;
        }

        /// <summary>
        /// Returns the closest <see cref="TinyContainer"/> upwards in the hierarchy. Falls back to the scene container, then to the global instance.
        /// </summary>
        public static TinyContainer For(Component component, bool includeInactive = true)
        {
            return component.GetComponentInParent<TinyContainer>(includeInactive).IsNull() ?? ForSceneOf(component) ?? Global;
        }
        
        /// <summary>
        /// Register the instance with the container.
        /// </summary>
        public TinyContainer Register<T>(T instance, bool force = false)
        {
            Type type = typeof(T);
            return Register(type, instance, force);
        }

        /// <summary>
        /// Register the instance with the container.
        /// </summary>
        public TinyContainer Register(Type type, object instance, bool force = false)
        {
            if (type.IsInstanceOfType(instance) == false)
                throw new ArgumentException("Type of instance does not match.", nameof(instance));

            if (force || IsNotRegistered(type))
            {
                _instances[type] = instance;
                InvokeInstanceChangeCallbacks(instance);
            }

            return this;
        }

        /// <summary>
        /// Register a per-request factory method with the container.
        /// </summary>
        public TinyContainer RegisterPerRequest<T>(Func<TinyContainer, T> factoryMethod) where T : class
        {
            Type type = typeof(T);

            if (IsNotRegistered(type) == false)
                return this;

            if (type.IsAssignableFrom(typeof(IUpdateHandler)) == false)
                Debug.LogWarning($"Type {type.Name} was registered per request. {nameof(IUpdateHandler.Update)} will not be called.");

            if (type.IsAssignableFrom(typeof(IFixedUpdateHandler)) == false)
                Debug.LogWarning($"Type {type.Name} was registered per request. {nameof(IFixedUpdateHandler.FixedUpdate)} will not be called.");

            _factories[type] = factoryMethod;

            return this;
        }

        public void Deregister(Type type)
        {
            _instances.Remove(type);
            _factories.Remove(type);
        }

        private bool IsNotRegistered(Type type)
        {
            if (_instances.ContainsKey(type) == false &&
                _factories.ContainsKey(type) == false)
                return true;

            Debug.LogError($"Type {type.FullName} has already been registered.", this);
            return false;
        }

        /// <summary>
        /// Returns the first instance for the type upwards in the hierarchy.
        /// </summary>
        public TinyContainer Get<T>(out T instance) where T : class
        {
            Type type = typeof(T);
            instance = null;

            if (TryGetInstance(type, ref instance))
                return this;

            if (TryGetInstanceFromFactory(type, ref instance))
                return this;

            if (TryGetNextContainerInHierarchy(out TinyContainer nextContainer))
            {
                nextContainer.Get(out instance);
                return this;
            }

            throw new TypeNotFoundException($"Could not resolve type '{typeof(T).Name}'.");
        }

        public T Get<T>() where T : class
        {
            Type type = typeof(T);
            T instance = null;

            if (TryGetInstance(type, ref instance))
                return instance;

            if (TryGetInstanceFromFactory(type, ref instance))
                return instance;

            if (TryGetNextContainerInHierarchy(out TinyContainer nextContainer))
                return nextContainer.Get<T>();

            throw new TypeNotFoundException($"Could not resolve type '{typeof(T).Name}'.");
        }

        /// <summary>
        /// Returns the first instance for the type upwards in the hierarchy.
        /// </summary>
        public bool TryGet<T>(out T instance) where T : class
        {
            Type type = typeof(T);
            instance = null;

            if (TryGetInstance(type, ref instance))
                return true;

            if (TryGetInstanceFromFactory(type, ref instance))
                return true;

            return TryGetNextContainerInHierarchy(out TinyContainer nextContainer) && nextContainer.TryGet(out instance);
        }

        private bool TryGetInstance<T>(Type type, ref T instance) where T : class
        {
            if (false == _instances.TryGetValue(type, out object obj))
                return false;

            instance = (T) obj;

            return true;
        }

        private bool TryGetInstanceFromFactory<T>(Type type, ref T instance) where T : class
        {
            if (false == _factories.TryGetValue(type, out Func<TinyContainer, object> objFactory))
                return false;

            instance = (T) objFactory.Invoke(this);

            return true;
        }

        private bool TryGetNextContainerInHierarchy(out TinyContainer container)
        {
            if (this == _global)
            {
                container = null;
                return false;
            }

            container = transform.parent.IsNull()?.GetComponentInParent<TinyContainer>().IsNull() ?? ForSceneOf(this);
            return true;
        }

        private void InvokeInstanceChangeCallbacks<T>(T instance)
        {
            var type = typeof(T);
            if (_onChangeInstanceCallbacks.TryGetValue(type, out var actions))
            {
                foreach (Action<T> action in actions)
                {
                    action?.Invoke(instance);
                }
            }
        }
        
        public void SubscribeToChange<T>(Action<T> onChange)
        {
            var type = typeof(T);
            if (!_onChangeInstanceCallbacks.TryGetValue(type, out var actions))
            {
                actions = new List<object>();
                actions.Add(onChange);
                _onChangeInstanceCallbacks.Add(type, actions);
            }
            
            actions.Add(onChange);
        }
        
        public void UnsubscribeToChange<T>(Action<T> onChange)
        {
            var type = typeof(T);
            if (_onChangeInstanceCallbacks.TryGetValue(type, out var actions))
            {
                actions.Remove(onChange);
                if (actions.Count == 0)
                {
                    _onChangeInstanceCallbacks.Remove(type);
                }
            }
        }

        private void FixedUpdate()
        {
            if (!enabledEventFunctions.HasFlag(EventFunction.FixedUpdate))
                return;

            foreach (object instance in _instances.Values)
                (instance as IFixedUpdateHandler)?.FixedUpdate();
        }
        
        private void Update()
        {
            if (!enabledEventFunctions.HasFlag(EventFunction.Update))
                return;

            foreach (object instance in _instances.Values)
                (instance as IUpdateHandler)?.Update();
        }

        private void LateUpdate()
        {
            if (!enabledEventFunctions.HasFlag(EventFunction.LateUpdate))
                return;

            foreach (object instance in _instances.Values)
                (instance as ILateUpdateHandler)?.LateUpdate();
        }

        private void OnDestroy()
        {
            UnregisterForSceneIfNecessary();
            HandleRegisteredIDisposables();
        }

        private void UnregisterForSceneIfNecessary()
        {
            if (_sceneContainers == null || _sceneContainers.ContainsValue(this) == false)
                return;

            bool removedSuccessfully = _sceneContainers.Remove(gameObject.scene);
            Debug.Assert(removedSuccessfully, "Error when removing TinyContainer from scene dictionary. You might have moved the container to a different scene. This is not supported.", this);
        }

        private void HandleRegisteredIDisposables()
        {
            if (disposeOnDestroy == false) return;

            foreach (IDisposable disposable in _instances.Values.OfType<IDisposable>())
                disposable.Dispose();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticFields()
        {
            _global = null;
            _sceneContainers = new Dictionary<Scene, TinyContainer>();
            _temporarySceneGameObjects = new List<GameObject>();
        }

        #if UNITY_EDITOR

        [MenuItem("GameObject/TinyContainer/Add Global Container")]
        private static void AddGlobalContainer()
        {
            var go = new GameObject("TinyContainer [Global]", typeof(TinyContainerGlobal));
        }

        [MenuItem("GameObject/TinyContainer/Add Scene Container")]
        private static void AddSceneContainer()
        {
            var go = new GameObject("TinyContainer [Scene]", typeof(TinyContainerScene));
        }

        #endif
    }
}
