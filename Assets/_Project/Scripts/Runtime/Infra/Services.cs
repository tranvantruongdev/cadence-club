using System;
using System.Collections.Generic;
using UnityEngine;

namespace Template.Infra
{
    /// <summary>
    /// Minimal service registry filled by the Boot scene. Deliberately tiny; switch to VContainer
    /// if a game grows enough to need scopes or constructor injection.
    /// </summary>
    public static class Services
    {
        private static readonly Dictionary<Type, object> Map = new Dictionary<Type, object>();

        public static void Register<T>(T service) where T : class
        {
            Map[typeof(T)] = service ?? throw new ArgumentNullException(nameof(service));
        }

        public static T Get<T>() where T : class
        {
            if (Map.TryGetValue(typeof(T), out var service))
            {
                return (T)service;
            }

            throw new InvalidOperationException($"Service {typeof(T).Name} isn't registered. Start from the Boot scene (first in Build Settings).");
        }

        public static bool TryGet<T>(out T service) where T : class
        {
            if (Map.TryGetValue(typeof(T), out var found))
            {
                service = (T)found;
                return true;
            }

            service = null;
            return false;
        }

        // Supports "Enter Play Mode Options" with domain reload off.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Map.Clear();
    }
}
