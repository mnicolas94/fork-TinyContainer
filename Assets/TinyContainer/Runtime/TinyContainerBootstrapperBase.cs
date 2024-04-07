using UnityEngine;

namespace Jnk.TinyContainer
{
    [DefaultExecutionOrder(ExecutionOrder.EXECUTION_ORDER_BOOTSTRAPPER)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TinyContainer))]
    public abstract class TinyContainerBootstrapperBase : MonoBehaviour
    {
        private TinyContainer _container;
        internal TinyContainer Container => _container.IsNull() ?? (_container = GetComponent<TinyContainer>());

        private void Awake() => Bootstrap();

        protected abstract void Bootstrap();
    }
}
