using System.Collections.Generic;
using UnityEngine;

namespace Jnk.TinyContainer
{
    public enum ContainerLevel
    {
        Global,
        Scene,
        Local
    }
    
    [AddComponentMenu("TinyContainer/TinyContainer Object Installer")]
    public class TinyContainerInstaller : MonoBehaviour
    {
        [SerializeField] private ContainerLevel _level;
        
        [SerializeField]
        private List<Object> objects;

        protected void Awake()
        {
            var container = GetContainer();

            foreach (var obj in objects)
                container.Register(obj.GetType(), obj);
        }

        private TinyContainer GetContainer()
        {
            switch (_level)
            {
                case ContainerLevel.Global:
                    return TinyContainer.Global;
                case ContainerLevel.Scene:
                    return TinyContainer.ForSceneOf(this);
                case ContainerLevel.Local:
                default:
                    return TinyContainer.For(this);                            
            }
        }
    }
}
