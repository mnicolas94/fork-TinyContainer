using UnityEngine;

namespace Jnk.TinyContainer
{
    [DefaultExecutionOrder(ExecutionOrder.EXECUTION_ORDER_BOOTSTRAPPER)]
    [AddComponentMenu("TinyContainer/TinyContainer Global")]
    public class TinyContainerGlobal : TinyContainerBootstrapperBase
    {
        [SerializeField]
        private bool dontDestroyOnLoad = true;
        public bool DoNotDestroyOnLoad
        {
            get => dontDestroyOnLoad;
            set => dontDestroyOnLoad = value;
        }

        protected override void Bootstrap()
        {
            Container.ConfigureAsGlobal(dontDestroyOnLoad);
        }

        public void BootstrapOnDemand() => Bootstrap();
    }
}
