﻿using UnityEngine;
using Jnk.TinyContainer;

namespace Examples
{
    public class ExampleInstaller : MonoBehaviour
    {
        private void Awake()
        {
            TinyContainer.Root
                .Register(new UpdatePrinter())
                .Register<ILocalization>(new MockLocalization());

            TinyContainer.ForSceneOf(this)
                .Register<ISerializer>(new JsonSerializer())
                .RegisterPerRequest(_ => new UpdatePrinter());
        }
    }
}
