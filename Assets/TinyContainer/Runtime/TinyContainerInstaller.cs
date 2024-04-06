using System.Collections.Generic;
using UnityEngine;

namespace Jnk.TinyContainer
{
    [DefaultExecutionOrder(ExecutionOrder.EXECUTION_ORDER_INSTALLER)]
    [AddComponentMenu("TinyContainer/TinyContainer Object Installer")]
    public class TinyContainerInstaller : MonoBehaviour
    {
        [SerializeField] private ContainerLevel _level;
        [SerializeField, Tooltip("Whether to deregister first in case an object of that type it's already registered")]
        private bool _force;
        
        [SerializeField]
        private List<Object> objects;

        protected void Awake()
        {
            var container = TinyContainer.ByLevel(_level, this);

            foreach (var obj in objects)
            {
                container.Register(obj.GetType(), obj, _force);
            }
        }
    }
}
