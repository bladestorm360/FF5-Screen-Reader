using System;
using System.Collections.Generic;
using UnityEngine;

namespace FFV_ScreenReader.Core
{
    /// <summary>
    /// Declarative keybinding registry with context-aware dispatch.
    /// Replaces scattered if/GetKeyDown chains with registered bindings.
    /// </summary>
    public class KeyBindingRegistry
    {
        // Bindings grouped by KeyCode for fast lookup
        private readonly Dictionary<KeyCode, List<KeyBinding>> _bindings = new Dictionary<KeyCode, List<KeyBinding>>();

        /// <summary>
        /// Register a keybinding. Most-specific modifier should be registered first
        /// (CtrlShift before Ctrl before Shift before None).
        /// </summary>
        public void Register(KeyCode key, KeyModifier modifier, KeyContext context, Action action, string description)
        {
            if (!_bindings.TryGetValue(key, out var list))
            {
                list = new List<KeyBinding>();
                _bindings[key] = list;
            }
            list.Add(new KeyBinding(key, modifier, context, action, description));
        }

        /// <summary>
        /// Register a keybinding with no modifier.
        /// </summary>
        public void Register(KeyCode key, KeyContext context, Action action, string description)
        {
            Register(key, KeyModifier.None, context, action, description);
        }

        /// <summary>
        /// Try to execute a matching binding for a pressed key.
        /// Returns true if a binding was found and executed.
        /// </summary>
        public bool TryExecute(KeyCode key, KeyModifier currentModifiers, KeyContext activeContext)
        {
            if (!_bindings.TryGetValue(key, out var list))
                return false;

            for (int i = 0; i < list.Count; i++)
            {
                var binding = list[i];

                // Modifier must match exactly
                if (binding.Modifier != currentModifiers)
                    continue;

                // Context check: Global matches everything
                if (binding.Context != KeyContext.Global && binding.Context != activeContext)
                    continue;

                binding.Action();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Sort all binding lists so most-specific modifiers are checked first.
        /// Call after all registrations are complete.
        /// </summary>
        public void FinalizeRegistration()
        {
            foreach (var list in _bindings.Values)
            {
                // Sort: CtrlShift (3) > Ctrl (2) > Shift (1) > None (0)
                // Then: more specific contexts first (Status/Battle/Field before Global)
                list.Sort((a, b) =>
                {
                    int modCompare = ((int)b.Modifier).CompareTo((int)a.Modifier);
                    if (modCompare != 0) return modCompare;
                    return ((int)b.Context).CompareTo((int)a.Context);
                });
            }
        }

        /// <summary>
        /// Gets all registered key codes for dispatch loop.
        /// </summary>
        public IEnumerable<KeyCode> RegisteredKeys => _bindings.Keys;
    }
}
