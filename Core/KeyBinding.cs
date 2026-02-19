using System;
using UnityEngine;

namespace FFV_ScreenReader.Core
{
    /// <summary>
    /// Modifier keys for key bindings.
    /// </summary>
    [Flags]
    public enum KeyModifier
    {
        None = 0,
        Shift = 1,
        Ctrl = 2,
        CtrlShift = Shift | Ctrl
    }

    /// <summary>
    /// Context in which a key binding is active.
    /// </summary>
    public enum KeyContext
    {
        Global,        // Always active (any screen)
        Field,         // Only on field map (not in battle/dialogue)
        Battle,        // Only in battle
        BattleResult,  // Only on battle results screen (EXP/stat display)
        Status,        // Only on status screen
        Bestiary       // Only in bestiary detail view
    }

    /// <summary>
    /// Represents a single keyboard shortcut binding.
    /// </summary>
    public struct KeyBinding
    {
        public KeyCode Key;
        public KeyModifier Modifier;
        public KeyContext Context;
        public Action Action;
        public string Description;

        public KeyBinding(KeyCode key, KeyModifier modifier, KeyContext context, Action action, string description)
        {
            Key = key;
            Modifier = modifier;
            Context = context;
            Action = action;
            Description = description;
        }
    }
}
